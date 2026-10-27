using Yazlab.Core;

namespace Yazlab.LLM
{
    /// <summary>
    /// Modelin aksiyon kararına körü körüne uyulmaz: <c>open_door</c> yalnızca şef gerçekten öldürüldüyse (oyun durumu)
    /// ve güven eşiği aşıldıysa uygulanır, aksi halde <see cref="NpcAction.None"/>. Oyun denetleyicisi ve senaryo testleri
    /// aynı kuralı kullanır.
    /// </summary>
    public static class NpcActionGuard
    {
        /// <summary>Korumadan sonra uygulanacak aksiyonu döndürür; <paramref name="rejection"/> reddedildiyse gerekçedir.</summary>
        public static NpcAction Apply(NpcAction requested, GameStateSnapshot? state, int trust, int openThreshold, out string rejection)
        {
            rejection = null;
            if (requested != NpcAction.OpenDoor)
            {
                return requested;
            }

            bool bossDefeated = state.HasValue && state.Value.BossDefeated;
            bool trustOk = trust >= openThreshold;
            if (bossDefeated && trustOk)
            {
                return NpcAction.OpenDoor;
            }

            rejection = $"open_door reddedildi (şef öldü: {bossDefeated}, güven {trust} / eşik {openThreshold})";
            return NpcAction.None;
        }
    }
}
