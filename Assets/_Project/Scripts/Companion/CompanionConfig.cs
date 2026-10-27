using UnityEngine;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpek refakatçinin hız, mesafe ve zaman aşımı parametrelerini tutan veri varlığı.
    /// Sihirli sayıları koddan ayırmak için kullanılır.
    /// </summary>
    [CreateAssetMenu(fileName = "CompanionConfig", menuName = "Yazlab/Config/Companion Config")]
    public class CompanionConfig : ScriptableObject
    {
        [Header("Hareket")]
        [Tooltip("Yürüme hızı (m/sn).")]
        [SerializeField] private float walkSpeed = 2.8f;

        [Tooltip("Dörtnala hızı (m/sn).")]
        [SerializeField] private float gallopSpeed = 6.5f;

        [Tooltip("NavMeshAgent ivmesi (m/sn²).")]
        [SerializeField] private float acceleration = 12f;

        [Tooltip("NavMeshAgent dönüş hızı (derece/sn).")]
        [SerializeField] private float angularSpeed = 360f;

        [Header("Takip")]
        [Tooltip("Oyuncuya bu mesafeye (m) yaklaşınca köpek durur (Follow → Idle).")]
        [SerializeField] private float followStopDistance = 3f;

        [Tooltip("Idle durumunda oyuncu durma mesafesinden bu kadar (m) daha uzaklaşınca takip yeniden başlar (titremeyi önler).")]
        [SerializeField] private float followResumeMargin = 1f;

        [Tooltip("Oyuncuya bu mesafeden (m) uzaksa köpek dörtnala koşar.")]
        [SerializeField] private float gallopThreshold = 6.5f;

        [Tooltip("Dörtnaladan yürümeye dönmek için gallopThreshold'un ne kadar (m) altına inilmeli (histerezis).")]
        [SerializeField] private float gallopExitMargin = 1.5f;

        [Tooltip("Hedef yolunun yenilenme aralığı (sn).")]
        [SerializeField] private float repathInterval = 0.25f;

        [Tooltip("Oyuncu konumunu köpeğin NavMesh'ine oturturken aranacak en büyük mesafe (m).")]
        [SerializeField] private float destinationSampleRadius = 3f;

        [Header("Sensör (B11)")]
        [Tooltip("Engel algılama ışınlarının maksimum menzili (m).")]
        [SerializeField] private float sensorRange = 8f;

        [Tooltip("Sensör ışınlarının köpek tabanından çıkış yüksekliği (m).")]
        [SerializeField] private float sensorHeightOffset = 0.45f;

        [Tooltip("Yelpaze şeklinde atılacak engel ışınlarının yatay açıları (derece).")]
        [SerializeField] private float[] sensorRayAngles = { -60f, -30f, 0f, 30f, 60f };

        [Tooltip("Etkileşim hedeflerinin (IInteractable) aranacağı yarıçap (m).")]
        [SerializeField] private float targetDetectRadius = 20f;

        [Tooltip("Engel ışınlarının çarpacağı fizik katmanları.")]
        [SerializeField] private LayerMask obstacleLayerMask = Physics.DefaultRaycastLayers;

        [Tooltip("Etkileşim hedeflerinin aranacağı fizik katmanları.")]
        [SerializeField] private LayerMask targetLayerMask = Physics.DefaultRaycastLayers;

        [Header("Görev (B12)")]
        [Tooltip("Etkileşim hedefine bu mesafeden (m) yakınsa köpek etkinleştirir.")]
        [SerializeField] private float activateDistance = 1.6f;

        [Tooltip("Köpek bu süre (sn) boyunca ilerleyemezse takıldı sayılır.")]
        [SerializeField] private float stuckTimeout = 4f;

        [Tooltip("Takılma sonrası en fazla kaç kez yeniden yol denenir.")]
        [SerializeField] private int maxRecoverRetries = 3;

        [Header("Köşe ve İlerleme (B13)")]
        [Tooltip("Yol köşesindeki dönüş bu açıdan (derece) büyükse köpek köşeden önce yavaşlar.")]
        [SerializeField] private float cornerAngleThreshold = 30f;

        [Tooltip("90° ve üstü dönüşte köşeden geçiş hızı (m/sn).")]
        [SerializeField] private float cornerMinSpeed = 2.2f;

        [Tooltip("Köşe öncesi fren yavaşlaması (m/sn²); Acceleration'dan küçük olmalı.")]
        [SerializeField] private float cornerBrakeDeceleration = 8f;

        [Tooltip("Köşeden sonraki çıkış yönünün ölçüldüğü yol mesafesi (m); yakın köşe çiftlerini tek dönüş sayar.")]
        [SerializeField] private float cornerLookAhead = 1.5f;

        [Tooltip("Sensörün sol ve sağ mesafesi ikisi birden bundan (m) küçükse dar geçit sayılır, yürüme hızına inilir.")]
        [SerializeField] private float narrowPassageClearance = 1.2f;

        [Tooltip("Hedefe kalan yol bu süre (sn) boyunca kısalmazsa takıldı sayılır (hız yüksek olsa bile).")]
        [SerializeField] private float progressTimeout = 3f;

        [Tooltip("İlerleme sayılması için kalan yolun en az bu kadar (m) kısalması gerekir.")]
        [SerializeField] private float progressMinGain = 0.75f;

        [Header("Savaş Desteği (B21)")]
        [Tooltip("Köpek oyuncunun yakınındaki saldırgan düşmanlara saldırsın mı.")]
        [SerializeField] private bool combatAssist = true;

        [Tooltip("Oyuncunun bu yarıçapındaki (m) saldırgan düşman köpeği savaşa çeker.")]
        [SerializeField] private float assistRadius = 9f;

        [Tooltip("Savaşta köpek oyuncudan bu mesafeden (m) fazla uzaklaşırsa takibe döner.")]
        [SerializeField] private float assistLeash = 15f;

        [Tooltip("Köpeğin ısırma menzili (m, düşman merkezine yatay).")]
        [SerializeField] private float biteRange = 2.2f;

        [Tooltip("Isırma başına hasar.")]
        [SerializeField] private int biteDamage = 1;

        [Tooltip("İki ısırma arası bekleme (sn).")]
        [SerializeField] private float biteCooldown = 1.5f;

        [Tooltip("Tehdit taraması aralığı (sn).")]
        [SerializeField] private float threatScanInterval = 0.3f;

        [Tooltip("Tehdit taramasında kullanılan fizik katmanları.")]
        [SerializeField] private LayerMask threatLayerMask = Physics.DefaultRaycastLayers;

        [Header("Hata Ayıklama")]
        [Tooltip("Durum geçişlerini Console'a yazar.")]
        [SerializeField] private bool debugLog = true;

        /// <summary>Yürüme hızı (m/sn).</summary>
        public float WalkSpeed => Mathf.Max(0.1f, walkSpeed);

        /// <summary>Dörtnala hızı (m/sn).</summary>
        public float GallopSpeed => Mathf.Max(WalkSpeed, gallopSpeed);

        /// <summary>NavMeshAgent ivmesi (m/sn²).</summary>
        public float Acceleration => Mathf.Max(0.1f, acceleration);

        /// <summary>NavMeshAgent dönüş hızı (derece/sn).</summary>
        public float AngularSpeed => Mathf.Max(1f, angularSpeed);

        /// <summary>Oyuncuya bu mesafeye (m) yaklaşınca köpek durur.</summary>
        public float FollowStopDistance => Mathf.Max(0.5f, followStopDistance);

        /// <summary>Idle'dan Follow'a dönmek için durma mesafesine eklenen pay (m).</summary>
        public float FollowResumeMargin => Mathf.Max(0f, followResumeMargin);

        /// <summary>Bu mesafeden uzakta köpek dörtnala koşar (m).</summary>
        public float GallopThreshold => Mathf.Max(FollowStopDistance, gallopThreshold);

        /// <summary>Dörtnaladan yürümeye dönüş histerezisi (m).</summary>
        public float GallopExitMargin => Mathf.Max(0f, gallopExitMargin);

        /// <summary>Hedef yolunun yenilenme aralığı (sn).</summary>
        public float RepathInterval => Mathf.Max(0.05f, repathInterval);

        /// <summary>Oyuncu konumunu NavMesh'e oturtma yarıçapı (m).</summary>
        public float DestinationSampleRadius => Mathf.Max(0.5f, destinationSampleRadius);

        /// <summary>Engel algılama ışınlarının menzili (m).</summary>
        public float SensorRange => Mathf.Max(0.5f, sensorRange);

        /// <summary>Sensör ışınlarının köpek tabanından çıkış yüksekliği (m).</summary>
        public float SensorHeightOffset => Mathf.Max(0.05f, sensorHeightOffset);

        /// <summary>Yelpaze ışınlarının yatay açıları (derece).</summary>
        public float[] SensorRayAngles => sensorRayAngles;

        /// <summary>Etkileşim hedeflerinin aranacağı yarıçap (m).</summary>
        public float TargetDetectRadius => Mathf.Max(1f, targetDetectRadius);

        /// <summary>Engel ışınlarının çarpacağı fizik katmanları.</summary>
        public LayerMask ObstacleLayerMask => obstacleLayerMask;

        /// <summary>Etkileşim hedeflerinin aranacağı fizik katmanları.</summary>
        public LayerMask TargetLayerMask => targetLayerMask;

        /// <summary>Etkileşim mesafesi (m).</summary>
        public float ActivateDistance => Mathf.Max(0.1f, activateDistance);

        /// <summary>Takılma zaman aşımı (sn).</summary>
        public float StuckTimeout => Mathf.Max(0.5f, stuckTimeout);

        /// <summary>Takılma sonrası en fazla yeniden deneme sayısı.</summary>
        public int MaxRecoverRetries => Mathf.Max(0, maxRecoverRetries);

        /// <summary>Yavaşlamayı başlatan köşe dönüş açısı (derece).</summary>
        public float CornerAngleThreshold => Mathf.Clamp(cornerAngleThreshold, 0f, 89f);

        /// <summary>Keskin (≥ 90°) köşeden geçiş hızı (m/sn).</summary>
        public float CornerMinSpeed => Mathf.Clamp(cornerMinSpeed, 0.3f, WalkSpeed);

        /// <summary>Köşe öncesi fren yavaşlaması (m/sn²).</summary>
        public float CornerBrakeDeceleration => Mathf.Max(0.5f, cornerBrakeDeceleration);

        /// <summary>Köşe çıkış yönü için ileri bakma mesafesi (m).</summary>
        public float CornerLookAhead => Mathf.Max(0.2f, cornerLookAhead);

        /// <summary>Dar geçit eşiği (m).</summary>
        public float NarrowPassageClearance => Mathf.Max(0.1f, narrowPassageClearance);

        /// <summary>İlerleme bekçisi zaman aşımı (sn).</summary>
        public float ProgressTimeout => Mathf.Max(0.5f, progressTimeout);

        /// <summary>İlerleme sayılacak en küçük kalan yol azalması (m).</summary>
        public float ProgressMinGain => Mathf.Max(0.05f, progressMinGain);

        /// <summary>Savaş desteği açık mı.</summary>
        public bool CombatAssist => combatAssist;

        /// <summary>Oyuncu çevresinde tehdit arama yarıçapı (m).</summary>
        public float AssistRadius => Mathf.Max(1f, assistRadius);

        /// <summary>Savaşta oyuncudan en fazla uzaklık (m).</summary>
        public float AssistLeash => Mathf.Max(AssistRadius, assistLeash);

        /// <summary>Isırma menzili (m).</summary>
        public float BiteRange => Mathf.Max(0.5f, biteRange);

        /// <summary>Isırma hasarı.</summary>
        public int BiteDamage => Mathf.Max(1, biteDamage);

        /// <summary>Isırma bekleme süresi (sn).</summary>
        public float BiteCooldown => Mathf.Max(0.2f, biteCooldown);

        /// <summary>Tehdit tarama aralığı (sn).</summary>
        public float ThreatScanInterval => Mathf.Max(0.05f, threatScanInterval);

        /// <summary>Tehdit tarama katmanları.</summary>
        public LayerMask ThreatLayerMask => threatLayerMask;

        /// <summary>Durum geçişleri Console'a yazılsın mı.</summary>
        public bool DebugLog => debugLog;
    }
}
