namespace Yazlab.LLM
{
    /// <summary>
    /// LLM isteğinin sonuç türü. Hata durumlarında oyun devam eder; arayüz <see cref="LlmResult.UserFriendlyError"/> gösterir.
    /// </summary>
    public enum LlmErrorKind
    {
        /// <summary>Hata yok.</summary>
        None,

        /// <summary>Sunucuya bağlanılamadı (Ollama kapalı, yanlış adres/port).</summary>
        ConnectionFailed,

        /// <summary>İstenen model sunucuda yüklü değil (HTTP 404 / "not found").</summary>
        ModelNotFound,

        /// <summary>Yanıt zaman aşımı süresinde gelmedi.</summary>
        Timeout,

        /// <summary>Sunucu başka bir HTTP hata kodu döndürdü.</summary>
        HttpError,

        /// <summary>İstek <see cref="System.Threading.CancellationToken"/> ile iptal edildi (ör. diyalog kapandı).</summary>
        Cancelled,

        /// <summary>Yanıt gövdesi beklenen Ollama biçiminde değil veya içerik boş.</summary>
        InvalidResponse,

        /// <summary>Ayar geçersiz (boş model, yerel/LAN olmayan adres).</summary>
        InvalidConfig,

        /// <summary>Beklenmeyen istisna yakalandı.</summary>
        Unexpected
    }
}
