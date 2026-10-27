using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Yazlab.Core;
using Yazlab.Enemies;

/// <summary>
/// Düşman (kurt ve kabile şefi) algılama, kovalama, gecikmeli saldırı, hasar alma ve ölüm davranışlarını yöneten bileşen.
/// <see cref="IDamageable"/> arayüzünü uygular.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyController : MonoBehaviour, IDamageable, IHostile
{
    private const float DefaultHomeReturnDistance = 40f; // oyuncu yuva+bu kadar uzaklaşınca takibi bırakır
    private const float AttackReachGraceBuffer = 0.6f;
    private static readonly int SpeedParamHash = Animator.StringToHash("Speed");
    private static readonly int AttackParamHash = Animator.StringToHash("Attack");
    private static readonly int DieParamHash = Animator.StringToHash("Die");
    private static readonly int HitParamHash = Animator.StringToHash("Hit");

    [Header("Yapılandırma")]
    [SerializeField] private EnemyConfig config;

    [Header("Algılama ve Hareket (Config yoksa kullanılır)")]
    [FormerlySerializedAs("algilamaMesafesi")]
    [SerializeField] private float _detectRange = 14f;

    [FormerlySerializedAs("hiz")]
    [SerializeField] private float _moveSpeed = 3.2f; // oyuncu (yürüme 4.5) her zaman kaçabilir

    [FormerlySerializedAs("donusHizi")]
    [SerializeField] private float _turnSpeed = 8f;

    [Header("Saldırı ve Sağlık")]
    [FormerlySerializedAs("saldiriMesafesi")]
    [SerializeField] private float _attackRange = 2.4f; // dev karakterlerde görsel temasa yakın

    [FormerlySerializedAs("saldiriBeklemesi")]
    [SerializeField] private float _attackCooldown = 1.6f; // yumruk arası süre

    [Tooltip("Saldırı animasyonu başladıktan hasarın uygulanacağı ana kadar geçen gecikme (sn).")]
    [SerializeField] private float _attackHitDelay = 0.25f;

    [FormerlySerializedAs("hasar")]
    [SerializeField] private int _damage = 10;

    [FormerlySerializedAs("can")]
    [SerializeField] private int _currentHealth = 3; // oyuncunun kaç yumruğu

    [SerializeField] private int _maxHealth = 3;

    [Header("Darbe Tepkisi ve Ölüm")]
    [Tooltip("Hasar alınca geri itilme hızı")]
    [FormerlySerializedAs("tepetmeGucu")]
    [SerializeField] private float _knockbackSpeed = 6f;

    [Tooltip("Hasar alınca hareketin kısa donma süresi (darbe hissi)")]
    [FormerlySerializedAs("donmaSuresi")]
    [SerializeField] private float _hitStunDuration = 0.12f;

    [Tooltip("Ölünce yere düşecek eşya (prefab) — örn. balta")]
    [FormerlySerializedAs("olumDrop")]
    [SerializeField] private GameObject _deathDropPrefab;

    [Tooltip("Ölünce üst ortada görünecek mesaj")]
    [FormerlySerializedAs("olumMesaji")]
    [SerializeField] private string _deathMessage;

    [FormerlySerializedAs("debugKayit")]
    [SerializeField] private bool _debugLog = true;

    [SerializeField] private Transform _playerTransform;
    private IDamageable _playerDamageable;
    private PlayerHealth _playerHealth;
    private CharacterController _controller;
    private Animator _animator;
    private Vector3 _homePosition;
    private Vector3 _knockbackVelocity;
    private float _nextAttackTime;
    private float _stunEndTime;
    private float _verticalVelocity;
    private bool _isDead;
    private bool _wasChasing;
    private bool _hasHitParam;
    private int _loggedAttackCount;
    private Coroutine _attackCoroutine;

    /// <inheritdoc />
    public bool IsDead => _isDead || _currentHealth <= 0;

    /// <inheritdoc />
    public int CurrentHealth => _currentHealth;

    /// <inheritdoc />
    public int MaxHealth => config != null ? config.MaxHealth : Mathf.Max(1, _maxHealth);

    /// <summary>
    /// Düşman yapılandırma varlığı.
    /// </summary>
    public EnemyConfig Config
    {
        get => config;
        set
        {
            config = value;
            if (config != null)
            {
                _maxHealth = config.MaxHealth;
                _currentHealth = _maxHealth;
            }
        }
    }

    /// <summary>
    /// Oyuncuyu fark etme mesafesi (m).
    /// </summary>
    public float DetectRange
    {
        get => config != null ? config.DetectRange : _detectRange;
        set => _detectRange = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Kovalama hızı (m/sn).
    /// </summary>
    public float MoveSpeed
    {
        get => config != null ? config.MoveSpeed : _moveSpeed;
        set => _moveSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Saldırı mesafesi (m).
    /// </summary>
    public float AttackRange
    {
        get => config != null ? config.AttackRange : _attackRange;
        set => _attackRange = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// İki saldırı arası bekleme süresi (sn).
    /// </summary>
    public float AttackCooldown
    {
        get => config != null ? config.AttackCooldown : _attackCooldown;
        set => _attackCooldown = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// Saldırı animasyonu başladıktan hasarın uygulanacağı ana kadar geçen gecikme (sn).
    /// </summary>
    public float AttackHitDelay
    {
        get => config != null ? config.AttackHitDelay : _attackHitDelay;
        set => _attackHitDelay = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Vuruş başına hasar.
    /// </summary>
    public int Damage
    {
        get => config != null ? config.Damage : _damage;
        set => _damage = Mathf.Max(1, value);
    }

    /// <summary>
    /// Dönüş yumuşatma hızı.
    /// </summary>
    public float TurnSpeed
    {
        get => config != null ? config.TurnSpeed : _turnSpeed;
        set => _turnSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Hasar alınca geri itilme hızı.
    /// </summary>
    public float KnockbackSpeed
    {
        get => config != null ? config.KnockbackSpeed : _knockbackSpeed;
        set => _knockbackSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Hasar alınca kısa donma süresi (sn).
    /// </summary>
    public float HitStunDuration
    {
        get => config != null ? config.HitStunDuration : _hitStunDuration;
        set => _hitStunDuration = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Yuvadan maksimum uzaklaşma mesafesi (m).
    /// </summary>
    public float HomeReturnDistance => config != null ? config.HomeReturnDistance : DefaultHomeReturnDistance;

    /// <summary>
    /// Bu düşmanın bölüm sonu şefi (Boss) olup olmadığını belirtir.
    /// </summary>
    public bool IsBoss => (config != null && config.IsBoss) || _deathDropPrefab != null;

    /// <summary>Bileşen açık ve düşman hayattaysa saldırgandır (kapalı kapı bekçisi sayılmaz).</summary>
    public bool IsHostile => isActiveAndEnabled && !IsDead;

    /// <summary>Saldırgan varlığın Transform'u (köpek savaş desteği hedefi).</summary>
    public Transform HostileTransform => transform;

    /// <inheritdoc />
    public event Action<IDamageable> Died;

    // Eski scriptlerle (DusmanKur vb.) geriye dönük uyumluluk özellikleri
    public float algilamaMesafesi { get => DetectRange; set => DetectRange = value; }
    public float hiz { get => MoveSpeed; set => MoveSpeed = value; }
    public float saldiriMesafesi { get => AttackRange; set => AttackRange = value; }
    public float saldiriBeklemesi { get => AttackCooldown; set => AttackCooldown = value; }
    public int hasar { get => Damage; set => Damage = value; }
    public int can
    {
        get => _currentHealth;
        set
        {
            _currentHealth = Mathf.Max(0, value);
            if (_currentHealth > _maxHealth)
            {
                _maxHealth = _currentHealth;
            }
        }
    }
    public float donusHizi { get => TurnSpeed; set => TurnSpeed = value; }
    public bool debugKayit { get => _debugLog; set => _debugLog = value; }
    public float tepetmeGucu { get => KnockbackSpeed; set => KnockbackSpeed = value; }
    public float donmaSuresi { get => HitStunDuration; set => HitStunDuration = value; }
    public GameObject olumDrop { get => _deathDropPrefab; set => _deathDropPrefab = value; }
    public string olumMesaji { get => _deathMessage; set => _deathMessage = value; }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();

        if (config != null)
        {
            _maxHealth = config.MaxHealth;
            _currentHealth = _maxHealth;
        }
        else
        {
            _maxHealth = Mathf.Max(1, _currentHealth);
        }
    }

    private void Start()
    {
        _homePosition = transform.position;
        CacheAnimatorParameters();
        ResolvePlayerReference();
    }

    private void CacheAnimatorParameters()
    {
        _hasHitParam = false;
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (var p in _animator.parameters)
        {
            if (p.nameHash == HitParamHash || p.name == "Hit")
            {
                _hasHitParam = true;
                break;
            }
        }
    }

    /// <summary>
    /// Hedef oyuncu referansını doğrudan atar (`Find` çağrısını önler).
    /// </summary>
    /// <param name="playerTransform">Oyuncunun Transform bileşeni.</param>
    public void SetPlayerTarget(Transform playerTransform)
    {
        _playerTransform = playerTransform;
        if (_playerTransform != null)
        {
            _playerHealth = _playerTransform.GetComponent<PlayerHealth>();
            _playerDamageable = _playerTransform.GetComponent<IDamageable>();
        }
    }

    private void ResolvePlayerReference()
    {
        if (_playerTransform != null)
        {
            _playerHealth = _playerTransform.GetComponent<PlayerHealth>();
            _playerDamageable = _playerTransform.GetComponent<IDamageable>();
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            playerObj = GameObject.Find("SurviverMale");
        }

        if (playerObj == null)
        {
            PlayerHealth foundHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            if (foundHealth != null)
            {
                playerObj = foundHealth.gameObject;
            }
        }

        if (playerObj != null)
        {
            SetPlayerTarget(playerObj.transform);
        }
    }

    private void Update()
    {
        if (_isDead || _playerTransform == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        float distanceFromHome = Vector3.Distance(transform.position, _homePosition);
        bool playerDead = (_playerDamageable != null && _playerDamageable.IsDead) || (_playerHealth != null && _playerHealth.IsDead);
        bool shouldChase = !playerDead && distanceToPlayer <= DetectRange && distanceFromHome <= HomeReturnDistance;

        Vector3 horizontalMove = Vector3.zero;

        if (Time.time < _stunEndTime)
        {
            horizontalMove = _knockbackVelocity; // darbe anı: sadece geri tepme işler
        }
        else if (!playerDead && distanceToPlayer <= AttackRange)
        {
            if (_wasChasing && _debugLog && _loggedAttackCount < 3)
            {
                Debug.Log($"[{name}] oyuncuya yetişti ({distanceToPlayer:0.0} m) — saldırıyor");
            }

            _wasChasing = false;
            RotateTowards(_playerTransform.position);

            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + AttackCooldown;
                if (IsAnimatorReady())
                {
                    _animator.SetTrigger(AttackParamHash);
                }

                if (_attackCoroutine != null)
                {
                    StopCoroutine(_attackCoroutine);
                }

                _attackCoroutine = StartCoroutine(PerformDelayedAttack());
            }
        }
        else if (shouldChase)
        {
            if (!_wasChasing && _debugLog && _loggedAttackCount < 3)
            {
                Debug.Log($"[{name}] kovalamaya başladı ({distanceToPlayer:0.0} m)");
            }

            _wasChasing = true;
            RotateTowards(_playerTransform.position);
            horizontalMove = (_playerTransform.position - transform.position).normalized * MoveSpeed;
        }
        else if (distanceFromHome > 0.5f)
        {
            _wasChasing = false;
            RotateTowards(_homePosition);
            horizontalMove = (_homePosition - transform.position).normalized * MoveSpeed;
        }

        if (_controller != null && _controller.enabled)
        {
            if (_controller.isGrounded)
            {
                _verticalVelocity = -1f;
            }
            else
            {
                _verticalVelocity += -20f * Time.deltaTime;
            }

            _controller.Move((horizontalMove + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        _knockbackVelocity = Vector3.Lerp(_knockbackVelocity, Vector3.zero, 12f * Time.deltaTime);

        if (IsAnimatorReady())
        {
            float animSpeed = (Time.time < _stunEndTime || horizontalMove.magnitude <= 0.01f) ? 0f : horizontalMove.magnitude;
            _animator.SetFloat(SpeedParamHash, animSpeed);
        }
    }

    private IEnumerator PerformDelayedAttack()
    {
        float delay = AttackHitDelay;
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        _attackCoroutine = null;

        if (_isDead || _playerTransform == null || Time.time < _stunEndTime)
        {
            yield break;
        }

        if (_playerDamageable == null && _playerTransform != null)
        {
            _playerDamageable = _playerTransform.GetComponent<IDamageable>();
        }

        if (_playerHealth == null && _playerTransform != null)
        {
            _playerHealth = _playerTransform.GetComponent<PlayerHealth>();
        }

        if ((_playerDamageable != null && _playerDamageable.IsDead) || (_playerHealth != null && _playerHealth.IsDead))
        {
            yield break;
        }

        float currentDistance = Vector3.Distance(transform.position, _playerTransform.position);
        if (currentDistance > AttackRange + AttackReachGraceBuffer)
        {
            yield break;
        }

        Vector3 hitDir = _playerTransform.position - transform.position;
        hitDir.y = 0f;
        if (hitDir.sqrMagnitude > 0.001f)
        {
            hitDir.Normalize();
        }

        int damageToDeal = Damage;
        if (_playerDamageable != null)
        {
            _playerDamageable.TakeDamage(damageToDeal, hitDir);
        }
        else if (_playerHealth != null)
        {
            _playerHealth.HasarAl(damageToDeal);
        }

        if (_debugLog && _loggedAttackCount < 3)
        {
            _loggedAttackCount++;
            Debug.Log($"[{name}] VURDU (-{damageToDeal})");
        }
    }

    /// <inheritdoc />
    public void TakeDamage(int amount, Vector3 hitDirection)
    {
        if (_isDead || amount <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        if (_debugLog)
        {
            Debug.Log($"[{name}] hasar -{amount} → can: {_currentHealth}");
        }

        if (_currentHealth <= 0)
        {
            HandleDeath();
            return;
        }

        hitDirection.y = 0f;
        if (hitDirection.sqrMagnitude > 0.001f)
        {
            _knockbackVelocity = hitDirection.normalized * KnockbackSpeed;
        }

        _stunEndTime = Time.time + HitStunDuration;
        if (IsAnimatorReady() && _hasHitParam)
        {
            _animator.SetTrigger(HitParamHash);
        }
    }

    /// <summary>
    /// Geriye dönük uyumluluk için 1 birim hasar uygular.
    /// </summary>
    public void HasarAl()
    {
        TakeDamage(1, Vector3.zero);
    }

    /// <summary>
    /// Geriye dönük uyumluluk için belirtilen vuruş yönüyle 1 birim hasar uygular.
    /// </summary>
    /// <param name="vuranYon">Darbenin yönü.</param>
    public void HasarAl(Vector3 vuranYon)
    {
        TakeDamage(1, vuranYon);
    }

    private void HandleDeath()
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (_debugLog)
        {
            Debug.Log($"[{name}] öldü");
        }
        if (IsAnimatorReady())
        {
            _animator.SetTrigger(DieParamHash);
        }

        if (_deathDropPrefab != null)
        {
            GameObject droppedItem = Instantiate(
                _deathDropPrefab,
                transform.position + Vector3.up * 0.2f,
                Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f));

            if (!droppedItem.TryGetComponent<AxePickup>(out var axePickup))
            {
                axePickup = droppedItem.AddComponent<AxePickup>(); // çift bileşen olmasın
            }

            axePickup.Initialize(_playerTransform);
        }

        if (!string.IsNullOrEmpty(_deathMessage))
        {
            if (ServiceRegistry.TryGet<IMessageService>(out var messageService))
            {
                messageService.Show(_deathMessage);
            }
            else
            {
                MesajPaneli.Goster(_deathMessage);
            }
        }

        if (IsBoss)
        {
            GameEvents.RaiseBossDefeated();
        }

        Died?.Invoke(this);

        if (_controller != null)
        {
            _controller.enabled = false;
        }

        StartCoroutine(SinkAndDestroyRoutine());
    }

    private IEnumerator SinkAndDestroyRoutine()
    {
        yield return new WaitForSecondsRealtime(3f);
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            transform.position = startPos + Vector3.down * (elapsed * 1.2f);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            TurnSpeed * Time.deltaTime);
    }

    private bool IsAnimatorReady()
    {
        return _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, DetectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}
