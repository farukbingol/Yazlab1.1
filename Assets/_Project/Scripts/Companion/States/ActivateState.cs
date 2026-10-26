using UnityEngine;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek hedefe ulaştığında yüzünü hedefe döner, <see cref="Yazlab.World.IInteractable.Interact"/>
    /// çağrısıyla şalteri/plakayı etkinleştirir ve kısa beklemenin ardından <see cref="ReturnToPlayerState"/>'e geçer.
    /// </summary>
    public sealed class ActivateState : ICompanionState
    {
        private const float InteractionPauseDuration = 0.45f;
        private const float LookTurnSpeed = 10f;

        private readonly CompanionContext _context;
        private float _timer;
        private bool _interacted;

        public ActivateState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "Activate";

        /// <inheritdoc />
        public void Enter()
        {
            _timer = InteractionPauseDuration;
            _interacted = false;

            if (_context.CanNavigate)
            {
                _context.Agent.ResetPath();
                _context.Agent.isStopped = true;
            }

            TriggerInteraction();
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            RotateTowardsTarget(dt);

            if (!_interacted)
            {
                TriggerInteraction();
            }

            _timer -= dt;
            if (_timer > 0f)
            {
                return;
            }

            _context.CurrentTarget = null;
            _context.RecoverAttemptCount = 0;
            _context.NotifyCommandCompleted(true);
            _context.Machine.ChangeState(_context.ReturnToPlayer, "hedef etkinleştirildi, oyuncuya dönülüyor");
        }

        /// <inheritdoc />
        public void Exit()
        {
            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
            }
        }

        private void TriggerInteraction()
        {
            _interacted = true;
            if (_context.CurrentTarget == null || _context.Agent == null)
            {
                return;
            }

            if (_context.CurrentTarget.CanInteract(_context.Agent.gameObject))
            {
                _context.CurrentTarget.Interact(_context.Agent.gameObject);
            }
        }

        private void RotateTowardsTarget(float dt)
        {
            if (_context.CurrentTarget == null || _context.Agent == null)
            {
                return;
            }

            Vector3 targetPos = _context.CurrentTarget is Component comp && comp != null
                ? comp.transform.position
                : _context.CurrentTarget.InteractionPoint.position;

            Vector3 lookDir = targetPos - _context.Agent.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                _context.Agent.transform.rotation = Quaternion.Slerp(
                    _context.Agent.transform.rotation,
                    desiredRot,
                    LookTurnSpeed * dt);
            }
        }
    }
}
