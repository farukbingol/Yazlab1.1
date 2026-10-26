using UnityEngine;
using Yazlab.Core;
using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Bölüm 2 geçidi önündeki tetik alanı: oyuncu içeri girdiğinde henüz açılmamış bir etkileşim hedefi
    /// varsa ekranda <c>"E: Köpeğe emir ver — geçidi açtır"</c> ipucunu gösterir.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class CommandZone : MonoBehaviour
    {
        [Tooltip("Oyuncu alana girdiğinde gösterilecek ekran ipucu.")]
        [SerializeField] private string _hintMessage = "E: Köpeğe emir ver — geçidi açtır";

        [Tooltip("Aktif etkileşim hedefi aranacak yarıçap (m).")]
        [SerializeField] private float _checkRadius = 35f;

        /// <summary>Oyuncu şu an emir alanı içinde mi.</summary>
        public bool IsPlayerInside { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            IsPlayerInside = true;

            if (InteractableRegistry.TryGetNearest(transform.position, _checkRadius, other.gameObject, out _))
            {
                if (ServiceRegistry.TryGet(out IMessageService messageService) && !string.IsNullOrEmpty(_hintMessage))
                {
                    messageService.Show(_hintMessage, 4f);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other != null && other.CompareTag("Player"))
            {
                IsPlayerInside = false;
            }
        }
    }
}
