using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Yazlab.Companion;
using Yazlab.Testing;
using Yazlab.World;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Bölüm 2 şalterine aday konumlar kurar: duvarın arkasında, yalnız tünelden ulaşılan beş nokta
    /// (<c>Setup_Bolum2/LeverSpots</c>) ve <c>Lever_01</c> üzerinde <see cref="RandomizedPlacement"/>.
    /// Her oyunda şalter bunlardan rastgele birine taşınır; companion test harness'ı tüm adayları sırayla dener.
    /// Idempotent: tekrar çalıştırıldığında noktaları yeniden kurar.
    /// </summary>
    public static class LeverPlacementSetup
    {
        private const string RootName = "Setup_Bolum2";
        private const string SpotsName = "LeverSpots";

        // Duvar z=30, tünel x=12. Hepsi Dog NavMesh'inde, emir alanına (0, 25.5) kuş uçuşu ≤ 30 m.
        private static readonly (string Name, Vector3 Position, float Yaw)[] Spots =
        {
            ("Spot_A_TunelArkasi", new Vector3(3f, 0f, 38f), 0f),
            ("Spot_B_Bati", new Vector3(-24f, 0f, 37f), 90f),
            ("Spot_C_Dogu", new Vector3(21f, 0f, 38f), -90f),
            ("Spot_D_KuzeyBati", new Vector3(-10f, 0f, 46f), 180f),
            ("Spot_E_KuzeyDogu", new Vector3(13f, 0f, 47f), 180f),
        };

        [MenuItem("Yazlab/Kurulum/Bolum 2 Salter Konumlari")]
        public static void Run()
        {
            GameObject root = GameObject.Find(RootName);
            GameObject leverGo = GameObject.Find(RootName + "/Lever_01");
            GameObject zone = GameObject.Find(RootName + "/CommandZone");
            CompanionAgent dog = Object.FindFirstObjectByType<CompanionAgent>();
            if (root == null || leverGo == null || zone == null || dog == null || !leverGo.TryGetComponent(out Lever lever))
            {
                Debug.LogError("[Kurulum] Setup_Bolum2/Lever_01, CommandZone veya köpek bulunamadı; önce 'Bolum 2 Engel Alani' çalıştır.");
                return;
            }

            Transform existing = root.transform.Find(SpotsName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var spotsRoot = new GameObject(SpotsName);
            spotsRoot.transform.SetParent(root.transform, false);
            var candidates = new Transform[Spots.Length];
            for (int i = 0; i < Spots.Length; i++)
            {
                var spot = new GameObject(Spots[i].Name);
                spot.transform.SetParent(spotsRoot.transform, false);
                spot.transform.SetPositionAndRotation(Spots[i].Position, Quaternion.Euler(0f, Spots[i].Yaw, 0f));
                candidates[i] = spot.transform;
            }

            RandomizedPlacement placement = leverGo.TryGetComponent(out RandomizedPlacement p) ? p : leverGo.AddComponent<RandomizedPlacement>();
            var so = new SerializedObject(placement);
            SerializedProperty list = so.FindProperty("_candidates");
            list.arraySize = candidates.Length;
            for (int i = 0; i < candidates.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = candidates[i];
            }

            so.FindProperty("_reachableFrom").objectReferenceValue = zone.transform;
            so.FindProperty("_probePoint").objectReferenceValue = lever.InteractionPoint;
            so.FindProperty("_agentTypeId").intValue = dog.GetComponent<NavMeshAgent>().agentTypeID;
            so.FindProperty("_randomizeOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            CompanionTestHarness harness = Object.FindFirstObjectByType<CompanionTestHarness>();
            if (harness != null)
            {
                var harnessSo = new SerializedObject(harness);
                harnessSo.FindProperty("_targetPlacement").objectReferenceValue = placement;
                harnessSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] Şalter aday konumları kuruldu ({candidates.Length} nokta), harness={(harness != null ? "bağlandı" : "yok")}. Sahne kaydedildi.");
        }
    }
}
