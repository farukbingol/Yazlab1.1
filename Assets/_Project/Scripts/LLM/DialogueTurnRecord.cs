namespace Yazlab.LLM
{
    /// <summary>
    /// Tek bir diyalog turunun log kaydı (B20 <c>llm_log.csv</c> ve B23 senaryo testleri için).
    /// </summary>
    public readonly struct DialogueTurnRecord
    {
        /// <summary>Oyuncu mesajı (selam turunda sahne notu).</summary>
        public string PlayerMessage { get; }

        /// <summary>Modelin ham yanıtı (son deneme).</summary>
        public string RawResponse { get; }

        /// <summary>Yanıt ayrıştırıldı mı.</summary>
        public bool ParseOk { get; }

        /// <summary>Ağ/ayrıştırma hata türü; başarıda <see cref="LlmErrorKind.None"/>.</summary>
        public LlmErrorKind ErrorKind { get; }

        /// <summary>Hata/ayrıştırma açıklaması.</summary>
        public string Error { get; }

        /// <summary>Modelin istediği aksiyon.</summary>
        public NpcAction RequestedAction { get; }

        /// <summary>Korumalardan sonra uygulanan aksiyon.</summary>
        public NpcAction AppliedAction { get; }

        /// <summary>Turdan sonraki güven.</summary>
        public int Trust { get; }

        /// <summary>Toplam gecikme (ms; düzeltme isteği dahil).</summary>
        public long LatencyMs { get; }

        /// <summary>Bozuk JSON için düzeltme isteği gönderildi mi.</summary>
        public bool CorrectionUsed { get; }

        /// <summary>Kullanılan model.</summary>
        public string Model { get; }

        public DialogueTurnRecord(string playerMessage, string rawResponse, bool parseOk, LlmErrorKind errorKind, string error,
            NpcAction requestedAction, NpcAction appliedAction, int trust, long latencyMs, bool correctionUsed, string model)
        {
            PlayerMessage = playerMessage ?? string.Empty;
            RawResponse = rawResponse ?? string.Empty;
            ParseOk = parseOk;
            ErrorKind = errorKind;
            Error = error ?? string.Empty;
            RequestedAction = requestedAction;
            AppliedAction = appliedAction;
            Trust = trust;
            LatencyMs = latencyMs;
            CorrectionUsed = correctionUsed;
            Model = model ?? string.Empty;
        }
    }
}
