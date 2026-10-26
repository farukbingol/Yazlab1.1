using System;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Hasar alabilen tüm oyun varlıkları (oyuncu, düşmanlar, saldırganlaşan NPC vb.) için ortak sözleşme.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Varlığın ölü olup olmadığını belirtir.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Varlığın mevcut can değeri.
        /// </summary>
        int CurrentHealth { get; }

        /// <summary>
        /// Varlığın maksimum can değeri.
        /// </summary>
        int MaxHealth { get; }

        /// <summary>
        /// Varlık öldüğünde tetiklenen olay.
        /// </summary>
        event Action<IDamageable> Died;

        /// <summary>
        /// Varlığa belirtilen miktarda ve yönde hasar uygular.
        /// </summary>
        /// <param name="amount">Uygulanacak pozitif hasar miktarı.</param>
        /// <param name="hitDirection">Darbenin geldiği dünya yönü.</param>
        void TakeDamage(int amount, Vector3 hitDirection);
    }
}
