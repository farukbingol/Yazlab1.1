using Newtonsoft.Json.Linq;

namespace Yazlab.LLM
{
    /// <summary>
    /// NPC yanıtının JSON sözleşmesi: Ollama <c>format</c> alanına gönderilen JSON Schema ve parser'ın kullandığı
    /// alan adları / enum değerleri. Şema ile parser aynı sabitleri kullanır (tek kaynak).
    /// </summary>
    public static class NpcResponseSchema
    {
        /// <summary>Replik alanı.</summary>
        public const string ReplyField = "reply";

        /// <summary>Duygu alanı.</summary>
        public const string EmotionField = "emotion";

        /// <summary>Güven değişimi alanı.</summary>
        public const string TrustDeltaField = "trust_delta";

        /// <summary>Aksiyon alanı.</summary>
        public const string ActionField = "action";

        /// <summary>Tek yanıtta izin verilen en düşük güven değişimi.</summary>
        public const int TrustDeltaMin = -20;

        /// <summary>Tek yanıtta izin verilen en yüksek güven değişimi.</summary>
        public const int TrustDeltaMax = 20;

        /// <summary><see cref="NpcEmotion"/> sırasıyla JSON değerleri.</summary>
        public static readonly string[] EmotionValues = { "neutral", "friendly", "suspicious", "angry" };

        /// <summary><see cref="NpcAction"/> sırasıyla JSON değerleri.</summary>
        public static readonly string[] ActionValues = { "none", "open_door", "attack", "end_conversation" };

        /// <summary>Ollama <c>format</c> alanı için yeni bir JSON Schema nesnesi oluşturur.</summary>
        public static JObject Build()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    [ReplyField] = new JObject { ["type"] = "string" },
                    [EmotionField] = new JObject { ["type"] = "string", ["enum"] = new JArray(EmotionValues) },
                    [TrustDeltaField] = new JObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = TrustDeltaMin,
                        ["maximum"] = TrustDeltaMax
                    },
                    [ActionField] = new JObject { ["type"] = "string", ["enum"] = new JArray(ActionValues) }
                },
                ["required"] = new JArray(ReplyField, EmotionField, TrustDeltaField, ActionField)
            };
        }

        /// <summary>Aksiyonun JSON değeri (ör. <c>open_door</c>).</summary>
        public static string ToWireName(NpcAction action) => ActionValues[(int)action];

        /// <summary>Duygunun JSON değeri (ör. <c>suspicious</c>).</summary>
        public static string ToWireName(NpcEmotion emotion) => EmotionValues[(int)emotion];
    }
}
