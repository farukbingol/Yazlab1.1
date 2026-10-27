using System;
using Yazlab.LLM;

namespace Yazlab.UI
{
    /// <summary>
    /// NPC diyalog penceresi sözleşmesi. Görünüm yalnızca gösterir ve oyuncu girdisini olay olarak bildirir;
    /// LLM isteği, güven hesabı ve aksiyonlar denetleyicinin (B18) işidir.
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>Oyuncu bir mesaj gönderdiğinde (Enter veya Gönder) kırpılmış metinle tetiklenir.</summary>
        event Action<string> MessageSubmitted;

        /// <summary>Oyuncu "Tekrar Dene"ye bastığında tetiklenir.</summary>
        event Action RetryRequested;

        /// <summary>Oyuncu pencereyi kapattığında (Esc veya Kapat) tetiklenir.</summary>
        event Action Closed;

        /// <summary>Pencere açık mı.</summary>
        bool IsVisible { get; }

        /// <summary>Pencereyi açar, geçmişi temizler ve oyuncu hareketini kilitler.</summary>
        void Show(string npcName);

        /// <summary>Pencereyi kapatır ve oyuncu kilidini bırakır (<see cref="Closed"/> tetiklenmez).</summary>
        void Hide();

        /// <summary>Oyuncu mesajını geçmişe ekler.</summary>
        void AppendPlayerMessage(string text);

        /// <summary>NPC repliğini geçmişe ekler, duygu/güven rozetini günceller.</summary>
        void AppendNpcMessage(string text, NpcEmotion emotion, int currentTrust);

        /// <summary>NPC'den ayrı görünen <c>[Sistem]</c> uyarısı ekler; <paramref name="canRetry"/> ise "Tekrar Dene" açılır.</summary>
        void AppendSystemError(string text, bool canRetry);

        /// <summary>"Düşünüyor..." göstergesini açar/kapatır; açıkken giriş ve Gönder kilitlenir.</summary>
        void SetThinking(bool isThinking);
    }
}
