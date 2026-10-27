using System.Collections.Generic;

namespace Yazlab.LLM
{
    /// <summary>
    /// Doğrulanmış NPC yanıtı: replik, duygu, güven değişimi ve aksiyon.
    /// Değerler parser tarafından aralığa çekilmiş/güvenli varsayılana düşürülmüştür; düzeltmeler <see cref="Warnings"/>'te listelenir.
    /// </summary>
    public sealed class NpcResponse
    {
        private static readonly IReadOnlyList<string> NoWarnings = new string[0];

        /// <summary>NPC'nin söylediği metin (boş olamaz).</summary>
        public string Reply { get; }

        /// <summary>NPC'nin duygu durumu.</summary>
        public NpcEmotion Emotion { get; }

        /// <summary>Güven değişimi (<see cref="NpcResponseSchema.TrustDeltaMin"/>..<see cref="NpcResponseSchema.TrustDeltaMax"/>).</summary>
        public int TrustDelta { get; }

        /// <summary>İstenen oyun aksiyonu.</summary>
        public NpcAction Action { get; }

        /// <summary>Ayrıştırma sırasında yapılan düzeltmeler (aralık dışı sayı, bilinmeyen enum, eksik alan).</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Düzeltme yapıldıysa <c>true</c>.</summary>
        public bool HasWarnings => Warnings.Count > 0;

        public NpcResponse(string reply, NpcEmotion emotion, int trustDelta, NpcAction action, IReadOnlyList<string> warnings = null)
        {
            Reply = reply ?? string.Empty;
            Emotion = emotion;
            TrustDelta = trustDelta;
            Action = action;
            Warnings = warnings ?? NoWarnings;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"[{Emotion}, güven {TrustDelta:+0;-0;0}, {Action}] {Reply}";
        }
    }
}
