using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Sonraki sahnede <see cref="GameProgress"/>'i uygular: balta taşındıysa oyuncuya verir, can tam doldurulur
    /// (köye varış ödülü). Doğrudan bu sahneden başlanırsa (test) hiçbir şey taşınmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProgressRestorer : MonoBehaviour
    {
        [SerializeField] private GameProgress _progress;
        [SerializeField] private PlayerCombat _playerCombat;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private string _arrivalMessage = "Köye vardın. Yaraların sarıldı — kabile reisini yen!";

        private void Start()
        {
            if (_progress == null || !_progress.ArrivedFromGate)
            {
                return;
            }

            if (_progress.HasAxe && _playerCombat != null && !_playerCombat.HasAxe)
            {
                _playerCombat.EquipAxe();
            }

            if (_playerHealth != null)
            {
                _playerHealth.RestoreFullHealth();
            }

            if (!string.IsNullOrEmpty(_arrivalMessage) && ServiceRegistry.TryGet(out IMessageService messages))
            {
                messages.Show(_arrivalMessage, 5f);
            }
        }
    }
}
