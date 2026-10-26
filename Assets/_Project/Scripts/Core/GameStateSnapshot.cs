using System;

namespace Yazlab.Core
{
    /// <summary>
    /// LLM system prompt'una ve oyun sonu ekranına aktarılacak anlık oyun durumu özeti.
    /// </summary>
    [Serializable]
    public readonly struct GameStateSnapshot
    {
        /// <summary>
        /// Kabile şefinin (boss) yenilip yenilmediği.
        /// </summary>
        public bool BossDefeated { get; }

        /// <summary>
        /// Oyuncunun baltayı (Hatchet) kuşanıp kuşanmadığı.
        /// </summary>
        public bool HasAxe { get; }

        /// <summary>
        /// Köpek companion'ın fiziksel engel kapısını (Gate) açıp açmadığı.
        /// </summary>
        public bool CompanionGateOpened { get; }

        /// <summary>
        /// Son kapının (bekçi kapısı) açılıp açılmadığı.
        /// </summary>
        public bool FinalDoorOpened { get; }

        /// <summary>
        /// Oyuncunun anlık sağlık puanı.
        /// </summary>
        public int PlayerHp { get; }

        /// <summary>
        /// Sahnede hayatta kalan kurt sayısı.
        /// </summary>
        public int WolvesAliveCount { get; }

        /// <summary>
        /// Yeni bir <see cref="GameStateSnapshot"/> örneği oluşturur.
        /// </summary>
        public GameStateSnapshot(
            bool bossDefeated,
            bool hasAxe,
            bool companionGateOpened,
            bool finalDoorOpened,
            int playerHp,
            int wolvesAliveCount)
        {
            BossDefeated = bossDefeated;
            HasAxe = hasAxe;
            CompanionGateOpened = companionGateOpened;
            FinalDoorOpened = finalDoorOpened;
            PlayerHp = playerHp;
            WolvesAliveCount = wolvesAliveCount;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"BossDefeated={BossDefeated}, HasAxe={HasAxe}, CompanionGateOpened={CompanionGateOpened}, " +
                   $"FinalDoorOpened={FinalDoorOpened}, PlayerHp={PlayerHp}, WolvesAliveCount={WolvesAliveCount}";
        }
    }
}
