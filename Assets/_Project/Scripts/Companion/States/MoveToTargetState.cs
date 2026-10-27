using UnityEngine;
using UnityEngine.AI;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek emirle atanan <see cref="Yazlab.World.IInteractable"/> hedefine NavMesh üzerinden ilerler.
    /// <see cref="RaycastSensor"/> verisini kullanarak ön tıkanıklığını ve hedef görüş hattını (Line of Sight)
    /// kontrol eder; takılırsa <see cref="RecoverState"/>'e, hedefe ulaşınca <see cref="ActivateState"/>'e geçer.
    /// Hız, <see cref="CompanionPathPlanner"/> ile önümüzdeki köşelere ve sensörün dar geçit okumasına göre sınırlanır;
    /// hedefe kalan yol kısalmıyorsa (ör. tünel ağzında salınım) hız yüksek olsa bile takılma sayılır.
    /// </summary>
    public sealed class MoveToTargetState : ICompanionState
    {
        private const float InitialMoveGraceTime = 0.4f;
        private const float FrontBlockedDistanceThreshold = 0.65f;
        private const float FrontBlockedSpeedThreshold = 0.35f;
        private const float FrontBlockedTriggerDuration = 0.75f;
        private const float StuckSpeedThreshold = 0.15f;

        private readonly CompanionContext _context;
        private readonly CompanionPathPlanner _planner;
        private float _repathTimer;
        private float _graceTimer;
        private float _stuckTimer;
        private float _frontBlockedTimer;
        private float _progressTimer;
        private float _bestRemaining;

        public MoveToTargetState(CompanionContext context)
        {
            _context = context;
            _planner = new CompanionPathPlanner(context.Agent, context.Config);
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
            _progressTimer = 0f;
            _bestRemaining = float.PositiveInfinity;
            _planner.Clear();

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

                if (CheckNoProgress(dt))
                {
                    return;
                }
            }

            // 4) Hız: köşe öncesi fren + sensör dar geçit (tünel) okumasında yürüme hızı
            ApplySpeedLimit(reading);

            // 5) Periyodik yol yenileme. Ön engel yakınken ek yenileme yapılmaz: yol NavMesh'ten gelir, sensör
            // okuması onu değiştirmez; her karede yenileme agent'ın köşe ilerleyişini bozuyordu (B13).
            _repathTimer -= dt;
            if (_repathTimer <= 0f)
            {
                _repathTimer = _context.Config.RepathInterval;
                RequestPathToTarget();
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
        }

        /// <summary>
        /// Hedefe kalan yol <see cref="CompanionConfig.ProgressTimeout"/> boyunca en az
        /// <see cref="CompanionConfig.ProgressMinGain"/> kısalmadıysa <see cref="RecoverState"/>'e geçer.
        /// </summary>
        private bool CheckNoProgress(float dt)
        {
            float remaining = _planner.RemainingLength();
            if (float.IsInfinity(remaining))
            {
                return false;
            }

            if (remaining < _bestRemaining - _context.Config.ProgressMinGain)
            {
                _bestRemaining = remaining;
                _progressTimer = 0f;
                return false;
            }

            _progressTimer += dt;
            if (_progressTimer < _context.Config.ProgressTimeout)
            {
                return false;
            }

            _context.Machine.ChangeState(
                _context.Recover,
                $"hedefe ilerleme yok ({_progressTimer:F1} sn, kalan yol {remaining:F1} m, en iyi {_bestRemaining:F1} m)");
            return true;
        }

        private void ApplySpeedLimit(SensorReading reading)
        {
            CompanionConfig config = _context.Config;
            float maxSpeed = config.GallopSpeed;
            bool narrowPassage = reading.LeftDistance < config.NarrowPassageClearance &&
                                 reading.RightDistance < config.NarrowPassageClearance;
            if (narrowPassage)
            {
                maxSpeed = config.WalkSpeed;
            }

            _context.Agent.speed = _planner.HasPath ? _planner.ComputeSpeedLimit(maxSpeed) : maxSpeed;
        }

        private void RequestPathToTarget()
        {
            if (!_context.HasValidTarget || !_context.CanNavigate)
            {
                return;
            }

            Vector3 desiredPoint = _context.CurrentTarget.InteractionPoint.position;
            Vector3 destination = _context.TrySampleDestination(desiredPoint, _context.Config.DestinationSampleRadius, out Vector3 sampled)
                ? sampled
                : desiredPoint;

            if (!_planner.PlanTo(destination))
            {
                _context.Agent.SetDestination(destination);
            }
        }
    }
}
