using System;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// <see cref="IDamageable"/> arayüzünü uygulayan yeniden kullanılabilir temel sağlık bileşeni.
    /// Oyuncu, düşman ve savaşılabilir NPC'ler tarafından ortaklaşa kullanılır.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private bool isInvulnerable;

        private int _currentHealth;
        private bool _isInitialized;

        /// <inheritdoc />
        public bool IsDead => _isInitialized && _currentHealth <= 0;

        /// <inheritdoc />
        public int CurrentHealth => _currentHealth;

        /// <inheritdoc />
        public int MaxHealth => maxHealth;

        /// <summary>
        /// Varlığın geçici olarak hasar almaz durumda olup olmadığını belirtir.
        /// </summary>
        public bool IsInvulnerable
        {
            get => isInvulnerable;
            set => isInvulnerable = value;
        }

        /// <inheritdoc />
        public event Action<IDamageable> Died;

        /// <summary>
        /// Can değeri değiştiğinde tetiklenir. Parametreler: (mevcutCan, maksimumCan).
        /// </summary>
        public event Action<int, int> HealthChanged;

        /// <summary>
        /// Geçerli bir hasar alındığında tetiklenir. Parametreler: (hasarMiktarı, vuruşYönü).
        /// </summary>
        public event Action<int, Vector3> Damaged;

        /// <summary>
        /// Varlık yeniden canlandırıldığında veya sağlığı sıfırlanıp doldurulduğunda tetiklenir.
        /// </summary>
        public event Action Revived;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        /// <summary>
        /// Maksimum can değerini yapılandırır ve mevcut canı maksimuma eşitler.
        /// </summary>
        /// <param name="configuredMaxHealth">Yeni maksimum can (en az 1).</param>
        public void Configure(int configuredMaxHealth)
        {
            maxHealth = Mathf.Max(1, configuredMaxHealth);
            _currentHealth = maxHealth;
            _isInitialized = true;
            HealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        /// <inheritdoc />
        public void TakeDamage(int amount, Vector3 hitDirection)
        {
            InitializeIfNeeded();

            if (IsDead || isInvulnerable || amount <= 0)
            {
                return;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            Damaged?.Invoke(amount, hitDirection);
            HealthChanged?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0)
            {
                Died?.Invoke(this);
            }
        }

        /// <summary>
        /// Varlığı belirtilen miktarda iyileştirir (ölü değilse).
        /// </summary>
        /// <param name="amount">Eklenecek pozitif can miktarı.</param>
        public void Heal(int amount)
        {
            InitializeIfNeeded();

            if (IsDead || amount <= 0)
            {
                return;
            }

            int previous = _currentHealth;
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
            if (_currentHealth != previous)
            {
                HealthChanged?.Invoke(_currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// Varlığın sağlığını tamamen doldurur ve ölü durumundan çıkarır (respawn için).
        /// </summary>
        public void RestoreFullHealth()
        {
            bool wasDead = IsDead;
            maxHealth = Mathf.Max(1, maxHealth);
            _currentHealth = maxHealth;
            _isInitialized = true;

            HealthChanged?.Invoke(_currentHealth, maxHealth);
            if (wasDead)
            {
                Revived?.Invoke();
            }
        }

        private void InitializeIfNeeded()
        {
            if (_isInitialized)
            {
                return;
            }

            maxHealth = Mathf.Max(1, maxHealth);
            _currentHealth = maxHealth;
            _isInitialized = true;
        }
    }
}
