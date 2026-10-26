using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Yazlab.Core;
using Yazlab.Player;

// Oyuncu sağlığı: hasar alır, canı HPText'e yazar, ölünce kontrol kilitlenir ve
// birkaç saniye sonra başlangıç noktasında tam canla yeniden doğar.
/// <summary>
/// Oyuncunun sağlık durumunu, ölüm/yeniden doğuş akışını ve HP arayüz metnini yöneten bileşen.
/// <see cref="IDamageable"/> arayüzünü uygular.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    private static readonly Vector3 DefaultSpawnPosition = new Vector3(0f, 0.05f, 2f);

    [Header("Yapılandırma ve Referanslar")]
    [SerializeField] private PlayerConfig config;
    [SerializeField] private PlayerSpawnPoint spawnPoint;
    [SerializeField] private Text hpText;

    [Header("Sağlık Parametreleri (Config yoksa kullanılır)")]
    [FormerlySerializedAs("maxCan")]
    [SerializeField] private int _maxHealth = 100;

    [FormerlySerializedAs("can")]
    [SerializeField] private int _currentHealth = 100;

    [FormerlySerializedAs("yenidenDogusSuresi")]
    [SerializeField] private float _respawnDelay = 3f;

    [SerializeField] private bool debugLog = true;

    private PlayerController _movement;
    private CharacterController _characterController;
    private Animator _animator;
    private Vector3 _initialSpawnPosition = DefaultSpawnPosition;
    private Quaternion _initialSpawnRotation = Quaternion.identity;
    private bool _hasInitialPose;
    private bool _isDead;
    private Coroutine _respawnCoroutine;

    /// <inheritdoc />
    public bool IsDead => _isDead || _currentHealth <= 0;

    /// <inheritdoc />
    public int CurrentHealth => _currentHealth;

    /// <inheritdoc />
    public int MaxHealth => config != null ? config.MaxHealth : Mathf.Max(1, _maxHealth);

    /// <summary>
    /// Oyuncu yapılandırma varlığı (varsa maksimum can ve yeniden doğuş süresi buradan okunur).
    /// </summary>
    public PlayerConfig Config
    {
        get => config;
        set => config = value;
    }

    /// <summary>
    /// Ölüm sonrası yeniden doğuşa kadar beklenecek gerçek zamanlı süre (saniye).
    /// </summary>
    public float RespawnDelay => config != null ? config.RespawnDelay : Mathf.Max(0f, _respawnDelay);

    /// <summary>
    /// Can değerinin yazıldığı UI metin bileşeni.
    /// </summary>
    public Text HpText
    {
        get => hpText;
        set
        {
            hpText = value;
            UpdateHealthUi();
        }
    }

    /// <summary>
    /// Oyuncunun yeniden doğacağı sahne noktası referansı.
    /// </summary>
    public PlayerSpawnPoint SpawnPoint
    {
        get => spawnPoint;
        set => spawnPoint = value;
    }

    /// <inheritdoc />
    public event Action<IDamageable> Died;

    /// <summary>
    /// Can değeri değiştiğinde tetiklenir. Parametreler: (mevcutCan, maksimumCan).
    /// </summary>
    public event Action<int, int> HealthChanged;

    /// <summary>
    /// Oyuncu yeniden doğduğunda tetiklenir.
    /// </summary>
    public event Action Revived;

    // Eski scriptlerle (PlayerCombat, EnemyController, DusmanKur) geriye dönük uyumluluk özellikleri
    public int maxCan
    {
        get => MaxHealth;
        set => _maxHealth = Mathf.Max(1, value);
    }

    public int can
    {
        get => _currentHealth;
        set
        {
            _currentHealth = Mathf.Clamp(value, 0, MaxHealth);
            _isDead = _currentHealth <= 0;
            UpdateHealthUi();
        }
    }

    public float yenidenDogusSuresi
    {
        get => RespawnDelay;
        set => _respawnDelay = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        _movement = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();
        _initialSpawnPosition = transform.position;
        _initialSpawnRotation = transform.rotation;
        _hasInitialPose = true;

        _maxHealth = MaxHealth;
        _currentHealth = _maxHealth;
        _isDead = false;
    }

    private void Start()
    {
        _maxHealth = MaxHealth;
        _currentHealth = _maxHealth;
        _isDead = false;

        if (hpText == null)
        {
            // Inspector'dan atanmamışsa yalnızca başlangıçta (bootstrap) bir kez çözümle
            GameObject hpObject = GameObject.Find("HPText");
            if (hpObject != null)
            {
                hpText = hpObject.GetComponent<Text>();
            }
        }

        UpdateHealthUi();
        HealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <inheritdoc />
    public void TakeDamage(int amount, Vector3 hitDirection)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        if (debugLog)
        {
            Debug.Log($"[Oyuncu] hasar -{amount} → can {_currentHealth}");
        }

        UpdateHealthUi();
        HealthChanged?.Invoke(_currentHealth, MaxHealth);

        if (_currentHealth == 0)
        {
            _isDead = true;
            Died?.Invoke(this);

            if (_respawnCoroutine != null)
            {
                StopCoroutine(_respawnCoroutine);
            }

            _respawnCoroutine = StartCoroutine(RespawnRoutine());
        }
    }

    /// <summary>
    /// Yön belirtmeden oyuncuya hasar uygular.
    /// </summary>
    /// <param name="amount">Uygulanacak pozitif hasar miktarı.</param>
    public void TakeDamage(int amount)
    {
        TakeDamage(amount, Vector3.zero);
    }

    /// <summary>
    /// Geriye dönük uyumluluk için <see cref="TakeDamage(int, Vector3)"/> sarmalayıcısı.
    /// </summary>
    /// <param name="miktar">Hasar miktarı.</param>
    public void HasarAl(int miktar)
    {
        TakeDamage(miktar, Vector3.zero);
    }

    /// <summary>
    /// Oyuncuyu belirtilen miktarda iyileştirir (ölü değilse).
    /// </summary>
    /// <param name="amount">İyileştirme miktarı.</param>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        int previous = _currentHealth;
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        if (_currentHealth != previous)
        {
            UpdateHealthUi();
            HealthChanged?.Invoke(_currentHealth, MaxHealth);
        }
    }

    /// <summary>
    /// Oyuncunun canını tamamen doldurur ve ölü durumundan çıkarır.
    /// </summary>
    public void RestoreFullHealth()
    {
        bool wasDead = _isDead || _currentHealth <= 0;
        _maxHealth = MaxHealth;
        _currentHealth = _maxHealth;
        _isDead = false;

        UpdateHealthUi();
        HealthChanged?.Invoke(_currentHealth, MaxHealth);

        if (wasDead)
        {
            Revived?.Invoke();
        }
    }

    private IEnumerator RespawnRoutine()
    {
        if (_movement != null)
        {
            _movement.enabled = false;
        }

        TriggerAnimatorParameter("Die"); // Death state'i oynar; animator açık kalır
        yield return new WaitForSecondsRealtime(RespawnDelay);

        ResolveSpawnPose(out Vector3 targetPosition, out Quaternion targetRotation, out bool applyRotation);

        // CharacterController açıkken transform.position ataması ezildiği/sıçradığı için
        // önce kapatıp pozisyonu atadıktan sonra tekrar açıyoruz (Hata #4 çözümü).
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        bool wasControllerEnabled = _characterController != null && _characterController.enabled;
        if (_characterController != null)
        {
            _characterController.enabled = false;
        }

        transform.position = targetPosition;
        if (applyRotation)
        {
            transform.rotation = targetRotation;
        }

        if (_characterController != null)
        {
            _characterController.enabled = wasControllerEnabled;
        }

        RestoreFullHealth();

        if (_movement != null)
        {
            _movement.enabled = true;
        }

        TriggerAnimatorParameter("Revive");
        _respawnCoroutine = null;
    }

    private void ResolveSpawnPose(out Vector3 position, out Quaternion rotation, out bool applyRotation)
    {
        if (spawnPoint != null)
        {
            position = spawnPoint.Position;
            rotation = spawnPoint.Rotation;
            applyRotation = true;
            return;
        }

        if (ServiceRegistry.TryGet<PlayerSpawnPoint>(out var registeredSpawn) && registeredSpawn != null)
        {
            position = registeredSpawn.Position;
            rotation = registeredSpawn.Rotation;
            applyRotation = true;
            return;
        }

        position = _hasInitialPose ? _initialSpawnPosition : DefaultSpawnPosition;
        rotation = _initialSpawnRotation;
        applyRotation = _hasInitialPose;
    }

    // Controller'da parametre yoksa sessizce geç (Death/Die kurulmadan da çalışsın)
    private void TriggerAnimatorParameter(string paramName)
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (var p in _animator.parameters)
        {
            if (p.name == paramName)
            {
                _animator.SetTrigger(paramName);
                return;
            }
        }
    }

    private void UpdateHealthUi()
    {
        if (hpText != null)
        {
            hpText.text = "HP: " + _currentHealth;
        }
    }
}
