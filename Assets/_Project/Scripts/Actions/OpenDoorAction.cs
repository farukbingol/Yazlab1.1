using UnityEngine;
using Yazlab.LLM;
using Yazlab.World;

namespace Yazlab.Actions
{
    /// <summary>
    /// <c>open_door</c>: bağlı son kapıyı (<see cref="IOpenable"/>) açar ve konuşmayı bitirir. Güven/şef koşulu
    /// denetleyicide kontrol edilir; bu sınıf yalnız uygular. Oyunun bitişi kapının arkasındaki geçiştedir.
    /// </summary>
    public class OpenDoorAction : MonoBehaviour, IGameAction
    {
        [Tooltip("Açılacak kapı (IOpenable uygulayan bileşen, ör. FinalGate üzerindeki Gate).")]
        [SerializeField] private MonoBehaviour _door;

        /// <inheritdoc />
        public NpcAction ActionType => NpcAction.OpenDoor;

        /// <inheritdoc />
        public DialogueOutcome Outcome => DialogueOutcome.Finish;

        /// <summary>Hedef kapı (kurulum araçları için).</summary>
        public MonoBehaviour Door
        {
            get => _door;
            set => _door = value;
        }

        /// <inheritdoc />
        public void Execute()
        {
            if (!(_door is IOpenable openable))
            {
                Debug.LogError($"[Actions] {name}: kapı atanmamış veya IOpenable değil.", this);
                return;
            }

            openable.Open();
        }
    }
}
