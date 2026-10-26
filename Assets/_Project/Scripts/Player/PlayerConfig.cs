using UnityEngine;

namespace Yazlab.Player
{
    /// <summary>
    /// Oyuncu hareket, yerçekimi, sağlık ve yeniden doğuş parametrelerini tutan veri varlığı.
    /// Sihirli sayıları koddan ayırmak için kullanılır.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "Yazlab/Config/Player Config")]
    public class PlayerConfig : ScriptableObject
    {
        [Header("Hareket")]
        [Tooltip("Normal yürüme hızı (m/sn).")]
        [SerializeField] private float walkSpeed = 4.5f;

        [Tooltip("Shift ile koşma hızı (m/sn).")]
        [SerializeField] private float runSpeed = 12f;

        [Tooltip("Hareket yönüne dönüş yumuşatma hızı.")]
        [SerializeField] private float turnSpeed = 12f;

        [Tooltip("Yerçekimi ivmesi (m/sn²).")]
        [SerializeField] private float gravity = -20f;

        [Header("Sağlık ve Yeniden Doğuş")]
        [Tooltip("Oyuncunun maksimum can değeri.")]
        [SerializeField] private int maxHealth = 100;

        [Tooltip("Ölüm sonrası yeniden doğuşa kadar beklenecek gerçek zamanlı süre (saniye).")]
        [SerializeField] private float respawnDelay = 3f;

        /// <summary>
        /// Normal yürüme hızı (m/sn).
        /// </summary>
        public float WalkSpeed => walkSpeed;

        /// <summary>
        /// Shift ile koşma hızı (m/sn).
        /// </summary>
        public float RunSpeed => runSpeed;

        /// <summary>
        /// Hareket yönüne dönüş yumuşatma hızı.
        /// </summary>
        public float TurnSpeed => turnSpeed;

        /// <summary>
        /// Yerçekimi ivmesi (m/sn²).
        /// </summary>
        public float Gravity => gravity;

        /// <summary>
        /// Oyuncunun maksimum can değeri.
        /// </summary>
        public int MaxHealth => Mathf.Max(1, maxHealth);

        /// <summary>
        /// Ölüm sonrası yeniden doğuşa kadar beklenecek gerçek zamanlı süre (saniye).
        /// </summary>
        public float RespawnDelay => Mathf.Max(0f, respawnDelay);
    }
}
