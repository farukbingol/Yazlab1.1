using UnityEngine;

namespace Yazlab.Enemies
{
    /// <summary>
    /// Düşman (kurt ve kabile şefi) algılama, hareket, saldırı ve sağlık parametrelerini tutan veri varlığı.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Yazlab/Config/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Algılama ve Hareket")]
        [Tooltip("Oyuncuyu fark edip kovalamaya başlama mesafesi (m).")]
        [SerializeField] private float detectRange = 16f;

        [Tooltip("Yürüme/kovalama hızı (m/sn).")]
        [SerializeField] private float moveSpeed = 3.4f;

        [Tooltip("Hedefe dönüş yumuşatma hızı.")]
        [SerializeField] private float turnSpeed = 8f;

        [Tooltip("Başlangıç yuvasından maksimum uzaklaşma mesafesi (m).")]
        [SerializeField] private float homeReturnDistance = 40f;

        [Header("Saldırı")]
        [Tooltip("Saldırıya geçme mesafesi (m).")]
        [SerializeField] private float attackRange = 1.6f;

        [Tooltip("İki saldırı arasındaki bekleme süresi (sn).")]
        [SerializeField] private float attackCooldown = 1.4f;

        [Tooltip("Saldırı animasyonu başladıktan hasarın uygulanacağı ana kadar geçen gecikme (sn).")]
        [SerializeField] private float attackHitDelay = 0.25f;

        [Tooltip("Her vuruşta oyuncuya verilen hasar miktarı.")]
        [SerializeField] private int damage = 8;

        [Header("Sağlık ve Darbe Tepkisi")]
        [Tooltip("Düşmanın maksimum can değeri (baltasız yumruk=1, baltalı=2 düşürür).")]
        [SerializeField] private int maxHealth = 2;

        [Tooltip("Hasar aldığında geri itilme hızı.")]
        [SerializeField] private float knockbackSpeed = 6f;

        [Tooltip("Hasar aldığında kısa süreli hareket donma süresi (sn).")]
        [SerializeField] private float hitStunDuration = 0.12f;

        [Tooltip("Bu düşman bölüm sonu şefi (Boss) mi?")]
        [SerializeField] private bool isBoss;

        /// <summary>
        /// Oyuncuyu fark edip kovalamaya başlama mesafesi (m).
        /// </summary>
        public float DetectRange => Mathf.Max(0f, detectRange);

        /// <summary>
        /// Yürüme/kovalama hızı (m/sn).
        /// </summary>
        public float MoveSpeed => Mathf.Max(0f, moveSpeed);

        /// <summary>
        /// Hedefe dönüş yumuşatma hızı.
        /// </summary>
        public float TurnSpeed => Mathf.Max(0f, turnSpeed);

        /// <summary>
        /// Başlangıç yuvasından maksimum uzaklaşma mesafesi (m).
        /// </summary>
        public float HomeReturnDistance => Mathf.Max(1f, homeReturnDistance);

        /// <summary>
        /// Saldırıya geçme mesafesi (m).
        /// </summary>
        public float AttackRange => Mathf.Max(0.1f, attackRange);

        /// <summary>
        /// İki saldırı arasındaki bekleme süresi (sn).
        /// </summary>
        public float AttackCooldown => Mathf.Max(0.1f, attackCooldown);

        /// <summary>
        /// Saldırı animasyonu başladıktan hasarın uygulanacağı ana kadar geçen gecikme (sn).
        /// </summary>
        public float AttackHitDelay => Mathf.Max(0f, attackHitDelay);

        /// <summary>
        /// Her vuruşta oyuncuya verilen hasar miktarı.
        /// </summary>
        public int Damage => Mathf.Max(1, damage);

        /// <summary>
        /// Düşmanın maksimum can değeri.
        /// </summary>
        public int MaxHealth => Mathf.Max(1, maxHealth);

        /// <summary>
        /// Hasar aldığında geri itilme hızı.
        /// </summary>
        public float KnockbackSpeed => Mathf.Max(0f, knockbackSpeed);

        /// <summary>
        /// Hasar aldığında kısa süreli hareket donma süresi (sn).
        /// </summary>
        public float HitStunDuration => Mathf.Max(0f, hitStunDuration);

        /// <summary>
        /// Bu düşmanın şef (Boss) olup olmadığını belirtir.
        /// </summary>
        public bool IsBoss => isBoss;

        /// <summary>
        /// Editor kurulum araçlarından varsayılan değerleri atamak için yapılandırma metodu.
        /// </summary>
        public void Configure(
            float configuredDetectRange,
            float configuredMoveSpeed,
            float configuredAttackRange,
            float configuredAttackCooldown,
            float configuredAttackHitDelay,
            int configuredDamage,
            int configuredMaxHealth,
            bool configuredIsBoss)
        {
            detectRange = configuredDetectRange;
            moveSpeed = configuredMoveSpeed;
            attackRange = configuredAttackRange;
            attackCooldown = configuredAttackCooldown;
            attackHitDelay = configuredAttackHitDelay;
            damage = configuredDamage;
            maxHealth = configuredMaxHealth;
            isBoss = configuredIsBoss;
        }
    }
}
