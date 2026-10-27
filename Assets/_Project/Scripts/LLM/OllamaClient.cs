using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Yazlab.LLM
{
    /// <summary>
    /// Yerel/LAN Ollama sunucusu istemcisi. <c>POST /api/chat</c> (<c>stream = false</c>, isteğe bağlı JSON Schema
    /// <c>format</c>) ve <c>GET /api/tags</c> sağlık kontrolü yapar. <see cref="UnityWebRequest"/> + <see cref="Awaitable"/>
    /// kullanır, ana thread'i bloklamaz; bağlantı, 404 (model yok), zaman aşımı, iptal ve bozuk yanıt durumlarını
    /// istisna fırlatmadan <see cref="LlmResult.Fail"/> olarak döndürür.
    /// </summary>
    public sealed class OllamaClient : ILlmClient
    {
        private const string ChatPath = "/api/chat";
        private const string TagsPath = "/api/tags";
        private const int MaxDetailLength = 500;

        private readonly LlmConfig _config;

        public OllamaClient(LlmConfig config)
        {
            _config = config;
        }

        /// <inheritdoc />
        public async Awaitable<LlmResult> SendChatAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            string model = _config != null ? _config.ModelName : string.Empty;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!TryValidateConfig(model, out LlmResult invalid))
                {
                    return invalid;
                }

                if (request == null || request.Messages.Count == 0)
                {
                    return LlmResult.Fail(LlmErrorKind.InvalidConfig, 0, model, "Mesaj listesi boş.");
                }

                byte[] body = Encoding.UTF8.GetBytes(BuildChatBody(request, model));
                HttpOutcome outcome = await SendAsync(
                    _config.BaseUrl + ChatPath, UnityWebRequest.kHttpVerbPOST, body, _config.TimeoutSeconds, cancellationToken);

                long latency = stopwatch.ElapsedMilliseconds;
                return outcome.Kind == LlmErrorKind.None
                    ? ParseChatResponse(outcome.Body, latency, model, outcome.Status)
                    : LlmResult.Fail(outcome.Kind, latency, model, outcome.Detail, outcome.Status);
            }
            catch (OperationCanceledException)
            {
                return LlmResult.Fail(LlmErrorKind.Cancelled, stopwatch.ElapsedMilliseconds, model, "İstek iptal edildi.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return LlmResult.Fail(LlmErrorKind.Unexpected, stopwatch.ElapsedMilliseconds, model, e.Message);
            }
        }

        /// <inheritdoc />
        public async Awaitable<LlmResult> CheckHealthAsync(CancellationToken cancellationToken)
        {
            string model = _config != null ? _config.ModelName : string.Empty;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!TryValidateConfig(model, out LlmResult invalid))
                {
                    return invalid;
                }

                HttpOutcome outcome = await SendAsync(
                    _config.BaseUrl + TagsPath, UnityWebRequest.kHttpVerbGET, null, _config.HealthTimeoutSeconds, cancellationToken);

                long latency = stopwatch.ElapsedMilliseconds;
                if (outcome.Kind != LlmErrorKind.None)
                {
                    return LlmResult.Fail(outcome.Kind, latency, model, outcome.Detail, outcome.Status);
                }

                return EvaluateTags(outcome.Body, latency, model, outcome.Status);
            }
            catch (OperationCanceledException)
            {
                return LlmResult.Fail(LlmErrorKind.Cancelled, stopwatch.ElapsedMilliseconds, model, "İstek iptal edildi.");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return LlmResult.Fail(LlmErrorKind.Unexpected, stopwatch.ElapsedMilliseconds, model, e.Message);
            }
        }

        private bool TryValidateConfig(string model, out LlmResult failure)
        {
            failure = null;
            if (_config == null)
            {
                failure = LlmResult.Fail(LlmErrorKind.InvalidConfig, 0, model, "LlmConfig atanmamış.");
                return false;
            }

            if (!_config.TryValidateEndpoint(out string reason))
            {
                failure = LlmResult.Fail(LlmErrorKind.InvalidConfig, 0, model, reason);
                return false;
            }

            return true;
        }

        private string BuildChatBody(LlmRequest request, string model)
        {
            var messages = new JArray();
            foreach (ChatMessage message in request.Messages)
            {
                messages.Add(new JObject
                {
                    ["role"] = message.Role,
                    ["content"] = message.Content
                });
            }

            var root = new JObject
            {
                ["model"] = model,
                ["stream"] = false,
                ["keep_alive"] = _config.KeepAlive,
                ["options"] = new JObject
                {
                    ["temperature"] = _config.Temperature,
                    ["num_predict"] = _config.MaxTokens
                },
                ["messages"] = messages
            };

            if (request.Format != null)
            {
                root["format"] = request.Format.DeepClone();
            }

            return root.ToString(Formatting.None);
        }

        /// <summary>
        /// Ollama yanıt gövdesinden <c>message.content</c>'i çıkarır. İçerik (NPC JSON'u) burada ayrıştırılmaz;
        /// o iş yanıt ayrıştırıcısınındır.
        /// </summary>
        private static LlmResult ParseChatResponse(string body, long latency, string model, long status)
        {
            JObject root;
            try
            {
                root = JObject.Parse(body ?? string.Empty);
            }
            catch (JsonException e)
            {
                return LlmResult.Fail(LlmErrorKind.InvalidResponse, latency, model, $"Gövde JSON değil: {e.Message}", status);
            }

            string serverError = root["error"]?.Type == JTokenType.String ? (string)root["error"] : null;
            if (!string.IsNullOrEmpty(serverError))
            {
                return LlmResult.Fail(LlmErrorKind.HttpError, latency, model, serverError, status);
            }

            JToken contentToken = root["message"]?["content"];
            string content = contentToken != null && contentToken.Type == JTokenType.String ? (string)contentToken : null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return LlmResult.Fail(LlmErrorKind.InvalidResponse, latency, model, "message.content boş veya yok.", status);
            }

            string doneReason = root["done_reason"]?.ToString() ?? "?";
            string evalCount = root["eval_count"]?.ToString() ?? "?";
            long serverMs = root["total_duration"]?.Type == JTokenType.Integer ? (long)root["total_duration"] / 1000000L : -1L;
            string detail = $"done_reason={doneReason}, eval_count={evalCount}, server_ms={serverMs}";
            return LlmResult.Success(content, latency, model, detail, status);
        }

        private static LlmResult EvaluateTags(string body, long latency, string model, long status)
        {
            var names = new List<string>();
            try
            {
                JObject root = JObject.Parse(body ?? string.Empty);
                if (root["models"] is JArray models)
                {
                    foreach (JToken entry in models)
                    {
                        string name = entry["name"]?.ToString() ?? entry["model"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch (JsonException e)
            {
                return LlmResult.Fail(LlmErrorKind.InvalidResponse, latency, model, $"/api/tags yanıtı JSON değil: {e.Message}", status);
            }

            string installed = names.Count > 0 ? string.Join(", ", names) : "(yok)";
            foreach (string name in names)
            {
                if (ModelMatches(name, model))
                {
                    return LlmResult.Success(name, latency, model, $"yüklü modeller: {installed}", status);
                }
            }

            return LlmResult.Fail(LlmErrorKind.ModelNotFound, latency, model, $"'{model}' yüklü değil; yüklü modeller: {installed}", status);
        }

        /// <summary>Ollama adları <c>ad:etiket</c> biçimindedir; etiketsiz ad <c>:latest</c> ile eşleşir.</summary>
        private static bool ModelMatches(string installed, string wanted)
        {
            if (string.Equals(installed, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return wanted.IndexOf(':') < 0 &&
                   string.Equals(installed, wanted + ":latest", StringComparison.OrdinalIgnoreCase);
        }

        private static async Awaitable<HttpOutcome> SendAsync(
            string url, string method, byte[] body, int timeoutSeconds, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new HttpOutcome(LlmErrorKind.Cancelled, 0, null, "İstek gönderilmeden iptal edildi.");
            }

            var stopwatch = Stopwatch.StartNew();
            using var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };

            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            // İptal (ör. diyalog kapandı) isteği keser; await normal biçimde tamamlanır.
            using (cancellationToken.Register(request.Abort))
            {
                await request.SendWebRequest();
            }

            long status = request.responseCode;
            string text = request.downloadHandler != null ? request.downloadHandler.text : null;

            if (cancellationToken.IsCancellationRequested)
            {
                return new HttpOutcome(LlmErrorKind.Cancelled, status, text, "İstek iptal edildi.");
            }

            switch (request.result)
            {
                case UnityWebRequest.Result.Success:
                    return new HttpOutcome(LlmErrorKind.None, status, text, null);

                case UnityWebRequest.Result.ProtocolError:
                    string serverError = ExtractServerError(text);
                    bool modelMissing = status == 404 &&
                                        serverError.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
                    return new HttpOutcome(modelMissing ? LlmErrorKind.ModelNotFound : LlmErrorKind.HttpError,
                        status, text, $"HTTP {status}: {serverError}");

                case UnityWebRequest.Result.ConnectionError:
                    bool timedOut = (request.error ?? string.Empty).IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    stopwatch.ElapsedMilliseconds >= timeoutSeconds * 1000L - 250L;
                    return new HttpOutcome(timedOut ? LlmErrorKind.Timeout : LlmErrorKind.ConnectionFailed,
                        status, text, $"{request.error} ({url})");

                default:
                    return new HttpOutcome(LlmErrorKind.InvalidResponse, status, text, request.error);
            }
        }

        /// <summary>Ollama hata gövdesindeki <c>error</c> alanını; yoksa kısaltılmış ham gövdeyi döndürür.</summary>
        private static string ExtractServerError(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            try
            {
                JToken error = JObject.Parse(body)["error"];
                if (error != null)
                {
                    return error.ToString();
                }
            }
            catch (JsonException)
            {
                // Gövde JSON değil; ham metin kullanılır.
            }

            return body.Length > MaxDetailLength ? body.Substring(0, MaxDetailLength) : body;
        }

        private readonly struct HttpOutcome
        {
            public LlmErrorKind Kind { get; }
            public long Status { get; }
            public string Body { get; }
            public string Detail { get; }

            public HttpOutcome(LlmErrorKind kind, long status, string body, string detail)
            {
                Kind = kind;
                Status = status;
                Body = body;
                Detail = detail;
            }
        }
    }
}
