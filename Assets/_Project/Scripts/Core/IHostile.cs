using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Şu anda saldırgan olan (oyuncuya düşman) varlık. Köpeğin savaş desteği hedefleri bununla tanır;
    /// kapalı veya ölü düşman saldırgan sayılmaz (ör. konuşma sırasında kapı bekçisi).
    /// </summary>
    public interface IHostile
    {
        /// <summary>Şu an saldırgan ve hayatta mı.</summary>
        bool IsHostile { get; }

        /// <summary>Varlığın dünya konumu.</summary>
        Transform HostileTransform { get; }
    }
}
