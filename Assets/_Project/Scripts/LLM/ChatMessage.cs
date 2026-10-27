namespace Yazlab.LLM
{
    /// <summary>
    /// Ollama <c>/api/chat</c> mesaj listesindeki tek mesaj (rol + içerik).
    /// </summary>
    public readonly struct ChatMessage
    {
        /// <summary>Sistem talimatı rolü.</summary>
        public const string SystemRole = "system";

        /// <summary>Oyuncu rolü.</summary>
        public const string UserRole = "user";

        /// <summary>Model (NPC) rolü.</summary>
        public const string AssistantRole = "assistant";

        /// <summary>Mesaj rolü: <c>system</c>, <c>user</c> veya <c>assistant</c>.</summary>
        public string Role { get; }

        /// <summary>Mesaj metni.</summary>
        public string Content { get; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content ?? string.Empty;
        }

        /// <summary>Sistem mesajı oluşturur.</summary>
        public static ChatMessage System(string content) => new ChatMessage(SystemRole, content);

        /// <summary>Oyuncu mesajı oluşturur.</summary>
        public static ChatMessage User(string content) => new ChatMessage(UserRole, content);

        /// <summary>Model yanıtı mesajı oluşturur (geçmişe eklemek için).</summary>
        public static ChatMessage Assistant(string content) => new ChatMessage(AssistantRole, content);
    }
}
