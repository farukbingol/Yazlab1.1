using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Yazlab.Companion;
using Yazlab.World;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Bölüm 2 fiziksel engel alanını kurar: oyuncunun geçemediği duvar hattı, ortada kilitli <see cref="Gate"/>,
    /// yanda yalnızca köpeğin sığdığı alçak tünel, tünelin arkasında <see cref="Lever"/> ve kapı önünde CommandZone.
    /// </summary>
    public static class ObstacleAreaSetup
    {
        private const string RootName = "Setup_Bolum2";
        private const string CliffPath = "Assets/Just_Survive/Just Survive/FBX/DirtCliff001.fbx";
        private const string FencePath = "Assets/Just_Survive/Just Survive/FBX/Fence001.fbx";
        private const string RockPath = "Assets/Nature/FBX/Rock_Medium_1.fbx";

        // Yerleşim (dünya koordinatları). Zemin x,z: -40..40.
        private const float WallZ = 30f;
        private const float WallThickness = 3f;
        private const float WallHeight = 6f;
        private const float WallHalfLength = 44f;
        private const float GateHalfWidth = 3f;
        private const float TunnelCenterX = 12f;
        private const float TunnelHalfWidth = 0.8f;
        private const float TunnelHeight = 1.3f;
        private const float CliffLength = 9.78f;
        private const float CliffThickness = 4.38f;
        private const float CliffHeight = 2.28f;

        /// <summary>Bölüm 2 engel alanını idempotent olarak kurar ve NavMesh'i yeniden bake eder.</summary>
        [MenuItem("Yazlab/Kurulum/Bolum 2 Engel Alani")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    Object.DestroyImmediate(go);
                }
            }

            GameObject root = new GameObject(RootName);
            Material cliffMat = SharedMaterialOf(CliffPath);
            Material woodMat = SharedMaterialOf(FencePath);

            BuildWalls(root.transform);
            BuildTunnel(root.transform, cliffMat);
            Gate gate = BuildGate(root.transform, woodMat);
            BuildBendRocks(root.transform);
            BuildLever(root.transform, gate, cliffMat, woodMat);
            BuildCommandZone(root.transform);

            EditorUtility.SetDirty(root);
            // Edit modunda yeni objelerin collider konumları fizik motoruna işlenmeden bake güvenilmez.
            Physics.SyncTransforms();
            // NavMesh'i yeniden bake eder (Dog agent, collider'lar, yüzey) ve sahneyi kaydeder.
            NavMeshSetup.Run();
            Debug.Log("[Kurulum] Bölüm 2 engel alanı kuruldu ve NavMesh yeniden bake edildi.");
        }

        private static Material SharedMaterialOf(string path)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponentInChildren<MeshRenderer>() != null)
            {
                return asset.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            }

            return null;
        }

        private static GameObject MakeBox(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool visible)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (!visible)
            {
                renderer.enabled = false;
            }
            else if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }

            return go;
        }

        private static void BuildWalls(Transform root)
        {
            Transform walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);

            float tunnelL = TunnelCenterX - TunnelHalfWidth;
            float tunnelR = TunnelCenterX + TunnelHalfWidth;
            BuildWallSegment(walls, "Wall_Left", -WallHalfLength, -GateHalfWidth);
            BuildWallSegment(walls, "Wall_Mid", GateHalfWidth, tunnelL);
            BuildWallSegment(walls, "Wall_Right", tunnelR, WallHalfLength);
        }

        private static void BuildWallSegment(Transform parent, string name, float x0, float x1)
        {
            float length = x1 - x0;
            float centerX = (x0 + x1) * 0.5f;

            // Görünmez fiziksel gövde (oyuncuyu kesin durdurur).
            GameObject body = MakeBox(parent, name + "_Collider", new Vector3(centerX, WallHeight * 0.5f, WallZ),
                new Vector3(length, WallHeight, WallThickness), null, false);

            // Collider kabuğu içi boş sayıldığından duvarın içindeki zemin walkable kalmasın diye Not Walkable hacmi.
            NavMeshModifierVolume volume = body.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = Vector3.one;
            volume.area = 1; // Not Walkable

            // Görsel: DirtCliff001 parçaları.
            GameObject cliff = AssetDatabase.LoadAssetAtPath<GameObject>(CliffPath);
            if (cliff == null)
            {
                return;
            }

            int count = Mathf.Max(1, Mathf.CeilToInt(length / CliffLength));
            float pieceLength = length / count;
            for (int i = 0; i < count; i++)
            {
                GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(cliff, parent);
                piece.name = $"{name}_Cliff{i}";
                piece.transform.position = new Vector3(x0 + pieceLength * (i + 0.5f), 0f, WallZ);
                piece.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                piece.transform.localScale = new Vector3(
                    WallThickness / CliffThickness,
                    WallHeight / CliffHeight,
                    pieceLength / CliffLength);
                foreach (Collider c in piece.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(c);
                }
            }
        }

        private static void BuildTunnel(Transform root, Material mat)
        {
            Transform tunnel = new GameObject("Tunnel").transform;
            tunnel.SetParent(root, false);

            float roofThickness = 1f;
            // Çatı: köpeğin altından geçeceği 1.3 m'lik açıklığın üstünde.
            MakeBox(tunnel, "Tunnel_Roof",
                new Vector3(TunnelCenterX, TunnelHeight + roofThickness * 0.5f, WallZ),
                new Vector3(TunnelHalfWidth * 2f + 0.4f, roofThickness, WallThickness), mat, true);
        }

        private static Gate BuildGate(Transform root, Material mat)
        {
            GameObject gateGo = new GameObject("Gate_Bolum2");
            gateGo.transform.SetParent(root, false);
            gateGo.transform.position = new Vector3(0f, 0f, WallZ);

            float width = GateHalfWidth * 2f;
            GameObject leaf = MakeBox(gateGo.transform, "GateLeaf",
                new Vector3(0f, WallHeight * 0.5f, WallZ), new Vector3(width, WallHeight, 0.8f), mat, true);

            // Kapı bake'e girmesin; kapalıyken NavMeshObstacle yolu keser, açılınca kapanır.
            NavMeshModifier modifier = leaf.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;

            NavMeshObstacle obstacle = leaf.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one;
            obstacle.carving = true;

            Gate gate = gateGo.AddComponent<Gate>();
            var so = new SerializedObject(gate);
            so.FindProperty("_leaf").objectReferenceValue = leaf.transform;
            so.FindProperty("_openOffset").vector3Value = new Vector3(0f, WallHeight + 0.5f, 0f);
            SerializedProperty colliders = so.FindProperty("_blockingColliders");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = leaf.GetComponent<BoxCollider>();
            so.FindProperty("_obstacle").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        private static void BuildBendRocks(Transform root)
        {
            GameObject rock = AssetDatabase.LoadAssetAtPath<GameObject>(RockPath);
            if (rock == null)
            {
                return;
            }

            Transform props = new GameObject("Props").transform;
            props.SetParent(root, false);
            AddRock(props, rock, "BendRock_1", new Vector3(TunnelCenterX, 0f, WallZ + 4f));
            AddRock(props, rock, "BendRock_2", new Vector3(TunnelCenterX - 4.5f, 0f, WallZ + 6.5f));
        }

        private static void AddRock(Transform parent, GameObject prefab, string name, Vector3 position)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = position;
            MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                Bounds b = mf.sharedMesh.bounds;
                box.center = go.transform.InverseTransformPoint(mf.transform.TransformPoint(b.center));
                box.size = b.size;
            }
        }

        private static void BuildLever(Transform root, Gate gate, Material baseMat, Material handleMat)
        {
            GameObject leverGo = new GameObject("Lever_01");
            leverGo.transform.SetParent(root, false);
            leverGo.transform.position = new Vector3(TunnelCenterX - 9f, 0f, WallZ + 8f);

            GameObject basePart = MakeBox(leverGo.transform, "Base",
                leverGo.transform.position + new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.5f, 0.7f), baseMat, true);
            Object.DestroyImmediate(basePart.GetComponent<BoxCollider>());

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(leverGo.transform, false);
            handle.transform.position = leverGo.transform.position + new Vector3(0f, 0.5f, 0f);
            GameObject stick = MakeBox(handle.transform, "Stick",
                handle.transform.position + new Vector3(0f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.12f), handleMat, true);
            Object.DestroyImmediate(stick.GetComponent<BoxCollider>());

            GameObject point = new GameObject("InteractionPoint");
            point.transform.SetParent(leverGo.transform, false);
            point.transform.position = leverGo.transform.position + new Vector3(1.2f, 0f, 0f);

            // Köpek şalterin yakınına gelebilsin diye ince bir trigger (NavMesh'e girmez).
            SphereCollider trigger = leverGo.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.5f;

            Lever lever = leverGo.AddComponent<Lever>();
            var so = new SerializedObject(lever);
            so.FindProperty("_target").objectReferenceValue = gate;
            so.FindProperty("_interactionPoint").objectReferenceValue = point.transform;
            so.FindProperty("_handle").objectReferenceValue = handle.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCommandZone(Transform root)
        {
            GameObject zone = new GameObject("CommandZone");
            zone.transform.SetParent(root, false);
            zone.transform.position = new Vector3(0f, 2f, WallZ - 4.5f);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 4f, 6f);
            zone.AddComponent<CommandZone>();
        }
    }
}
