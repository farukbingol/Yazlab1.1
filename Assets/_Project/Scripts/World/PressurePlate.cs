using UnityEngine;
using UnityEngine.AI;

namespace Yazlab.World
{
    /// <summary>
    /// Üzerine köpek veya oyuncu basınca bağlı <see cref="IOpenable"/> hedefi açan basınç plakası (trigger collider gerekir).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : MonoBehaviour, IInteractable
    {
        [Tooltip("IOpenable uygulayan hedef (Gate vb.).")]
        [SerializeField] private MonoBehaviour _target;
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private string _prompt = "Basınç plakası";
        [SerializeField] private bool _triggeredByPlayer = true;
        [SerializeField] private bool _triggeredByCompanion = true;
        [SerializeField] private bool _debugLog;

        private IOpenable _openable;

        /// <inheritdoc/>
        public string Prompt => _prompt;

        /// <inheritdoc/>
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;

        /// <inheritdoc/>
        public bool IsActivated { get; private set; }

        private void Reset()
        {
            if (TryGetComponent(out Collider c))
            {
                c.isTrigger = true;
            }
        }

        private void Awake()
        {
            _openable = _target as IOpenable;
            if (_target != null && _openable == null)
            {
                Debug.LogError($"[PressurePlate] {name}: hedef IOpenable uygulamıyor.", this);
            }
        }

        private void OnEnable() => InteractableRegistry.Register(this);

        private void OnDisable() => InteractableRegistry.Unregister(this);

        /// <inheritdoc/>
        public bool CanInteract(GameObject actor) => !IsActivated && _openable != null && !_openable.IsOpen;

        /// <inheritdoc/>
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor))
            {
                return;
            }

            IsActivated = true;
            if (_debugLog)
            {
                Debug.Log($"[PressurePlate] {name} basıldı (aktör: {(actor != null ? actor.name : "?")}).");
            }

            _openable.Open();
        }

        private void OnTriggerEnter(Collider other)
        {
            GameObject actor = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            bool isPlayer = _triggeredByPlayer && other.CompareTag("Player");
            bool isCompanion = _triggeredByCompanion && actor.GetComponentInParent<NavMeshAgent>() != null;
            if (isPlayer || isCompanion)
            {
                Interact(actor);
            }
        }
    }
}
