namespace Yazlab.Core
{
    /// <summary>
    /// Oyunun genel akış durumlarını tanımlayan numaralandırma.
    /// </summary>
    public enum GameState
    {
        /// <summary>
        /// Oyuncu sahnede serbestçe dolaşıyor ve savaşıyor.
        /// </summary>
        Exploring = 0,

        /// <summary>
        /// Oyuncu kapı bekçisi NPC ile diyalog halinde.
        /// </summary>
        InDialogue = 1,

        /// <summary>
        /// Oyun duraklatılmış durumda.
        /// </summary>
        Paused = 2,

        /// <summary>
        /// Senaryo başarıyla tamamlandı (Son kapı açıldı veya bekçi yenildi).
        /// </summary>
        Victory = 3,

        /// <summary>
        /// Oyuncu öldü / oyun kaybedildi.
        /// </summary>
        GameOver = 4
    }
}
