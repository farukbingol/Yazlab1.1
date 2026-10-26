using System;

namespace Yazlab.World
{
    /// <summary>
    /// Açılıp kapanabilen nesnelerin (kapı, geçit) sözleşmesi.
    /// </summary>
    public interface IOpenable
    {
        /// <summary>Nesne açık durumdaysa <c>true</c>.</summary>
        bool IsOpen { get; }

        /// <summary>Nesneyi açar.</summary>
        void Open();

        /// <summary>Nesneyi kapatır.</summary>
        void Close();

        /// <summary>Nesne açılmaya başladığında tetiklenir.</summary>
        event Action Opened;
    }
}
