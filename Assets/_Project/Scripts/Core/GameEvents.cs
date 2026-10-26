using System;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Modüller arası gevşek bağlı (loosely-coupled) iletişimi sağlayan merkezi C# olay kanalı.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>
        /// Bölüm 1'deki kurt şefi (boss) yenildiğinde tetiklenir.
        /// </summary>
        public static event Action BossDefeated;

        /// <summary>
        /// Şeften düşen balta (Hatchet) oyuncu tarafından alındığında tetiklenir.
        /// </summary>
        public static event Action AxePickedUp;

        /// <summary>
        /// Bölüm 2'deki fiziksel engel kapısı (Gate) companion tarafından açıldığında tetiklenir.
        /// </summary>
        public static event Action GateOpened;

        /// <summary>
        /// Oyuncu kapı bekçisi NPC ile diyaloğa başladığında tetiklenir.
        /// </summary>
        public static event Action DialogueStarted;

        /// <summary>
        /// Kapı bekçisi NPC ile diyalog sona erdiğinde tetiklenir.
        /// </summary>
        public static event Action DialogueEnded;

        /// <summary>
        /// Son kapı açılıp oyun kazanıldığında tetiklenir.
        /// </summary>
        public static event Action GameWon;

        /// <summary>
        /// <see cref="BossDefeated"/> olayını tetikler.
        /// </summary>
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();

        /// <summary>
        /// <see cref="AxePickedUp"/> olayını tetikler.
        /// </summary>
        public static void RaiseAxePickedUp() => AxePickedUp?.Invoke();

        /// <summary>
        /// <see cref="GateOpened"/> olayını tetikler.
        /// </summary>
        public static void RaiseGateOpened() => GateOpened?.Invoke();

        /// <summary>
        /// <see cref="DialogueStarted"/> olayını tetikler.
        /// </summary>
        public static void RaiseDialogueStarted() => DialogueStarted?.Invoke();

        /// <summary>
        /// <see cref="DialogueEnded"/> olayını tetikler.
        /// </summary>
        public static void RaiseDialogueEnded() => DialogueEnded?.Invoke();

        /// <summary>
        /// <see cref="GameWon"/> olayını tetikler.
        /// </summary>
        public static void RaiseGameWon() => GameWon?.Invoke();

        /// <summary>
        /// Sahne geçişlerinde veya testlerde tüm olay aboneliklerini temizler.
        /// </summary>
        public static void ClearAll()
        {
            BossDefeated = null;
            AxePickedUp = null;
            GateOpened = null;
            DialogueStarted = null;
            DialogueEnded = null;
            GameWon = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            ClearAll();
        }
    }
}
