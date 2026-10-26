using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Yazlab.Core;

namespace Yazlab.World
{
    /// <summary>
    /// Kapı/geçit: kanadı yumuşakça kaydırır, açılınca engel collider'ını ve <see cref="NavMeshObstacle"/>'ı kapatır.
    /// </summary>
    public class Gate : MonoBehaviour, IOpenable
    {
        [Tooltip("Hareket eden kapı kanadı. Boşsa bu objenin kendisi.")]
        [SerializeField] private Transform _leaf;
        [Tooltip("Açılırken kanadın yerel uzayda kayacağı miktar.")]
        [SerializeField] private Vector3 _openOffset = new Vector3(0f, 3f, 0f);
        [SerializeField] private float _openDuration = 1.2f;
        [Tooltip("Açılınca devre dışı kalacak fiziksel engeller.")]
        [SerializeField] private Collider[] _blockingColliders;
        [Tooltip("Varsa kapı açılınca kapatılır (NavMesh yolu açılır).")]
        [SerializeField] private NavMeshObstacle _obstacle;
        [SerializeField] private string _openedMessage = "Geçit açıldı!";
        [SerializeField] private bool _raiseGateOpenedEvent = true;
        [SerializeField] private bool _debugLog;

        private Vector3 _closedLocalPosition;
        private Coroutine _moveRoutine;

        /// <inheritdoc/>
        public bool IsOpen { get; private set; }

        /// <inheritdoc/>
        public event Action Opened;

        private void Awake()
        {
            if (_leaf == null)
            {
                _leaf = transform;
            }

            _closedLocalPosition = _leaf.localPosition;
        }

        /// <inheritdoc/>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            SetBlocking(false);
            StartMove(_closedLocalPosition + _openOffset);

            if (_debugLog)
            {
                Debug.Log($"[Gate] {name} açıldı.");
            }

            if (ServiceRegistry.TryGet(out IMessageService messages) && !string.IsNullOrEmpty(_openedMessage))
            {
                messages.Show(_openedMessage);
            }

            Opened?.Invoke();
            if (_raiseGateOpenedEvent)
            {
                GameEvents.RaiseGateOpened();
            }
        }

        /// <inheritdoc/>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            SetBlocking(true);
            StartMove(_closedLocalPosition);
        }

        private void SetBlocking(bool blocking)
        {
            if (_blockingColliders != null)
            {
                foreach (Collider c in _blockingColliders)
                {
                    if (c != null)
                    {
                        c.enabled = blocking;
                    }
                }
            }

            if (_obstacle != null)
            {
                _obstacle.enabled = blocking;
            }
        }

        private void StartMove(Vector3 targetLocalPosition)
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
            }

            _moveRoutine = StartCoroutine(MoveRoutine(targetLocalPosition));
        }

        private IEnumerator MoveRoutine(Vector3 targetLocalPosition)
        {
            Vector3 start = _leaf.localPosition;
            float duration = Mathf.Max(0.01f, _openDuration);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                _leaf.localPosition = Vector3.Lerp(start, targetLocalPosition, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            _leaf.localPosition = targetLocalPosition;
            _moveRoutine = null;
        }
    }
}
