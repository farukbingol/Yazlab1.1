using Yazlab.LLM;

namespace Yazlab.Actions
{
    /// <summary>Aksiyon uygulandıktan sonra diyaloğun ne olacağı.</summary>
    public enum DialogueOutcome
    {
        /// <summary>Konuşma sürer.</summary>
        Continue,

        /// <summary>Pencere kapanır; oyuncu daha sonra tekrar konuşabilir.</summary>
        Pause,

        /// <summary>Konuşma kalıcı olarak biter (kapı açıldı, savaş başladı).</summary>
        Finish
    }

    /// <summary>
    /// NPC yanıtındaki yapılandırılmış <see cref="NpcAction"/> değerine bağlı oyun aksiyonu.
    /// Yeni aksiyon = yeni sınıf; <see cref="GameActionDispatcher"/> değişmez.
    /// </summary>
    public interface IGameAction
    {
        /// <summary>Bu aksiyonun karşılık geldiği NPC aksiyonu.</summary>
        NpcAction ActionType { get; }

        /// <summary>Aksiyondan sonra diyaloğun durumu.</summary>
        DialogueOutcome Outcome { get; }

        /// <summary>Aksiyonu uygular.</summary>
        void Execute();
    }
}
