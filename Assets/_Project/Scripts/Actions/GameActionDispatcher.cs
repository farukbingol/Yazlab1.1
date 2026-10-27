using System;
using System.Collections.Generic;
using UnityEngine;
using Yazlab.LLM;

namespace Yazlab.Actions
{
    /// <summary>
    /// <see cref="NpcAction"/> değerini kayıtlı <see cref="IGameAction"/>'a yönlendirir.
    /// <see cref="NpcAction.None"/> ve kaydı olmayan aksiyonlar hiçbir şey yapmaz; aksiyon istisnası oyunu durdurmaz.
    /// </summary>
    public sealed class GameActionDispatcher
    {
        private readonly Dictionary<NpcAction, IGameAction> _actions = new Dictionary<NpcAction, IGameAction>();

        /// <summary>Kayıtlı aksiyon sayısı.</summary>
        public int Count => _actions.Count;

        /// <summary>Aksiyonu kaydeder; aynı türde kayıt varsa üzerine yazar ve uyarır.</summary>
        public void Register(IGameAction action)
        {
            if (action == null || action.ActionType == NpcAction.None)
            {
                return;
            }

            if (_actions.ContainsKey(action.ActionType))
            {
                Debug.LogWarning($"[Actions] {action.ActionType} için birden fazla aksiyon var; sonuncusu kullanılacak.");
            }

            _actions[action.ActionType] = action;
        }

        /// <summary>Bu aksiyon için kayıt var mı.</summary>
        public bool Has(NpcAction action) => _actions.ContainsKey(action);

        /// <summary>Aksiyonu çalıştırmadan diyalog sonucunu döndürür (kayıt yoksa <c>false</c>).</summary>
        public bool TryGetOutcome(NpcAction action, out DialogueOutcome outcome)
        {
            outcome = DialogueOutcome.Continue;
            if (action == NpcAction.None || !_actions.TryGetValue(action, out IGameAction handler) || handler == null)
            {
                return false;
            }

            outcome = handler.Outcome;
            return true;
        }

        /// <summary>
        /// Aksiyonu çalıştırır. Kayıt yoksa veya <see cref="NpcAction.None"/> ise <c>false</c> döner ve
        /// <paramref name="outcome"/> <see cref="DialogueOutcome.Continue"/> olur.
        /// </summary>
        public bool TryDispatch(NpcAction action, out DialogueOutcome outcome)
        {
            outcome = DialogueOutcome.Continue;
            if (action == NpcAction.None)
            {
                return false;
            }

            if (!_actions.TryGetValue(action, out IGameAction handler) || handler == null)
            {
                Debug.LogWarning($"[Actions] {action} için kayıtlı aksiyon yok; yok sayıldı.");
                return false;
            }

            try
            {
                handler.Execute();
                outcome = handler.Outcome;
                Debug.Log($"[Actions] {action} uygulandı (diyalog: {outcome}).");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}
