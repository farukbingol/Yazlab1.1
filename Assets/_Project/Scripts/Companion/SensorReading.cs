using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpek refakatçinin sensör taraması sonucunda ürettiği anlık çevre ve hedef gözlem verisi.
    /// </summary>
    public readonly struct SensorReading
    {
        /// <summary>Tam ileri (0°) ışınının ölçtüğü mesafe (m). Engel yoksa sensör menziline eşittir.</summary>
        public float FrontDistance { get; }

        /// <summary>Sol yelpaze ışınlarının (-30°, -60° vb.) ölçtüğü en yakın engel mesafesi (m).</summary>
        public float LeftDistance { get; }

        /// <summary>Sağ yelpaze ışınlarının (+30°, +60° vb.) ölçtüğü en yakın engel mesafesi (m).</summary>
        public float RightDistance { get; }

        /// <summary>Tüm ışınlar arasındaki en yakın engelin mesafesi (m). Engel yoksa sensör menziline eşittir.</summary>
        public float MinObstacleDistance { get; }

        /// <summary>En yakın engelin köpeğin ileri yönüne göre yatay açısı (derece, sol negatif / sağ pozitif).</summary>
        public float MinObstacleAngle { get; }

        /// <summary>Işınlar arasında önü en açık olan kaçış yönünün yatay açısı (derece).</summary>
        public float ClearDirectionAngle { get; }

        /// <summary>Sensör menzili içinde en az bir engele çarpıldıysa <c>true</c>.</summary>
        public bool HasObstacle { get; }

        /// <summary>Görüş hattında algılanan etkileşim hedefi (yoksa <c>null</c>).</summary>
        public IInteractable VisibleTarget { get; }

        /// <summary>Algılanan veya sorgulanan hedefe olan yatay mesafe (m). Hedef yoksa <see cref="float.PositiveInfinity"/>.</summary>
        public float TargetDistance { get; }

        /// <summary>Algılanan veya sorgulanan hedefin köpeğin ileri yönüne göre yatay açısı (derece).</summary>
        public float TargetAngle { get; }

        /// <summary>Hedefe engel çarpmadan doğrudan görüş ışını (Line of Sight) ulaşıyorsa <c>true</c>.</summary>
        public bool HasLineOfSightToTarget { get; }

        public SensorReading(
            float frontDistance,
            float leftDistance,
            float rightDistance,
            float minObstacleDistance,
            float minObstacleAngle,
            float clearDirectionAngle,
            bool hasObstacle,
            IInteractable visibleTarget,
            float targetDistance,
            float targetAngle,
            bool hasLineOfSightToTarget)
        {
            FrontDistance = frontDistance;
            LeftDistance = leftDistance;
            RightDistance = rightDistance;
            MinObstacleDistance = minObstacleDistance;
            MinObstacleAngle = minObstacleAngle;
            ClearDirectionAngle = clearDirectionAngle;
            HasObstacle = hasObstacle;
            VisibleTarget = visibleTarget;
            TargetDistance = targetDistance;
            TargetAngle = targetAngle;
            HasLineOfSightToTarget = hasLineOfSightToTarget;
        }
    }
}
