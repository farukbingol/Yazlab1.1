using UnityEngine;
using UnityEngine.AI;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpeğin görev yolunu kendisi hesaplayıp (<see cref="NavMesh.CalculatePath"/>) agent'a verir ve yol köşelerini
    /// saklar. Böylece her karede tahsis yapmadan (1) önümüzdeki köşelerin dönüş açısına göre güvenli hız sınırı ve
    /// (2) hedefe kalan yol uzunluğu hesaplanabilir. NavMeshAgent köşelerde kendiliğinden yavaşlamadığı için
    /// dar tünel ağzında dörtnala gelen köpek girişi kaçırıp duvar boyunca salınıyordu (B13 testi).
    /// </summary>
    public sealed class CompanionPathPlanner
    {
        private const int MaxCorners = 64;
        private const float MinSegmentSqr = 0.0001f;

        private readonly NavMeshAgent _agent;
        private readonly CompanionConfig _config;
        private readonly NavMeshPath _path = new NavMeshPath();
        private readonly Vector3[] _corners = new Vector3[MaxCorners];
        private int _cornerCount;

        public CompanionPathPlanner(NavMeshAgent agent, CompanionConfig config)
        {
            _agent = agent;
            _config = config;
        }

        /// <summary>Planlanmış ve agent'a verilmiş bir yol var mı.</summary>
        public bool HasPath => _cornerCount > 1;

        /// <summary>Son planlanan yolun durumu.</summary>
        public NavMeshPathStatus Status => _path.status;

        /// <summary>Planlanmış yolu unutur (agent'ın kendi yolu etkilenmez).</summary>
        public void Clear()
        {
            _cornerCount = 0;
        }

        /// <summary>
        /// Agent konumundan <paramref name="destination"/>'a yolu hesaplar ve agent'a verir.
        /// Yol bulunamazsa <c>false</c> döner (çağıran <see cref="NavMeshAgent.SetDestination"/> ile devam edebilir).
        /// </summary>
        public bool PlanTo(Vector3 destination)
        {
            _cornerCount = 0;
            if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
            {
                return false;
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _agent.agentTypeID,
                areaMask = _agent.areaMask
            };

            if (!NavMesh.CalculatePath(_agent.nextPosition, destination, filter, _path) ||
                _path.status == NavMeshPathStatus.PathInvalid ||
                !_agent.SetPath(_path))
            {
                return false;
            }

            _cornerCount = _path.GetCornersNonAlloc(_corners);
            return true;
        }

        /// <summary>
        /// Önümüzdeki köşeler için güvenli hız sınırını (m/sn) döndürür: dönüş açısı eşiği aşan her köşe için
        /// köşe hızı açıyla <paramref name="maxSpeed"/>'den <see cref="CompanionConfig.CornerMinSpeed"/>'e iner,
        /// köşeye kalan mesafe <c>d</c> için izin verilen hız <c>√(v_köşe² + 2·a·d)</c> olur; en küçüğü seçilir.
        /// </summary>
        public float ComputeSpeedLimit(float maxSpeed)
        {
            int start = SteeringIndex();
            if (start < 0)
            {
                return maxSpeed;
            }

            float minSpeed = Mathf.Min(_config.CornerMinSpeed, maxSpeed);
            float brake = _config.CornerBrakeDeceleration;
            float threshold = _config.CornerAngleThreshold;
            float scanRange = (maxSpeed * maxSpeed - minSpeed * minSpeed) / (2f * brake) + 0.5f;

            Vector3 previous = Flat(_agent.nextPosition);
            float distanceToCorner = PlanarDistance(previous, _corners[start]);
            float limit = maxSpeed;

            // Son köşe hedefin kendisidir; orada agent'ın autoBraking/stoppingDistance davranışı geçerli.
            for (int i = start; i < _cornerCount - 1 && distanceToCorner <= scanRange; i++)
            {
                Vector3 corner = Flat(_corners[i]);
                Vector3 incoming = corner - previous;
                Vector3 outgoing = PointAlongPath(i, _config.CornerLookAhead) - corner;

                if (incoming.sqrMagnitude > MinSegmentSqr && outgoing.sqrMagnitude > MinSegmentSqr)
                {
                    float angle = Vector3.Angle(incoming, outgoing);
                    if (angle > threshold)
                    {
                        float severity = Mathf.InverseLerp(threshold, 90f, angle);
                        float cornerSpeed = Mathf.Lerp(maxSpeed, minSpeed, severity);
                        float allowed = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * brake * distanceToCorner);
                        limit = Mathf.Min(limit, allowed);
                    }
                }

                distanceToCorner += PlanarDistance(_corners[i], _corners[i + 1]);
                previous = corner;
            }

            return Mathf.Clamp(limit, minSpeed, maxSpeed);
        }

        /// <summary>Agent konumundan planlanmış yol boyunca hedefe kalan yatay uzunluk (m); yol yoksa sonsuz.</summary>
        public float RemainingLength()
        {
            int start = SteeringIndex();
            if (start < 0)
            {
                return float.PositiveInfinity;
            }

            float length = PlanarDistance(_agent.nextPosition, _corners[start]);
            for (int i = start; i < _cornerCount - 1; i++)
            {
                length += PlanarDistance(_corners[i], _corners[i + 1]);
            }

            return length;
        }

        /// <summary>Agent'ın şu an yöneldiği köşenin (<see cref="NavMeshAgent.steeringTarget"/>) planlanmış yoldaki sırası.</summary>
        private int SteeringIndex()
        {
            if (!HasPath || _agent == null || !_agent.hasPath)
            {
                return -1;
            }

            Vector3 steering = _agent.steeringTarget;
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int i = 1; i < _cornerCount; i++)
            {
                float sqr = (Flat(_corners[i]) - Flat(steering)).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        /// <summary><paramref name="fromIndex"/> köşesinden yol boyunca <paramref name="distance"/> kadar ilerideki yatay nokta.</summary>
        private Vector3 PointAlongPath(int fromIndex, float distance)
        {
            float remaining = distance;
            for (int i = fromIndex; i < _cornerCount - 1; i++)
            {
                Vector3 a = Flat(_corners[i]);
                Vector3 b = Flat(_corners[i + 1]);
                float segment = Vector3.Distance(a, b);
                if (segment >= remaining)
                {
                    return segment > 0f ? Vector3.Lerp(a, b, remaining / segment) : b;
                }

                remaining -= segment;
            }

            return Flat(_corners[_cornerCount - 1]);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
