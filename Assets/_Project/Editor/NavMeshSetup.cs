using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Sahnede NavMesh altyapısını idempotent olarak kuran Editor menü aracı:
    /// "Dog" agent type, ağaç/kaya collider'ları, <see cref="NavMeshSurface"/> bake'i
    /// ve köpek üzerinde <see cref="NavMeshAgent"/>.
    /// </summary>
    public static class NavMeshSetup
    {
        private const string DogAgentName = "Dog";
        private const float DogRadius = 0.28f;
        private const float DogHeight = 0.9f;
        private const float DogClimb = 0.4f;
        private const float DogSlope = 45f;

        private const string SurfaceObjectName = "Setup_NavMeshSurface";
        private const string DogObjectName = "Kopek";
        private const string ForestObjectName = "Forest";
        private const string NavMeshAssetFolder = "Assets/_Project/NavMesh";
        // Her sahnenin kendi bake verisi olur (GameScene → GameScene_Dog_NavMesh.asset, köy → Bolum3_Koy_Dog_NavMesh.asset).
        private static string NavMeshAssetPath => $"{NavMeshAssetFolder}/{SceneManager.GetActiveScene().name}_Dog_NavMesh.asset";

        // Collider eklenecek (yol tıkayan) obje adı parçaları; çalı/çimen bilerek dışarıda.
        private static readonly string[] BlockerNameParts = { "Tree", "Pine", "Rock" };
        private static readonly string[] RockNameParts = { "Rock" };

        // Köpek için Not Walkable ayak izi hacimleri: yerden FootprintMaxHeight m'ye kadar mesh köşeleri ölçülür (köpek boyu 0.9 m).
        private const string BlockerRootName = "Setup_NavBlockers";
        private const float FootprintMinHeight = 0.05f;
        private const float FootprintMaxHeight = 1.2f;
        private const float FootprintMargin = 0.05f;
        private const float BlockerVolumeHeight = 3f;

        /// <summary>
        /// Dog agent type'ını, ağaç/kaya collider'larını, NavMesh yüzeyini ve köpeğin ajanını kurar.
        /// </summary>
        [MenuItem("Yazlab/Kurulum/NavMesh ve Kopek Agent")]
        public static void Run()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            int dogAgentTypeId = EnsureDogAgentType();
            int colliderCount = EnsureForestColliders();
            int blockerCount = EnsureNavBlockers();

            NavMeshSurface surface = EnsureSurface(activeScene, dogAgentTypeId);
            BakeSurface(surface);

            EnsureDogAgent(dogAgentTypeId);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] NavMesh hazır — Dog agentTypeID={dogAgentTypeId}, yeni collider={colliderCount}, ayak izi hacmi={blockerCount}, " +
                      $"navMeshData={(surface.navMeshData != null ? "OK" : "YOK")}. Sahne kaydedildi.");
        }

        /// <summary>"Dog" agent type'ı yoksa oluşturur; varsa ölçülerini günceller. ID'yi döndürür.</summary>
        private static int EnsureDogAgentType()
        {
            int id = FindAgentTypeId(DogAgentName);
            if (id == int.MinValue)
            {
                NavMesh.CreateSettings();
                id = FindUnnamedAgentTypeId();
            }

            Object settingsAsset = LoadNavMeshProjectSettings();
            var so = new SerializedObject(settingsAsset);
            SerializedProperty settings = so.FindProperty("m_Settings");
            SerializedProperty names = so.FindProperty("m_SettingNames");

            for (int i = 0; i < settings.arraySize; i++)
            {
                SerializedProperty entry = settings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("agentTypeID").intValue != id)
                {
                    continue;
                }

                entry.FindPropertyRelative("agentRadius").floatValue = DogRadius;
                entry.FindPropertyRelative("agentHeight").floatValue = DogHeight;
                entry.FindPropertyRelative("agentClimb").floatValue = DogClimb;
                entry.FindPropertyRelative("agentSlope").floatValue = DogSlope;
                if (i < names.arraySize)
                {
                    names.GetArrayElementAtIndex(i).stringValue = DogAgentName;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return id;
        }

        private static Object LoadNavMeshProjectSettings()
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            return all.Length > 0 ? all[0] : null;
        }

        private static int FindAgentTypeId(string agentName)
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == agentName)
                {
                    return s.agentTypeID;
                }
            }

            return int.MinValue;
        }

        /// <summary>Yeni oluşturulan (adı boş) agent type'ın ID'sini bulur.</summary>
        private static int FindUnnamedAgentTypeId()
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = count - 1; i >= 0; i--)
            {
                NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
                string n = NavMesh.GetSettingsNameFromID(s.agentTypeID);
                if (string.IsNullOrEmpty(n) || n == "Unknown")
                {
                    return s.agentTypeID;
                }
            }

            return NavMesh.GetSettingsByIndex(count - 1).agentTypeID;
        }

        /// <summary>Forest altındaki ağaç/kaya objelerine, collider'ı yoksa uygun collider ekler.</summary>
        private static int EnsureForestColliders()
        {
            GameObject forest = GameObject.Find(ForestObjectName);
            if (forest == null)
            {
                Debug.LogWarning("[Kurulum] 'Forest' bulunamadı; collider eklenmedi.");
                return 0;
            }

            int added = 0;
            foreach (Transform child in forest.transform)
            {
                if (!NameMatches(child.name, BlockerNameParts) || !TryGetLocalBounds(child, out Bounds local))
                {
                    continue;
                }

                if (NameMatches(child.name, RockNameParts))
                {
                    // Kayalar: yalnızca collider'ı yoksa kutu ekle (mevcutlara dokunma).
                    if (child.GetComponentInChildren<Collider>() == null)
                    {
                        BoxCollider box = child.gameObject.AddComponent<BoxCollider>();
                        box.center = local.center;
                        box.size = local.size;
                        EditorUtility.SetDirty(child.gameObject);
                        added++;
                    }

                    continue;
                }

                // Ağaçlar: gövde/alt dal ayak izine göre kapsül ekle veya mevcut kapsülü güncelle.
                bool existed = child.TryGetComponent(out CapsuleCollider existing);
                if (!existed && child.GetComponentInChildren<Collider>() != null)
                {
                    continue; // başka tür collider'ı olan ağaçlara dokunma
                }

                AddOrUpdateTrunkCollider(child, local, existed ? existing : null);
                EditorUtility.SetDirty(child.gameObject);
                if (!existed)
                {
                    added++;
                }
            }

            return added;
        }

        /// <summary>
        /// Gövde kapsülünü ekler/günceller. Döndürülmüş ağaçlarda (ör. FBX -90°) kapsülün ekseni dünya yukarısına en yakın
        /// yerel eksen seçilir; alt uç zemine gömülür (içi boş kabuğun altında yürünebilir boşluk kalmasın).
        /// </summary>
        private static void AddOrUpdateTrunkCollider(Transform tree, Bounds local, CapsuleCollider existing)
        {
            Vector3[] axes = { tree.right, tree.up, tree.forward };
            int axis = 0;
            for (int i = 1; i < 3; i++)
            {
                if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[axis].y))
                {
                    axis = i;
                }
            }

            bool flipped = axes[axis].y < 0f;
            Vector3 scale = tree.lossyScale;
            float scaleAlong = Mathf.Max(0.0001f, Mathf.Abs(scale[axis]));
            float scaleRadial = 0.0001f;
            float minRadialSize = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                if (i == axis)
                {
                    continue;
                }

                scaleRadial = Mathf.Max(scaleRadial, Mathf.Abs(scale[i]));
                minRadialSize = Mathf.Min(minRadialSize, local.size[i] * Mathf.Abs(scale[i]));
            }

            float worldRadius = Mathf.Clamp(minRadialSize * 0.12f, 0.2f, 0.5f);
            float localRadius = worldRadius / scaleRadial;

            float groundEnd = flipped ? local.max[axis] : local.min[axis];
            float topEnd = flipped ? local.min[axis] : local.max[axis];
            groundEnd += flipped ? localRadius : -localRadius;
            float length = Mathf.Max(Mathf.Abs(topEnd - groundEnd), localRadius * 2f + 0.01f / scaleAlong);

            Vector3 center = local.center;
            center[axis] = groundEnd + Mathf.Sign(topEnd - groundEnd) * length * 0.5f;

            CapsuleCollider capsule = existing != null ? existing : tree.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = axis;
            capsule.radius = localRadius;
            capsule.height = length;
            capsule.center = center;
        }

        /// <summary>
        /// Her ağaç/kaya için köpek boyundaki ayak izini kaplayan Not Walkable <see cref="NavMeshModifierVolume"/> kurar.
        /// Fiziksel collider küçük kalsa da (oyuncu ormanda sıkışmasın) köpeğin yolu dal/gövde içinden geçmez.
        /// </summary>
        private static int EnsureNavBlockers()
        {
            GameObject forest = GameObject.Find(ForestObjectName);
            if (forest == null)
            {
                return 0;
            }

            GameObject oldRoot = GameObject.Find(BlockerRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            var root = new GameObject(BlockerRootName);
            int count = 0;
            foreach (Transform child in forest.transform)
            {
                if (!NameMatches(child.name, BlockerNameParts) || !TryMeasureFootprint(child, out Bounds footprint))
                {
                    continue;
                }

                var go = new GameObject($"Blocker_{count:D2}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(footprint.center.x, BlockerVolumeHeight * 0.25f, footprint.center.z);

                NavMeshModifierVolume volume = go.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = new Vector3(footprint.size.x + FootprintMargin * 2f, BlockerVolumeHeight, footprint.size.z + FootprintMargin * 2f);
                volume.area = 1; // Not Walkable
                count++;
            }

            return count;
        }

        /// <summary>
        /// Objenin yerden <see cref="FootprintMaxHeight"/> m'ye kadar olan mesh köşelerinin dünya eksenli yatay sınırlarını verir.
        /// </summary>
        private static bool TryMeasureFootprint(Transform root, out Bounds footprint)
        {
            footprint = default;
            bool has = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 world = toWorld.MultiplyPoint3x4(vertex);
                    if (world.y < FootprintMinHeight || world.y > FootprintMaxHeight)
                    {
                        continue;
                    }

                    var flat = new Vector3(world.x, 0f, world.z);
                    if (!has)
                    {
                        footprint = new Bounds(flat, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        footprint.Encapsulate(flat);
                    }
                }
            }

            return has;
        }

        private static bool NameMatches(string objectName, string[] parts)
        {
            foreach (string part in parts)
            {
                if (objectName.Contains(part))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Alt mesh'lerin kök yerel uzayındaki birleşik sınırını hesaplar.</summary>
        private static bool TryGetLocalBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null)
                {
                    continue;
                }

                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f,
                        (i & 2) == 0 ? -1f : 1f,
                        (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!has)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return has;
        }

        private static NavMeshSurface EnsureSurface(Scene scene, int agentTypeId)
        {
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == SurfaceObjectName)
                {
                    Object.DestroyImmediate(rootGo);
                }
            }

            GameObject go = new GameObject(SurfaceObjectName);
            NavMeshSurface surface = go.AddComponent<NavMeshSurface>();
            surface.agentTypeID = agentTypeId;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.defaultArea = 0; // Walkable
            return surface;
        }

        private static void BakeSurface(NavMeshSurface surface)
        {
            // CharacterController'lar Collider olduğu için bake'e girip delik açardı; geçici kapat.
            var disabled = new List<CharacterController>();
            foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (cc.enabled)
                {
                    cc.enabled = false;
                    disabled.Add(cc);
                }
            }

            // NavMeshModifier.ignoreFromBuild fiziksel collider kaynağında işlemediği için (ör. kapı yaprağı)
            // bu işaretli objelerin collider'larını da bake süresince geçici kapat.
            var ignoredColliders = new List<Collider>();
            foreach (NavMeshModifier modifier in Object.FindObjectsByType<NavMeshModifier>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!modifier.ignoreFromBuild)
                {
                    continue;
                }

                foreach (Collider col in modifier.GetComponents<Collider>())
                {
                    if (col.enabled)
                    {
                        col.enabled = false;
                        ignoredColliders.Add(col);
                    }
                }
            }

            try
            {
                Physics.SyncTransforms();
                surface.BuildNavMesh();
            }
            finally
            {
                foreach (CharacterController cc in disabled)
                {
                    if (cc != null)
                    {
                        cc.enabled = true;
                    }
                }

                foreach (Collider col in ignoredColliders)
                {
                    if (col != null)
                    {
                        col.enabled = true;
                    }
                }
            }

            if (surface.navMeshData == null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(NavMeshAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "NavMesh");
            }

            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(surface);
        }

        private static void EnsureDogAgent(int agentTypeId)
        {
            GameObject dog = GameObject.Find(DogObjectName);
            if (dog == null)
            {
                Debug.LogWarning("[Kurulum] 'Kopek' bulunamadı; NavMeshAgent eklenemedi.");
                return;
            }

            // Eski CharacterController tabanlı hareket NavMeshAgent ile çakışır (B10'da yenisi yazılacak).
            if (dog.TryGetComponent(out CompanionController legacy))
            {
                legacy.enabled = false;
            }

            if (dog.TryGetComponent(out CharacterController cc))
            {
                cc.enabled = false;
            }

            if (!dog.TryGetComponent(out NavMeshAgent agent))
            {
                agent = dog.AddComponent<NavMeshAgent>();
            }

            agent.agentTypeID = agentTypeId;
            agent.radius = DogRadius;
            agent.height = DogHeight;
            agent.speed = 3.5f;
            agent.stoppingDistance = 2.5f;
            agent.acceleration = 12f;
            agent.angularSpeed = 360f;
            EditorUtility.SetDirty(dog);
        }
    }
}
