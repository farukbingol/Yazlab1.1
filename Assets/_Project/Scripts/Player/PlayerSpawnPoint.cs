using UnityEngine;
using Yazlab.Core;

namespace Yazlab.Player
{
    /// <summary>
    /// Oyuncunun sahnede doğacağı veya öldükten sonra yeniden ışınlanacağı referans noktası.
    /// Kendisini <see cref="ServiceRegistry"/> üzerinden erişilebilir kılar.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private float gizmoRadius = 0.6f;

        /// <summary>
        /// Doğuş noktasının dünya konumu.
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// Doğuş noktasının dünya rotasyonu.
        /// </summary>
        public Quaternion Rotation => transform.rotation;

        private void Awake()
        {
            ServiceRegistry.Register(this);
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * (gizmoRadius * 2f));
        }
    }
}
