using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Build doğrulama ve build alma menüleri: sahne listesi (0 GameScene, 1 Bolum3_Koy), runtime script'lerde korumasız
    /// <c>UnityEditor</c> kullanımı taraması, şirket/ürün adı; Linux ve Windows standalone build (<c>Builds/</c>, git dışı).
    /// </summary>
    public static class BuildTools
    {
        private const string CompanyName = "KOU-Yazlab";
        private const string ProductName = "Yazlab";
        private const string BuildRoot = "Builds";

        /// <summary>Build ayarlarını düzeltir ve runtime kodunu tarar; sorun yoksa <c>true</c>.</summary>
        [MenuItem("Yazlab/Build/Build Ayarlarini Dogrula")]
        public static bool Validate()
        {
            IntegrationSetup.SetupBuildSettings();
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            // Oyuncu alt-tab yapınca da LLM isteği/sağlık kontrolü ve oyun akışı sürsün (demo sırasında pencere değişebilir).
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            int problems = 0;

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!File.Exists(scene.path))
                {
                    report.AppendLine($"  ✗ Sahne yok: {scene.path}");
                    problems++;
                }
            }

            foreach (string file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (IsEditorOnlyPath(path))
                {
                    continue;
                }

                foreach (string line in UnguardedEditorUsages(File.ReadAllLines(path)))
                {
                    report.AppendLine($"  ✗ {path}: {line.Trim()}");
                    problems++;
                }
            }

            string scenes = string.Join(", ", EditorBuildSettings.scenes.Select((s, i) => $"{i}:{Path.GetFileNameWithoutExtension(s.path)}"));
            string summary = $"[Build] Doğrulama: sahneler [{scenes}], şirket '{PlayerSettings.companyName}', ürün '{PlayerSettings.productName}', " +
                             $"runInBackground {PlayerSettings.runInBackground}, sorun {problems}.";
            if (problems > 0)
            {
                Debug.LogError(summary + "\n" + report);
            }
            else
            {
                Debug.Log(summary + " Runtime kodunda korumasız UnityEditor kullanımı yok.");
            }

            return problems == 0;
        }

        /// <summary>Linux standalone build (<c>Builds/Linux/Yazlab.x86_64</c>).</summary>
        [MenuItem("Yazlab/Build/Linux Build Al")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Linux", ProductName + ".x86_64");

        /// <summary>Windows standalone build (<c>Builds/Windows/Yazlab.exe</c>).</summary>
        [MenuItem("Yazlab/Build/Windows Build Al")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Windows", ProductName + ".exe");

        private static void Build(BuildTarget target, string folder, string executable)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Build] Play modunda build alınamaz.");
                return;
            }

            if (!Validate())
            {
                Debug.LogError("[Build] Doğrulama başarısız; build alınmadı.");
                return;
            }

            string outputDir = Path.Combine(BuildRoot, folder);
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(outputDir, executable),
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            long folderBytes = Directory.Exists(outputDir)
                ? new DirectoryInfo(outputDir).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0L;
            string message = $"[Build] {folder}: {s.result} — hata {s.totalErrors}, uyarı {s.totalWarnings}, süre {s.totalTime:mm\\:ss}, " +
                             $"klasör {folderBytes / (1024f * 1024f):F1} MB → {options.locationPathName}";
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }

        private static bool IsEditorOnlyPath(string path) =>
            path.Contains("/Editor/") || path.Contains("/Tests/") || path.StartsWith("Assets/Editor/");

        /// <summary><c>#if UNITY_EDITOR</c> bloğu dışında kalan <c>UnityEditor</c> geçen satırlar (basit önişlemci takibi).</summary>
        private static System.Collections.Generic.IEnumerable<string> UnguardedEditorUsages(string[] lines)
        {
            var guardStack = new System.Collections.Generic.Stack<bool>();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("#if"))
                {
                    guardStack.Push(line.Contains("UNITY_EDITOR") && !line.Contains("!UNITY_EDITOR"));
                    continue;
                }

                if (line.StartsWith("#else") && guardStack.Count > 0)
                {
                    guardStack.Push(!guardStack.Pop());
                    continue;
                }

                if (line.StartsWith("#endif") && guardStack.Count > 0)
                {
                    guardStack.Pop();
                    continue;
                }

                if (line.StartsWith("//") || line.StartsWith("///"))
                {
                    continue;
                }

                bool guarded = guardStack.Any(g => g);
                if (!guarded && Regex.IsMatch(line, @"\bUnityEditor\b"))
                {
                    yield return raw;
                }
            }
        }
    }
}
