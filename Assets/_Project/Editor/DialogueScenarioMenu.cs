using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Yazlab.LLM;
using Yazlab.Testing;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// LLM senaryo testlerini Editor'den veya batchmode'dan çalıştırır ve raporu yazar
    /// (<c>persistentDataPath/llm_scenarios_report.md</c> + <c>Docs/test-results/llm_scenarios_report_{tarih}.md</c>).
    /// Batchmode: <c>-executeMethod Yazlab.EditorTools.DialogueScenarioMenu.RunBatch -models a,b -reps 3</c> (bitince Editor kapanır).
    /// </summary>
    public static class DialogueScenarioMenu
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/LlmConfig.asset";
        private const string PersonaPath = "Assets/_Project/ScriptableObjects/NpcPersona_Kael.asset";
        private static readonly string[] DefaultModels = { "qwen2.5:3b", "llama3.2:3b", "qwen2.5:7b" };
        private const int DefaultRepetitions = 3;

        private static bool _running;

        /// <summary>Varsayılan 3 modelde, senaryo başına 3 tekrar.</summary>
        [MenuItem("Yazlab/Test/LLM Senaryo Testleri (3 model)")]
        public static async void RunFromMenu()
        {
            try
            {
                await RunAsync(DefaultModels, DefaultRepetitions);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Batchmode girişi: argümanları okur, koşar, bitince Editor'den çıkar (0 = başarılı).</summary>
        public static async void RunBatch()
        {
            int exitCode = 1;
            try
            {
                string[] models = ArgValue("-models")?.Split(',') ?? DefaultModels;
                int reps = int.TryParse(ArgValue("-reps"), out int r) ? Mathf.Max(1, r) : DefaultRepetitions;
                exitCode = await RunAsync(models, reps) ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static async Awaitable<bool> RunAsync(IReadOnlyList<string> models, int repetitions)
        {
            if (_running)
            {
                Debug.LogWarning("[Senaryo] Koşu zaten devam ediyor.");
                return false;
            }

            var config = AssetDatabase.LoadAssetAtPath<LlmConfig>(ConfigPath);
            var persona = AssetDatabase.LoadAssetAtPath<NpcPersona>(PersonaPath);
            if (config == null || persona == null)
            {
                Debug.LogError("[Senaryo] LlmConfig veya NpcPersona_Kael bulunamadı.");
                return false;
            }

            _running = true;
            try
            {
                IReadOnlyList<DialogueScenario> scenarios = DialogueScenario.Standard();
                var runner = new DialogueScenarioRunner(config, persona);
                var runs = new List<DialogueScenarioRunner.ModelRun>();
                foreach (string model in models)
                {
                    runs.Add(await runner.RunModelAsync(model.Trim(), scenarios, repetitions, CancellationToken.None));
                }

                string report = DialogueScenarioReport.Build(runs, scenarios, repetitions, models[0].Trim(), config.OpenTrustThreshold);
                string primary = Path.Combine(Application.persistentDataPath, "llm_scenarios_report.md");
                string docs = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Docs", "test-results", $"llm_scenarios_report_{DateTime.Now:yyyyMMdd}.md");
                Directory.CreateDirectory(Path.GetDirectoryName(docs));
                File.WriteAllText(primary, report, new UTF8Encoding(false));
                File.WriteAllText(docs, report, new UTF8Encoding(false));
                Debug.Log($"[Senaryo] Bitti: {runs.Count} model. Rapor → {primary} ve {docs}");
                return runs.Exists(r => r.Available);
            }
            finally
            {
                _running = false;
            }
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
