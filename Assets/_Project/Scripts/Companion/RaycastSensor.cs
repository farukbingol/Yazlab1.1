using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Yelpaze şeklinde çoklu ışın (<see cref="Physics.RaycastNonAlloc"/>) ve küresel tarama
    /// (<see cref="Physics.OverlapSphereNonAlloc"/>) kullanarak engelleri ve etkileşim hedeflerini
    /// GC tahsisi (allocation) yapmadan algılayan sensör bileşeni.
    /// Hem Scene görünümünde (<c>OnDrawGizmos</c>) hem de Play modunda Game görünümünde (<see cref="LineRenderer"/>)
    /// ışınları görselleştirir.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaycastSensor : MonoBehaviour, IObstacleSensor
    {
        private const int MaxRayCount = 16;
        private const int MaxHitsPerRay = 8;
        private const int MaxOverlapColliders = 32;
        private const float RuntimeLineWidth = 0.045f;

        private static readonly float[] DefaultRayAngles = { -60f, -30f, 0f, 30f, 60f };
        private static readonly Color ClearRayColor = new Color(0.15f, 0.95f, 0.3f, 0.9f);
        private static readonly Color HitRayColor = new Color(1f, 0.2f, 0.2f, 0.95f);
        private static readonly Color VisibleTargetColor = new Color(0.15f, 0.55f, 1f, 0.95f);
        private static readonly Color OccludedTargetColor = new Color(0.2f, 0.45f, 0.85f, 0.35f);

        [Tooltip("Sensör menzil, açı ve katman ayarlarını içeren CompanionConfig varlığı.")]
        [SerializeField] private CompanionConfig _config;

        [Tooltip("Scene ve Game görünümünde sensör ışınlarını ve hedef görüş hattını çizer.")]
        [SerializeField] private bool _showGizmos = true;

        private readonly RaycastHit[] _rayHitBuffer = new RaycastHit[MaxHitsPerRay];
        private readonly Collider[] _overlapBuffer = new Collider[MaxOverlapColliders];

        private readonly float[] _cachedAngles = new float[MaxRayCount];
        private readonly Vector3[] _cachedDirections = new Vector3[MaxRayCount];
        private readonly float[] _cachedDistances = new float[MaxRayCount];
        private readonly bool[] _cachedHits = new bool[MaxRayCount];
        private int _cachedRayCount;

        private readonly LineRenderer[] _rayLines = new LineRenderer[MaxRayCount];
        private LineRenderer _targetLine;
        private Material _lineMaterial;
        private Transform _visualRoot;

        private Vector3 _lastOrigin;
        private Vector3 _lastTargetPoint;
        private bool _hasTargetCandidate;
        private bool _hasSampledOnce;
        private SensorReading _lastReading;

        /// <inheritdoc/>
        public SensorReading LastReading => _lastReading;

        /// <summary>Gizmo çizimi açık mı.</summary>
        public bool ShowGizmos
        {
            get => _showGizmos;
            set => _showGizmos = value;
        }

        /// <summary>Sensörün kullandığı yapılandırma varlığı.</summary>
        public CompanionConfig Config
        {
            get => _config;
            set => _config = value;
        }

        private void Awake()
        {
            EnsureRuntimeLinesCreated();
        }

        private void LateUpdate()
        {
            if (!_hasSampledOnce)
            {
                return;
            }

            float heightOffset = _config != null ? _config.SensorHeightOffset : 0.45f;
            _lastOrigin = transform.position + Vector3.up * heightOffset;
            UpdateRuntimeLines();
        }

        private void OnDisable()
        {
            SetRuntimeLinesActive(false);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
                _lineMaterial = null;
            }
        }

        /// <inheritdoc/>
        public SensorReading Sense(IInteractable desiredTarget = null)
        {
            float range = _config != null ? _config.SensorRange : 8f;
            float heightOffset = _config != null ? _config.SensorHeightOffset : 0.45f;
            float targetRadius = _config != null ? _config.TargetDetectRadius : 20f;
            LayerMask obstacleMask = _config != null ? _config.ObstacleLayerMask : (LayerMask)Physics.DefaultRaycastLayers;
            LayerMask targetMask = _config != null ? _config.TargetLayerMask : (LayerMask)Physics.DefaultRaycastLayers;
            float[] configuredAngles = _config != null && _config.SensorRayAngles != null && _config.SensorRayAngles.Length > 0
                ? _config.SensorRayAngles
                : DefaultRayAngles;

            Vector3 origin = transform.position + Vector3.up * heightOffset;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            _lastOrigin = origin;
            _cachedRayCount = Mathf.Min(configuredAngles.Length, MaxRayCount);

            float frontDistance = range;
            float bestFrontAngleAbs = float.PositiveInfinity;
            float leftDistance = range;
            float rightDistance = range;
            float minObstacleDistance = range;
            float minObstacleAngle = 0f;
            bool hasObstacle = false;

            for (int i = 0; i < _cachedRayCount; i++)
            {
                float angle = configuredAngles[i];
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;

                float hitDist = CastObstacleRay(origin, dir, range, obstacleMask, out bool didHit);

                _cachedAngles[i] = angle;
                _cachedDirections[i] = dir;
                _cachedDistances[i] = hitDist;
                _cachedHits[i] = didHit;

                float absAngle = Mathf.Abs(angle);
                if (absAngle < bestFrontAngleAbs)
                {
                    bestFrontAngleAbs = absAngle;
                    frontDistance = hitDist;
                }

                if (angle < -0.01f && hitDist < leftDistance)
                {
                    leftDistance = hitDist;
                }
                else if (angle > 0.01f && hitDist < rightDistance)
                {
                    rightDistance = hitDist;
                }

                if (didHit && hitDist < minObstacleDistance)
                {
                    minObstacleDistance = hitDist;
                    minObstacleAngle = angle;
                    hasObstacle = true;
                }
            }

            float clearDirectionAngle = SelectClearDirectionAngle(leftDistance, rightDistance);

            EvaluateTarget(
                origin,
                forward,
                heightOffset,
                targetRadius,
                obstacleMask,
                targetMask,
                desiredTarget,
                out IInteractable visibleTarget,
                out float targetDistance,
                out float targetAngle,
                out bool hasLineOfSight);

            _lastReading = new SensorReading(
                frontDistance,
                leftDistance,
                rightDistance,
                minObstacleDistance,
                minObstacleAngle,
                clearDirectionAngle,
                hasObstacle,
                visibleTarget,
                targetDistance,
                targetAngle,
                hasLineOfSight);

            _hasSampledOnce = true;
            if (Application.isPlaying)
            {
                UpdateRuntimeLines();
            }

            return _lastReading;
        }

        private float CastObstacleRay(Vector3 origin, Vector3 direction, float maxDistance, LayerMask mask, out bool didHit)
        {
            int hitCount = Physics.RaycastNonAlloc(origin, direction, _rayHitBuffer, maxDistance, mask, QueryTriggerInteraction.Ignore);
            float closest = maxDistance;
            didHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _rayHitBuffer[i].transform;
                if (hitTransform == null || IsSelfOrChild(hitTransform))
                {
                    continue;
                }

                float d = _rayHitBuffer[i].distance;
                if (d < closest)
                {
                    closest = d;
                    didHit = true;
                }
            }

            return closest;
        }

        private float SelectClearDirectionAngle(float leftClearance, float rightClearance)
        {
            if (_cachedRayCount == 0)
            {
                return 0f;
            }

            int bestIndex = 0;
            float bestDist = -1f;
            float bestAbsAngle = float.PositiveInfinity;

            for (int i = 0; i < _cachedRayCount; i++)
            {
                float dist = _cachedDistances[i];
                float angle = _cachedAngles[i];
                float absAngle = Mathf.Abs(angle);

                if (dist > bestDist + 0.01f)
                {
                    bestDist = dist;
                    bestAbsAngle = absAngle;
                    bestIndex = i;
                }
                else if (Mathf.Abs(dist - bestDist) <= 0.01f)
                {
                    if (absAngle < bestAbsAngle - 0.01f)
                    {
                        bestAbsAngle = absAngle;
                        bestIndex = i;
                    }
                    else if (Mathf.Abs(absAngle - bestAbsAngle) <= 0.01f)
                    {
                        // Simetrik iki açı (örn. -30° ve +30°) eşit mesafedeyse daha açık olan yan seçilir.
                        bool preferRight = rightClearance >= leftClearance;
                        if ((preferRight && angle > _cachedAngles[bestIndex]) ||
                            (!preferRight && angle < _cachedAngles[bestIndex]))
                        {
                            bestIndex = i;
                        }
                    }
                }
            }

            return _cachedAngles[bestIndex];
        }

        private void EvaluateTarget(
            Vector3 origin,
            Vector3 forward,
            float heightOffset,
            float targetRadius,
            LayerMask obstacleMask,
            LayerMask targetMask,
            IInteractable desiredTarget,
            out IInteractable visibleTarget,
            out float targetDistance,
            out float targetAngle,
            out bool hasLineOfSight)
        {
            visibleTarget = null;
            targetDistance = float.PositiveInfinity;
            targetAngle = 0f;
            hasLineOfSight = false;
            _hasTargetCandidate = false;

            if (IsCandidateValid(desiredTarget))
            {
                CheckCandidateLineOfSight(
                    origin,
                    forward,
                    heightOffset,
                    obstacleMask,
                    desiredTarget,
                    out targetDistance,
                    out targetAngle,
                    out hasLineOfSight,
                    out _lastTargetPoint);

                _hasTargetCandidate = true;
                if (hasLineOfSight)
                {
                    visibleTarget = desiredTarget;
                }

                return;
            }

            IInteractable bestVisible = null;
            float bestVisibleDist = float.PositiveInfinity;
            float bestVisibleAngle = 0f;
            Vector3 bestVisiblePoint = Vector3.zero;

            IInteractable bestFallback = null;
            float bestFallbackDist = float.PositiveInfinity;
            float bestFallbackAngle = 0f;
            Vector3 bestFallbackPoint = Vector3.zero;

            // 1) Küresel fizik taraması (OverlapSphereNonAlloc, trigger collider'lar dahil).
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                targetRadius,
                _overlapBuffer,
                targetMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || IsSelfOrChild(col.transform))
                {
                    continue;
                }

                IInteractable candidate = ResolveInteractableFromCollider(col);
                if (!IsCandidateValid(candidate) || candidate == bestVisible || candidate == bestFallback)
                {
                    continue;
                }

                CheckCandidateLineOfSight(
                    origin,
                    forward,
                    heightOffset,
                    obstacleMask,
                    candidate,
                    out float dist,
                    out float angle,
                    out bool los,
                    out Vector3 aimPoint);

                if (los && dist < bestVisibleDist)
                {
                    bestVisible = candidate;
                    bestVisibleDist = dist;
                    bestVisibleAngle = angle;
                    bestVisiblePoint = aimPoint;
                }
                else if (dist < bestFallbackDist)
                {
                    bestFallback = candidate;
                    bestFallbackDist = dist;
                    bestFallbackAngle = angle;
                    bestFallbackPoint = aimPoint;
                }
            }

            // 2) InteractableRegistry yedeği (collider'ı olmayan veya farklı alt objede olan hedefler için).
            IReadOnlyList<IInteractable> registered = InteractableRegistry.All;
            for (int i = 0; i < registered.Count; i++)
            {
                IInteractable candidate = registered[i];
                if (!IsCandidateValid(candidate) || candidate == bestVisible || candidate == bestFallback)
                {
                    continue;
                }

                Vector3 delta = GetCandidatePosition(candidate) - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > targetRadius * targetRadius)
                {
                    continue;
                }

                CheckCandidateLineOfSight(
                    origin,
                    forward,
                    heightOffset,
                    obstacleMask,
                    candidate,
                    out float dist,
                    out float angle,
                    out bool los,
                    out Vector3 aimPoint);

                if (los && dist < bestVisibleDist)
                {
                    bestVisible = candidate;
                    bestVisibleDist = dist;
                    bestVisibleAngle = angle;
                    bestVisiblePoint = aimPoint;
                }
                else if (dist < bestFallbackDist)
                {
                    bestFallback = candidate;
                    bestFallbackDist = dist;
                    bestFallbackAngle = angle;
                    bestFallbackPoint = aimPoint;
                }
            }

            if (bestVisible != null)
            {
                visibleTarget = bestVisible;
                targetDistance = bestVisibleDist;
                targetAngle = bestVisibleAngle;
                hasLineOfSight = true;
                _lastTargetPoint = bestVisiblePoint;
                _hasTargetCandidate = true;
            }
            else if (bestFallback != null)
            {
                targetDistance = bestFallbackDist;
                targetAngle = bestFallbackAngle;
                hasLineOfSight = false;
                _lastTargetPoint = bestFallbackPoint;
                _hasTargetCandidate = true;
            }
        }

        private void CheckCandidateLineOfSight(
            Vector3 origin,
            Vector3 forward,
            float heightOffset,
            LayerMask obstacleMask,
            IInteractable candidate,
            out float planarDistance,
            out float signedAngle,
            out bool hasLineOfSight,
            out Vector3 aimPoint)
        {
            Vector3 basePos = GetCandidatePosition(candidate);
            aimPoint = new Vector3(basePos.x, basePos.y + heightOffset, basePos.z);

            Vector3 planarDelta = basePos - transform.position;
            planarDelta.y = 0f;
            planarDistance = planarDelta.magnitude;

            if (planarDistance > 0.001f)
            {
                signedAngle = Vector3.SignedAngle(forward, planarDelta / planarDistance, Vector3.up);
            }
            else
            {
                signedAngle = 0f;
            }

            Vector3 rayDelta = aimPoint - origin;
            float rayDistance = rayDelta.magnitude;
            if (rayDistance <= 0.01f)
            {
                hasLineOfSight = true;
                return;
            }

            Vector3 rayDir = rayDelta / rayDistance;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                rayDir,
                _rayHitBuffer,
                rayDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            Transform candidateRoot = (candidate as Component)?.transform;
            hasLineOfSight = true;

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _rayHitBuffer[i].transform;
                if (hitTransform == null || IsSelfOrChild(hitTransform))
                {
                    continue;
                }

                if (candidateRoot != null && (hitTransform == candidateRoot || hitTransform.IsChildOf(candidateRoot)))
                {
                    continue;
                }

                hasLineOfSight = false;
                break;
            }
        }

        private static IInteractable ResolveInteractableFromCollider(Collider col)
        {
            Transform colTransform = col.transform;
            IReadOnlyList<IInteractable> registered = InteractableRegistry.All;
            for (int i = 0; i < registered.Count; i++)
            {
                IInteractable item = registered[i];
                if (item is Component comp && comp != null)
                {
                    Transform root = comp.transform;
                    if (colTransform == root || colTransform.IsChildOf(root))
                    {
                        return item;
                    }
                }
            }

            if (col.TryGetComponent(out IInteractable direct))
            {
                return direct;
            }

            return col.GetComponentInParent<IInteractable>();
        }

        private bool IsCandidateValid(IInteractable candidate)
        {
            if (candidate == null || candidate.InteractionPoint == null || candidate.IsActivated)
            {
                return false;
            }

            if (Application.isPlaying && !candidate.CanInteract(gameObject))
            {
                return false;
            }

            return true;
        }

        private static Vector3 GetCandidatePosition(IInteractable candidate)
        {
            if (candidate is Component comp && comp != null)
            {
                return comp.transform.position;
            }

            return candidate.InteractionPoint.position;
        }

        private bool IsSelfOrChild(Transform other)
        {
            return other == transform || other.IsChildOf(transform);
        }

        private void EnsureRuntimeLinesCreated()
        {
            if (!Application.isPlaying || _visualRoot != null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored");
            }

            if (shader != null)
            {
                _lineMaterial = new Material(shader) { name = "SensorRayMaterial" };
            }

            GameObject rootGo = new GameObject("SensorVisuals");
            _visualRoot = rootGo.transform;
            _visualRoot.SetParent(transform, false);

            for (int i = 0; i < MaxRayCount; i++)
            {
                _rayLines[i] = CreateLineRenderer($"Ray_{i}", RuntimeLineWidth);
            }

            _targetLine = CreateLineRenderer("TargetRay", RuntimeLineWidth * 1.15f);
        }

        private LineRenderer CreateLineRenderer(string lineName, float width)
        {
            GameObject go = new GameObject(lineName);
            go.transform.SetParent(_visualRoot, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (_lineMaterial != null)
            {
                lr.sharedMaterial = _lineMaterial;
            }

            lr.enabled = false;
            return lr;
        }

        private void SetRuntimeLinesActive(bool active)
        {
            for (int i = 0; i < MaxRayCount; i++)
            {
                if (_rayLines[i] != null)
                {
                    _rayLines[i].enabled = active && i < _cachedRayCount;
                }
            }

            if (_targetLine != null)
            {
                _targetLine.enabled = active && _hasTargetCandidate;
            }
        }

        private void UpdateRuntimeLines()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_showGizmos)
            {
                SetRuntimeLinesActive(false);
                return;
            }

            EnsureRuntimeLinesCreated();

            for (int i = 0; i < MaxRayCount; i++)
            {
                LineRenderer lr = _rayLines[i];
                if (lr == null)
                {
                    continue;
                }

                if (i < _cachedRayCount)
                {
                    Color c = _cachedHits[i] ? HitRayColor : ClearRayColor;
                    Vector3 endPoint = _lastOrigin + _cachedDirections[i] * _cachedDistances[i];
                    lr.enabled = true;
                    lr.startColor = c;
                    lr.endColor = c;
                    lr.SetPosition(0, _lastOrigin);
                    lr.SetPosition(1, endPoint);
                }
                else
                {
                    lr.enabled = false;
                }
            }

            if (_targetLine != null)
            {
                if (_hasTargetCandidate)
                {
                    Color tc = _lastReading.HasLineOfSightToTarget ? VisibleTargetColor : OccludedTargetColor;
                    _targetLine.enabled = true;
                    _targetLine.startColor = tc;
                    _targetLine.endColor = tc;
                    _targetLine.SetPosition(0, _lastOrigin);
                    _targetLine.SetPosition(1, _lastTargetPoint);
                }
                else
                {
                    _targetLine.enabled = false;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!_showGizmos)
            {
                return;
            }

            if (!Application.isPlaying || !_hasSampledOnce)
            {
                Sense();
            }

            for (int i = 0; i < _cachedRayCount; i++)
            {
                bool didHit = _cachedHits[i];
                Gizmos.color = didHit ? HitRayColor : ClearRayColor;
                Vector3 endPoint = _lastOrigin + _cachedDirections[i] * _cachedDistances[i];
                Gizmos.DrawLine(_lastOrigin, endPoint);

                if (didHit)
                {
                    Gizmos.DrawSphere(endPoint, 0.09f);
                }
            }

            if (_hasTargetCandidate)
            {
                if (_lastReading.HasLineOfSightToTarget)
                {
                    Gizmos.color = VisibleTargetColor;
                    Gizmos.DrawLine(_lastOrigin, _lastTargetPoint);
                    Gizmos.DrawWireSphere(_lastTargetPoint, 0.22f);
                }
                else
                {
                    Gizmos.color = OccludedTargetColor;
                    Gizmos.DrawLine(_lastOrigin, _lastTargetPoint);
                }
            }
        }
    }
}
