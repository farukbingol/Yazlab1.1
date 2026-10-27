using UnityEngine;

namespace Yazlab.World
{
    /// <summary>
    /// Bağlı kapı (<see cref="IOpenable"/>) açılınca bu karakteri (ör. kapı bekçisi) <see cref="CharacterController"/> ile
    /// yürüterek yoldan çeker, sonra oyuncu tarafına döndürür. Kapı aralığı oyuncunun geniş kapsülüne açık kalır.
    /// Karakter savaşa girerse (yakındaki <see cref="EnemyController"/> açıldıysa) çekilme iptal edilir.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class StepAsideOnOpen : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [Tooltip("Açılması beklenen kapı (IOpenable).")]
        [SerializeField] private MonoBehaviour _door;
        [Tooltip("Başlangıç konumuna göre çekilinecek dünya ofseti (m).")]
        [SerializeField] private Vector3 _sideOffset = new Vector3(5.5f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float _walkSpeed = 2.2f;
        [SerializeField, Min(0f)] private float _startDelay = 0.4f;
        [Tooltip("Çekildikten sonra bakılacak yön (dünya). Sıfırsa yön değişmez.")]
        [SerializeField] private Vector3 _faceDirection = Vector3.back;

        private CharacterController _controller;
        private Animator _animator;
        private EnemyController _enemy;
        private IOpenable _openable;
        private Vector3 _target;
        private float _delay;
        private bool _moving;

        /// <summary>Kenara çekilme tamamlandı mı.</summary>
        public bool HasSteppedAside { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _enemy = GetComponent<EnemyController>();
            _openable = _door as IOpenable;
        }

        private void OnEnable()
        {
            if (_openable != null)
            {
                _openable.Opened += OnDoorOpened;
            }
        }

        private void OnDisable()
        {
            if (_openable != null)
            {
                _openable.Opened -= OnDoorOpened;
            }
        }

        private void OnDoorOpened()
        {
            if (HasSteppedAside || IsInCombat)
            {
                return;
            }

            _target = transform.position + _sideOffset;
            _delay = _startDelay;
            _moving = true;
        }

        private bool IsInCombat => _enemy != null && _enemy.enabled;

        private void Update()
        {
            if (!_moving)
            {
                return;
            }

            if (IsInCombat)
            {
                Stop();
                return;
            }

            if (_delay > 0f)
            {
                _delay -= Time.deltaTime;
                return;
            }

            Vector3 delta = _target - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.1f)
            {
                Stop();
                HasSteppedAside = true;
                if (_faceDirection.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(_faceDirection.normalized, Vector3.up);
                }

                return;
            }

            Vector3 direction = delta / distance;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction, Vector3.up), 10f * Time.deltaTime);
            float step = Mathf.Min(distance, _walkSpeed * Time.deltaTime);
            // Yerçekimi payı: karakter zeminde kalsın.
            _controller.Move(direction * step + Vector3.down * 0.5f * Time.deltaTime);
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, _walkSpeed);
            }
        }

        private void Stop()
        {
            _moving = false;
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, 0f);
            }
        }
    }
}
