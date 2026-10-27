using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yazlab.Companion;
using Yazlab.Core;
using Yazlab.UI;
using static Yazlab.EditorTools.DialogueUiSetup;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Uçtan uca akış kurulumu (idempotent): GameScene'e sahne geçişi, görev satırı ve başlangıç kartı; Arda'nın köy sahnesini
    /// (<c>Bolum3_Koy</c>) güncel mimariye uyarlama (oyuncu, GameManager, NavMesh, köpek, ilerleme, Reis ölümü = zafer);
    /// Build Settings; debug loglarını kapatma.
    /// </summary>
    public static class IntegrationSetup
    {
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string VillageScenePath = "Assets/Scenes/Bolum3_Koy.unity";
        private const string VillageSceneName = "Bolum3_Koy";
        private const string ProgressPath = "Assets/_Project/ScriptableObjects/GameProgress.asset";
        private const string CompanionConfigPath = "Assets/_Project/ScriptableObjects/CompanionConfig.asset";
        private const string FlowRootName = "Setup_Flow";
        private const string AxeModelPath = "Assets/Just_Survive/Just Survive/FBX/Hatchet.fbx";
        private const string ReisConfigPath = "Assets/_Project/ScriptableObjects/ReisEnemyConfig.asset";
        private const string ReisName = "Dusman_Reis";
        private const string VillageWolfConfigPath = "Assets/_Project/ScriptableObjects/KoyKurtEnemyConfig.asset";
        private const string VillageWolfPrefix = "Dusman_Kurt";
        private const string HudName = "OyuncuUI";

        private static readonly string[] VillageObjectives = { "4/4: Köydeki kabile reisini yen" };

        private const string ControlsText =
            "WASD: Hareket   ·   Shift: Koşu\n" +
            "Sol Tık / F: Saldırı\n" +
            "E: Köpeğe emir ver / Muhafızla konuş\n" +
            "Enter: Mesaj gönder   ·   Esc: Diyaloğu kapat\n" +
            "F2: LLM (Ollama) ayarları\n\n" +
            "Kurt şefini yen, köpeğinle geçidi aç, muhafızı ikna et ve köyü kurtar.";

        /// <summary>Tüm adımlar: GameScene akışı, köy sahnesi, Build Settings, debug logları kapalı.</summary>
        [MenuItem("Yazlab/Kurulum/Entegrasyon/Tumu (GameScene + Koy + Build)")]
        public static void RunAll()
        {
            if (!EnsureEditMode())
            {
                return;
            }

            SetupGameScene();
            SetupVillageScene();
            SetupBuildSettings();
            Debug.Log("[Kurulum] Entegrasyon tamam: GameScene → Bolum3_Koy akışı, Build Settings, debug logları kapalı.");
        }

        /// <summary>GameScene: sahne geçişi, GameManager bayrakları, görev satırı, başlangıç kartı, debug kapalı.</summary>
        [MenuItem("Yazlab/Kurulum/Entegrasyon/1 GameScene Akisi")]
        public static void SetupGameScene()
        {
            if (!EnsureEditMode() || !OpenScene(GameScenePath))
            {
                return;
            }

            GameProgress progress = LoadOrCreateProgress();

            GameObject flow = GameObject.Find(FlowRootName);
            if (flow == null)
            {
                flow = new GameObject(FlowRootName);
            }

            SceneTransition transition = GetOrAdd<SceneTransition>(flow);
            SetFields(transition, ("_nextScene", VillageSceneName), ("_progress", progress), ("_resetProgressOnAwake", true));

            SetGameManagerFlags(finalGateEndsGame: false, bossDefeatEndsGame: false);

            GameObject hud = GameObject.Find(HudName);
            BuildObjectiveUi(hud, null);
            BuildStartCard(hud);

            SetDebugLogs(false);
            SaveActiveScene();
            Debug.Log("[Kurulum] GameScene akışı kuruldu (son kapı → Bolum3_Koy, görev satırı, başlangıç kartı).");
        }

        /// <summary>Köy sahnesini uyarlar ve kaydeder; sonunda GameScene'i yeniden açar.</summary>
        [MenuItem("Yazlab/Kurulum/Entegrasyon/2 Koy Sahnesi")]
        public static void SetupVillageScene()
        {
            if (!EnsureEditMode() || !OpenScene(VillageScenePath))
            {
                return;
            }

            // Mevcut kurulum araçları aktif sahnede çalışır (her biri sahneyi kaydeder).
            FinishSetup.Run();                 // oyuncu: PlayerInputReader, PlayerConfig, PlayerSpawnPoint, HP
            GameManagerSetup.Run();            // GameManager + TimeScaleService, bitiş ekranı, EventSystem
            NavMeshSetup.Run();                // Dog agent + yüzey bake (Bolum3_Koy_Dog_NavMesh.asset)
            CompanionSetup.Run();              // köpek: CompanionAgent, sensör, komutçu

            SetGameManagerFlags(finalGateEndsGame: false, bossDefeatEndsGame: true);

            // Köyde yol güneye (−z) gider: kamera oyuncunun kuzeyinde durup güneye baksın (hareket kamera yönüne göre).
            CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>();
            if (follow != null)
            {
                SetFields(follow, ("_yaw", 180f), ("_pitch", 41f));
            }
            GameManager villageManager = Object.FindFirstObjectByType<GameManager>();
            if (villageManager != null)
            {
                SetFields(villageManager, ("gameProgress", LoadOrCreateProgress()));
            }

            GameObject hud = GameObject.Find(HudName);
            GameObject flow = GameObject.Find(FlowRootName);
            if (flow == null)
            {
                flow = new GameObject(FlowRootName);
            }

            GameObject player = GameObject.FindWithTag("Player");
            ProgressRestorer restorer = GetOrAdd<ProgressRestorer>(flow);
            SetFields(restorer,
                ("_progress", LoadOrCreateProgress()),
                ("_playerCombat", player != null ? player.GetComponent<PlayerCombat>() : null),
                ("_playerHealth", player != null ? player.GetComponent<PlayerHealth>() : null),
                ("_axePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(AxeModelPath)));

            BuildObjectiveUi(hud, VillageObjectives);
            EnemyController reis = SetupBoss();
            BuildBossBar(hud, reis);
            SetDebugLogs(false);
            SaveActiveScene();
            Debug.Log("[Kurulum] Bolum3_Koy uyarlandı (oyuncu, GameManager, NavMesh, köpek savaş desteği, Reis = zafer).");

            OpenScene(GameScenePath);
        }

        /// <summary>Build Settings: GameScene (0) ve Bolum3_Koy (1). Bolum2_Gecit build dışı.</summary>
        [MenuItem("Yazlab/Kurulum/Entegrasyon/3 Build Settings")]
        public static void SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(VillageScenePath, true)
            };
            Debug.Log("[Kurulum] Build Settings: 0 GameScene, 1 Bolum3_Koy.");
        }

        /// <summary>Aktif sahnedeki ve companion config'teki debug loglarını kapatır.</summary>
        [MenuItem("Yazlab/Ayar/Debug Loglarini Kapat")]
        public static void DebugLogsOff()
        {
            SetDebugLogs(false);
            SaveActiveScene();
        }

        /// <summary>Aktif sahnedeki ve companion config'teki debug loglarını açar (geliştirme/test için).</summary>
        [MenuItem("Yazlab/Ayar/Debug Loglarini Ac")]
        public static void DebugLogsOn()
        {
            SetDebugLogs(true);
            SaveActiveScene();
        }

        /// <summary>
        /// Sahnedeki tüm bileşenlerde <c>_debugLog</c> / <c>debugLog</c> adlı bool alanları ve companion config'i ayarlar.
        /// </summary>
        public static int SetDebugLogs(bool enabled)
        {
            int changed = 0;
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour.gameObject.scene != SceneManager.GetActiveScene())
                {
                    continue;
                }

                changed += SetBoolFields(behaviour, enabled, "_debugLog", "debugLog");
            }

            var config = AssetDatabase.LoadAssetAtPath<CompanionConfig>(CompanionConfigPath);
            if (config != null)
            {
                changed += SetBoolFields(config, enabled, "debugLog");
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[Kurulum] Debug logları {(enabled ? "açıldı" : "kapatıldı")} ({changed} alan).");
            return changed;
        }

        private static int SetBoolFields(Object target, bool value, params string[] names)
        {
            var so = new SerializedObject(target);
            int changed = 0;
            foreach (string n in names)
            {
                SerializedProperty p = so.FindProperty(n);
                if (p != null && p.propertyType == SerializedPropertyType.Boolean && p.boolValue != value)
                {
                    p.boolValue = value;
                    changed++;
                }
            }

            if (changed > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }

            return changed;
        }

        private static void SetGameManagerFlags(bool finalGateEndsGame, bool bossDefeatEndsGame)
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                Debug.LogWarning("[Kurulum] GameManager yok; bayraklar ayarlanmadı.");
                return;
            }

            SetFields(manager,
                ("triggerVictoryWhenAllEnemiesCleared", false),
                ("finalGateEndsGame", finalGateEndsGame),
                ("bossDefeatEndsGame", bossDefeatEndsGame));
        }

        private static void BuildObjectiveUi(GameObject hud, string[] objectives)
        {
            if (hud == null)
            {
                Debug.LogWarning("[Kurulum] OyuncuUI yok; görev satırı kurulmadı.");
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DefaultControls.Resources res = LoadResources();
            GameObject root = RebuildChildRoot(hud.transform, "ObjectiveUI");
            var rt = (RectTransform)root.transform;
            SetRect(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(560f, 40f));
            // Kök korunduğu için Image ikinci çalıştırmada zaten vardır (AddComponent null döner).
            if (root.TryGetComponent(out Image background))
            {
                background.sprite = res.standard;
                background.type = Image.Type.Sliced;
                background.color = new Color(0.07f, 0.08f, 0.1f, 0.75f);
            }
            else
            {
                AddImage(root, res.standard, new Color(0.07f, 0.08f, 0.1f, 0.75f));
            }

            Text label = NewText("Label", root.transform, font, 16, FontStyle.Bold, new Color(0.96f, 0.86f, 0.62f), TextAnchor.MiddleLeft, "Görev");
            Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(12f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetLayerRecursive(root, hud.layer);

            ObjectiveTracker tracker = GetOrAdd<ObjectiveTracker>(root);
            var so = new SerializedObject(tracker);
            so.FindProperty("_label").objectReferenceValue = label;
            if (objectives != null)
            {
                SerializedProperty list = so.FindProperty("_objectives");
                list.arraySize = objectives.Length;
                for (int i = 0; i < objectives.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).stringValue = objectives[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Reis'e boss config'i bağlar: geri itilmez ve sersemlemez (vuruşlar saldırısını iptal etmez), daha sık ve geniş
        /// menzilli saldırır, can 28. Varsayılan değerlerle (config yok) oyuncu vurdukça menzil dışına itilip hiç saldıramıyordu.
        /// </summary>
        private static EnemyController SetupBoss()
        {
            GameObject reisGo = GameObject.Find(ReisName);
            if (reisGo == null || !reisGo.TryGetComponent(out EnemyController reis))
            {
                Debug.LogWarning("[Kurulum] Dusman_Reis bulunamadı; boss ayarlanmadı.");
                return null;
            }

            Yazlab.Enemies.EnemyConfig config = LoadOrCreateEnemyConfig(ReisConfigPath);
            var so = new SerializedObject(config);
            so.FindProperty("detectRange").floatValue = 24f;
            so.FindProperty("moveSpeed").floatValue = 3.6f;
            so.FindProperty("turnSpeed").floatValue = 10f;
            so.FindProperty("homeReturnDistance").floatValue = 60f;
            so.FindProperty("attackRange").floatValue = 3.2f;
            so.FindProperty("attackCooldown").floatValue = 1.2f;
            so.FindProperty("attackHitDelay").floatValue = 0.3f;
            so.FindProperty("damage").intValue = 8;
            so.FindProperty("maxHealth").intValue = 28;
            so.FindProperty("knockbackSpeed").floatValue = 0.8f;
            so.FindProperty("hitStunDuration").floatValue = 0f;
            so.FindProperty("isBoss").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            SetFields(reis, ("config", config));
            SetupVillageWolves();
            return reis;
        }

        /// <summary>
        /// Reis'in yanındaki köy kurtlarına ortak config bağlar (config'siz kurtlar varsayılan 10 hasarla vuruyordu;
        /// Reis'le birlikte saldırınca oyuncu çok hızlı ölüyordu).
        /// </summary>
        private static void SetupVillageWolves()
        {
            Yazlab.Enemies.EnemyConfig config = LoadOrCreateEnemyConfig(VillageWolfConfigPath);
            var so = new SerializedObject(config);
            so.FindProperty("detectRange").floatValue = 16f;
            so.FindProperty("moveSpeed").floatValue = 3.4f;
            so.FindProperty("attackRange").floatValue = 1.6f;
            so.FindProperty("attackCooldown").floatValue = 1.8f;
            so.FindProperty("attackHitDelay").floatValue = 0.25f;
            so.FindProperty("damage").intValue = 5;
            so.FindProperty("maxHealth").intValue = 3;
            so.FindProperty("isBoss").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            int count = 0;
            foreach (EnemyController enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.name.StartsWith(VillageWolfPrefix))
                {
                    SetFields(enemy, ("config", config));
                    count++;
                }
            }

            Debug.Log($"[Kurulum] Köy kurtları: {count} kurda config bağlandı (hasar 5).");
        }

        private static Yazlab.Enemies.EnemyConfig LoadOrCreateEnemyConfig(string path)
        {
            var config = AssetDatabase.LoadAssetAtPath<Yazlab.Enemies.EnemyConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<Yazlab.Enemies.EnemyConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            return config;
        }

        private static void BuildBossBar(GameObject hud, EnemyController boss)
        {
            if (hud == null || boss == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DefaultControls.Resources res = LoadResources();
            GameObject root = RebuildChildRoot(hud.transform, "BossBarUI");
            Stretch((RectTransform)root.transform, 0f);

            RectTransform bar = NewUi("Bar", root.transform);
            SetRect(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(540f, 34f));
            AddImage(bar.gameObject, res.standard, new Color(0.08f, 0.04f, 0.06f, 0.9f));

            RectTransform fillRt = NewUi("Fill", bar);
            Stretch(fillRt, 4f);
            Image fill = AddImage(fillRt.gameObject, res.standard, new Color(0.72f, 0.12f, 0.16f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;

            Text label = NewText("Label", bar, font, 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Kabile Reisi");
            Stretch(label.rectTransform, 0f);
            SetLayerRecursive(root, hud.layer);

            BossHealthBar health = GetOrAdd<BossHealthBar>(root);
            SetFields(health, ("_boss", boss), ("_bar", bar.gameObject), ("_fill", fill), ("_label", label));
            bar.gameObject.SetActive(false);
        }

        private static void BuildStartCard(GameObject hud)
        {
            if (hud == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DefaultControls.Resources res = LoadResources();
            GameObject root = RebuildChildRoot(hud.transform, "StartCardUI");
            Stretch((RectTransform)root.transform, 0f);
            if (!root.TryGetComponent(out Canvas canvas))
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 60;
            if (!root.TryGetComponent(out GraphicRaycaster _))
            {
                root.AddComponent<GraphicRaycaster>();
            }

            RectTransform overlay = NewUi("Overlay", root.transform);
            Stretch(overlay, 0f);
            AddImage(overlay.gameObject, null, new Color(0f, 0f, 0f, 0.62f));

            RectTransform panel = NewUi("Panel", overlay);
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 380f));
            AddImage(panel.gameObject, res.standard, new Color(0.08f, 0.09f, 0.11f, 0.96f));

            Text title = NewText("Title", panel, font, 26, FontStyle.Bold, new Color(0.96f, 0.84f, 0.6f), TextAnchor.UpperCenter, "Kontroller");
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(-40f, 36f));

            Text body = NewText("Body", panel, font, 18, FontStyle.Normal, new Color(0.9f, 0.9f, 0.88f), TextAnchor.UpperCenter, ControlsText);
            SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(-48f, 220f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;

            Button start = NewButton("StartButton", panel, res, font, "Başla  (Enter)", new Color(0.24f, 0.45f, 0.26f, 1f));
            SetRect((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(200f, 44f));

            SetLayerRecursive(root, hud.layer);
            StartScreen screen = GetOrAdd<StartScreen>(root);
            SetFields(screen, ("_panel", overlay.gameObject), ("_startButton", start));
            overlay.gameObject.SetActive(false);
        }

        /// <summary>Alt objeyi bulur (yoksa yaratır), çocuklarını siler; bileşenleri korunur.</summary>
        private static GameObject RebuildChildRoot(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            return root;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
        }

        private static void SetFields(Object target, params (string Name, object Value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string name, object value) in fields)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null)
                {
                    Debug.LogWarning($"[Kurulum] {target.GetType().Name}.{name} bulunamadı.");
                    continue;
                }

                switch (value)
                {
                    case float f:
                        p.floatValue = f;
                        break;
                    case bool b:
                        p.boolValue = b;
                        break;
                    case string s:
                        p.stringValue = s;
                        break;
                    case Object o:
                        p.objectReferenceValue = o;
                        break;
                    case null:
                        p.objectReferenceValue = null;
                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static GameProgress LoadOrCreateProgress()
        {
            var progress = AssetDatabase.LoadAssetAtPath<GameProgress>(ProgressPath);
            if (progress != null)
            {
                return progress;
            }

            progress = ScriptableObject.CreateInstance<GameProgress>();
            AssetDatabase.CreateAsset(progress, ProgressPath);
            AssetDatabase.SaveAssets();
            return progress;
        }

        private static bool OpenScene(string path)
        {
            if (SceneManager.GetActiveScene().path == path && SceneManager.sceneCount == 1)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return true;
        }

        private static void SaveActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static bool EnsureEditMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return true;
            }

            Debug.LogError("[Kurulum] Play modunda sahneye kalıcı yazılamaz; Play'den çık ve tekrar çalıştır.");
            return false;
        }
    }
}
