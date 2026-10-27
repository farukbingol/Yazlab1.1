using UnityEngine;
using Yazlab.Core;

namespace Yazlab.Companion
{
    /// <summary>
    /// Oyuncunun çevresindeki en yakın saldırgan (<see cref="IHostile"/>) varlığı fizik sorgusuyla bulur.
    /// Sabit liste veya koordinat kullanmaz; tampon önceden ayrılır (tarama başına tahsis yok).
    /// </summary>
    public sealed class ThreatScanner
    {
        private const int BufferSize = 32;

        private readonly Collider[] _buffer = new Collider[BufferSize];

        /// <summary>
        /// <paramref name="center"/> çevresinde <paramref name="radius"/> içindeki en yakın saldırgan hedefi döndürür; yoksa <c>null</c>.
        /// </summary>
        public IHostile FindNearest(Vector3 center, float radius, LayerMask mask)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, mask, QueryTriggerInteraction.Ignore);
            IHostile best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = _buffer[i];
                if (c == null)
                {
                    continue;
                }

                IHostile hostile = c.GetComponentInParent<IHostile>();
                if (hostile == null || !hostile.IsHostile || hostile.HostileTransform == null)
                {
                    continue;
                }

                Vector3 delta = hostile.HostileTransform.position - center;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = hostile;
                }
            }

            return best;
        }
    }
}
