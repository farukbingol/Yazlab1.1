using System;
using System.Threading;
using UnityEngine;
using Yazlab.Core;

namespace Yazlab.LLM
{
    /// <summary>
    /// <see cref="ILlmRuntime"/> uygulaması: config asset'inin çalışma anı kopyasını ve tek <see cref="OllamaClient"/>'ı tutar,
    /// açılışta ve periyodik olarak (gerçek zamanla) sağlık kontrolü yapar, model ilk kez hazır olunca ısınma isteği atar.
    /// Sohbet sonuçları <see cref="Report"/> ile bildirilir; bağlantı koparsa durum anında <see cref="LlmHealthState.Unavailable"/> olur.
    /// Ağ çağrıları <see cref="Awaitable"/> ile yapılır, ana thread bloklanmaz, istisna dışarı sızmaz.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)]
    public class LlmRuntime : MonoBehaviour, ILlmRuntime
    {
        private const string WarmUpPrompt = "Hazır mısın? Tek kelimeyle cevap ver.";

        [Tooltip("Kaynak config asset'i (çalışma anında kopyası kullanılır; asset değişmez).")]
        [SerializeField] private LlmConfig _configAsset;
        [SerializeField] private bool _debugLog = true;

        private LlmConfig _config;
        private ILlmClient _client;
        private bool _checking;
        private bool _warmedUp;
        private float _nextCheckTime;

        /// <inheritdoc />
        public LlmConfig Config => _config;

        /// <inheritdoc />
        public ILlmClient Client => _client;

        /// <inheritdoc />
        public LlmHealthState State { get; private set; } = LlmHealthState.Connecting;

        /// <inheritdoc />
        public string StatusText { get; private set; } = "Ollama'ya bağlanılıyor...";

        /// <inheritdoc />
        public LlmResult LastHealth { get; private set; }

        /// <inheritdoc />
        public event Action<LlmHealthState, string> StatusChanged;

        /// <summary>Kaynak asset (fault injector varsayılana dönüş için).</summary>
        public LlmConfig ConfigAsset => _configAsset;

        private void Awake()
        {
            if (_configAsset == null)
            {
                Debug.LogError("[LlmRuntime] LlmConfig atanmamış; LLM devre dışı.", this);
                SetStatus(LlmHealthState.Unavailable, "LLM ayarı yok");
                enabled = false;
                return;
            }

            _config = Instantiate(_configAsset);
            _config.name = _configAsset.name + " (çalışma anı)";
            _client = new OllamaClient(_config);
            ServiceRegistry.Register<ILlmRuntime>(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<ILlmRuntime>(this);
            if (_config != null)
            {
                Destroy(_config);
            }
        }

        private void Update()
        {
            if (!_checking && Time.unscaledTime >= _nextCheckTime)
            {
                RequestHealthCheck();
            }
        }

        /// <inheritdoc />
        public bool TryReconfigure(string baseUrl, string modelName, out string error)
        {
            string previousUrl = _config.BaseUrl;
            string previousModel = _config.ModelName;
            _config.SetEndpoint(baseUrl?.Trim(), modelName?.Trim());
            if (!_config.TryValidateEndpoint(out error))
            {
                _config.SetEndpoint(previousUrl, previousModel);
                return false;
            }

            _client = new OllamaClient(_config);
            _warmedUp = false;
            Log($"Yeniden yapılandırıldı: {_config.BaseUrl} / {_config.ModelName}");
            SetStatus(LlmHealthState.Connecting, "Ollama'ya bağlanılıyor...");
            RequestHealthCheck();
            return true;
        }

        /// <summary>Zaman aşımı ve token sınırını çalışma anı kopyasında değiştirir (hata senaryosu simülasyonu).</summary>
        public void ApplyLimits(int timeoutSeconds, int maxTokens)
        {
            _config.SetLimits(timeoutSeconds, maxTokens);
            _client = new OllamaClient(_config);
        }

        /// <summary>Çalışma anı kopyasını asset değerlerine döndürür.</summary>
        public void ResetToAsset()
        {
            _config.SetEndpoint(_configAsset.BaseUrl, _configAsset.ModelName);
            _config.SetLimits(_configAsset.TimeoutSeconds, _configAsset.MaxTokens);
            _client = new OllamaClient(_config);
            _warmedUp = false;
            RequestHealthCheck();
        }

        /// <inheritdoc />
        public void RequestHealthCheck()
        {
            if (_checking || _client == null)
            {
                return;
            }

            _ = CheckHealthAsync();
        }

        /// <inheritdoc />
        public void Report(LlmResult result)
        {
            if (result == null || result.ErrorKind == LlmErrorKind.Cancelled)
            {
                return;
            }

            if (result.IsSuccess)
            {
                _warmedUp = true;
                SetStatus(LlmHealthState.Ready, ReadyText());
            }
            else if (IsConnectionProblem(result.ErrorKind))
            {
                _warmedUp = false;
                SetStatus(LlmHealthState.Unavailable, UnavailableText(result.ErrorKind));
            }
        }

        private async Awaitable CheckHealthAsync()
        {
            _checking = true;
            ILlmClient client = _client;
            try
            {
                LastHealth = await client.CheckHealthAsync(destroyCancellationToken);
                if (client != _client)
                {
                    return; // Bu arada yeniden yapılandırıldı; eski sonucu yok say.
                }

                if (!LastHealth.IsSuccess)
                {
                    _warmedUp = false;
                    SetStatus(LlmHealthState.Unavailable, UnavailableText(LastHealth.ErrorKind));
                    return;
                }

                if (!_warmedUp)
                {
                    SetStatus(LlmHealthState.WarmingUp, $"Model ısınıyor ({_config.ModelName})...");
                    var warmUp = new LlmRequest(new[] { ChatMessage.User(WarmUpPrompt) });
                    LlmResult result = await client.SendChatAsync(warmUp, destroyCancellationToken);
                    if (client != _client)
                    {
                        return;
                    }

                    if (!result.IsSuccess)
                    {
                        SetStatus(LlmHealthState.Unavailable, UnavailableText(result.ErrorKind));
                        return;
                    }

                    _warmedUp = true;
                    Log($"Model ısındı ({_config.ModelName}, {result.LatencyMs} ms).");
                }

                SetStatus(LlmHealthState.Ready, ReadyText());
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                SetStatus(LlmHealthState.Unavailable, "Beklenmeyen hata");
            }
            finally
            {
                _checking = false;
                // Kontrol sürerken yeniden yapılandırıldıysa yeni ayarla hemen tekrar kontrol et.
                _nextCheckTime = client != _client
                    ? Time.unscaledTime
                    : Time.unscaledTime + (_config != null ? _config.HealthCheckIntervalSeconds : 15f);
            }
        }

        private static bool IsConnectionProblem(LlmErrorKind kind) =>
            kind == LlmErrorKind.ConnectionFailed || kind == LlmErrorKind.ModelNotFound ||
            kind == LlmErrorKind.Timeout || kind == LlmErrorKind.InvalidConfig || kind == LlmErrorKind.HttpError;

        private string ReadyText() => $"Ollama hazır ({_config.ModelName})";

        private string UnavailableText(LlmErrorKind kind)
        {
            switch (kind)
            {
                case LlmErrorKind.ModelNotFound:
                    return $"Model yok: {_config.ModelName}";
                case LlmErrorKind.Timeout:
                    return "Ollama yanıt vermiyor";
                case LlmErrorKind.InvalidConfig:
                    return "LLM ayarı geçersiz";
                case LlmErrorKind.HttpError:
                    return "Ollama hata döndürdü";
                default:
                    return "Ollama bağlantısı yok";
            }
        }

        private void SetStatus(LlmHealthState state, string text)
        {
            if (State == state && StatusText == text)
            {
                return;
            }

            State = state;
            StatusText = text;
            Log($"Durum: {state} — {text}");
            StatusChanged?.Invoke(state, text);
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log($"[LlmRuntime] {message}");
            }
        }
    }
}
