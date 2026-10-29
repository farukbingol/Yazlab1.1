using UnityEngine;
using UnityEngine.AI;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpeğin görev yolunu kendisi hesaplayıp (<see cref="NavMesh.CalculatePath"/>) agent'a verir ve yol köşelerini
    /// saklar. Böylece her karede tahsis yapmadan (1) sıradaki köşenin dönüş açısına göre hız sınırı ve
    /// (2) hedefe kalan yol uzunluğu hesaplanabilir. NavMeshAgent köşelerde kendiliğinden yavaşlamadığı için
    /// dar tünel ağzında dörtnala gelen köpek girişi kaçırıp duvar boyunca salınıyordu (B13 testi).
    /// </summary>
    public sealed class CompanionPathPlanner
    {
        private const int MaxCorners = 64;

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
        /// Köşe frenlemesi: sıradaki köşeye <see cref="CompanionConfig.CornerSlowDistance"/>'tan az kaldıysa ve oradaki
        /// dönüş <see cref="CompanionConfig.CornerAngleThreshold"/>'tan keskinse köşe hızını
        /// (<see cref="CompanionConfig.CornerMinSpeed"/>), aksi hâlde <paramref name="maxSpeed"/>'i döndürür.
        /// </summary>
        public float ComputeSpeedLimit(float maxSpeed)
        {
            int next = SteeringIndex();
            // Son köşe hedefin kendisidir; orada agent'ın kendi durma davranışı geçerli.
            if (next < 0 || next >= _cornerCount - 1)
            {
                return maxSpeed;
            }

            Vector3 here = Flat(_agent.nextPosition);
            Vector3 corner = Flat(_corners[next]);
            Vector3 afterCorner = Flat(_corners[next + 1]);
            if (Vector3.Distance(here, corner) > _config.CornerSlowDistance)
            {
                return maxSpeed;
            }

            float turnAngle = Vector3.Angle(corner - here, afterCorner - corner);
            return turnAngle > _config.CornerAngleThreshold ? Mathf.Min(_config.CornerMinSpeed, maxSpeed) : maxSpeed;
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
