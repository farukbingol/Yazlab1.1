using System;
using UnityEngine;

namespace Yazlab.Companion
{
    /// <summary>
    /// Aktif <see cref="ICompanionState"/>'i tutan düz C# durum makinesi.
    /// Geçişleri loglar ve <see cref="StateChanged"/> olayını fırlatır; durum koduna sızan istisnalar
    /// oyunu çökertmez, loglanır.
    /// </summary>
    public sealed class CompanionStateMachine
    {
        private readonly bool _logTransitions;

        /// <summary>Şu an aktif durum (başlangıçta null).</summary>
        public ICompanionState Current { get; private set; }

        /// <summary>Durum değişince (önceki, yeni) ile tetiklenir.</summary>
        public event Action<ICompanionState, ICompanionState> StateChanged;

        /// <param name="logTransitions">Geçişler Console'a yazılsın mı.</param>
        public CompanionStateMachine(bool logTransitions)
        {
            _logTransitions = logTransitions;
        }

        /// <summary>
        /// Yeni duruma geçer. Hedef null veya zaten aktifse hiçbir şey yapmaz.
        /// </summary>
        /// <param name="next">Geçilecek durum.</param>
        /// <param name="reason">Loga yazılacak kısa geçiş nedeni (Türkçe).</param>
        public void ChangeState(ICompanionState next, string reason = null)
        {
            if (next == null || ReferenceEquals(next, Current))
            {
                return;
            }

            ICompanionState previous = Current;
            SafeInvoke(previous != null ? previous.Exit : (Action)null, previous, "Exit");
            Current = next;
            SafeInvoke(next.Enter, next, "Enter");

            if (_logTransitions)
            {
                string from = previous != null ? previous.Name : "-";
                string suffix = string.IsNullOrEmpty(reason) ? string.Empty : $" ({reason})";
                Debug.Log($"[Companion] {from} → {next.Name}{suffix}");
            }

            StateChanged?.Invoke(previous, next);
        }

        /// <summary>Aktif durumun <see cref="ICompanionState.Tick"/> metodunu çağırır.</summary>
        /// <param name="dt">Son kareden geçen süre (sn).</param>
        public void Tick(float dt)
        {
            ICompanionState state = Current;
            if (state == null)
            {
                return;
            }

            try
            {
                state.Tick(dt);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Companion] '{state.Name}' Tick içinde hata: {e.Message}");
                Debug.LogException(e);
            }
        }

        private static void SafeInvoke(Action action, ICompanionState state, string phase)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Companion] '{state.Name}' {phase} içinde hata: {e.Message}");
                Debug.LogException(e);
            }
        }
    }
}
