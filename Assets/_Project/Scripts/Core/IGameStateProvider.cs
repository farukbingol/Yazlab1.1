namespace Yazlab.Core
{
    /// <summary>
    /// Diyalog oturumu (LLM) ve UI bileşenleri için anlık oyun durumunu sağlayan servis arayüzü.
    /// </summary>
    public interface IGameStateProvider
    {
        /// <summary>
        /// Oyunun mevcut çalışma durumu.
        /// </summary>
        GameState CurrentState { get; }

        /// <summary>
        /// Sahnedeki güncel oyun durumunun anlık görüntüsünü yakalar.
        /// </summary>
        /// <returns>Anlık <see cref="GameStateSnapshot"/> verisi.</returns>
        GameStateSnapshot Capture();
    }
}
