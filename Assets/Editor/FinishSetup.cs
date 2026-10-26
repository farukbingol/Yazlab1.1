using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Kurulum v2: iki FBX'i de Humanoid'e cevirir (retarget ile kemik yolu uyumsuzlugu imkansizlasir),
// avatar/motion referanslarini tazeler, olcege uyumlu kapsul/hiz/tempo ayarlar, scriptleri baglar,
// sahneyi kaydeder. Tekrar calistirilabilir (idempotent).
// Kullanim: Unity menusu -> Yazlab -> Kurulumu Tamamla
public static class FinishSetup
{
    // Mixamo ile rig'lenmiş hayatta kalma karakteri (iskelti: mixamorig, Humanoid)
    const string CharFbx = "Assets/Just_Survive/Karakter/SurviverMale.fbx";
    const string PlayerName = "SurviverMale";
    const string EskiPlayerAdi = "Superhero_Male_FullBody"; // sahnedeyse silinip yenisiyle değiştirilir
    const string AnimFbx = "Assets/Universal Animation Library[Standard]/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx";
    const string ControllerPath = "Assets/PlayerAnimator.controller";
    const float JogEsigi = 7f;   // walkSpeed 4.5 < 7 < runSpeed 9
    const float AnimHizi = 1.8f; // kalça sallaması sakin dursun diye 2.2'den düşürüldü
    const float HedefBoy = 4.18f; // eski karakterin kalibre boyu (scale 2.2) — capsule/hız ayarları buna göre

    [MenuItem("Yazlab/Kurulumu Tamamla")]
    public static void Run()
    {
        // 1) Iki FBX'i de Humanoid yap (gerekiyorsa reimport)
        ImportHumanoid(CharFbx);
        ImportHumanoid(AnimFbx);

        // 1.5) "_Loop" isimli klipleri döngüye al (loopTime yoksa animasyon bir tur oynayıp donar)
        LooplariAyarla(AnimFbx);

        // Karakter değişimi: eski superhero sahnede duruyorsa sil, Mixamo karakterini yerine koy
        var player = GameObject.Find(PlayerName);
        if (player == null)
        {
            var eski = GameObject.Find(EskiPlayerAdi);
            var pos = eski != null ? eski.transform.position : new Vector3(0f, 0.05f, 2f);
            if (eski != null) Object.DestroyImmediate(eski);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
            if (model == null) { Debug.LogError($"[Kurulum] Karakter FBX bulunamadı: {CharFbx}"); return; }
            player = (GameObject)PrefabUtility.InstantiatePrefab(model);
            player.transform.position = pos;
            Debug.Log("[Kurulum] Eski karakter kaldırıldı, SurviverMale yerleştirildi.");
        }

        // Kameraya baksın; boyu ham mesh'ten ölç, eski karakterin kalibre boyuna ölçekle
        // (Mixamo FBX'in birimi emin değil — ölçerek otomatik otursun, kapsül/hız kalibrasyonu bozulmasın)
        var e = player.transform.eulerAngles;
        e.y = 180f;
        player.transform.eulerAngles = e;
        player.transform.localScale = Vector3.one;
        float hamBoy = BoyOlc(player);
        player.transform.localScale = Vector3.one * (hamBoy > 0.001f ? HedefBoy / hamBoy : 2.2f);
        Debug.Log($"[Kurulum] Ham boy {hamBoy:0.00} → ölçek {player.transform.localScale.x:0.00}");

        var animator = player.GetComponent<Animator>();
        if (animator == null) animator = player.AddComponent<Animator>();

        // 2) Avatar tazele — reimport sonrası referans tazelensin
        var avatar = AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<Avatar>().FirstOrDefault();
        if (avatar != null)
        {
            animator.avatar = avatar;
            Debug.Log($"[Kurulum] Avatar bağlandı: {avatar.name}");
        }
        else Debug.LogError("[Kurulum] Karakterin Humanoid avatarı bulunamadı!");
        animator.applyRootMotion = false;

        // 3) Controller motion'larını isimle tazele (reimport sonrası fileID'ler değişir)
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { Debug.LogError("[Kurulum] PlayerAnimator.controller bulunamadı."); return; }

        var clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__")).ToList();
        Debug.Log($"[Kurulum] Humanoid FBX'te {clips.Count} clip bulundu.");

        AnimationClip Bul(string anahtar, string tercih)
        {
            return clips.Where(c => c.name.ToLowerInvariant().Contains(anahtar))
                .OrderByDescending(c => c.name.ToLowerInvariant().Contains(tercih))
                .ThenBy(c => c.name.Length)
                .FirstOrDefault();
        }
        var idle = Bul("idle", "loop");
        var walk = Bul("walk", "loop");
        var jog = Bul("sprint", "loop"); // gercek kosu animasyonu (kalça sallamayan)
        if (jog == null) jog = Bul("jog", "fwd"); // yoksa jog'a dön
        if (idle == null) Debug.LogWarning("[Kurulum] Idle clip'i bulunamadı!");
        if (walk == null) Debug.LogWarning("[Kurulum] Walk clip'i bulunamadı!");
        if (jog == null) Debug.LogWarning("[Kurulum] Jog clip'i bulunamadı!");

        AnimatorState idleState = null, walkState = null, jogState = null;
        foreach (var layer in controller.layers)
        {
            foreach (var cs in layer.stateMachine.states)
            {
                var st = cs.state;
                if (st.name.Contains("Idle")) { idleState = st; if (idle != null) st.motion = idle; }
                else if (st.name.Contains("Walk")) { walkState = st; if (walk != null) st.motion = walk; st.speed = AnimHizi; }
                else if (st.name.Contains("Jog")) { jogState = st; if (jog != null) st.motion = jog; st.speed = AnimHizi; }
            }
        }
        EditorUtility.SetDirty(controller);

        // Walk <-> Jog gecişlerini yeniden kur (esik 8)
        if (walkState != null && jogState != null)
        {
            foreach (var t in walkState.transitions.Where(t => t.destinationState == jogState).ToArray())
                walkState.RemoveTransition(t);
            foreach (var t in jogState.transitions.Where(t => t.destinationState == walkState).ToArray())
                jogState.RemoveTransition(t);

            var w2j = walkState.AddTransition(jogState);
            w2j.hasExitTime = false;
            w2j.duration = 0.15f;
            w2j.AddCondition(AnimatorConditionMode.Greater, JogEsigi, "Speed");

            var j2w = jogState.AddTransition(walkState);
            j2w.hasExitTime = false;
            j2w.duration = 0.15f;
            j2w.AddCondition(AnimatorConditionMode.Less, JogEsigi, "Speed");
        }

        // Karakter 2.2x buyuk — kapsulu ona gore ayarla (CharacterController transform olcegini takip etmez)
        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.AddComponent<CharacterController>();
        cc.height = 4f;
        cc.radius = 0.66f;
        cc.center = new Vector3(0f, 2f, 0f);

        // PlayerConfig ScriptableObject varlığını yükle veya oluştur
        const string configDir = "Assets/_Project/ScriptableObjects";
        const string configPath = configDir + "/PlayerConfig.asset";
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(configDir))
            AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

        var playerConfig = AssetDatabase.LoadAssetAtPath<Yazlab.Player.PlayerConfig>(configPath);
        if (playerConfig == null)
        {
            playerConfig = ScriptableObject.CreateInstance<Yazlab.Player.PlayerConfig>();
            AssetDatabase.CreateAsset(playerConfig, configPath);
            Debug.Log($"[Kurulum] PlayerConfig oluşturuldu: {configPath}");
        }

        // Sahne doğuş noktası (PlayerSpawnPoint)
        var spawnGo = GameObject.Find("PlayerSpawnPoint");
        if (spawnGo == null)
        {
            spawnGo = new GameObject("PlayerSpawnPoint");
            spawnGo.transform.position = new Vector3(0f, 0.05f, 2f);
            spawnGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
        var spawnPoint = spawnGo.GetComponent<Yazlab.Player.PlayerSpawnPoint>();
        if (spawnPoint == null) spawnPoint = spawnGo.AddComponent<Yazlab.Player.PlayerSpawnPoint>();

        // Hareket ve girdi scriptleri (var olan bileşenin değerlerini de kalibre et — serileştirilmiş eski değer kalmasın)
        var inputReader = player.GetComponent<Yazlab.Player.PlayerInputReader>();
        if (inputReader == null) inputReader = player.AddComponent<Yazlab.Player.PlayerInputReader>();

        var pc = player.GetComponent<PlayerController>();
        if (pc == null) pc = player.AddComponent<PlayerController>();
        pc.Config = playerConfig;
        pc.InputReader = inputReader;
        pc.WalkSpeed = 4.5f;
        pc.RunSpeed = 12f; // Sprint_Loop'un adım temposuna uygun

        var ph = player.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.Config = playerConfig;
            ph.SpawnPoint = spawnPoint;
            var hpGo = GameObject.Find("HPText");
            if (hpGo != null && hpGo.TryGetComponent<UnityEngine.UI.Text>(out var hpTextComp))
                ph.HpText = hpTextComp;
        }

        // Material'lerin texture'ı boş kaldıysa paketin palet dokusunu bağla (FBX dış referanslı)
        var pallet = AssetDatabase.FindAssets("T_Pallet t:Texture2D").FirstOrDefault();
        var palletTex = string.IsNullOrEmpty(pallet) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(pallet));
        if (palletTex != null)
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                foreach (var mat in r.sharedMaterials)
                    if (mat != null && mat.mainTexture == null)
                    {
                        mat.mainTexture = palletTex;
                        EditorUtility.SetDirty(mat);
                    }

        // Kamera takibi
        var cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = player.transform;
            follow.offset = new Vector3(0f, 8f, -11f);
            Debug.Log("[Kurulum] Kamera takibi bağlandı.");
        }
        else
        {
            Debug.LogWarning("[Kurulum] Kamera bulunamadı — Main Camera'nın etiketi 'MainCamera' mı kontrol et.");
        }

        EditorUtility.SetDirty(player);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[Kurulum] TAMAM — Play'e bas: WASD yürür, Shift koşar.");
    }

    static void ImportHumanoid(string path)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp == null) { Debug.LogError($"[Kurulum] Importer bulunamadı: {path}"); return; }
        // NOT: Unity 6'da ModelImporterAnimationType.Humanoid üyesi yok — sabit değer 3 (Humanoid)
        if (imp.animationType != (ModelImporterAnimationType)3)
        {
            imp.animationType = (ModelImporterAnimationType)3;
            imp.SaveAndReimport();
            Debug.Log($"[Kurulum] Humanoid'e çevrildi: {path}");
        }
    }

    static void LooplariAyarla(string path)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp == null) { Debug.LogError($"[Kurulum] Importer bulunamadı: {path}"); return; }
        var settings = imp.clipAnimations;
        if (settings == null || settings.Length == 0) settings = imp.defaultClipAnimations;
        bool degisti = false;
        foreach (var s in settings)
        {
            // Sadece "_Loop" ile biten klipler döngüde olsun (Walk_Start gibi tek atışlılar kalmasın)
            bool dongu = s.name.ToLowerInvariant().Contains("loop");
            if (s.loopTime != dongu) { s.loopTime = dongu; degisti = true; }
        }
        if (degisti)
        {
            imp.clipAnimations = settings;
            imp.SaveAndReimport();
            Debug.Log("[Kurulum] Loop klipleri döngüye alındı.");
        }
    }

    // Tüm renderer'ların y-ekseni toplam boyu (lossyScale 1 iken çağır)
    static float BoyOlc(GameObject go)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            min = Mathf.Min(min, r.bounds.min.y);
            max = Mathf.Max(max, r.bounds.max.y);
        }
        return max > min ? max - min : 0f;
    }
}
