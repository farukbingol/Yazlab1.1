using UnityEngine;
using Yazlab.Core;

namespace Yazlab.World
{
    /// <summary>
    /// Son kapının arkasındaki tetik alanı: oyuncu girince bir kez <see cref="GameEvents.RaiseFinalGatePassed"/> çağırır.
    /// Sonraki adımı (zafer ekranı veya sonraki sahneye geçiş) olayı dinleyen taraf belirler.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FinalGateZone : MonoBehaviour
    {
        [SerializeField] private string _playerTag = "Player";

        private bool _triggered;

        /// <summary>Oyuncu alandan geçti mi.</summary>
        public bool Triggered => _triggered;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || !other.CompareTag(_playerTag))
            {
                return;
            }

            _triggered = true;
            GameEvents.RaiseFinalGatePassed();
        }
    }
}
