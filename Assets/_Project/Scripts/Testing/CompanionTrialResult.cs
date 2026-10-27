using UnityEngine;

namespace Yazlab.Testing
{
    /// <summary>
    /// Tek bir companion test denemesinin ölçüm sonucu (CSV satırı ve özet tablo için).
    /// </summary>
    public readonly struct CompanionTrialResult
    {
        /// <summary>Koşu içindeki genel deneme numarası (1'den başlar).</summary>
        public int TrialNumber { get; }

        /// <summary>Başlangıç noktasının sırası (0'dan başlar).</summary>
        public int SpawnIndex { get; }

        /// <summary>Başlangıç noktasının sahnedeki adı.</summary>
        public string SpawnName { get; }

        /// <summary>Şalterin (hedefin) bu denemedeki aday konum sırası; yerleşim değişmiyorsa -1.</summary>
        public int TargetIndex { get; }

        /// <summary>Şalterin bu denemedeki aday konum adı.</summary>
        public string TargetName { get; }

        /// <summary>Köpeğin denemeye başladığı NavMesh noktası.</summary>
        public Vector3 SpawnPosition { get; }

        /// <summary>Başlangıç ile hedef arasındaki kuş uçuşu yatay mesafe (m).</summary>
        public float StraightDistance { get; }

        /// <summary>NavMesh'in hesapladığı en kısa yol uzunluğu (m); hesaplanamadıysa -1.</summary>
        public float OptimalPathLength { get; }

        /// <summary>Hedef etkinleştirildi mi.</summary>
        public bool Success { get; }

        /// <summary>Emirden sonuca kadar geçen oyun süresi (sn).</summary>
        public float Duration { get; }

        /// <summary>Köpeğin fiilen kat ettiği yatay yol (m).</summary>
        public float PathLength { get; }

        /// <summary>Deneme sırasında girilen Recover durumu sayısı.</summary>
        public int RecoverCount { get; }

        /// <summary>Sonuç kodu: <c>success</c>, <c>timeout</c>, <c>recover_limit</c>, <c>aborted_*</c>, <c>command_rejected</c>, <c>warp_failed</c>.</summary>
        public string Outcome { get; }

        public CompanionTrialResult(
            int trialNumber,
            int spawnIndex,
            string spawnName,
            Vector3 spawnPosition,
            float straightDistance,
            float optimalPathLength,
            bool success,
            float duration,
            float pathLength,
            int recoverCount,
            string outcome,
            int targetIndex = -1,
            string targetName = null)
        {
            TrialNumber = trialNumber;
            TargetIndex = targetIndex;
            TargetName = targetName;
            SpawnIndex = spawnIndex;
            SpawnName = spawnName;
            SpawnPosition = spawnPosition;
            StraightDistance = straightDistance;
            OptimalPathLength = optimalPathLength;
            Success = success;
            Duration = duration;
            PathLength = pathLength;
            RecoverCount = recoverCount;
            Outcome = outcome;
        }
    }
}
