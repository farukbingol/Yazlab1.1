using UnityEngine;
using Yazlab.LLM;

namespace Yazlab.Actions
{
    /// <summary>
    /// <c>end_conversation</c>: dünyada bir şey değiştirmez, diyalog penceresini kapatır. Oyuncu daha sonra
    /// tekrar konuşabilir (güven ve geçmiş korunur).
    /// </summary>
    public class EndConversationAction : MonoBehaviour, IGameAction
    {
        /// <inheritdoc />
        public NpcAction ActionType => NpcAction.EndConversation;

        /// <inheritdoc />
        public DialogueOutcome Outcome => DialogueOutcome.Pause;

        /// <inheritdoc />
        public void Execute()
        {
        }
    }
}
