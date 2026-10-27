using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Yazlab.LLM
{
    /// <summary>
    /// Tek bir sohbet isteği: mesaj geçmişi ve isteğe bağlı yanıt biçimi (Ollama <c>format</c>: JSON Schema nesnesi).
    /// Model, zaman aşımı ve üretim parametreleri <see cref="LlmConfig"/>'ten gelir.
    /// </summary>
    public sealed class LlmRequest
    {
        /// <summary>Sırasıyla gönderilecek mesajlar (sistem, geçmiş, son oyuncu mesajı).</summary>
        public IReadOnlyList<ChatMessage> Messages { get; }

        /// <summary>Yanıtın uyması gereken JSON Schema; <c>null</c> ise serbest metin istenir.</summary>
        public JToken Format { get; }

        public LlmRequest(IReadOnlyList<ChatMessage> messages, JToken format = null)
        {
            Messages = messages ?? new List<ChatMessage>();
            Format = format;
        }
    }
}
