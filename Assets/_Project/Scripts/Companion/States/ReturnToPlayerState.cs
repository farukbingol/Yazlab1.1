using UnityEngine;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek etkileşim görevini tamamladıktan sonra oyuncunun yanına geri döner ve
    /// durma mesafesine yaklaştığında <see cref="FollowState"/>'e geçer.
    /// </summary>
    public sealed class ReturnToPlayerState : ICompanionState
    {
        private readonly CompanionContext _context;
        private float _repathTimer;

        public ReturnToPlayerState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "ReturnToPlayer";

        /// <inheritdoc />
        public void Enter()
        {
            _repathTimer = 0f;
            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
                _context.Agent.speed = _context.Config.GallopSpeed;
                _context.Agent.stoppingDistance = Mathf.Max(0.1f, _context.Config.FollowStopDistance - 0.3f);
            }
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (!_context.HasPlayer)
            {
                _context.Machine.ChangeState(_context.Idle, "oyuncu bulunamadı");
                return;
            }

            float distance = _context.PlanarDistanceToPlayer();
            if (distance <= _context.Config.FollowStopDistance + 0.3f)
            {
                _context.Machine.ChangeState(_context.Follow, $"oyuncunun yanına döndü, {distance:F1} m");
                return;
            }

            if (!_context.CanNavigate)
            {
                return;
            }

            _repathTimer -= dt;
            if (_repathTimer > 0f)
            {
                return;
            }

            _repathTimer = _context.Config.RepathInterval;
            if (_context.TrySamplePlayerDestination(out Vector3 destination))
            {
                _context.Agent.SetDestination(destination);
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
        }
    }
}
