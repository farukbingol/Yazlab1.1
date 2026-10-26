using UnityEngine;
using Yazlab.Core;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek hedefe giderken sıkıştığında veya önü tıkandığında <see cref="RaycastSensor"/>'ün
    /// verdiği <see cref="SensorReading.ClearDirectionAngle"/> açısını kullanarak açık yöne doğru
    /// kısa bir kurtarma manevrası yapar ve yeniden <see cref="MoveToTargetState"/>'e döner.
    /// Deneme sınırı (<see cref="CompanionConfig.MaxRecoverRetries"/>) aşılırsa görevi iptal edip
    /// oyuncuya bilgi verir ve <see cref="FollowState"/>'e döner.
    /// </summary>
    public sealed class RecoverState : ICompanionState
    {
        private const float ManeuverDistance = 2.2f;
        private const float ManeuverDuration = 0.7f;
        private const string UnreachableMessage = "Köpek hedefe ulaşamadı!";

        private readonly CompanionContext _context;
        private float _maneuverTimer;
        private bool _exceededRetries;
        private float _chosenEscapeAngle;

        public RecoverState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "Recover";

        /// <inheritdoc />
        public void Enter()
        {
            _context.RecoverAttemptCount++;
            _context.TotalRecoverCount++;

            if (_context.RecoverAttemptCount > _context.Config.MaxRecoverRetries)
            {
                _exceededRetries = true;
                return;
            }

            _exceededRetries = false;
            _maneuverTimer = ManeuverDuration;

            SensorReading reading = _context.Sensor != null
                ? _context.Sensor.Sense(_context.CurrentTarget)
                : _context.LastReading;
            _context.LastReading = reading;

            _chosenEscapeAngle = SelectEscapeAngle(reading);

            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
                _context.Agent.speed = _context.Config.WalkSpeed;

                Vector3 forward = _context.Agent.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f)
                {
                    forward = Vector3.forward;
                }
                else
                {
                    forward.Normalize();
                }

                Vector3 escapeDir = Quaternion.AngleAxis(_chosenEscapeAngle, Vector3.up) * forward;
                Vector3 candidatePos = _context.Agent.transform.position + escapeDir * ManeuverDistance;

                if (_context.TrySampleDestination(candidatePos, _context.Config.DestinationSampleRadius, out Vector3 sampled))
                {
                    _context.Agent.SetDestination(sampled);
                }
            }
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_exceededRetries)
            {
                _context.CurrentTarget = null;
                _context.RecoverAttemptCount = 0;

                if (ServiceRegistry.TryGet(out IMessageService messageService))
                {
                    messageService.Show(UnreachableMessage);
                }

                _context.NotifyCommandCompleted(false);
                _context.Machine.ChangeState(
                    _context.Follow,
                    $"kurtarma denemesi sınırı ({_context.Config.MaxRecoverRetries}) aşıldı");
                return;
            }

            _maneuverTimer -= dt;
            bool reachedEscapePoint = _context.CanNavigate &&
                                      !_context.Agent.pathPending &&
                                      _context.Agent.remainingDistance <= 0.35f;

            if (_maneuverTimer <= 0f || reachedEscapePoint)
            {
                _context.Machine.ChangeState(
                    _context.MoveToTarget,
                    $"açık yön ({_chosenEscapeAngle:F0}°) ile yeniden deneme #{_context.RecoverAttemptCount}");
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
        }

        private static float SelectEscapeAngle(SensorReading reading)
        {
            float angle = reading.ClearDirectionAngle;
            // Tam ileri açı seçildiyse ancak ön hâlâ çok yakınsa daha açık olan yana (+60° / -60°) kır.
            if (Mathf.Abs(angle) < 5f && reading.FrontDistance < 1.2f)
            {
                return reading.RightDistance >= reading.LeftDistance ? 60f : -60f;
            }

            return angle;
        }
    }
}
