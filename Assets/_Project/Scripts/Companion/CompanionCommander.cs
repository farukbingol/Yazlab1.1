using UnityEngine;
using Yazlab.Core;
using Yazlab.Player;
using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Oyuncunun <c>E</c> tuşuyla (<see cref="PlayerInputReader.InteractTriggered"/>) köpeğe emir vermesini sağlar.
    /// Hedefi sabit koordinatla değil, <see cref="RaycastSensor"/> ve <see cref="InteractableRegistry"/>
    /// üzerinden dinamik olarak bulur ve <see cref="CompanionAgent.IssueCommand"/> çağırır.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompanionCommander : MonoBehaviour
    {
        [Tooltip("Emir verilecek köpek refakatçi.")]
        [SerializeField] private CompanionAgent _companion;

        [Tooltip("Oyuncu girdi okuyucusu. Boşsa aynı objeden otomatik alınır.")]
        [SerializeField] private PlayerInputReader _inputReader;

        [Tooltip("Etkileşim hedefinin aranacağı en büyük mesafe (m).")]
        [SerializeField] private float _commandRange = 35f;

        [Tooltip("Emir verildiğinde ekranda gösterilecek bilgi mesajı.")]
        [SerializeField] private string _commandIssuedMessage = "Köpeğe emir verildi: Geçidi aç!";

        [Tooltip("Console'a emir logu yazar.")]
        [SerializeField] private bool _debugLog = true;

        /// <summary>Bağlı köpek refakatçi.</summary>
        public CompanionAgent Companion
        {
            get => _companion;
            set => _companion = value;
        }

        private void Awake()
        {
            if (_inputReader == null)
            {
                _inputReader = GetComponent<PlayerInputReader>();
            }
        }

        private void OnEnable()
        {
            if (_inputReader != null)
            {
                _inputReader.InteractTriggered += OnInteractTriggered;
            }
        }

        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.InteractTriggered -= OnInteractTriggered;
            }
        }

        /// <summary>
        /// Menzildeki en yakın aktifleşmemiş <see cref="IInteractable"/> hedefi çözüp köpeğe emir verir.
        /// </summary>
        public bool TryIssueCommand()
        {
            if (InputLock.IsLocked || _companion == null)
            {
                return false;
            }

            IInteractable target = ResolveTarget();
            if (target == null)
            {
                return false;
            }

            if (!_companion.IssueCommand(target))
            {
                return false;
            }

            if (_debugLog)
            {
                Debug.Log($"[Companion] Emir verildi -> {target.Prompt}");
            }

            if (ServiceRegistry.TryGet(out IMessageService messageService) &&
                !string.IsNullOrEmpty(_commandIssuedMessage))
            {
                messageService.Show(_commandIssuedMessage, 3.5f);
            }

            return true;
        }

        private void OnInteractTriggered()
        {
            TryIssueCommand();
        }

        private IInteractable ResolveTarget()
        {
            GameObject dogActor = _companion.gameObject;

            // 1) Önce köpeğin sensörü doğrudan görüş hattında bir hedef görüyorsa onu seç.
            if (_companion.Sensor != null)
            {
                SensorReading reading = _companion.Sensor.Sense();
                if (reading.VisibleTarget != null && reading.VisibleTarget.CanInteract(dogActor))
                {
                    return reading.VisibleTarget;
                }
            }

            // 2) Oyuncuya en yakın uygun hedefi InteractableRegistry üzerinden çöz.
            if (InteractableRegistry.TryGetNearest(transform.position, _commandRange, dogActor, out IInteractable nearestFromPlayer))
            {
                return nearestFromPlayer;
            }

            // 3) Köpeğe en yakın uygun hedefi InteractableRegistry üzerinden çöz.
            if (InteractableRegistry.TryGetNearest(_companion.transform.position, _commandRange, dogActor, out IInteractable nearestFromDog))
            {
                return nearestFromDog;
            }

            return null;
        }
    }
}
