using UnityEngine;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek oyuncunun yakınında durur. Oyuncu durma mesafesinin ötesine geçince takibe başlar.
    /// </summary>
    public sealed class IdleState : ICompanionState
    {
        private readonly CompanionContext _context;

        public IdleState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "Idle";

        /// <inheritdoc />
        public void Enter()
        {
            if (_context.CanNavigate)
            {
                _context.Agent.ResetPath();
            }
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_context.ShouldAssist)
            {
                _context.Machine.ChangeState(_context.AssistCombat, $"oyuncunun yakınında düşman: {_context.CurrentThreat.HostileTransform.name}");
                return;
            }

            if (!_context.HasPlayer)
            {
                return;
            }

            float distance = _context.PlanarDistanceToPlayer();
            float resumeDistance = _context.Config.FollowStopDistance + _context.Config.FollowResumeMargin;
            if (distance > resumeDistance)
            {
                _context.Machine.ChangeState(_context.Follow, $"oyuncu uzaklaştı, {distance:F1} m");
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
        }
    }
}
