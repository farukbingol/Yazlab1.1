using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yazlab.Companion;
using Yazlab.Testing;
using Yazlab.World;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Sahneye companion test harness'ını ve başlangıç noktalarını kurar (idempotent),
    /// Play modunda testi tek tuşla (F9) başlatıp durdurmayı sağlar.
    /// </summary>
    public static class CompanionTestSetup
    {
        private const string RootName = "Setup_CompanionTest";

        /// <summary>
        /// Başlangıç noktaları: farklı mesafe (15–58 m kuş uçuşu) ve açılar; kapı önü (tünele sapmak zorunda),
        /// ağaç arkası, orman içinden uzak nokta. Hedef değil, yalnızca köpeğin denemeye başladığı yerlerdir.
        /// </summary>
        private static readonly (string Name, Vector3 Position)[] SpawnLayout =
        {
            ("Spawn_01_OyuncuYani", new Vector3(-1.5f, 0f, 0f)),
            ("Spawn_02_KapiOnu", new Vector3(0f, 0f, 20f)),
            ("Spawn_03_AgacArkasi", new Vector3(-13f, 0f, 22.5f)),
            ("Spawn_04_TunelSagi", new Vector3(20f, 0f, 24f)),
            ("Spawn_05_GuneyOrman", new Vector3(5f, 0f, -20f)),
            ("Spawn_06_BatiUzak", new Vector3(-22f, 0f, 8f)),
        };

        /// <summary>Harness objesini, başlangıç noktalarını ve sıfırlanacak nesneleri kurar; sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Companion Test Harness")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Kurulum] Play modunda sahneye kalıcı yazılamaz; Play'den çık ve tekrar çalıştır.");
                return;
            }

            CompanionAgent companion = Object.FindFirstObjectByType<CompanionAgent>();
            if (companion == null)
            {
                Debug.LogError("[Kurulum] CompanionAgent bulunamadı; önce 'Yazlab/Kurulum/Companion (Kopek)' çalıştır.");
                return;
            }

            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
            }

            if (!root.TryGetComponent(out CompanionTestHarness harness))
            {
                harness = root.AddComponent<CompanionTestHarness>();
            }

            var spawns = new Transform[SpawnLayout.Length];
            for (int i = 0; i < SpawnLayout.Length; i++)
            {
                Transform spawn = root.transform.Find(SpawnLayout[i].Name);
                if (spawn == null)
                {
                    spawn = new GameObject(SpawnLayout[i].Name).transform;
                    spawn.SetParent(root.transform, false);
                }

                spawn.position = SpawnLayout[i].Position;
                spawns[i] = spawn;
            }

            var so = new SerializedObject(harness);
            so.FindProperty("_companion").objectReferenceValue = companion;

            SerializedProperty spawnProp = so.FindProperty("_spawnPoints");
            spawnProp.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
            {
                spawnProp.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
            }

            // Kapı önce, şalter sonra sıfırlanır (şalterin CanInteract'i kapının kapalı olmasına bağlı).
            Gate[] gates = Object.FindObjectsByType<Gate>(FindObjectsSortMode.InstanceID);
            Lever[] levers = Object.FindObjectsByType<Lever>(FindObjectsSortMode.InstanceID);
            SerializedProperty resetProp = so.FindProperty("_resettables");
            resetProp.arraySize = gates.Length + levers.Length;
            for (int i = 0; i < gates.Length; i++)
            {
                resetProp.GetArrayElementAtIndex(i).objectReferenceValue = gates[i];
            }

            for (int i = 0; i < levers.Length; i++)
            {
                resetProp.GetArrayElementAtIndex(gates.Length + i).objectReferenceValue = levers[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] Companion test harness hazır — {spawns.Length} başlangıç noktası, " +
                      $"{gates.Length} kapı + {levers.Length} şalter sıfırlanacak. Play'de F9 ile başlat. Sahne kaydedildi.");
        }

        /// <summary>Play modunda sahnedeki harness ile testi başlatır; çalışıyorsa durdurur.</summary>
        [MenuItem("Yazlab/Test/Companion Testini Baslat-Durdur _F9")]
        public static void ToggleRun()
        {
            CompanionTestHarness harness = Object.FindFirstObjectByType<CompanionTestHarness>();
            if (harness == null)
            {
                Debug.LogError("[CompanionTest] Sahnede CompanionTestHarness yok; 'Yazlab/Kurulum/Companion Test Harness' çalıştır.");
                return;
            }

            if (harness.IsRunning)
            {
                harness.StopRun();
            }
            else
            {
                harness.StartRun();
            }
        }

        [MenuItem("Yazlab/Test/Companion Testini Baslat-Durdur _F9", true)]
        private static bool ToggleRunValidate() => EditorApplication.isPlaying;

        /// <summary>Sonuç klasörünü (Application.persistentDataPath) dosya yöneticisinde açar.</summary>
        [MenuItem("Yazlab/Test/Sonuc Klasorunu Ac")]
        public static void RevealResults()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
