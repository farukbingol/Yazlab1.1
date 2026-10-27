using System.Collections;
using UnityEngine;
using Yazlab.Core;

namespace Yazlab.World
{
    /// <summary>
    /// Çekilince bağlı bir <see cref="IOpenable"/> hedefi (örn. <see cref="Gate"/>) açan şalter.
    /// Hedefe yalnızca arayüz üzerinden bağlıdır.
    /// </summary>
    public class Lever : MonoBehaviour, IInteractable, IResettable
    {
        [Tooltip("IOpenable uygulayan hedef (Gate vb.).")]
        [SerializeField] private MonoBehaviour _target;
        [Tooltip("Köpeğin/oyuncunun yaklaşacağı nokta. Boşsa şalterin kendisi.")]
        [SerializeField] private Transform _interactionPoint;
        [Tooltip("Dönen kol. Boşsa dönme animasyonu yapılmaz.")]
        [SerializeField] private Transform _handle;
        [SerializeField] private Vector3 _handleRotationOffset = new Vector3(60f, 0f, 0f);
        [SerializeField] private float _pullDuration = 0.4f;
        [SerializeField] private string _prompt = "Şalter";
        [SerializeField] private bool _debugLog;

        private IOpenable _openable;
        private Quaternion _handleStartRotation;
        private Coroutine _pullRoutine;

        /// <inheritdoc/>
        public string Prompt => _prompt;

        /// <inheritdoc/>
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;

        /// <inheritdoc/>
        public bool IsActivated { get; private set; }

        private void Awake()
        {
            _openable = _target as IOpenable;
            if (_target != null && _openable == null)
            {
                Debug.LogError($"[Lever] {name}: hedef IOpenable uygulamıyor.", this);
            }

            if (_handle != null)
            {
                _handleStartRotation = _handle.localRotation;
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
                Debug.Log($"[Lever] {name} çekildi (aktör: {(actor != null ? actor.name : "?")}).");
            }

            if (_handle != null)
            {
                _pullRoutine = StartCoroutine(PullRoutine());
            }

            _openable.Open();
        }

        /// <inheritdoc/>
        public void ResetState()
        {
            if (_pullRoutine != null)
            {
                StopCoroutine(_pullRoutine);
                _pullRoutine = null;
            }

            if (_handle != null)
            {
                _handle.localRotation = _handleStartRotation;
            }

            IsActivated = false;
        }

        private IEnumerator PullRoutine()
        {
            Quaternion end = _handleStartRotation * Quaternion.Euler(_handleRotationOffset);
            float duration = Mathf.Max(0.01f, _pullDuration);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                _handle.localRotation = Quaternion.Slerp(_handleStartRotation, end, t);
                yield return null;
            }

            _handle.localRotation = end;
            _pullRoutine = null;
        }
    }
}
