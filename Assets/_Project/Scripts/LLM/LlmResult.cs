namespace Yazlab.LLM
{
    /// <summary>
    /// LLM isteğinin sonucu. İstemci hiçbir zaman istisna fırlatmaz; başarısızlık bu nesneyle bildirilir.
    /// Hata durumunda <see cref="UserFriendlyError"/> oyuncuya gösterilecek <c>[Sistem]</c> önekli Türkçe metindir
    /// (NPC adına sahte replik değildir).
    /// </summary>
    public sealed class LlmResult
    {
        /// <summary>İstek başarılı ve içerik alındı mı.</summary>
        public bool IsSuccess => ErrorKind == LlmErrorKind.None;

        /// <summary>Modelin ürettiği ham metin (<c>message.content</c>); sağlık kontrolünde bulunan model adı.</summary>
        public string RawContent { get; }

        /// <summary>Hata türü; başarıda <see cref="LlmErrorKind.None"/>.</summary>
        public LlmErrorKind ErrorKind { get; }

        /// <summary>Oyuncuya gösterilecek <c>[Sistem]</c> mesajı; başarıda boş.</summary>
        public string UserFriendlyError { get; }

        /// <summary>Log için teknik ayrıntı (HTTP gövdesi, istisna mesajı, yüklü modeller vb.).</summary>
        public string TechnicalDetail { get; }

        /// <summary>İstek süresi (ms).</summary>
        public long LatencyMs { get; }

        /// <summary>İstekte kullanılan model adı.</summary>
        public string Model { get; }

        /// <summary>HTTP durum kodu (bağlantı kurulamadıysa 0).</summary>
        public long HttpStatus { get; }

        private LlmResult(string rawContent, LlmErrorKind kind, string userFriendlyError, string technicalDetail,
            long latencyMs, string model, long httpStatus)
        {
            RawContent = rawContent ?? string.Empty;
            ErrorKind = kind;
            UserFriendlyError = userFriendlyError ?? string.Empty;
            TechnicalDetail = technicalDetail ?? string.Empty;
            LatencyMs = latencyMs;
            Model = model ?? string.Empty;
            HttpStatus = httpStatus;
        }

        /// <summary>Başarılı sonuç oluşturur.</summary>
        public static LlmResult Success(string rawContent, long latencyMs, string model, string technicalDetail = null, long httpStatus = 200)
        {
            return new LlmResult(rawContent, LlmErrorKind.None, null, technicalDetail, latencyMs, model, httpStatus);
        }

        /// <summary>Başarısız sonuç oluşturur; oyuncu mesajı hata türünden üretilir.</summary>
        public static LlmResult Fail(LlmErrorKind kind, long latencyMs, string model, string technicalDetail = null, long httpStatus = 0)
        {
            return new LlmResult(null, kind, BuildUserMessage(kind, model, httpStatus), technicalDetail, latencyMs, model, httpStatus);
        }

        /// <summary>Hata türü için oyuncuya gösterilecek Türkçe <c>[Sistem]</c> mesajı.</summary>
        public static string BuildUserMessage(LlmErrorKind kind, string model, long httpStatus = 0)
        {
            switch (kind)
            {
                case LlmErrorKind.None:
                    return string.Empty;
                case LlmErrorKind.ConnectionFailed:
                    return "[Sistem] Ollama sunucusuna ulaşılamıyor. 'ollama serve' çalışıyor mu?";
                case LlmErrorKind.ModelNotFound:
                    return $"[Sistem] Model bulunamadı: ollama pull {model}";
                case LlmErrorKind.Timeout:
                    return "[Sistem] NPC yanıt vermedi (zaman aşımı).";
                case LlmErrorKind.HttpError:
                    return $"[Sistem] Ollama hata döndürdü (HTTP {httpStatus}).";
                case LlmErrorKind.Cancelled:
                    return "[Sistem] İstek iptal edildi.";
                case LlmErrorKind.InvalidResponse:
                    return "[Sistem] Yanıt anlaşılamadı.";
                case LlmErrorKind.InvalidConfig:
                    return "[Sistem] LLM ayarı geçersiz; yalnızca yerel/LAN Ollama adresi kullanılabilir.";
                default:
                    return "[Sistem] Beklenmeyen hata.";
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return IsSuccess
                ? $"OK ({LatencyMs} ms, {Model}): {RawContent}"
                : $"{ErrorKind} ({LatencyMs} ms, {Model}, HTTP {HttpStatus}): {TechnicalDetail}";
        }
    }
}
