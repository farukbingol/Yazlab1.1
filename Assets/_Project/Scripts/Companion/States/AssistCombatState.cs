using UnityEngine;
using Yazlab.Core;

namespace Yazlab.Companion.States
{
    /// <summary>
    /// Savaş desteği: oyuncunun yakınındaki saldırgan düşmana NavMesh üzerinden koşar, ısırma menzilindeyse
    /// <see cref="CompanionConfig.BiteCooldown"/> aralıkla <see cref="IDamageable.TakeDamage(int, Vector3)"/> uygular.
    /// Düşman ölünce/saldırganlığı bitince veya köpek oyuncudan <see cref="CompanionConfig.AssistLeash"/>'ten fazla
    /// uzaklaşınca <see cref="FollowState"/>'e döner. Emir görevleri (<see cref="MoveToTargetState"/>) önceliklidir.
    /// </summary>
    public sealed class AssistCombatState : ICompanionState
    {
        private const float TurnSpeed = 12f;

        private readonly CompanionContext _context;
        private IHostile _target;
        private IDamageable _damageable;
        private float _repathTimer;
        private float _biteTimer;

        public AssistCombatState(CompanionContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public string Name => "AssistCombat";

        /// <inheritdoc />
        public void Enter()
        {
            _target = _context.CurrentThreat;
            _damageable = _target != null ? _target.HostileTransform.GetComponent<IDamageable>() : null;
            _repathTimer = 0f;
            _biteTimer = 0f;

            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
                _context.Agent.speed = _context.Config.GallopSpeed;
                _context.Agent.stoppingDistance = _context.Config.BiteRange * 0.6f;
            }
        }

        /// <inheritdoc />
        public void Tick(float dt)
        {
            if (_target == null || !_target.IsHostile || _target.HostileTransform == null || _damageable == null)
            {
                _context.Machine.ChangeState(_context.Follow, "düşman etkisiz");
                return;
            }

            if (_context.PlanarDistanceToPlayer() > _context.Config.AssistLeash)
            {
                _context.Machine.ChangeState(_context.Follow, "oyuncudan çok uzaklaştı");
                return;
            }

            Vector3 enemyPos = _target.HostileTransform.position;
            float distance = _context.PlanarDistanceTo(enemyPos);

            _repathTimer -= dt;
            if (_repathTimer <= 0f && _context.CanNavigate)
            {
                _repathTimer = _context.Config.RepathInterval;
                if (_context.TrySampleDestination(enemyPos, _context.Config.DestinationSampleRadius, out Vector3 destination))
                {
                    _context.Agent.SetDestination(destination);
                }
            }

            _biteTimer -= dt;
            if (distance > _context.Config.BiteRange)
            {
                return;
            }

            FaceTowards(enemyPos, dt);
            if (_biteTimer > 0f)
            {
                return;
            }

            _biteTimer = _context.Config.BiteCooldown;
            Vector3 hitDirection = enemyPos - _context.Agent.transform.position;
            hitDirection.y = 0f;
            _damageable.TakeDamage(_context.Config.BiteDamage, hitDirection.normalized);
            if (_context.Config.DebugLog)
            {
                Debug.Log($"[Companion] {_target.HostileTransform.name} ısırıldı (-{_context.Config.BiteDamage}).");
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
            _target = null;
            _damageable = null;
            if (_context.CanNavigate)
            {
                _context.Agent.isStopped = false;
                _context.Agent.stoppingDistance = Mathf.Max(0.1f, _context.Config.FollowStopDistance - 0.3f);
            }
        }

        private void FaceTowards(Vector3 position, float dt)
        {
            Vector3 look = position - _context.Agent.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
            {
                _context.Agent.transform.rotation = Quaternion.Slerp(
                    _context.Agent.transform.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), TurnSpeed * dt);
            }
        }
    }
}
