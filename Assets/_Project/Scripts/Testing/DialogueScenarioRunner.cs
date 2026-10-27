using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Yazlab.Core;
using Yazlab.LLM;

namespace Yazlab.Testing
{
    /// <summary>
    /// LLM diyalog senaryolarını gerçek Ollama sunucusunda oyundaki akışla (DialogueSession → OllamaClient → parser,
    /// bozuk JSON'da 1 düzeltme isteği, <see cref="NpcActionGuard"/>) çalıştırır; her turu <c>llm_log.csv</c>'ye ekler.
    /// Sahte/hazır yanıt üretmez. Sonuçlar <see cref="DialogueScenarioReport"/> ile Markdown'a çevrilir.
    /// </summary>
    public sealed class DialogueScenarioRunner
    {
        private const int WarmUpTimeoutSeconds = 120;

        private readonly LlmConfig _baseConfig;
        private readonly NpcPersona _persona;
        private readonly INpcResponseParser _parser = new JsonNpcResponseParser(logWarnings: false);
        private readonly CsvLogger _log;
        private readonly string[] _row = new string[LlmLogFormat.Header.Length];
        private readonly string _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        /// <summary>Koşu sırasında ilerleme mesajı (Console için).</summary>
        public event Action<string> Progress;

        public DialogueScenarioRunner(LlmConfig baseConfig, NpcPersona persona)
        {
            _baseConfig = baseConfig;
            _persona = persona;
            _log = new CsvLogger(Path.Combine(Application.persistentDataPath, "llm_log.csv"), LlmLogFormat.Header, append: true);
        }

        /// <summary>Bir modelin ölçüm özeti (ısınma, VRAM).</summary>
        public sealed class ModelRun
        {
            public string Model { get; set; }
            public long WarmUpMs { get; set; }
            public bool Available { get; set; }
            public string UnavailableReason { get; set; }
            public long VramBytes { get; set; }
            public List<ScenarioRunResult> Results { get; } = new List<ScenarioRunResult>();
        }

        /// <summary>Verilen modelde tüm senaryoları <paramref name="repetitions"/> kez çalıştırır.</summary>
        public async Awaitable<ModelRun> RunModelAsync(string model, IReadOnlyList<DialogueScenario> scenarios, int repetitions,
            CancellationToken ct)
        {
            var run = new ModelRun { Model = model };
            LlmConfig config = CloneFor(model, _baseConfig.TimeoutSeconds);
            LlmConfig warmConfig = CloneFor(model, WarmUpTimeoutSeconds);
            try
            {
                // Soğuk yükleme (VRAM'e alma) 25 sn'yi aşabilir: ısınma uzun zaman aşımıyla yapılır, ölçümler sıcak modelde.
                var warmClient = new OllamaClient(warmConfig);
                LlmResult health = await warmClient.CheckHealthAsync(ct);
                if (!health.IsSuccess)
                {
                    run.UnavailableReason = health.UserFriendlyError;
                    Report($"{model}: kullanılamıyor — {health.UserFriendlyError}");
                    return run;
                }

                LlmResult warm = await warmClient.SendChatAsync(new LlmRequest(new[] { ChatMessage.User("Hazır mısın? Tek kelime.") }), ct);
                run.WarmUpMs = warm.LatencyMs;
                run.Available = warm.IsSuccess;
                if (!warm.IsSuccess)
                {
                    run.UnavailableReason = warm.UserFriendlyError;
                    return run;
                }

                Report($"{model}: ısındı ({warm.LatencyMs} ms).");
                var client = new OllamaClient(config);
                foreach (DialogueScenario scenario in scenarios)
                {
                    for (int rep = 1; rep <= repetitions; rep++)
                    {
                        ScenarioRunResult result = await RunScenarioAsync(client, config, model, scenario, rep, ct);
                        run.Results.Add(result);
                        Report($"{model} · {scenario.Id} #{rep}: {(result.Passed ? "✓" : "✗")}");
                    }
                }

                run.VramBytes = await QueryVramAsync(model, ct);
                return run;
            }
            finally
            {
                DestroyClone(config);
                DestroyClone(warmConfig);
            }
        }

        private async Awaitable<ScenarioRunResult> RunScenarioAsync(ILlmClient client, LlmConfig config, string model,
            DialogueScenario scenario, int rep, CancellationToken ct)
        {
            var result = new ScenarioRunResult { Model = model, Scenario = scenario, Repetition = rep };
            var session = new DialogueSession(_persona, config.OpenTrustThreshold, new FixedStateProvider(scenario.State));
            session.Start();

            foreach (string line in scenario.PlayerLines)
            {
                if (!session.TryBeginTurn(line, out LlmRequest request, out string beginError))
                {
                    result.Turns.Add(new ScenarioTurnResult { PlayerText = line, Error = beginError, ErrorKind = LlmErrorKind.Unexpected });
                    continue;
                }

                ScenarioTurnResult turn = await RunTurnAsync(client, session, request, line, ct);
                result.Turns.Add(turn);
                WriteLog(turn, model, scenario.Id);
            }

            result.Passed = scenario.Passes(result.Turns);
            return result;
        }

        private async Awaitable<ScenarioTurnResult> RunTurnAsync(ILlmClient client, DialogueSession session, LlmRequest request,
            string line, CancellationToken ct)
        {
            var turn = new ScenarioTurnResult { PlayerText = line };
            LlmResult result = await client.SendChatAsync(request, ct);
            turn.LatencyMs = result.LatencyMs;
            if (!result.IsSuccess)
            {
                session.FailTurn();
                turn.ErrorKind = result.ErrorKind;
                turn.Error = result.TechnicalDetail;
                turn.TrustAfter = session.CurrentTrust;
                return turn;
            }

            turn.RawResponse = result.RawContent;
            bool ok = _parser.TryParse(result.RawContent, out NpcResponse response, out string error);
            turn.ParsedFirstTry = ok;
            if (!ok)
            {
                // Oyundaki gibi tek düzeltme isteği.
                turn.CorrectionUsed = true;
                var messages = new List<ChatMessage>(request.Messages)
                {
                    ChatMessage.Assistant(result.RawContent),
                    ChatMessage.User(NpcDialogueController.CorrectionPrompt)
                };
                LlmResult retry = await client.SendChatAsync(new LlmRequest(messages, request.Format), ct);
                turn.LatencyMs += retry.LatencyMs;
                if (retry.IsSuccess)
                {
                    turn.RawResponse = retry.RawContent;
                    ok = _parser.TryParse(retry.RawContent, out response, out error);
                }
            }

            if (!ok)
            {
                session.FailTurn();
                turn.ErrorKind = LlmErrorKind.InvalidResponse;
                turn.Error = error;
                turn.TrustAfter = session.CurrentTrust;
                return turn;
            }

            (_, int trust) = session.CompleteTurn(response);
            turn.Parsed = true;
            turn.Reply = response.Reply;
            turn.Emotion = response.Emotion;
            turn.TrustDelta = response.TrustDelta;
            turn.TrustAfter = trust;
            turn.Requested = response.Action;
            turn.Applied = NpcActionGuard.Apply(response.Action, session.LastSnapshot, trust, session.OpenThreshold, out string rejection);
            turn.GuardBlocked = rejection != null;
            return turn;
        }

        private void WriteLog(ScenarioTurnResult t, string model, string scenarioId)
        {
            var record = new DialogueTurnRecord(t.PlayerText, t.RawResponse, t.Parsed, t.ErrorKind, t.Error, t.Requested, t.Applied,
                t.Emotion, t.TrustDelta, t.TrustAfter, t.LatencyMs, t.CorrectionUsed, model);
            LlmLogFormat.Fill(_row, record, _sessionId, "b23_" + scenarioId);
            _log.AppendRow(_row);
        }

        /// <summary>Ollama <c>/api/ps</c>'ten modelin VRAM kullanımı (bulunamazsa 0).</summary>
        private async Awaitable<long> QueryVramAsync(string model, CancellationToken ct)
        {
            try
            {
                using var request = UnityWebRequest.Get(_baseConfig.BaseUrl + "/api/ps");
                request.timeout = 5;
                using (ct.Register(request.Abort))
                {
                    await request.SendWebRequest();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    return 0;
                }

                if (JObject.Parse(request.downloadHandler.text)["models"] is JArray models)
                {
                    foreach (JToken m in models)
                    {
                        if (string.Equals((string)m["name"], model, StringComparison.OrdinalIgnoreCase))
                        {
                            return m["size_vram"]?.Value<long>() ?? 0;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Senaryo] VRAM okunamadı: {e.Message}");
            }

            return 0;
        }

        private LlmConfig CloneFor(string model, int timeoutSeconds)
        {
            LlmConfig clone = UnityEngine.Object.Instantiate(_baseConfig);
            clone.hideFlags = HideFlags.DontSave;
            clone.SetEndpoint(_baseConfig.BaseUrl, model);
            clone.SetLimits(timeoutSeconds, _baseConfig.MaxTokens);
            return clone;
        }

        private static void DestroyClone(UnityEngine.Object clone)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(clone);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private void Report(string message)
        {
            Debug.Log($"[Senaryo] {message}");
            Progress?.Invoke(message);
        }

        /// <summary>Senaryo boyunca sabit oyun durumu.</summary>
        private sealed class FixedStateProvider : IGameStateProvider
        {
            private readonly GameStateSnapshot _snapshot;

            public FixedStateProvider(GameStateSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public GameState CurrentState => GameState.InDialogue;

            public GameStateSnapshot Capture() => _snapshot;
        }
    }
}
