using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Yazlab.Actions;
using Yazlab.Core;
using Yazlab.LLM;
using Yazlab.UI;
using Yazlab.World;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Bölüm 3'ü kurar (idempotent): Bölüm 2 geçidinin kuzeyine zemin uzantısı, kapatma duvarları, son kapı
    /// (<c>FinalGate</c>), kapı bekçisi NPC (<c>Npc_KapiBekcisi</c> — <c>Dusman_</c> öneki YOK) ve kapının arkasında
    /// <see cref="FinalGateZone"/>. <c>Setup_Bolum3</c> kökünü silip yeniden kurar, GameManager'ın erken zaferini kapatır,
    /// NavMesh'i yeniden bake edip sahneyi kaydeder.
    /// </summary>
    public static class NpcAreaSetup
    {
        private const string RootName = "Setup_Bolum3";
        private const string NpcName = "Npc_KapiBekcisi";
        private const string CharFbx = "Assets/Just_Survive/Karakter/SurviverMale.fbx";
        private const string NpcControllerPath = "Assets/DusmanAnimator.controller";
        private const string NpcMaterialPath = ProjectMaterials.Folder + "/Mat_KapiBekcisi.mat";
        private const string LlmConfigPath = "Assets/_Project/ScriptableObjects/LlmConfig.asset";
        private const string PersonaPath = "Assets/_Project/ScriptableObjects/NpcPersona_Kael.asset";

        // Yerleşim (dünya koordinatları). Ana zemin x,z: -40..40; Bölüm 2 duvarı z=30.
        private const float AreaHalfWidth = 20f;
        private const float AreaStartZ = 40f;
        private const float AreaEndZ = 80f;
        private const float FinalWallZ = 60f;
        private const float GateHalfWidth = 2.5f;
        private const float WallHeight = 6f;
        private const float WallThickness = 2f;
        private const float OuterEdge = 44f;
        private static readonly Vector3 NpcPosition = new Vector3(0f, 0.05f, 56.5f);
        private static readonly Color NpcColor = new Color(0.12f, 0.36f, 0.42f, 1f);



        // Kapı bekçisinin savaş değerleri ('attack' aksiyonunda açılır).
        private const float NpcHeight = 4.18f;
        private const float NpcDetectRange = 16f;
        private const float NpcMoveSpeed = 3.2f;
        private const float NpcAttackRange = 2.6f;
        private const float NpcAttackCooldown = 1.5f;
        private const int NpcDamage = 12;
        private const int NpcHealth = 14;

        /// <summary>Bölüm 3'ü kurar, NavMesh'i bake eder ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Bolum 3 NPC ve Son Kapi")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Kurulum] Play modunda sahneye kalıcı yazılamaz; Play'den çık ve tekrar çalıştır.");
                return;
            }

            DialogueView view = Object.FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            var config = AssetDatabase.LoadAssetAtPath<LlmConfig>(LlmConfigPath);
            var persona = AssetDatabase.LoadAssetAtPath<NpcPersona>(PersonaPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(NpcControllerPath);
            if (view == null || config == null || persona == null || model == null || controller == null)
            {
                Debug.LogError("[Kurulum] Eksik önkoşul: DialogueView ('Yazlab/Kurulum/Diyalog Paneli UI'), LlmConfig ('Yazlab/Kurulum/LLM Config'), " +
                               "NpcPersona ('Yazlab/Kurulum/NPC Persona (Kael)'), SurviverMale.fbx veya DusmanAnimator.controller.");
                return;
            }

            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    Object.DestroyImmediate(go);
                }
            }

            var root = new GameObject(RootName).transform;
            GameObject cliff = AssetDatabase.LoadAssetAtPath<GameObject>(CliffWallBuilder.CliffPath);
            // Model materyalleri palet dokusudur; düz küpe uygulanınca renkli karo görünür → kapıya düz renk materyal.
            Material gateMat = ProjectMaterials.Wood;

            BuildGround(root);
            BuildWalls(root, cliff);
            Gate gate = BuildFinalGate(root, gateMat);
            BuildTorches(root);
            BuildZone(root);
            GameObject npc = BuildNpc(root, model, controller, gate, view, config, persona);

            DisableEarlyVictory();

            EditorUtility.SetDirty(root.gameObject);
            Physics.SyncTransforms();
            // NavMesh'i (zemin uzantısı + duvarlar dahil) yeniden bake eder ve sahneyi kaydeder.
            NavMeshSetup.Run();
            Debug.Log($"[Kurulum] Bölüm 3 kuruldu: FinalGate (z={FinalWallZ}), {npc.name}, FinalGateZone. NavMesh bake edildi, sahne kaydedildi.");
        }

        private static void BuildGround(Transform root)
        {
            GameObject mainGround = GameObject.Find("Ground");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Bolum3";
            ground.transform.SetParent(root, false);
            // Plane 10×10 birimdir: x -20..20, z 40..80.
            ground.transform.position = new Vector3(0f, 0f, (AreaStartZ + AreaEndZ) * 0.5f);
            ground.transform.localScale = new Vector3(AreaHalfWidth * 0.2f, 1f, (AreaEndZ - AreaStartZ) * 0.1f);
            if (mainGround != null && mainGround.TryGetComponent(out Renderer source))
            {
                ground.GetComponent<Renderer>().sharedMaterial = source.sharedMaterial;
                ground.layer = mainGround.layer;
            }
        }

        private static void BuildWalls(Transform root, GameObject cliff)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);

            // Son kapı duvarı (ortada kapı aralığı).
            Wall(walls, "FinalWall_Left", new Vector3(-(AreaHalfWidth + GateHalfWidth) * 0.5f, 0f, FinalWallZ), new Vector3(AreaHalfWidth - GateHalfWidth, WallHeight, WallThickness), cliff, true);
            Wall(walls, "FinalWall_Right", new Vector3((AreaHalfWidth + GateHalfWidth) * 0.5f, 0f, FinalWallZ), new Vector3(AreaHalfWidth - GateHalfWidth, WallHeight, WallThickness), cliff, true);

            // Bölüm 3 kenarları: yanlar ve arka.
            float sideLength = AreaEndZ - AreaStartZ;
            Wall(walls, "Side_West", new Vector3(-AreaHalfWidth - WallThickness * 0.5f, 0f, AreaStartZ + sideLength * 0.5f), new Vector3(WallThickness, WallHeight, sideLength), cliff, false);
            Wall(walls, "Side_East", new Vector3(AreaHalfWidth + WallThickness * 0.5f, 0f, AreaStartZ + sideLength * 0.5f), new Vector3(WallThickness, WallHeight, sideLength), cliff, false);
            Wall(walls, "Back", new Vector3(0f, 0f, AreaEndZ + WallThickness * 0.5f), new Vector3(AreaHalfWidth * 2f + WallThickness * 2f, WallHeight, WallThickness), cliff, true);

            // Ana zeminin kuzey kenarı (Bölüm 2 duvarının arkasında kalan şerit) — zeminden düşmeyi önler.
            float edgeLength = OuterEdge - AreaHalfWidth;
            Wall(walls, "Edge_NorthWest", new Vector3(-(OuterEdge + AreaHalfWidth) * 0.5f, 0f, AreaStartZ + WallThickness * 0.5f), new Vector3(edgeLength, WallHeight, WallThickness), cliff, true);
            Wall(walls, "Edge_NorthEast", new Vector3((OuterEdge + AreaHalfWidth) * 0.5f, 0f, AreaStartZ + WallThickness * 0.5f), new Vector3(edgeLength, WallHeight, WallThickness), cliff, true);
            Wall(walls, "Edge_West", new Vector3(-40f - WallThickness * 0.5f, 0f, 35.75f), new Vector3(WallThickness, WallHeight, 8.5f), cliff, false);
            Wall(walls, "Edge_East", new Vector3(40f + WallThickness * 0.5f, 0f, 35.75f), new Vector3(WallThickness, WallHeight, 8.5f), cliff, false);
        }

        /// <summary>
        /// Görünmez fiziksel duvar kutusu + içi Not Walkable hacim (collider kabuğu içi boş sayıldığı için) +
        /// uzun eksen boyunca dizilmiş DirtCliff001 kayalık parçaları (görsel, collider'sız).
        /// </summary>
        private static void Wall(Transform parent, string name, Vector3 baseCenter, Vector3 size, GameObject cliff, bool alongX)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = baseCenter + Vector3.up * (size.y * 0.5f);
            wall.transform.localScale = size;
            wall.GetComponent<Renderer>().enabled = false;

            NavMeshModifierVolume volume = wall.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = Vector3.one;
            volume.area = 1; // Not Walkable

            if (cliff == null)
            {
                wall.GetComponent<Renderer>().enabled = true;
                return;
            }

            CliffWallBuilder.Build(parent, name, baseCenter, alongX ? size.x : size.z, alongX ? size.z : size.x, size.y, alongX);
        }

        private static Gate BuildFinalGate(Transform root, Material mat)
        {
            var gateGo = new GameObject("FinalGate");
            gateGo.transform.SetParent(root, false);
            gateGo.transform.position = new Vector3(0f, 0f, FinalWallZ);

            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "FinalGateLeaf";
            leaf.transform.SetParent(gateGo.transform, false);
            leaf.transform.position = new Vector3(0f, WallHeight * 0.5f, FinalWallZ);
            leaf.transform.localScale = new Vector3(GateHalfWidth * 2f, WallHeight, 0.8f);
            if (mat != null)
            {
                leaf.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // Kapı bake'e girmez; kapalıyken NavMeshObstacle yolu keser, açılınca kapanır.
            leaf.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            NavMeshObstacle obstacle = leaf.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size = Vector3.one;
            obstacle.carving = true;

            Gate gate = gateGo.AddComponent<Gate>();
            var so = new SerializedObject(gate);
            so.FindProperty("_leaf").objectReferenceValue = leaf.transform;
            so.FindProperty("_openOffset").vector3Value = new Vector3(0f, WallHeight + 0.5f, 0f);
            so.FindProperty("_openDuration").floatValue = 2f;
            SerializedProperty colliders = so.FindProperty("_blockingColliders");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = leaf.GetComponent<BoxCollider>();
            so.FindProperty("_obstacle").objectReferenceValue = obstacle;
            so.FindProperty("_openedMessage").stringValue = "Son kapı açıldı!";
            // Bu kapı köpeğin geçidi değil: GameManager.CompanionGateOpened'ı tetiklemesin.
            so.FindProperty("_raiseGateOpenedEvent").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        private static void BuildTorches(Transform root)
        {
            var torches = new GameObject("Torches").transform;
            torches.SetParent(root, false);
            foreach (float x in new[] { -GateHalfWidth - 0.8f, GateHalfWidth + 0.8f })
            {
                var torch = new GameObject(x < 0f ? "Torch_West" : "Torch_East");
                torch.transform.SetParent(torches, false);
                torch.transform.position = new Vector3(x, 3.2f, FinalWallZ - 1.4f);
                Light light = torch.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.62f, 0.3f);
                light.range = 9f;
                light.intensity = 2.2f;
            }
        }

        private static void BuildZone(Transform root)
        {
            var zone = new GameObject("FinalGateZone");
            zone.transform.SetParent(root, false);
            zone.transform.position = new Vector3(0f, 1.5f, FinalWallZ + 4f);
            BoxCollider box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(AreaHalfWidth * 2f - 1f, 3f, 4f);
            zone.AddComponent<FinalGateZone>();
            // Tetik hacmi NavMesh'e girmesin.
            zone.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        private static GameObject BuildNpc(Transform root, GameObject model, RuntimeAnimatorController controller, Gate gate,
            DialogueView view, LlmConfig config, NpcPersona persona)
        {
            GameObject player = GameObject.FindWithTag("Player");
            Material mat = LoadOrCreateNpcMaterial();

            GameObject npc = DusmanKur.InsanDusmanEkle(model, controller, mat, NpcName, NpcPosition, 180f, NpcHeight,
                NpcDetectRange, NpcMoveSpeed, NpcAttackRange, NpcAttackCooldown, NpcDamage, NpcHealth,
                null, "Muhafız Kael yere yığıldı. Son kapı açılıyor...", player);
            npc.transform.SetParent(root, true);

            // Savaş bileşeni başlangıçta kapalı: GameManager onu kurt saymaz, yalnız 'attack' aksiyonunda açılır.
            EnemyController enemy = npc.GetComponent<EnemyController>();
            enemy.enabled = false;

            OpenDoorAction open = npc.AddComponent<OpenDoorAction>();
            open.Door = gate;

            StartCombatAction combat = npc.AddComponent<StartCombatAction>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("_enemy").objectReferenceValue = enemy;
            combatSo.FindProperty("_doorOnDefeat").objectReferenceValue = gate;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            npc.AddComponent<EndConversationAction>();

            NpcDialogueController dialogue = npc.AddComponent<NpcDialogueController>();
            var so = new SerializedObject(dialogue);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_persona").objectReferenceValue = persona;
            so.FindProperty("_view").objectReferenceValue = view;
            so.FindProperty("_player").objectReferenceValue = player != null ? player.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            // NPC'nin kapsülü bake'te yerinde delik açmasın; köpek etrafından dolaşır (agent kaçınması yeterli).
            npc.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return npc;
        }

        private static Material LoadOrCreateNpcMaterial() => ProjectMaterials.LoadOrCreate(NpcMaterialPath, NpcColor);

        private static void DisableEarlyVictory()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                Debug.LogWarning("[Kurulum] GameManager yok; 'Yazlab/Kurulum/GameManager ve Bitis Ekrani' çalıştırılmalı.");
                return;
            }

            // Zafer artık son kapıdan sonra; kurtlar + şef ölünce erken zafer ekranı açılmasın.
            var so = new SerializedObject(manager);
            so.FindProperty("triggerVictoryWhenAllEnemiesCleared").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
    }
}
