using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Yazlab.LLM;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// LLM bağlantı tanılama menüleri: <see cref="LlmConfig"/> asset'ini oluşturur, Ollama sağlık kontrolü ve
    /// gerçek bir deneme isteği yapar, hata senaryolarını (sunucu kapalı, model yok, zaman aşımı, iptal,
    /// yerel olmayan adres) geçici config kopyalarıyla dener. Edit veya Play modunda çalışır, editörü dondurmaz.
    /// </summary>
    public static class LlmDiagnostics
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/LlmConfig.asset";
        private const string LogPrefix = "[LLM Tanı]";

        /// <summary>LlmConfig asset'ini yoksa varsayılan değerlerle oluşturur ve seçer.</summary>
        [MenuItem("Yazlab/Kurulum/LLM Config")]
        public static void CreateConfig()
        {
            LlmConfig config = LoadOrCreateConfig();
            Selection.activeObject = config;
            Debug.Log($"{LogPrefix} LlmConfig hazır: {ConfigPath} (model {config.ModelName}, {config.BaseUrl}).");
        }

        /// <summary>Sunucunun açık ve modelin yüklü olduğunu denetler.</summary>
        [MenuItem("Yazlab/LLM/Saglik Kontrolu")]
        public static async void HealthCheck()
        {
            try
            {
                LlmResult result = await new OllamaClient(LoadOrCreateConfig()).CheckHealthAsync(CancellationToken.None);
                Log("Sağlık kontrolü", result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Config'teki modelle JSON şemalı kısa bir gerçek istek gönderir.</summary>
        [MenuItem("Yazlab/LLM/Deneme Istegi")]
        public static async void TestChat()
        {
            try
            {
                LlmResult result = await new OllamaClient(LoadOrCreateConfig()).SendChatAsync(BuildProbeRequest(), CancellationToken.None);
                Log("Deneme isteği", result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// NPC yanıt şemasıyla (<see cref="NpcResponseSchema"/>) gerçek istek atar ve sonucu
        /// <see cref="JsonNpcResponseParser"/> ile ayrıştırır (B15 uçtan uca kontrol; persona B16'da).
        /// </summary>
        [MenuItem("Yazlab/LLM/NPC Sema Denemesi")]
        public static async void TestNpcSchema()
        {
            try
            {
                var messages = new[]
                {
                    ChatMessage.System("Şema testi: bir kapı bekçisisin. Türkçe kısa cevap ver. Yalnızca şemaya uyan JSON döndür."),
                    ChatMessage.User("Selam bekçi, kapıyı açar mısın?")
                };
                LlmResult result = await new OllamaClient(LoadOrCreateConfig())
                    .SendChatAsync(new LlmRequest(messages, NpcResponseSchema.Build()), CancellationToken.None);
                Log("NPC şema isteği", result);
                if (!result.IsSuccess)
                {
                    return;
                }

                bool ok = new JsonNpcResponseParser().TryParse(result.RawContent, out NpcResponse response, out string error);
                Debug.Log(ok
                    ? $"{LogPrefix} Parser OK → {response} (uyarı {response.Warnings.Count})"
                    : $"{LogPrefix} Parser başarısız → {error}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Hata senaryolarını config kopyalarıyla sırayla dener ve özet tablo basar.</summary>
        [MenuItem("Yazlab/LLM/Hata Senaryolarini Dene")]
        public static async void ErrorScenarios()
        {
            try
            {
                await RunErrorScenariosAsync(LoadOrCreateConfig());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Senaryolar: sağlık, başarılı istek, sunucu kapalı (kullanılmayan port), model yok, 1 sn zaman aşımı,
        /// istek sırasında iptal, yerel olmayan adres. <paramref name="baseConfig"/> değiştirilmez.
        /// </summary>
        public static async Awaitable<string> RunErrorScenariosAsync(LlmConfig baseConfig)
        {
            var rows = new List<(string Name, LlmResult Result)>();
            LlmRequest probe = BuildProbeRequest();

            rows.Add(("Sağlık kontrolü", await new OllamaClient(baseConfig).CheckHealthAsync(CancellationToken.None)));
            rows.Add(("Normal istek", await new OllamaClient(baseConfig).SendChatAsync(probe, CancellationToken.None)));

            LlmConfig closed = CloneWith(baseConfig, "baseUrl", "http://localhost:11999");
            rows.Add(("Sunucu kapalı (sağlık)", await new OllamaClient(closed).CheckHealthAsync(CancellationToken.None)));
            rows.Add(("Sunucu kapalı (istek)", await new OllamaClient(closed).SendChatAsync(probe, CancellationToken.None)));

            LlmConfig missingModel = CloneWith(baseConfig, "modelName", "yazlab-olmayan-model:1b");
            rows.Add(("Model yok (sağlık)", await new OllamaClient(missingModel).CheckHealthAsync(CancellationToken.None)));
            rows.Add(("Model yok (istek)", await new OllamaClient(missingModel).SendChatAsync(probe, CancellationToken.None)));

            LlmConfig shortTimeout = CloneWith(baseConfig, "timeoutSeconds", 1);
            rows.Add(("Zaman aşımı (1 sn)", await new OllamaClient(shortTimeout).SendChatAsync(BuildSlowRequest(), CancellationToken.None)));

            // İstek ilk await'e kadar eşzamanlı ilerler; iptal bu yüzden ana thread'de, istek uçuştayken yapılır.
            using (var cts = new CancellationTokenSource())
            {
                Awaitable<LlmResult> pending = new OllamaClient(baseConfig).SendChatAsync(BuildSlowRequest(), cts.Token);
                cts.Cancel();
                rows.Add(("İstek sırasında iptal", await pending));
            }

            LlmConfig remote = CloneWith(baseConfig, "baseUrl", "https://llm.example.com");
            rows.Add(("Yerel olmayan adres", await new OllamaClient(remote).SendChatAsync(probe, CancellationToken.None)));

            UnityEngine.Object.DestroyImmediate(closed);
            UnityEngine.Object.DestroyImmediate(missingModel);
            UnityEngine.Object.DestroyImmediate(shortTimeout);
            UnityEngine.Object.DestroyImmediate(remote);

            var sb = new StringBuilder();
            sb.AppendLine($"### OllamaClient hata senaryoları (model {baseConfig.ModelName})");
            sb.AppendLine();
            sb.AppendLine("| Senaryo | Sonuç | Süre (ms) | Oyuncuya gösterilen | Ayrıntı |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach ((string name, LlmResult result) in rows)
            {
                string shown = result.IsSuccess ? Shorten(result.RawContent, 80) : result.UserFriendlyError;
                sb.AppendLine($"| {name} | {result.ErrorKind} | {result.LatencyMs} | {shown} | {Shorten(result.TechnicalDetail, 90)} |");
            }

            string table = sb.ToString();
            Debug.Log($"{LogPrefix} Senaryolar bitti.\n\n{table}");
            return table;
        }

        private static LlmRequest BuildProbeRequest()
        {
            var schema = JObject.Parse(
                "{\"type\":\"object\",\"properties\":{\"reply\":{\"type\":\"string\"}},\"required\":[\"reply\"]}");
            var messages = new[]
            {
                ChatMessage.System("Bağlantı testi. Yalnızca JSON döndür: {\"reply\": \"<en fazla 5 kelime Türkçe>\"}."),
                ChatMessage.User("Merhaba, beni duyabiliyor musun?")
            };
            return new LlmRequest(messages, schema);
        }

        /// <summary>Uzun yanıt isteyen istek (zaman aşımı ve iptal senaryoları için).</summary>
        private static LlmRequest BuildSlowRequest()
        {
            var messages = new[]
            {
                ChatMessage.System("Ayrıntılı ve uzun yaz."),
                ChatMessage.User("Bir orman köyünün tarihini en az 300 kelimeyle anlat.")
            };
            return new LlmRequest(messages);
        }

        private static LlmConfig CloneWith(LlmConfig source, string field, object value)
        {
            LlmConfig clone = UnityEngine.Object.Instantiate(source);
            clone.hideFlags = HideFlags.DontSave;
            var so = new SerializedObject(clone);
            SerializedProperty property = so.FindProperty(field);
            if (value is string s)
            {
                property.stringValue = s;
            }
            else if (value is int i)
            {
                property.intValue = i;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return clone;
        }

        internal static LlmConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<LlmConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<LlmConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void Log(string label, LlmResult result)
        {
            if (result.IsSuccess)
            {
                Debug.Log($"{LogPrefix} {label}: OK ({result.LatencyMs} ms, {result.Model}) → {result.RawContent}\n{result.TechnicalDetail}");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} {label}: {result.ErrorKind} ({result.LatencyMs} ms) → {result.UserFriendlyError}\n{result.TechnicalDetail}");
            }
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string oneLine = text.Replace("\n", " ").Replace("|", "/");
            return oneLine.Length > max ? oneLine.Substring(0, max) + "…" : oneLine;
        }
    }
}
