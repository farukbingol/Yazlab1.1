using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Yazlab.Testing
{
    /// <summary>
    /// Companion deneme sonuçlarından README'ye doğrudan yapıştırılabilecek Markdown özet tablosu üretir.
    /// Süre ve yol istatistikleri yalnızca başarılı denemelerden, recover ortalaması tüm denemelerden hesaplanır.
    /// </summary>
    public static class CompanionTestReport
    {
        /// <summary>Başlangıç noktası başına bir satır ve en altta toplam satırı olan Markdown tablosu döndürür.</summary>
        public static string BuildMarkdown(IReadOnlyList<CompanionTrialResult> results, IReadOnlyList<string> spawnNames, float timeScale)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine($"### Companion Test Sonuçları ({results.Count} deneme, timeScale = {F(timeScale, "F1")})");
            sb.AppendLine();
            sb.AppendLine("| Başlangıç | Kuş uçuşu (m) | En kısa yol (m) | Deneme | Başarı % | Ort. süre (sn) | Std (sn) | Ort. yol (m) | Ort. recover |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

            for (int s = 0; s < spawnNames.Count; s++)
            {
                AppendRow(sb, spawnNames[s], results, s);
            }

            AppendRow(sb, "**Toplam**", results, -1);
            AppendFailureBreakdown(sb, results);
            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, string label, IReadOnlyList<CompanionTrialResult> results, int spawnIndex)
        {
            int count = 0;
            int successCount = 0;
            int recoverSum = 0;
            float straightSum = 0f;
            float optimalSum = 0f;
            int optimalCount = 0;
            var durations = new List<float>();
            float pathSum = 0f;

            for (int i = 0; i < results.Count; i++)
            {
                CompanionTrialResult r = results[i];
                if (spawnIndex >= 0 && r.SpawnIndex != spawnIndex)
                {
                    continue;
                }

                count++;
                recoverSum += r.RecoverCount;
                straightSum += r.StraightDistance;
                if (r.OptimalPathLength >= 0f)
                {
                    optimalSum += r.OptimalPathLength;
                    optimalCount++;
                }

                if (r.Success)
                {
                    successCount++;
                    durations.Add(r.Duration);
                    pathSum += r.PathLength;
                }
            }

            if (count == 0)
            {
                sb.AppendLine($"| {label} | - | - | 0 | - | - | - | - | - |");
                return;
            }

            float successRate = 100f * successCount / count;
            float meanDuration = Mean(durations);
            float stdDuration = SampleStdDev(durations, meanDuration);
            string meanPath = successCount > 0 ? F(pathSum / successCount) : "-";
            string straight = spawnIndex >= 0 ? F(straightSum / count, "F1") : "-";
            string optimal = spawnIndex >= 0 && optimalCount > 0 ? F(optimalSum / optimalCount, "F1") : "-";

            sb.AppendLine(
                $"| {label} | {straight} | {optimal} | {count} | {F(successRate, "F0")} | " +
                $"{(successCount > 0 ? F(meanDuration) : "-")} | {(successCount > 1 ? F(stdDuration) : "-")} | " +
                $"{meanPath} | {F((float)recoverSum / count)} |");
        }

        private static void AppendFailureBreakdown(StringBuilder sb, IReadOnlyList<CompanionTrialResult> results)
        {
            var counts = new SortedDictionary<string, int>();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Success)
                {
                    continue;
                }

                string key = results[i].Outcome ?? "unknown";
                counts.TryGetValue(key, out int c);
                counts[key] = c + 1;
            }

            if (counts.Count == 0)
            {
                return;
            }

            sb.AppendLine();
            sb.Append("Başarısız denemeler: ");
            bool first = true;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                sb.Append(first ? string.Empty : ", ").Append(pair.Key).Append(" × ").Append(pair.Value);
                first = false;
            }

            sb.AppendLine();
        }

        private static float Mean(List<float> values)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            return sum / values.Count;
        }

        /// <summary>Örneklem standart sapması (n − 1); iki değerden azsa 0.</summary>
        private static float SampleStdDev(List<float> values, float mean)
        {
            if (values.Count < 2)
            {
                return 0f;
            }

            float sq = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                float d = values[i] - mean;
                sq += d * d;
            }

            return UnityEngine.Mathf.Sqrt(sq / (values.Count - 1));
        }

        private static string F(float value, string format = "F2")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
