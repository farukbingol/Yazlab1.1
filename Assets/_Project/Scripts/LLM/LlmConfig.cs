using System;
using System.Net;
using UnityEngine;

namespace Yazlab.LLM
{
    /// <summary>
    /// Yerel/LAN Ollama sunucusu bağlantı ve üretim parametreleri. Bulut LLM adresi kabul edilmez:
    /// <see cref="TryValidateEndpoint"/> yalnızca localhost, özel ağ (10/8, 172.16/12, 192.168/16) ve <c>.local</c> adreslerine izin verir.
    /// </summary>
    [CreateAssetMenu(fileName = "LlmConfig", menuName = "Yazlab/Config/LLM Config")]
    public class LlmConfig : ScriptableObject
    {
        [Header("Sunucu")]
        [Tooltip("Ollama adresi (yalnızca yerel veya LAN). Örn. http://localhost:11434")]
        [SerializeField] private string baseUrl = "http://localhost:11434";

        [Tooltip("Ollama model adı (ollama list çıktısındaki ad).")]
        [SerializeField] private string modelName = "qwen2.5:3b";

        [Tooltip("Modelin istekten sonra bellekte tutulma süresi (Ollama keep_alive).")]
        [SerializeField] private string keepAlive = "10m";

        [Header("Zaman Aşımı")]
        [Tooltip("Sohbet isteği zaman aşımı (sn).")]
        [SerializeField] private int timeoutSeconds = 25;

        [Tooltip("Sağlık kontrolü (/api/tags) zaman aşımı (sn).")]
        [SerializeField] private int healthTimeoutSeconds = 5;

        [Header("Üretim")]
        [Tooltip("Örnekleme sıcaklığı (0 = deterministik).")]
        [SerializeField, Range(0f, 2f)] private float temperature = 0.6f;

        [Tooltip("Yanıt için en fazla üretilecek token (Ollama num_predict).")]
        [SerializeField] private int maxTokens = 256;

        [Header("NPC")]
        [Tooltip("Kapının açılabilmesi için gereken en düşük güven (0–100).")]
        [SerializeField, Range(0, 100)] private int openTrustThreshold = 30;

        /// <summary>Sondaki '/' kırpılmış Ollama adresi.</summary>
        public string BaseUrl => string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.Trim().TrimEnd('/');

        /// <summary>Kullanılacak model adı.</summary>
        public string ModelName => string.IsNullOrWhiteSpace(modelName) ? string.Empty : modelName.Trim();

        /// <summary>Ollama <c>keep_alive</c> değeri.</summary>
        public string KeepAlive => string.IsNullOrWhiteSpace(keepAlive) ? "5m" : keepAlive.Trim();

        /// <summary>Sohbet isteği zaman aşımı (sn, en az 1).</summary>
        public int TimeoutSeconds => Mathf.Max(1, timeoutSeconds);

        /// <summary>Sağlık kontrolü zaman aşımı (sn, en az 1).</summary>
        public int HealthTimeoutSeconds => Mathf.Max(1, healthTimeoutSeconds);

        /// <summary>Örnekleme sıcaklığı.</summary>
        public float Temperature => Mathf.Clamp(temperature, 0f, 2f);

        /// <summary>En fazla üretilecek token.</summary>
        public int MaxTokens => Mathf.Max(16, maxTokens);

        /// <summary>Kapı açma güven eşiği (0–100).</summary>
        public int OpenTrustThreshold => Mathf.Clamp(openTrustThreshold, 0, 100);

        /// <summary>
        /// Adresin geçerli ve yerel/LAN olduğunu doğrular. Geçersizse <paramref name="reason"/> Türkçe açıklama içerir.
        /// </summary>
        public bool TryValidateEndpoint(out string reason)
        {
            if (string.IsNullOrEmpty(ModelName))
            {
                reason = "Model adı boş.";
                return false;
            }

            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                reason = $"Geçersiz adres: '{BaseUrl}'.";
                return false;
            }

            if (!IsLocalOrLanHost(uri.Host))
            {
                reason = $"'{uri.Host}' yerel/LAN adresi değil; yalnızca yerel Ollama sunucusu kullanılabilir.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>localhost, döngü adresi, özel IPv4 blokları veya <c>.local</c> ana bilgisayar adı mı.</summary>
        public static bool IsLocalOrLanHost(string host)
        {
            if (string.IsNullOrEmpty(host))
            {
                return false;
            }

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!IPAddress.TryParse(host, out IPAddress ip))
            {
                // Çıplak makine adı (nokta yok) LAN içi kabul edilir; alan adları (ör. api.ornek.com) reddedilir.
                return host.IndexOf('.') < 0;
            }

            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            byte[] b = ip.GetAddressBytes();
            return b.Length == 4 &&
                   (b[0] == 10 ||
                    (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                    (b[0] == 192 && b[1] == 168));
        }
    }
}
