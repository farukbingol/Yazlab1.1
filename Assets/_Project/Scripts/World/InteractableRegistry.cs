using System.Collections.Generic;
using UnityEngine;

namespace Yazlab.World
{
    /// <summary>
    /// Sahnedeki aktif <see cref="IInteractable"/> hedeflerinin kaydı. Köpek hedefi sabit koordinatla değil,
    /// buradan (ve sensörle) çözer. Nesneler <c>OnEnable</c>/<c>OnDisable</c> içinde kendilerini kaydeder.
    /// </summary>
    public static class InteractableRegistry
    {
        private static readonly List<IInteractable> Targets = new List<IInteractable>();

        /// <summary>Kayıtlı hedeflerin salt okunur görünümü.</summary>
        public static IReadOnlyList<IInteractable> All => Targets;

        /// <summary>Kayıtlı hedef sayısı.</summary>
        public static int Count => Targets.Count;

        /// <summary>Hedefi kaydeder (zaten kayıtlıysa yok sayar).</summary>
        public static void Register(IInteractable target)
        {
            if (target != null && !Targets.Contains(target))
            {
                Targets.Add(target);
            }
        }

        /// <summary>Hedefin kaydını siler.</summary>
        public static void Unregister(IInteractable target)
        {
            if (target != null)
            {
                Targets.Remove(target);
            }
        }

        /// <summary>
        /// <paramref name="actor"/> için etkileşime uygun, <paramref name="origin"/>'e en yakın hedefi bulur.
        /// </summary>
        /// <param name="origin">Aramanın merkezi (örn. köpeğin konumu).</param>
        /// <param name="maxDistance">En büyük arama mesafesi.</param>
        /// <param name="actor">Etkileşimi yapacak obje (<see cref="IInteractable.CanInteract"/> için).</param>
        /// <param name="nearest">Bulunan hedef.</param>
        public static bool TryGetNearest(Vector3 origin, float maxDistance, GameObject actor, out IInteractable nearest)
        {
            nearest = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Targets.Count; i++)
            {
                IInteractable candidate = Targets[i];
                if (candidate == null || candidate.InteractionPoint == null || !candidate.CanInteract(actor))
                {
                    continue;
                }

                float sqr = (candidate.InteractionPoint.position - origin).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    nearest = candidate;
                }
            }

            return nearest != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            Targets.Clear();
        }
    }
}
