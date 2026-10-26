using UnityEngine;
using UnityEngine.AI;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek emirle atanan <see cref="Yazlab.World.IInteractable"/> hedefine NavMesh üzerinden ilerler.
    /// <see cref="RaycastSensor"/> verisini kullanarak ön tıkanıklığını ve hedef görüş hattını (Line of Sight)
    /// kontrol eder; takılırsa <see cref="RecoverState"/>'e, hedefe ulaşınca <see cref="ActivateState"/>'e geçer.
    /// </summary>
    public sealed class MoveToTargetState : ICompanionState
    {
        private const float InitialMoveGraceTime = 0.4f;
        private const float FrontBlockedDistanceThreshold = 0.65f;
        private const float FrontBlockedSpeedThreshold = 0.35f;
        private const float FrontBlockedTriggerDuration = 0.75f;
        private const float StuckSpeedThreshold = 0.15f;

        private readonly CompanionContext _context;
        private float _repathTimer;
        private float _graceTimer;
        private float _stuckTimer;
        private float _frontBlockedTimer;

        public MoveToTargetState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "MoveToTarget";

        /// <inheritdoc />
        public void Enter()
        {
            _repathTimer = 0f;
            _graceTimer = InitialMoveGraceTime;
            _stuckTimer = 0f;
            _frontBlockedTimer = 0f;

            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
                _context.Agent.speed = _context.Config.GallopSpeed;
                _context.Agent.stoppingDistance = Mathf.Max(0.15f, _context.Config.ActivateDistance * 0.35f);
                RequestPathToTarget();
            }
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            SensorReading reading = _context.Sensor != null
                ? _context.Sensor.Sense(_context.CurrentTarget)
                : _context.LastReading;
            _context.LastReading = reading;

            // Hedef boşsa veya sensör görünür yeni bir hedef yakaladıysa hedefi kesinleştir.
            if (!_context.HasValidTarget)
            {
                if (reading.VisibleTarget != null && !reading.VisibleTarget.IsActivated)
                {
                    _context.CurrentTarget = reading.VisibleTarget;
                }
                else
                {
                    _context.Machine.ChangeState(_context.ReturnToPlayer, "hedef geçersiz veya zaten etkinleştirilmiş");
                    return;
                }
            }

            if (!_context.CanNavigate)
            {
                return;
            }

            // 1) Hedefe ulaşma ve sensör Line-of-Sight doğrulaması -> ActivateState
            float targetDistance = _context.PlanarDistanceToTarget();
            float activateThreshold = _context.Config.ActivateDistance;
            bool closeAndVisible = targetDistance <= activateThreshold && reading.HasLineOfSightToTarget;
            bool pointBlankArrival = targetDistance <= activateThreshold * 0.65f;

            if (closeAndVisible || pointBlankArrival)
            {
                _context.Machine.ChangeState(
                    _context.Activate,
                    $"hedef menzilde ({targetDistance:F2} m, LoS={reading.HasLineOfSightToTarget})");
                return;
            }

            // 2) Yol durumu kontrolü (PathInvalid / PathPartial) -> RecoverState
            if (!_context.Agent.pathPending &&
                _context.Agent.hasPath &&
                _context.Agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                _context.Machine.ChangeState(
                    _context.Recover,
                    $"NavMesh yolu kesintili ({_context.Agent.pathStatus})");
                return;
            }

            // 3) Sensör ön engel ve hız tabanlı takılma tespiti -> RecoverState
            if (_graceTimer > 0f)
            {
                _graceTimer -= dt;
            }
            else
            {
                float currentSpeed = _context.Agent.velocity.magnitude;

                if (reading.FrontDistance < FrontBlockedDistanceThreshold && currentSpeed < FrontBlockedSpeedThreshold)
                {
                    _frontBlockedTimer += dt;
                    if (_frontBlockedTimer >= FrontBlockedTriggerDuration)
                    {
                        _context.Machine.ChangeState(
                            _context.Recover,
                            $"sensör ön tıkanıklık tespit etti (ön={reading.FrontDistance:F2} m, açık açı={reading.ClearDirectionAngle:F0}°)");
                        return;
                    }
                }
                else
                {
                    _frontBlockedTimer = 0f;
                }

                if (currentSpeed < StuckSpeedThreshold)
                {
                    _stuckTimer += dt;
                    if (_stuckTimer >= _context.Config.StuckTimeout)
                    {
                        _context.Machine.ChangeState(
                            _context.Recover,
                            $"köpek {_stuckTimer:F1} sn boyunca ilerleyemedi");
                        return;
                    }
                }
                else
                {
                    _stuckTimer = 0f;
                }
            }

            // 4) Periyodik (veya önünde yakın engel varsa erken) yol yenileme
            _repathTimer -= dt;
            if (_repathTimer <= 0f || (reading.HasObstacle && reading.FrontDistance < 1.2f && _repathTimer > 0.1f))
            {
                _repathTimer = _context.Config.RepathInterval;
                RequestPathToTarget();
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
        }

        private void RequestPathToTarget()
        {
            if (!_context.HasValidTarget || !_context.CanNavigate)
            {
                return;
            }

            Vector3 desiredPoint = _context.CurrentTarget.InteractionPoint.position;
            if (_context.TrySampleDestination(desiredPoint, _context.Config.DestinationSampleRadius, out Vector3 sampled))
            {
                _context.Agent.SetDestination(sampled);
            }
            else
            {
                _context.Agent.SetDestination(desiredPoint);
            }
        }
    }
}
