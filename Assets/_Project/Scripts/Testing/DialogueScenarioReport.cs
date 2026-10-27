using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Yazlab.LLM;

namespace Yazlab.Testing
{
    /// <summary>
    /// Senaryo koşusu sonuçlarından README'ye yapıştırılabilir Markdown üretir: model karşılaştırma tablosu,
    /// senaryo × model başarı matrisi ve her senaryo için gerçek model yanıtlarından örnek diyaloglar.
    /// </summary>
    public static class DialogueScenarioReport
    {
        /// <summary>Raporu üretir; <paramref name="primaryModel"/> örnek diyaloglarda ham JSON'u gösterilen modeldir.</summary>
        public static string Build(IReadOnlyList<DialogueScenarioRunner.ModelRun> runs, IReadOnlyList<DialogueScenario> scenarios,
            int repetitions, string primaryModel, int openThreshold)
        {
            var sb = new StringBuilder(8192);
            sb.AppendLine($"## LLM senaryo testleri ({DateTime.Now:yyyy-MM-dd HH:mm})");
            sb.AppendLine();
            sb.AppendLine($"Her senaryo temiz oturumda, sabit oyun durumuyla ve {repetitions} tekrar çalıştırıldı (oyundaki akış: " +
                          $"DialogueSession → OllamaClient → JsonNpcResponseParser, bozuk JSON'da 1 düzeltme isteği, open_door koruması: " +
                          $"şef ölü + güven ≥ {openThreshold}). Gecikmeler ısınmış modelde, istemci tarafında ölçüldü. " +
                          "Tüm yanıtlar gerçek Ollama çıktısıdır (`llm_log.csv`, etiket `b23_*`).");
            sb.AppendLine();

            AppendComparison(sb, runs);
            AppendMatrix(sb, runs, scenarios, repetitions);
            AppendExamples(sb, runs, scenarios, primaryModel);
            return sb.ToString();
        }

        private static void AppendComparison(StringBuilder sb, IReadOnlyList<DialogueScenarioRunner.ModelRun> runs)
        {
            sb.AppendLine("### Model karşılaştırması");
            sb.AppendLine();
            sb.AppendLine("| Model | Soğuk ısınma (ms) | Ort. gecikme (ms) | Medyan | p95 | İlk denemede geçerli JSON | Düzeltmeyle geçerli | Beklenen davranış | Korumanın engellediği open_door | VRAM |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (DialogueScenarioRunner.ModelRun run in runs)
            {
                if (!run.Available)
                {
                    sb.AppendLine($"| {run.Model} | — | — | — | — | — | — | — | — | kullanılamadı: {Clean(run.UnavailableReason)} |");
                    continue;
                }

                List<ScenarioTurnResult> turns = run.Results.SelectMany(r => r.Turns).ToList();
                List<long> latencies = turns.Where(t => t.ErrorKind == LlmErrorKind.None || t.Parsed).Select(t => t.LatencyMs).OrderBy(x => x).ToList();
                int firstTry = turns.Count(t => t.ParsedFirstTry);
                int parsed = turns.Count(t => t.Parsed);
                int passed = run.Results.Count(r => r.Passed);
                int blocked = turns.Count(t => t.GuardBlocked);
                sb.AppendLine($"| {run.Model} | {run.WarmUpMs} | {Mean(latencies):F0} | {Percentile(latencies, 0.5)} | {Percentile(latencies, 0.95)} | " +
                              $"{Pct(firstTry, turns.Count)} ({firstTry}/{turns.Count}) | {Pct(parsed, turns.Count)} | " +
                              $"{Pct(passed, run.Results.Count)} ({passed}/{run.Results.Count}) | {blocked} | {Gb(run.VramBytes)} |");
            }

            sb.AppendLine();
        }

        private static void AppendMatrix(StringBuilder sb, IReadOnlyList<DialogueScenarioRunner.ModelRun> runs,
            IReadOnlyList<DialogueScenario> scenarios, int repetitions)
        {
            sb.AppendLine("### Senaryo bazında beklenen davranış (başarılı tekrar / toplam)");
            sb.AppendLine();
            sb.Append("| Senaryo | Beklenen |");
            foreach (DialogueScenarioRunner.ModelRun run in runs)
            {
                sb.Append($" {run.Model} |");
            }

            sb.AppendLine();
            sb.Append("|---|---|");
            sb.AppendLine(string.Concat(Enumerable.Repeat("---|", runs.Count)));
            foreach (DialogueScenario scenario in scenarios)
            {
                sb.Append($"| {scenario.Title} | {scenario.Expectation} |");
                foreach (DialogueScenarioRunner.ModelRun run in runs)
                {
                    List<ScenarioRunResult> results = run.Results.Where(r => r.Scenario == scenario).ToList();
                    sb.Append(results.Count == 0 ? " — |" : $" {results.Count(r => r.Passed)}/{results.Count} |");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        private static void AppendExamples(StringBuilder sb, IReadOnlyList<DialogueScenarioRunner.ModelRun> runs,
            IReadOnlyList<DialogueScenario> scenarios, string primaryModel)
        {
            sb.AppendLine("### Örnek diyaloglar (her modelin 1. tekrarı)");
            sb.AppendLine();
            foreach (DialogueScenario scenario in scenarios)
            {
                sb.AppendLine($"#### {scenario.Title}");
                sb.AppendLine();
                sb.AppendLine($"Beklenen: {scenario.Expectation}");
                sb.AppendLine();
                foreach (DialogueScenarioRunner.ModelRun run in runs.Where(r => r.Available))
                {
                    ScenarioRunResult result = run.Results.FirstOrDefault(r => r.Scenario == scenario && r.Repetition == 1);
                    if (result == null)
                    {
                        continue;
                    }

                    sb.AppendLine($"**{run.Model}** — {(result.Passed ? "✓ beklenen davranış" : "✗ beklenenden farklı")}");
                    sb.AppendLine();
                    foreach (ScenarioTurnResult t in result.Turns)
                    {
                        sb.AppendLine($"> **Oyuncu:** {Clean(t.PlayerText)}  ");
                        if (t.Parsed)
                        {
                            string action = NpcResponseSchema.ToWireName(t.Requested);
                            string applied = t.Applied != t.Requested ? $" → uygulanan `{NpcResponseSchema.ToWireName(t.Applied)}` (koruma)" : string.Empty;
                            sb.AppendLine($"> **Muhafız Kael** (`{NpcResponseSchema.ToWireName(t.Emotion)}`, Δ {t.TrustDelta:+0;-0;0}, güven {t.TrustAfter}, " +
                                          $"action `{action}`{applied}, {t.LatencyMs} ms{(t.CorrectionUsed ? ", düzeltme isteğiyle" : string.Empty)}): {Clean(t.Reply)}  ");
                        }
                        else
                        {
                            sb.AppendLine($"> **[Sistem]** {t.ErrorKind}: {Clean(t.Error)}  ");
                        }
                    }

                    sb.AppendLine();
                    if (string.Equals(run.Model, primaryModel, StringComparison.OrdinalIgnoreCase) && result.Turns.Count > 0)
                    {
                        sb.AppendLine("Ham JSON (son tur):");
                        sb.AppendLine();
                        sb.AppendLine("```json");
                        sb.AppendLine(result.Turns[result.Turns.Count - 1].RawResponse ?? string.Empty);
                        sb.AppendLine("```");
                        sb.AppendLine();
                    }
                }
            }
        }

        private static double Mean(List<long> values) => values.Count == 0 ? 0 : values.Average();

        private static string Percentile(List<long> sorted, double p)
        {
            if (sorted.Count == 0)
            {
                return "—";
            }

            int index = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))].ToString(CultureInfo.InvariantCulture);
        }

        private static string Pct(int part, int total) =>
            total == 0 ? "—" : (100.0 * part / total).ToString("F0", CultureInfo.InvariantCulture) + "%";

        private static string Gb(long bytes) =>
            bytes <= 0 ? "—" : (bytes / (1024.0 * 1024 * 1024)).ToString("F1", CultureInfo.InvariantCulture) + " GB";

        private static string Clean(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
    }
}
