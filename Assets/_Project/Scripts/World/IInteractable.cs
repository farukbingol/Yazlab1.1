using UnityEngine;

namespace Yazlab.World
{
    /// <summary>
    /// Oyuncu veya köpek tarafından etkinleştirilebilen dünya nesnelerinin sözleşmesi (şalter, basınç plakası vb.).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Oyuncuya gösterilecek kısa etkileşim yazısı.</summary>
        string Prompt { get; }

        /// <summary>Etkileşimin yapılacağı dünya noktası (köpek bu noktaya yaklaşır).</summary>
        Transform InteractionPoint { get; }

        /// <summary>Nesne daha önce etkinleştirildiyse <c>true</c>.</summary>
        bool IsActivated { get; }

        /// <summary><paramref name="actor"/> şu an bu nesneyle etkileşime girebilir mi?</summary>
        bool CanInteract(GameObject actor);

        /// <summary>Etkileşimi gerçekleştirir.</summary>
        void Interact(GameObject actor);
    }
}
