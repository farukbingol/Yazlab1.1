using UnityEngine;
using Yazlab.Core;
using Yazlab.LLM;
using Yazlab.World;

namespace Yazlab.Actions
{
    /// <summary>
    /// <c>attack</c>: NPC'yi düşmana çevirir — üzerindeki başlangıçta kapalı <see cref="EnemyController"/>'ı oyuncuya
    /// hedefleyip açar. NPC yenilince bağlı kapı (<see cref="IOpenable"/>) açılır. Konuşma kalıcı olarak biter.
    /// </summary>
    public class StartCombatAction : MonoBehaviour, IGameAction
    {
        [Tooltip("NPC üzerindeki, başlangıçta KAPALI düşman bileşeni (kapalıyken GameManager onu saymaz).")]
        [SerializeField] private EnemyController _enemy;

        [Tooltip("NPC yenilince açılacak kapı (IOpenable). Boşsa kapı açılmaz.")]
        [SerializeField] private MonoBehaviour _doorOnDefeat;

        [Tooltip("Savaş başlarken ekranda gösterilecek anlatıcı bildirimi (NPC repliği değildir).")]
        [SerializeField] private string _combatNotice = "Muhafız silahını çekti — savaş!";

        private bool _started;

        /// <inheritdoc />
        public NpcAction ActionType => NpcAction.Attack;

        /// <inheritdoc />
        public DialogueOutcome Outcome => DialogueOutcome.Finish;

        /// <summary>Savaş başladı mı.</summary>
        public bool HasStarted => _started;

        /// <inheritdoc />
        public void Execute()
        {
            if (_started)
            {
                return;
            }

            if (_enemy == null)
            {
                Debug.LogError($"[Actions] {name}: EnemyController atanmamış; savaş başlatılamadı.", this);
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _enemy.SetPlayerTarget(player.transform);
            }

            _enemy.Died += OnEnemyDied;
            _enemy.enabled = true;
            _started = true;

            if (!string.IsNullOrEmpty(_combatNotice) && ServiceRegistry.TryGet(out IMessageService messages))
            {
                messages.Show(_combatNotice);
            }
        }

        private void OnEnemyDied(IDamageable _)
        {
            if (_enemy != null)
            {
                _enemy.Died -= OnEnemyDied;
            }

            if (_doorOnDefeat is IOpenable door)
            {
                door.Open();
            }
        }
    }
}
