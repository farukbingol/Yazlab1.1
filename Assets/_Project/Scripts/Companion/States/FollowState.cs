using UnityEngine;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Köpek oyuncuyu NavMeshAgent ile takip eder. Uzaklığa göre yürür veya dörtnala koşar.
    /// Hareket yalnızca agent yoluyla olur; ışınlanma ve doğrudan konum atama yoktur.
    /// </summary>
    public sealed class FollowState : ICompanionState
    {
        private readonly CompanionContext _context;
        private float _repathTimer;
        private bool _galloping;

        public FollowState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "Follow";

        /// <inheritdoc />
        public void Enter()
        {
            _repathTimer = 0f;
            _galloping = false;
            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
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
            if (distance <= _context.Config.FollowStopDistance)
            {
                _context.Machine.ChangeState(_context.Idle, $"oyuncuya yetişti, {distance:F1} m");
                return;
            }

            if (!_context.CanNavigate)
            {
                return;
            }

            UpdateSpeed(distance);

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

        private void UpdateSpeed(float distance)
        {
            CompanionConfig config = _context.Config;
            if (!_galloping && distance > config.GallopThreshold)
            {
                _galloping = true;
            }
            else if (_galloping && distance < config.GallopThreshold - config.GallopExitMargin)
            {
                _galloping = false;
            }

            _context.Agent.speed = _galloping ? config.GallopSpeed : config.WalkSpeed;
        }
    }
}
