using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Yazlab.Core;
using Yazlab.Player;

// Sol tık / F ile yumruk. Hasar, yumruk animasyonunun vuruş anına denk gelir
// (vurulmaGecikmesi); isabet alınca kısa zaman donması (hit-stop) vuruş hissi verir.
// debugKayit: ilk birkaç yumruğu Console'a yazar (zincirin nerede kırıldığını görmek için).
/// <summary>
/// Oyuncunun yakın dövüş (yumruk ve balta) saldırılarını yöneten bileşen.
/// <see cref="Physics.OverlapSphereNonAlloc"/> ile <see cref="IDamageable"/> hedefleri bulur ve
/// HitStop için <see cref="ITimeScaleService"/> kullanır.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private const int OverlapBufferSize = 32;
    private const int MaxUniqueTargetsPerSwing = 16;
    private const float DefaultHitStopScale = 0.05f;
    private static readonly int AttackParamHash = Animator.StringToHash("Attack");

    [Header("Bağımlılıklar")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private LayerMask hitLayerMask = ~0;

    [Header("Saldırı Menzili ve Zamanlama")]
    [FormerlySerializedAs("menzil")]
    [SerializeField] private float _attackRange = 3.2f; // karakterler dev (4.2 birim) — görsel temasa uygun geniş

    [FormerlySerializedAs("aciDerece")]
    [SerializeField] private float _attackAngle = 90f;

    [FormerlySerializedAs("vurusBeklemesi")]
    [SerializeField] private float _attackCooldown = 0.55f;

    [Tooltip("Yumruktan hasara kadar geçen süre — animasyonun vuruş anına denk gelmeli")]
    [FormerlySerializedAs("vurulmaGecikmesi")]
    [SerializeField] private float _attackHitDelay = 0.22f;

    [Tooltip("İsabet alınca oyundaki kısa yavaşlatma (0 = kapalı)")]
    [FormerlySerializedAs("hitStopSuresi")]
    [SerializeField] private float _hitStopDuration = 0.06f;

    [Header("Hasar")]
    [FormerlySerializedAs("hasar")]
    [SerializeField] private int _currentDamage = 1; // kurt 2 canlı → 2 yumruk

    [Tooltip("Baltasız temel hasar — her oyun başında hasar buna döner")]
    [FormerlySerializedAs("tabanHasar")]
    [SerializeField] private int _baseDamage = 1;

    [Tooltip("Balta kuşanınca hasarın katlanacağı çarpan")]
    [FormerlySerializedAs("baltaHasarCarpani")]
    [SerializeField] private int _axeDamageMultiplier = 2;

    [FormerlySerializedAs("debugKayit")]
    [SerializeField] private bool _debugLog = true;

    private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
    private readonly IDamageable[] _damagedTargetsBuffer = new IDamageable[MaxUniqueTargetsPerSwing];

    private Animator _animator;
    private IDamageable _selfDamageable;
    private PlayerHealth _playerHealth;
    private float _nextAttackTime;
    private int _loggedSwingCount;
    private bool _isAttacking; // yumruğun vuruş anına kadar yeni yumruk başlamasın
    private bool _hasAxe;

    /// <summary>
    /// Oyuncu girdi okuyucu bileşeni.
    /// </summary>
    public PlayerInputReader InputReader
    {
        get => inputReader;
        set => inputReader = value;
    }

    /// <summary>
    /// Oyuncunun şu anki vuruş hasarı (baltasız 1, baltalı 2).
    /// </summary>
    public int CurrentDamage => _currentDamage;

    /// <summary>
    /// Baltasız taban hasar değeri.
    /// </summary>
    public int BaseDamage
    {
        get => _baseDamage;
        set
        {
            _baseDamage = Mathf.Max(1, value);
            RecalculateDamage();
        }
    }

    /// <summary>
    /// Balta kuşanıldığında uygulanacak hasar çarpanı.
    /// </summary>
    public int AxeDamageMultiplier
    {
        get => _axeDamageMultiplier;
        set
        {
            _axeDamageMultiplier = Mathf.Max(1, value);
            RecalculateDamage();
        }
    }

    /// <summary>
    /// Oyuncunun baltayı kuşanmış olup olmadığını belirtir.
    /// </summary>
    public bool HasAxe => _hasAxe;

    /// <summary>
    /// Saldırı menzili (m).
    /// </summary>
    public float AttackRange
    {
        get => _attackRange;
        set => _attackRange = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// İleri yönden maksimum vuruş açısı (derece).
    /// </summary>
    public float AttackAngle
    {
        get => _attackAngle;
        set => _attackAngle = Mathf.Clamp(value, 1f, 180f);
    }

    /// <summary>
    /// İki saldırı arası bekleme süresi (sn).
    /// </summary>
    public float AttackCooldown
    {
        get => _attackCooldown;
        set => _attackCooldown = Mathf.Max(0.05f, value);
    }

    /// <summary>
    /// Vuruş animasyonu başladıktan hasarın uygulanacağı ana kadar geçen süre (sn).
    /// </summary>
    public float AttackHitDelay
    {
        get => _attackHitDelay;
        set => _attackHitDelay = Mathf.Max(0f, value);
    }

    /// <summary>
    /// İsabet alındığında uygulanacak HitStop süresi (gerçek saniye).
    /// </summary>
    public float HitStopDuration
    {
        get => _hitStopDuration;
        set => _hitStopDuration = Mathf.Max(0f, value);
    }

    // Eski scriptlerle (DusmanKur, AxePickup) geriye dönük uyumluluk özellikleri
    public float menzil { get => AttackRange; set => AttackRange = value; }
    public float aciDerece { get => AttackAngle; set => AttackAngle = value; }
    public float vurusBeklemesi { get => AttackCooldown; set => AttackCooldown = value; }
    public float vurulmaGecikmesi { get => AttackHitDelay; set => AttackHitDelay = value; }
    public float hitStopSuresi { get => HitStopDuration; set => HitStopDuration = value; }
    public int hasar { get => _currentDamage; set => _currentDamage = Mathf.Max(1, value); }
    public int tabanHasar { get => BaseDamage; set => BaseDamage = value; }
    public int baltaHasarCarpani { get => AxeDamageMultiplier; set => AxeDamageMultiplier = value; }
    public bool debugKayit { get => _debugLog; set => _debugLog = value; }

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _playerHealth = GetComponent<PlayerHealth>();
        _selfDamageable = GetComponent<IDamageable>();

        if (inputReader == null)
        {
            TryGetComponent(out inputReader);
        }
    }

    private void Start()
    {
        _hasAxe = false;
        RecalculateDamage(); // sahneye kaydedilmiş eski değeri değil, temel hasarı kullan
    }

    /// <summary>
    /// Baltayı kuşanır ve vuruş hasarını çarpanla günceller (idempotent).
    /// </summary>
    public void EquipAxe()
    {
        _hasAxe = true;
        RecalculateDamage();
        if (_debugLog)
        {
            Debug.Log($"[Saldiri] balta kuşanıldı — hasar {_currentDamage}");
        }
    }

    /// <summary>
    /// Geriye dönük uyumluluk için <see cref="EquipAxe"/> sarmalayıcısı (AxePickup çağırır).
    /// </summary>
    public void BaltaKusandi()
    {
        EquipAxe();
    }

    private void RecalculateDamage()
    {
        _baseDamage = Mathf.Max(1, _baseDamage);
        _axeDamageMultiplier = Mathf.Max(1, _axeDamageMultiplier);
        _currentDamage = _hasAxe ? _baseDamage * _axeDamageMultiplier : _baseDamage;
    }

    private void Update()
    {
        if (InputLock.IsLocked)
        {
            return;
        }

        if ((_selfDamageable != null && _selfDamageable.IsDead) || (_playerHealth != null && _playerHealth.IsDead))
        {
            return; // ölüyken yumruk yok
        }

        bool attackPressed = inputReader != null
            ? inputReader.AttackPressed
            : (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F));

        if (!attackPressed || Time.time < _nextAttackTime || _isAttacking)
        {
            return;
        }

        _nextAttackTime = Time.time + _attackCooldown;
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            _animator.SetTrigger(AttackParamHash);
        }

        _isAttacking = true;
        StartCoroutine(PerformAttackSwing());
    }

    private IEnumerator PerformAttackSwing()
    {
        if (_attackHitDelay > 0f)
        {
            yield return new WaitForSeconds(_attackHitDelay); // yumruk hedefe varana kadar bekle
        }

        _isAttacking = false;

        if ((_selfDamageable != null && _selfDamageable.IsDead) || (_playerHealth != null && _playerHealth.IsDead))
        {
            yield break;
        }

        Vector3 sphereCenter = transform.position + Vector3.up * 1.0f;
        float sphereRadius = _attackRange + 2.0f;
        int colliderCount = Physics.OverlapSphereNonAlloc(
            sphereCenter,
            sphereRadius,
            _overlapBuffer,
            hitLayerMask,
            QueryTriggerInteraction.Ignore);

        int hitCount = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null || col.transform.root == transform.root)
            {
                continue;
            }

            if (!col.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable == null || damageable.IsDead || ReferenceEquals(damageable, _selfDamageable))
            {
                continue;
            }

            if (ContainsTarget(damageable, hitCount))
            {
                continue;
            }

            Transform targetTransform = (damageable as Component)?.transform ?? col.transform;
            Vector3 toTarget = targetTransform.position - transform.position;
            toTarget.y = 0f;
            float horizontalDistance = toTarget.magnitude;

            if (horizontalDistance < closestDistance)
            {
                closestDistance = horizontalDistance;
            }

            if (horizontalDistance > _attackRange)
            {
                continue;
            }

            if (horizontalDistance > 0.001f && Vector3.Angle(transform.forward, toTarget) > _attackAngle)
            {
                continue;
            }

            if (hitCount < MaxUniqueTargetsPerSwing)
            {
                _damagedTargetsBuffer[hitCount] = damageable;
            }

            hitCount++;
            Vector3 hitDirection = horizontalDistance > 0.001f ? toTarget.normalized : transform.forward;
            damageable.TakeDamage(_currentDamage, hitDirection);
        }

        // Referansları temizle
        for (int i = 0; i < hitCount && i < MaxUniqueTargetsPerSwing; i++)
        {
            _damagedTargetsBuffer[i] = null;
        }

        if (hitCount > 0 && _hitStopDuration > 0f)
        {
            TriggerHitStop();
        }

        if (_debugLog && _loggedSwingCount < 5)
        {
            _loggedSwingCount++;
            Debug.Log(
                $"[Saldiri] vuruş #{_loggedSwingCount} (hasar={_currentDamage}) → {hitCount} hedef vuruldu " +
                $"(en yakın {(closestDistance < float.MaxValue ? closestDistance : -1f):0.0} m, menzil {_attackRange:0.0} m, açı {_attackAngle:0}°)");
        }
    }

    private bool ContainsTarget(IDamageable target, int count)
    {
        int limit = Mathf.Min(count, MaxUniqueTargetsPerSwing);
        for (int i = 0; i < limit; i++)
        {
            if (ReferenceEquals(_damagedTargetsBuffer[i], target))
            {
                return true;
            }
        }

        return false;
    }

    private void TriggerHitStop()
    {
        if (!ServiceRegistry.TryGet<ITimeScaleService>(out var timeScaleService))
        {
            if (!TryGetComponent<TimeScaleService>(out var fallbackService))
            {
                fallbackService = gameObject.AddComponent<TimeScaleService>();
            }

            timeScaleService = fallbackService;
        }

        timeScaleService.TriggerHitStop(_hitStopDuration, DefaultHitStopScale);
    }

    // Menzil dairesini Scene görünümünde göster (oyuncuyu seçince)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}
