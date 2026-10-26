using System;
using UnityEngine;
using UnityEngine.AI;
using Yazlab.Companion.States;
using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpek refakatçinin sahne tarafı: NavMeshAgent, Animator ve <see cref="CompanionStateMachine"/> bağlantısını kurar,
    /// her karede durum makinesini çalıştırır. Mantık durum sınıflarındadır; bu sınıf yalnızca bağlar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class CompanionAgent : MonoBehaviour
    {
        private const float PlayerSearchInterval = 1f;
        private const float AnimatorDampTime = 0.1f;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [Tooltip("Hız ve mesafe parametreleri.")]
        [SerializeField] private CompanionConfig _config;

        [Tooltip("Takip edilecek oyuncu. Boşsa 'Player' etiketli obje aranır.")]
        [SerializeField] private Transform _player;

        [Tooltip("Engel ve etkileşim hedefi algılayıcı sensör. Boşsa aynı objedeki IObstacleSensor bileşeni kullanılır.")]
        [SerializeField] private RaycastSensor _sensor;

        private NavMeshAgent _agent;
        private Animator _animator;
        private IObstacleSensor _obstacleSensor;
        private CompanionStateMachine _machine;
        private CompanionContext _context;
        private float _nextPlayerSearchTime;
        private bool _offMeshWarned;

        /// <summary>Durum makinesi (B12'de yeni durumlar eklemek için).</summary>
        public CompanionStateMachine Machine => _machine;

        /// <summary>Durumların paylaştığı bağlam.</summary>
        public CompanionContext Context => _context;

        /// <summary>Köpeğin çevre ve hedef algılayıcı sensörü.</summary>
        public IObstacleSensor Sensor => _obstacleSensor ?? _sensor;

        /// <summary>En son yapılan sensör okumasının sonucu.</summary>
        public SensorReading LastReading => _context != null ? _context.LastReading : default;

        /// <summary>Aktif durumun adı (hata ayıklama için); makine kurulmadıysa boş metin.</summary>
        public string CurrentStateName => _machine?.Current?.Name ?? string.Empty;

        /// <summary>Emir görevi tamamlandığında veya başarısız olduğunda tetiklenir (<c>true</c>: başarılı).</summary>
        public event Action<bool> CommandCompleted
        {
            add
            {
                if (_context != null)
                {
                    _context.CommandCompleted += value;
                }
            }
            remove
            {
                if (_context != null)
                {
                    _context.CommandCompleted -= value;
                }
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();

            if (_sensor == null)
            {
                _sensor = GetComponent<RaycastSensor>();
            }

            _obstacleSensor = _sensor != null ? (IObstacleSensor)_sensor : GetComponent<IObstacleSensor>();

            if (_config == null)
            {
                Debug.LogError("[Companion] CompanionConfig atanmamış; köpek devre dışı. " +
                               "'Yazlab/Kurulum/Companion (Kopek)' menüsünü çalıştır.", this);
                enabled = false;
                return;
            }

            if (_sensor != null && _sensor.Config == null)
            {
                _sensor.Config = _config;
            }

            ApplyConfigToAgent();

            _machine = new CompanionStateMachine(_config.DebugLog);
            _context = new CompanionContext(_agent, _config, _machine, _obstacleSensor) { Player = _player };
            _context.BindStates(
                new IdleState(_context),
                new FollowState(_context),
                new MoveToTargetState(_context),
                new ActivateState(_context),
                new RecoverState(_context),
                new ReturnToPlayerState(_context));
        }

        private void Start()
        {
            if (_machine == null)
            {
                return;
            }

            ResolvePlayer();
            _machine.ChangeState(_context.Idle, "başlangıç");
        }

        private void Update()
        {
            if (_machine == null)
            {
                return;
            }

            if (_context.Player == null)
            {
                ResolvePlayer();
            }

            if (!_agent.isOnNavMesh && !_offMeshWarned)
            {
                _offMeshWarned = true;
                Debug.LogWarning("[Companion] Köpek NavMesh üzerinde değil; hareket edemez. NavMesh'i bake et.", this);
            }

            if (_obstacleSensor != null)
            {
                _context.LastReading = _obstacleSensor.Sense(_context.CurrentTarget);
            }

            _machine.Tick(Time.deltaTime);

            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, _agent.velocity.magnitude, AnimatorDampTime, Time.deltaTime);
            }
        }

        /// <summary>
        /// Köpeğe verilen <paramref name="target"/> hedefini etkinleştirme emri verir.
        /// Sabit koordinat kullanmaz; hedefi <see cref="MoveToTargetState"/> üzerinden çözer.
        /// </summary>
        public bool IssueCommand(IInteractable target)
        {
            if (_context == null || _machine == null || target == null || target.InteractionPoint == null)
            {
                return false;
            }

            if (!target.CanInteract(gameObject))
            {
                return false;
            }

            _context.CurrentTarget = target;
            _context.RecoverAttemptCount = 0;

            float dist = _context.PlanarDistanceToTarget();
            string label = !string.IsNullOrEmpty(target.Prompt) ? target.Prompt : "Hedef";
            _machine.ChangeState(_context.MoveToTarget, $"hedef: {label}, {dist:F1} m");
            return true;
        }

        /// <summary>
        /// Sensör veya <see cref="InteractableRegistry"/> üzerinden en yakın uygun <see cref="IInteractable"/>
        /// hedefini bulup köpeğe etkinleştirme emri verir.
        /// </summary>
        public bool CommandActivateNearest(float searchRadius = 40f)
        {
            if (_context == null)
            {
                return false;
            }

            if (_obstacleSensor != null)
            {
                SensorReading reading = _obstacleSensor.Sense();
                _context.LastReading = reading;
                if (reading.VisibleTarget != null && reading.VisibleTarget.CanInteract(gameObject))
                {
                    return IssueCommand(reading.VisibleTarget);
                }
            }

            if (InteractableRegistry.TryGetNearest(transform.position, searchRadius, gameObject, out IInteractable nearestFromDog))
            {
                return IssueCommand(nearestFromDog);
            }

            if (_context.HasPlayer &&
                InteractableRegistry.TryGetNearest(_context.Player.position, searchRadius, gameObject, out IInteractable nearestFromPlayer))
            {
                return IssueCommand(nearestFromPlayer);
            }

            return false;
        }

        private void ApplyConfigToAgent()
        {
            _agent.speed = _config.WalkSpeed;
            _agent.acceleration = _config.Acceleration;
            _agent.angularSpeed = _config.AngularSpeed;
            // Agent, durma mesafesinin biraz içinde durur; Follow→Idle eşiği (FollowStopDistance) böylece güvenle sağlanır.
            _agent.stoppingDistance = Mathf.Max(0.1f, _config.FollowStopDistance - 0.3f);
        }

        /// <summary>Oyuncu referansı yoksa seyrek aralıklarla 'Player' etiketli objeyi arar (Update'te sürekli Find yok).</summary>
        private void ResolvePlayer()
        {
            if (Time.unscaledTime < _nextPlayerSearchTime)
            {
                return;
            }

            _nextPlayerSearchTime = Time.unscaledTime + PlayerSearchInterval;
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null)
            {
                _player = found.transform;
                _context.Player = _player;
            }
        }
    }
}
