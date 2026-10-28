using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yazlab.World;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Görsel çevre düzenlemesi (oyun mantığına dokunmaz):
    /// <list type="bullet">
    /// <item>GameScene: orman kenarına görünmez sınır (oyuncu boşluğa düşmesin), dışına sık ağaç/kaya kuşağı, ufka kadar
    /// uzanan arka zemin, sis + gökyüzü, kayalık duvarların yeniden dizilmesi, kapılara kütük palisad görseli.</item>
    /// <item>Bolum3_Koy: çit direklerinden kapılı kütük palisad (collider'lı), ağaç/çadır/kayalara collider, sınır +
    /// ağaç kuşağı, sis + gökyüzü, köpek NavMesh'inin yeniden bake'i.</item>
    /// </list>
    /// Üretilen her şey <c>Setup_Environment</c> kökü altındadır; menü tekrar çalıştırıldığında yeniden kurulur.
    /// Rastgelelik sabit tohumludur (her kurulumda aynı yerleşim).
    /// </summary>
    public static class EnvironmentSetup
    {
        private const string RootName = "Setup_Environment";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string VillageScenePath = "Assets/Scenes/Bolum3_Koy.unity";
        private const string NatureDir = "Assets/Nature/FBX (Unity)/";
        private const string SurviveDir = "Assets/Just_Survive/Just Survive/FBX/";
        private const string LogMaterialPath = "Assets/_Project/Materials/Mat_PalisadeLog.mat";
        private const string SkyMaterialPath = "Assets/_Project/Materials/Sky_Forest.mat";
        private const string BarkTexture = "Assets/Nature/Textures/Bark_NormalTree.png";

        private static readonly string[] BeltTrees =
        {
            NatureDir + "Pine_1.fbx", NatureDir + "Pine_2.fbx", NatureDir + "Pine_3.fbx", NatureDir + "Pine_4.fbx",
            NatureDir + "Pine_5.fbx", NatureDir + "CommonTree_1.fbx", NatureDir + "CommonTree_2.fbx",
            NatureDir + "CommonTree_3.fbx", SurviveDir + "PineTree.fbx", NatureDir + "Pine_1.fbx", NatureDir + "Pine_3.fbx",
        };

        private static readonly string[] EdgeRocks =
        {
            NatureDir + "Rock_Medium_1.fbx", NatureDir + "Rock_Medium_2.fbx", NatureDir + "Rock_Medium_3.fbx",
        };

        // (model, en küçük ölçek, en büyük ölçek) — çiçek öbekleri ölçek 1'de 2-3 m boyunda.
        private static readonly (string Path, float Min, float Max)[] GroundDetail =
        {
            (NatureDir + "Grass_Common_Short.fbx", 0.9f, 1.4f), (NatureDir + "Grass_Common_Short.fbx", 0.9f, 1.4f),
            (NatureDir + "Grass_Wispy_Short.fbx", 0.9f, 1.3f), (NatureDir + "Grass_Common_Tall.fbx", 0.6f, 0.9f),
            (NatureDir + "Clover_1.fbx", 1f, 1.5f), (NatureDir + "Clover_2.fbx", 1f, 1.5f),
            (NatureDir + "Flower_3_Group.fbx", 0.3f, 0.45f), (NatureDir + "Flower_4_Group.fbx", 0.3f, 0.45f),
            (NatureDir + "Pebble_Round_2.fbx", 0.8f, 1.2f),
        };

        private static readonly string[] EdgeBushes =
        {
            NatureDir + "Bush_Common.fbx", NatureDir + "Bush_Common_Flowers.fbx", NatureDir + "Fern_1.fbx",
        };

        [MenuItem("Yazlab/Gorsel/Cevre Duzeni (Iki Sahne)")]
        public static void RunAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string start = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(GameScenePath);
            ApplyForest();
            Save();

            EditorSceneManager.OpenScene(VillageScenePath);
            ApplyVillage();
            Save();

            if (!string.IsNullOrEmpty(start))
            {
                EditorSceneManager.OpenScene(start);
            }
        }

        private static void Save()
        {
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ================================================================ GameScene

        private static void ApplyForest()
        {
            Transform root = NewRoot();
            var rng = new System.Random(2611);

            // Oynanabilir alanlar (ağaç kuşağı bunların dışına dizilir).
            var keepOut = new List<Rect>
            {
                Rect.MinMaxRect(-41.5f, -41.5f, 41.5f, 41f),   // orman + Bölüm 2 arkası
                Rect.MinMaxRect(-23.5f, 39f, 23.5f, 83.5f),    // Bölüm 3
            };

            // 1) Görünmez sınır: orman zemini x,z ±40'ta bitiyor; güney/doğu/batı açıktı.
            Transform bounds = Child(root, "Bounds");
            Boundary(bounds, "Bound_South", new Vector3(0f, 0f, -40.75f), new Vector3(83f, 10f, 1.5f));
            Boundary(bounds, "Bound_West", new Vector3(-40.75f, 0f, -4.5f), new Vector3(1.5f, 10f, 73f));
            Boundary(bounds, "Bound_East", new Vector3(40.75f, 0f, -4.5f), new Vector3(1.5f, 10f, 73f));

            // 2) Arka zemin: ufka kadar çimen (boşluk görünmesin), collider'sız.
            GameObject groundGo = GameObject.Find("Ground");
            Material groundMat = groundGo != null && groundGo.TryGetComponent(out Renderer groundRenderer) ? groundRenderer.sharedMaterial : null;
            BackdropGround(root, groundMat, new Vector3(0f, -0.06f, 20f), 460f);

            // 3) Kenar kuşağı: sınır çizgisine kaya/çalı, dışına sık ağaç.
            Transform belt = Child(root, "TreeBelt");
            EdgeDecor(belt, new Vector3(-40f, 0f, -40f), new Vector3(40f, 0f, -40f), Vector3.back, rng);
            EdgeDecor(belt, new Vector3(-40f, 0f, -40f), new Vector3(-40f, 0f, 29f), Vector3.left, rng);
            EdgeDecor(belt, new Vector3(40f, 0f, -40f), new Vector3(40f, 0f, 29f), Vector3.right, rng);
            TreeBelt(belt, Rect.MinMaxRect(-85f, -80f, 85f, 115f), keepOut, 6.5f, rng);

            Transform detail = Child(root, "GroundDetail");
            ScatterDetail(detail, Rect.MinMaxRect(-38f, -38f, 38f, 27f), new List<Rect> { Rect.MinMaxRect(-6f, 22f, 6f, 29f) }, rng);
            ScatterDetail(detail, Rect.MinMaxRect(-19f, 42f, 19f, 79f), new List<Rect> { Rect.MinMaxRect(-5f, 50f, 5f, 66f) }, rng);
            DressTunnel();

            // 4) Kayalık duvarlar: parçalar bindirilerek yeniden dizilir.
            RebuildWalls("Setup_Bolum2/Walls");
            RebuildWalls("Setup_Bolum3/Walls");

            // 5) Kapılar: düz kutu yerine kütük palisad.
            foreach (Gate gate in Object.FindObjectsByType<Gate>(FindObjectsSortMode.None))
            {
                DressGate(gate, root, rng);
            }

            // 6) Atmosfer.
            Atmosphere(new Color(0.66f, 0.76f, 0.80f), 50f, 175f);
            MarkStatic(root.gameObject);
            Debug.Log("[Gorsel] GameScene çevre düzeni kuruldu (sınır, ağaç kuşağı, arka zemin, duvarlar, kapılar, sis).");
        }

        private static void RebuildWalls(string path)
        {
            GameObject walls = GameObject.Find(path);
            if (walls != null)
            {
                CliffWallBuilder.RebuildAll(walls.transform);
                MarkStatic(walls);
            }
        }

        /// <summary>Kapı kanadının düz kutu görselini gizler, yerine kanatla birlikte hareket eden kütük palisad koyar.</summary>
        private static void DressGate(Gate gate, Transform root, System.Random rng)
        {
            var so = new SerializedObject(gate);
            var leaf = so.FindProperty("_leaf").objectReferenceValue as Transform;
            if (leaf == null)
            {
                return;
            }

            // Kanat ölçekli bir küp: eğik kütükler onun altında çarpılır. Bu yüzden ölçeksiz bir pivot kurulur;
            // eski kanat (collider + NavMesh engeli) ve palisad onun altına girer, Gate pivotu hareket ettirir.
            Transform pivot = leaf;
            if (leaf.name != "LeafPivot")
            {
                pivot = new GameObject("LeafPivot").transform;
                pivot.SetParent(leaf.parent, false);
                pivot.SetPositionAndRotation(leaf.position, Quaternion.identity);
                leaf.SetParent(pivot, true);
                so.FindProperty("_leaf").objectReferenceValue = pivot;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            MeshRenderer leafRenderer = pivot.GetComponentsInChildren<MeshRenderer>(true)
                .FirstOrDefault(r => r.GetComponent<BoxCollider>() != null && r.transform != pivot);
            if (leafRenderer == null)
            {
                return;
            }

            Transform old = pivot.Find("Palisade");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            leafRenderer.enabled = true; // ölçüm için
            Bounds b = leafRenderer.bounds;
            leafRenderer.enabled = false;
            bool alongX = b.size.x >= b.size.z;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            float width = alongX ? b.size.x : b.size.z;
            Vector3 baseCenter = new Vector3(b.center.x, b.min.y, b.center.z);

            var palisade = new GameObject("Palisade").transform;
            palisade.SetParent(pivot, false);
            Material log = LogMaterial();
            LogRun(palisade, baseCenter - axis * (width * 0.5f), baseCenter + axis * (width * 0.5f), b.size.y, 0.5f, log, rng, false);
            // İki yatay kuşak (kapı kanadının iki yüzünde).
            Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;
            foreach (float h in new[] { 1.3f, b.size.y - 1.6f })
            {
                foreach (float s in new[] { -0.32f, 0.32f })
                {
                    GameObject beam = Cylinder(palisade, "Beam", baseCenter + Vector3.up * h + side * s, Quaternion.FromToRotation(Vector3.up, axis),
                        new Vector3(0.28f, width * 0.5f, 0.28f), log);
                    beam.GetComponent<Renderer>().sharedMaterial = log;
                }
            }

            // Kapı yanlarına sabit kalın direkler (kanatla hareket etmez).
            Transform posts = Child(root, $"GatePosts_{gate.name}");
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 p = baseCenter + axis * (s * (width * 0.5f + 0.45f));
                Cylinder(posts, "Post", p + Vector3.up * (b.size.y + 0.9f) * 0.5f, Quaternion.identity,
                    new Vector3(0.95f, (b.size.y + 0.9f) * 0.5f, 0.95f), log);
            }
        }

        // ================================================================ Bolum3_Koy

        private static void ApplyVillage()
        {
            Transform root = NewRoot();
            var rng = new System.Random(3127);
            Rect play = Rect.MinMaxRect(-38f, -80f, 38f, 16f);

            // 1) Ağaç, çadır, kaya: içinden geçilmesin.
            int colliders = 0;
            foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                string n = go.name;
                if (n.StartsWith("Orman_") || n.StartsWith("Ic_Kume_"))
                {
                    colliders += TrunkCollider(go) ? 1 : 0;
                }
                else if (n.StartsWith("Cadir_") || n.StartsWith("Kaya_Koy") || n.StartsWith("Sef_Kule"))
                {
                    colliders += BoundsCollider(go, n.StartsWith("Cadir_") ? 0.7f : 0.85f) ? 1 : 0;
                }
            }

            // 2) Çit direklerinden kütük palisad (kuzeydeki giriş açık kalır).
            int segments = BuildVillagePalisade(root, rng);

            // 3) Sınır + dış ağaç kuşağı.
            Transform bounds = Child(root, "Bounds");
            Boundary(bounds, "Bound_North", new Vector3(0f, 0f, play.yMax + 0.75f), new Vector3(play.width + 3f, 10f, 1.5f));
            Boundary(bounds, "Bound_South", new Vector3(0f, 0f, play.yMin - 0.75f), new Vector3(play.width + 3f, 10f, 1.5f));
            Boundary(bounds, "Bound_West", new Vector3(play.xMin - 0.75f, 0f, play.center.y), new Vector3(1.5f, 10f, play.height + 3f));
            Boundary(bounds, "Bound_East", new Vector3(play.xMax + 0.75f, 0f, play.center.y), new Vector3(1.5f, 10f, play.height + 3f));

            Transform belt = Child(root, "TreeBelt");
            EdgeDecor(belt, new Vector3(play.xMin, 0f, play.yMax), new Vector3(play.xMax, 0f, play.yMax), Vector3.forward, rng);
            EdgeDecor(belt, new Vector3(play.xMin, 0f, play.yMin), new Vector3(play.xMax, 0f, play.yMin), Vector3.back, rng);
            EdgeDecor(belt, new Vector3(play.xMin, 0f, play.yMin), new Vector3(play.xMin, 0f, play.yMax), Vector3.left, rng);
            EdgeDecor(belt, new Vector3(play.xMax, 0f, play.yMin), new Vector3(play.xMax, 0f, play.yMax), Vector3.right, rng);
            var keepOut = new List<Rect> { Rect.MinMaxRect(play.xMin - 2f, play.yMin - 2f, play.xMax + 2f, play.yMax + 2f) };
            TreeBelt(belt, Rect.MinMaxRect(-80f, -100f, 80f, 60f), keepOut, 6.5f, rng);

            // Zemin detayı: patikalar, meydan ve şef avlusu (toprak alanlar) hariç.
            var dirt = new List<Rect>();
            foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if ((go.name.StartsWith("Patika") || go.name == "Meydan" || go.name == "Sef_Avlu") && TryWorldBounds(go, out Bounds db))
                {
                    dirt.Add(Rect.MinMaxRect(db.min.x - 1f, db.min.z - 1f, db.max.x + 1f, db.max.z + 1f));
                }
            }

            ScatterDetail(Child(root, "GroundDetail"), Rect.MinMaxRect(play.xMin + 1f, play.yMin + 1f, play.xMax - 1f, play.yMax - 1f), dirt, rng);

            // 4) Atmosfer (köy: biraz daha sıcak akşamüstü).
            Atmosphere(new Color(0.74f, 0.74f, 0.70f), 45f, 160f);
            MarkStatic(root.gameObject);

            // 5) Köpeğin yolu yeni engellere göre.
            NavMeshSetup.Run();
            Debug.Log($"[Gorsel] Köy çevre düzeni kuruldu ({colliders} collider, {segments} palisad parçası, sınır, ağaç kuşağı, sis).");
        }

        private static int BuildVillagePalisade(Transform root, System.Random rng)
        {
            var posts = new SortedDictionary<int, Transform>();
            foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name.StartsWith("Cit_") && int.TryParse(go.name.Substring(4), out int index))
                {
                    posts[index] = go.transform;
                }
            }

            if (posts.Count < 3)
            {
                return 0;
            }

            Transform palisade = Child(root, "Palisade");
            Material log = LogMaterial();
            int[] keys = posts.Keys.ToArray();
            int segments = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                int a = keys[i];
                int b = keys[(i + 1) % keys.Length];
                bool consecutive = b == a + 1 || (i == keys.Length - 1 && b == keys[0]);
                if (!consecutive)
                {
                    continue; // eksik direkler = giriş
                }

                Vector3 pa = posts[a].position;
                Vector3 pb = posts[b].position;
                pa.y = pb.y = 0f;
                if (Vector3.Distance(pa, pb) > 8f)
                {
                    continue;
                }

                // Kütükler bir sonraki segmentle birleşsin diye uçlar hafif uzatılır.
                Vector3 dir = (pb - pa).normalized;
                LogRun(palisade, pa - dir * 0.2f, pb + dir * 0.2f, 3.8f, 0.42f, log, rng, true);
                segments++;
            }

            // Giriş direkleri + meşale ışığı.
            foreach (int gate in GapEnds(keys))
            {
                Vector3 p = posts[gate].position;
                p.y = 0f;
                Cylinder(palisade, "GatePost", p + Vector3.up * 2.6f, Quaternion.identity, new Vector3(0.75f, 2.6f, 0.75f), log);
                Torch(palisade, p + Vector3.up * 5.3f);
            }

            foreach (Transform post in posts.Values)
            {
                post.gameObject.SetActive(false); // eski cılız direkler palisadın içinde kalıyordu
            }

            return segments;
        }

        private static IEnumerable<int> GapEnds(int[] keys)
        {
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (keys[i + 1] != keys[i] + 1)
                {
                    yield return keys[i];
                    yield return keys[i + 1];
                }
            }
        }

        // ================================================================ yapı taşları

        private static Transform NewRoot()
        {
            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            foreach (Gate gate in Object.FindObjectsByType<Gate>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(gate);
                if (so.FindProperty("_leaf").objectReferenceValue is Transform leaf && leaf.Find("Palisade") is Transform p)
                {
                    Object.DestroyImmediate(p.gameObject);
                }
            }

            return new GameObject(RootName).transform;
        }

        private static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static void Boundary(Transform parent, string name, Vector3 baseCenter, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = baseCenter + Vector3.up * (size.y * 0.5f);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
        }

        private static void BackdropGround(Transform root, Material mat, Vector3 center, float size)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "BackdropGround";
            plane.transform.SetParent(root, false);
            plane.transform.position = center;
            plane.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            if (mat != null)
            {
                // Aynı doku yoğunluğu: orman zemini 80 m, bu düzlem size m.
                var tiled = new Material(mat) { name = "Mat_BackdropGround" };
                tiled.mainTextureScale = mat.mainTextureScale * (size / 80f);
                string path = "Assets/_Project/Materials/Mat_BackdropGround_" + EditorSceneManager.GetActiveScene().name + ".mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(tiled, path);
                plane.GetComponent<Renderer>().sharedMaterial = tiled;
            }
        }

        /// <summary>Sınır çizgisinin hemen dışına kaya ve çalı dizer (kenar "bitti" gibi görünmesin).</summary>
        private static void EdgeDecor(Transform parent, Vector3 a, Vector3 b, Vector3 outward, System.Random rng)
        {
            float length = Vector3.Distance(a, b);
            Vector3 dir = (b - a).normalized;
            for (float d = 0f; d <= length; d += 3.2f + (float)rng.NextDouble() * 2.5f)
            {
                Vector3 p = a + dir * d + outward * (1.6f + (float)rng.NextDouble() * 2.5f);
                bool rock = rng.NextDouble() < 0.55;
                string asset = rock ? Pick(EdgeRocks, rng) : Pick(EdgeBushes, rng);
                float scale = rock ? 1.1f + (float)rng.NextDouble() * 1.3f : 1.2f + (float)rng.NextDouble() * 1.4f;
                Optimize(Spawn(asset, parent, p, (float)rng.NextDouble() * 360f, scale), rock, 0.015f);
            }
        }

        /// <summary>Dış alana jitter'lı ızgarayla ağaç dizer; oynanabilir dikdörtgenlere girmez.</summary>
        private static void TreeBelt(Transform parent, Rect outer, List<Rect> keepOut, float spacing, System.Random rng)
        {
            for (float x = outer.xMin; x <= outer.xMax; x += spacing)
            {
                for (float z = outer.yMin; z <= outer.yMax; z += spacing)
                {
                    var p = new Vector2(x + Jitter(rng, spacing * 0.42f), z + Jitter(rng, spacing * 0.42f));
                    if (keepOut.Any(r => r.Contains(p)))
                    {
                        continue;
                    }

                    // Sınıra yakın sıralar daha sık, uzaklar seyrek (sis zaten örter).
                    float edge = keepOut.Min(r => DistanceToRect(r, p));
                    if (edge > 22f && rng.NextDouble() < 0.45)
                    {
                        continue;
                    }

                    float scale = 1.05f + (float)rng.NextDouble() * 0.75f + Mathf.Clamp01(edge / 40f) * 0.4f;
                    GameObject tree = Spawn(Pick(BeltTrees, rng), parent, new Vector3(p.x, 0f, p.y), (float)rng.NextDouble() * 360f, scale);
                    // Performans: yalnız sınıra yakın sıralar gölge düşürür; uzaktakiler ekranda küçülünce çizilmez.
                    Optimize(tree, edge < 12f, 0.025f);
                }
            }
        }

        /// <summary>Zemine seyrek çimen/yonca/çiçek öbekleri serper (collider'sız).</summary>
        private static void ScatterDetail(Transform parent, Rect area, List<Rect> avoid, System.Random rng)
        {
            const float step = 3.4f;
            for (float x = area.xMin; x <= area.xMax; x += step)
            {
                for (float z = area.yMin; z <= area.yMax; z += step)
                {
                    if (rng.NextDouble() < 0.45)
                    {
                        continue;
                    }

                    var p = new Vector2(x + Jitter(rng, step * 0.45f), z + Jitter(rng, step * 0.45f));
                    if (avoid.Any(r => r.Contains(p)))
                    {
                        continue;
                    }

                    var item = GroundDetail[rng.Next(GroundDetail.Length)];
                    GameObject detail = Spawn(item.Path, parent, new Vector3(p.x, 0f, p.y), (float)rng.NextDouble() * 360f,
                        Mathf.Lerp(item.Min, item.Max, (float)rng.NextDouble()));
                    Optimize(detail, false, 0.012f);
                }
            }
        }

        private static void DressTunnel()
        {
            GameObject roof = GameObject.Find("Setup_Bolum2/Tunnel/Tunnel_Roof");
            if (roof == null || !roof.TryGetComponent(out Renderer renderer))
            {
                return;
            }

            const string path = "Assets/_Project/Materials/Mat_TunnelRock.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { name = "Mat_TunnelRock" };
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nature/Textures/Rocks_Diffuse.png");
                mat.mainTextureScale = new Vector2(1.5f, 1.5f);
                mat.SetFloat("_Glossiness", 0.1f);
                AssetDatabase.CreateAsset(mat, path);
            }

            renderer.sharedMaterial = mat;
        }

        /// <summary>Gölge düşürmeyi ayarlar ve ekranda <paramref name="cullHeight"/> oranından küçülünce çizimi keser.</summary>
        private static void Optimize(GameObject go, bool castShadows, float cullHeight)
        {
            if (go == null)
            {
                return;
            }

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            LODGroup lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(cullHeight, renderers) });
            lod.RecalculateBounds();
        }

        private static float DistanceToRect(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>a→b boyunca dikey kütükler (sivri uçlu) dizer; <paramref name="withCollider"/> ise tek kutu collider ekler.</summary>
        private static void LogRun(Transform parent, Vector3 a, Vector3 b, float height, float diameter, Material mat, System.Random rng, bool withCollider)
        {
            float length = Vector3.Distance(a, b);
            Vector3 dir = (b - a).normalized;
            var run = new GameObject("LogRun").transform;
            run.SetParent(parent, false);
            run.position = (a + b) * 0.5f;
            run.rotation = Quaternion.LookRotation(dir, Vector3.up);

            int count = Mathf.Max(1, Mathf.RoundToInt(length / (diameter * 0.92f)));
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                Vector3 p = Vector3.Lerp(a, b, t);
                float h = height * (0.9f + (float)rng.NextDouble() * 0.18f);
                float d = diameter * (0.88f + (float)rng.NextDouble() * 0.24f);
                Quaternion tilt = Quaternion.Euler(Jitter(rng, 2f), (float)rng.NextDouble() * 360f, Jitter(rng, 2f));
                Cylinder(run, "Log", p + Vector3.up * (h * 0.5f - 0.1f), tilt, new Vector3(d, h * 0.5f, d), mat);
                // Sivri uç: 45° döndürülmüş küçük küp (kütük ucunda piramit gibi görünür).
                GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(tip.GetComponent<Collider>());
                tip.name = "Tip";
                tip.transform.SetParent(run, true);
                tip.transform.position = p + Vector3.up * (h - 0.1f);
                tip.transform.rotation = Quaternion.Euler(45f, (float)rng.NextDouble() * 90f, 45f);
                tip.transform.localScale = Vector3.one * d * 0.62f;
                tip.GetComponent<Renderer>().sharedMaterial = mat;
            }

            if (withCollider)
            {
                BoxCollider box = run.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, height * 0.5f, 0f);
                box.size = new Vector3(diameter * 1.4f, height + 0.6f, length);
            }
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void Torch(Transform parent, Vector3 position)
        {
            GameObject torch = Spawn(SurviveDir + "Torch.fbx", parent, position - Vector3.up * 0.6f, 0f, 1.6f);
            var lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = position + Vector3.up * 0.5f;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 11f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            if (torch == null)
            {
                Cylinder(parent, "TorchStick", position, Quaternion.identity, new Vector3(0.18f, 0.6f, 0.18f), LogMaterial());
            }
        }

        private static bool TrunkCollider(GameObject tree)
        {
            if (tree.GetComponentInChildren<Collider>() != null || !TryWorldBounds(tree, out Bounds b))
            {
                return false;
            }

            CapsuleCollider capsule = tree.AddComponent<CapsuleCollider>();
            float scale = Mathf.Max(0.01f, tree.transform.lossyScale.y);
            capsule.direction = 1;
            capsule.radius = 0.55f / scale;
            capsule.height = b.size.y / scale;
            Vector3 baseWorld = new Vector3(tree.transform.position.x, b.min.y, tree.transform.position.z);
            capsule.center = tree.transform.InverseTransformPoint(baseWorld + Vector3.up * (b.size.y * 0.5f));
            return true;
        }

        private static bool BoundsCollider(GameObject go, float shrink)
        {
            if (go.GetComponentInChildren<Collider>() != null || !TryWorldBounds(go, out Bounds b))
            {
                return false;
            }

            // Dünya eksenli kutu (çocuk olarak) — kök döndürülmüş olsa da ayak izi doğru kalsın.
            var child = new GameObject("Collider");
            child.transform.SetParent(go.transform, true);
            child.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            child.transform.localScale = new Vector3(1f / go.transform.lossyScale.x, 1f / go.transform.lossyScale.y, 1f / go.transform.lossyScale.z);
            BoxCollider box = child.AddComponent<BoxCollider>();
            box.size = new Vector3(b.size.x * shrink, b.size.y, b.size.z * shrink);
            return true;
        }

        private static bool TryWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!has)
                {
                    bounds = r.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return has;
        }

        private static GameObject Spawn(string assetPath, Transform parent, Vector3 position, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
            go.transform.localScale = go.transform.localScale * scale;
            foreach (Collider c in go.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(c);
            }

            return go;
        }

        private static void Atmosphere(Color fogColor, float fogStart, float fogEnd)
        {
            // Gölge mesafesi: sisin içinde kalan uzak gölgeler boşa çiziliyordu (tüm kalite seviyeleri).
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, 90f);
            }

            QualitySettings.SetQualityLevel(current, false);

            // Sisin ötesi çizilmesin (ağaç kuşağı uzakta kalabalık).
            Camera main = Camera.main;
            if (main != null)
            {
                main.farClipPlane = fogEnd + 40f;
                EditorUtility.SetDirty(main);
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Sky_Forest" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }

            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.72f));
            sky.SetColor("_GroundColor", fogColor);
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.54f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.3f, 0.24f);
            DynamicGI.UpdateEnvironment();
        }

        private static Material LogMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LogMaterialPath);
            if (mat != null)
            {
                return mat;
            }

            mat = new Material(Shader.Find("Standard")) { name = "Mat_PalisadeLog" };
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BarkTexture);
            mat.mainTextureScale = new Vector2(1f, 2f);
            mat.color = new Color(0.86f, 0.74f, 0.62f);
            mat.SetFloat("_Glossiness", 0.08f);
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, LogMaterialPath);
            return mat;
        }

        private static void MarkStatic(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
            }
        }

        private static string Pick(string[] items, System.Random rng) => items[rng.Next(items.Length)];

        private static float Jitter(System.Random rng, float amount) => ((float)rng.NextDouble() * 2f - 1f) * amount;
    }
}
