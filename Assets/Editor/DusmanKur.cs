using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Dusman kurulumu v2:
//  - 6 KURT (Ultimate Animated Animals/Wolf.fbx — Generic rig + kendi klipleri, gri Mat_Kurt, 2 canlı, hız 4)
//  - 1 SEF (kırmızı SurviverMale klonu — büyük, 6 canlı, sert vurur; ölünce HATCHET düşer + "Bölge temizlendi!")
//  - Oyuncu: tag + PlayerHealth + PlayerCombat + PlayerAnimator'a Punch state'i
//  - UI: HP yazısı (sol alt) + MesajPaneli (üst orta)
// Tekrar çalıştırılabilir: eskileri silip yeniden kurar.
// Kullanım: önce "Yazlab/Kurulumu Tamamla", sonra bu menü -> Yazlab/Dusmanlari Kur
public static class DusmanKur
{
    const string CharFbx = "Assets/Just_Survive/Karakter/SurviverMale.fbx";
    const string PlayerName = "SurviverMale";
    const string AnimFbx = "Assets/Universal Animation Library[Standard]/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx";
    const string WolfFbx = "Assets/Ultimate Animated Animals - July 2021/FBX/Wolf.fbx";
    const string HuskyFbx = "Assets/Ultimate Animated Animals - July 2021/FBX/Husky.fbx";
    const string HatchetFbx = "Assets/Just_Survive/Just Survive/FBX/Hatchet.fbx";
    const string ControllerPath = "Assets/DusmanAnimator.controller"; // şef (insan)
    const string KurtControllerPath = "Assets/KurtAnimator.controller";
    const string KopekControllerPath = "Assets/KopekAnimator.controller";
    const string MatSefPath = "Assets/Mat_Sef.mat";
    const string MatKurtPath = "Assets/Mat_Kurt.mat";
    const string MatKopekPath = "Assets/Mat_Kopek.mat";
    const string KopekAdi = "Kopek";
    const string UiAdi = "OyuncuUI";
    const string OnekAd = "Dusman_";
    const float HedefBoy = 4.18f;    // insan boyu kalibrasyonu (oyuncuyla aynı)
    const float KopekHedefBoy = 1.0f; // husky oyuncunun baldırına kadar
    const float KurtHedefBoy = 1.5f; // kurt oyuncunun dizine kadar
    const int KurtSayisi = 6;

    [MenuItem("Yazlab/Dusmanlari Kur")]
    public static void Run()
    {
        // 0) Döngü klipleri — Quaternius import'larında loopTime hiç yok,
        //    verilmezse klipler bir tur oynayıp DONAR (kurt yürüyüşündeki bozukluk bundandı)
        LoopGaranti(AnimFbx);
        LoopGaranti(WolfFbx);
        LoopGaranti(HuskyFbx);

        // 1) Klipler — şef için UAL (Humanoid), kurt için Wolf FBX (Generic, kendi klipleri)
        HumanoidGaranti(AnimFbx);
        var ual = KlipListesi(AnimFbx);
        var kurtKlipleri = KlipListesi(WolfFbx);
        Debug.Log($"[Dusman] UAL {ual.Count} klip, Wolf {kurtKlipleri.Count} klip.");

        var ualIdle = Bul(ual, "idle", "loop");
        var ualWalk = Bul(ual, "walk", "loop");
        var yumruk = Bul(ual, "punch_cross"); if (yumruk == null) yumruk = Bul(ual, "punch");
        var olum = Bul(ual, "death"); if (olum == null) olum = Bul(ual, "die");

        var kIdle = Bul(kurtKlipleri, "idle");
        var kWalk = Bul(kurtKlipleri, "walk");
        if (kWalk == null) kWalk = Bul(kurtKlipleri, "gallop"); // yürüme klipli gelmezse Gallop/Run dene
        if (kWalk == null) kWalk = Bul(kurtKlipleri, "run");
        if (kWalk == null) kWalk = kIdle;
        var kAttack = Bul(kurtKlipleri, "attack");
        var kDeath = Bul(kurtKlipleri, "death");
        Debug.Log($"[Dusman] Kurt klipleri: Idle={(kIdle != null ? kIdle.name : "YOK")} Walk={(kWalk != null ? kWalk.name : "YOK")} Attack={(kAttack != null ? kAttack.name : "YOK")} Death={(kDeath != null ? kDeath.name : "YOK")}");

        // 2) Controller'lar — şef + kurt (lokma = hasar alınca kısa sarsılma klibi; walkHizi = yürüme klip hızı)
        var sefController = ControllerKur(ControllerPath, ualIdle, ualWalk, yumruk, olum, Bul(ual, "hit_chest"), 1.5f, 1.2f);
        var kurtController = ControllerKur(KurtControllerPath, kIdle, kWalk, kAttack, kDeath, Bul(kurtKlipleri, "hitreact"), 1.2f, 1.6f);

        // 3) Material'ler + balta texture'ı
        var sefMat = MaterialKur(MatSefPath, new Color(0.42f, 0.15f, 0.12f, 1f)); // koyu kızıl — kabile boyası
        var kurtMat = MaterialKur(MatKurtPath, new Color(0.38f, 0.35f, 0.33f, 1f)); // kurt gri-kahve
        HatchetTextureDuzelt();

        // 3.5) EnemyConfig ScriptableObject varlıklarını oluştur veya güncelle
        const string configDir = "Assets/_Project/ScriptableObjects";
        const string wolfConfigPath = configDir + "/WolfEnemyConfig.asset";
        const string bossConfigPath = configDir + "/BossEnemyConfig.asset";
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            AssetDatabase.CreateFolder("Assets", "_Project");
        if (!AssetDatabase.IsValidFolder(configDir))
            AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

        var wolfConfig = AssetDatabase.LoadAssetAtPath<Yazlab.Enemies.EnemyConfig>(wolfConfigPath);
        if (wolfConfig == null)
        {
            wolfConfig = ScriptableObject.CreateInstance<Yazlab.Enemies.EnemyConfig>();
            AssetDatabase.CreateAsset(wolfConfig, wolfConfigPath);
        }
        wolfConfig.Configure(16f, 3.4f, 1.6f, 1.4f, 0.25f, 8, 2, false);
        EditorUtility.SetDirty(wolfConfig);

        var bossConfig = AssetDatabase.LoadAssetAtPath<Yazlab.Enemies.EnemyConfig>(bossConfigPath);
        if (bossConfig == null)
        {
            bossConfig = ScriptableObject.CreateInstance<Yazlab.Enemies.EnemyConfig>();
            AssetDatabase.CreateAsset(bossConfig, bossConfigPath);
        }
        bossConfig.Configure(18f, 3f, 2.4f, 2f, 0.25f, 15, 6, true);
        EditorUtility.SetDirty(bossConfig);

        // 4) Oyuncu: tag + sağlık + saldırı + Punch state'i (değerleri taze yaz)
        var player = GameObject.Find(PlayerName);
        if (player == null) { Debug.LogError("[Dusman] Sahnede oyuncu yok — önce Yazlab/Kurulumu Tamamla."); return; }
        player.tag = "Player";
        var ph = player.GetComponent<PlayerHealth>(); if (ph == null) ph = player.AddComponent<PlayerHealth>();
        ph.maxCan = 100; ph.can = 100;
        var pcb = player.GetComponent<PlayerCombat>(); if (pcb == null) pcb = player.AddComponent<PlayerCombat>();
        pcb.InputReader = player.GetComponent<Yazlab.Player.PlayerInputReader>();
        pcb.menzil = 3.2f; pcb.aciDerece = 90f; pcb.vurusBeklemesi = 0.55f;
        pcb.vurulmaGecikmesi = 0.22f; pcb.hitStopSuresi = 0.06f;
        pcb.tabanHasar = 1; pcb.hasar = 1; pcb.baltaHasarCarpani = 2; pcb.debugKayit = true;

        // Yumruk hissi: Cross = tek sert yumruk (Jab iki el arası gidip geliyordu)
        PlayerAnimasyonTamamla(yumruk, olum);

        // 5) Eskileri temizle
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name.StartsWith(OnekAd) || go.name == UiAdi)
                Object.DestroyImmediate(go);

        var hatchet = AssetDatabase.LoadAssetAtPath<GameObject>(HatchetFbx);

        // 6) Kurt sürüsü — halka düzeninde
        var kurtModel = AssetDatabase.LoadAssetAtPath<GameObject>(WolfFbx);
        if (kurtModel == null) { Debug.LogError("[Dusman] Wolf.fbx bulunamadı: " + WolfFbx); return; }
        Vector3 merkez = player.transform.position;
        for (int i = 0; i < KurtSayisi; i++)
        {
            float aci = (i * 60f + 15f) * Mathf.Deg2Rad;
            float yariCap = 18f + i * 2f;
            var pos = merkez + new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci)) * yariCap;
            pos.y = 0.05f;
            var kurt = DusmanYerlestir(kurtModel, OnekAd + "Kurt" + (i + 1), pos, i * 60f + 15f, KurtHedefBoy);
            AnimasyonBagla(kurt, kurtController, null); // Generic: avatar gerekmez

            var cc = kurt.GetComponent<CharacterController>();
            if (cc == null) cc = kurt.AddComponent<CharacterController>();
            // CC ölçüleri dünya biriminde kalsın diye kök ölçeğine böl; stepOffset=0 şart —
            // aksi halde Unity "Step Offset must be less or equal..." deyip CC'yi KAPATIYOR
            float ks = Mathf.Max(0.0001f, kurt.transform.localScale.x);
            cc.height = 1.5f / ks; cc.radius = 0.35f / ks; cc.center = new Vector3(0f, 0.75f / ks, 0f);
            cc.stepOffset = 0f; cc.skinWidth = Mathf.Min(0.08f, cc.radius * 0.1f);

            var ec = kurt.AddComponent<EnemyController>();
            ec.Config = wolfConfig;
            ec.SetPlayerTarget(player.transform);
            ec.algilamaMesafesi = 16f; ec.hiz = 3.4f; ec.saldiriMesafesi = 1.6f;
            ec.saldiriBeklemesi = 1.4f; ec.hasar = 8; ec.can = 2; ec.debugKayit = true;

            foreach (var r in kurt.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(_ => kurtMat).ToArray(); // boş slotları da doldur
        }

        // 7) Şef — tek insan, daha uzakta, daha büyük, ölümde balta + zafer mesajı
        var sefModel = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<Avatar>().FirstOrDefault();
        var sefAcisi = 210f;
        var sefPos = merkez + new Vector3(Mathf.Cos(sefAcisi * Mathf.Deg2Rad), 0f, Mathf.Sin(sefAcisi * Mathf.Deg2Rad)) * 32f;
        sefPos.y = 0.05f;
        var sef = DusmanYerlestir(sefModel, OnekAd + "Sef", sefPos, sefAcisi, HedefBoy * 1.15f);
        AnimasyonBagla(sef, sefController, avatar);

        var scc = sef.GetComponent<CharacterController>();
        if (scc == null) scc = sef.AddComponent<CharacterController>();
        float ss = Mathf.Max(0.0001f, sef.transform.localScale.x);
        scc.height = 4f / ss; scc.radius = 0.66f / ss; scc.center = new Vector3(0f, 2f / ss, 0f);
        scc.stepOffset = 0f; scc.skinWidth = Mathf.Min(0.08f, scc.radius * 0.1f);

        var sec = sef.AddComponent<EnemyController>();
        sec.Config = bossConfig;
        sec.SetPlayerTarget(player.transform);
        sec.algilamaMesafesi = 18f; sec.hiz = 3f; sec.saldiriMesafesi = 2.4f;
        sec.saldiriBeklemesi = 2f; sec.hasar = 15; sec.can = 6; sec.debugKayit = true;
        sec.olumDrop = hatchet;
        sec.olumMesaji = "Bölge temizlendi! Kabile şefi yenildi.";

        foreach (var r in sef.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(_ => sefMat).ToArray();

        // 8) UI: HP + mesaj paneli
        UiKur(ph);

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Dusman] TAMAM — {KurtSayisi} kurt + 1 şef kurulu. Play: kurtlar 16 m'de kovalar (hız 4), şef 6 yumrukta ölür ve balta düşer.");
    }

    // FBX modelini isimle, konumla, rotasyonla, hedef boya ölçekleyip sahneye koyar.
    // Pivot farklarına karşı ayakları yere sabitler (süzülme/batma olmasın).
    static GameObject DusmanYerlestir(GameObject model, string ad, Vector3 pos, float rotY, float hedefBoy)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = ad;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        go.transform.localScale = Vector3.one;
        float boy = BoyOlc(go);
        go.transform.localScale = Vector3.one * (boy > 0.001f ? hedefBoy / boy : 1f);

        float minY, maxY;
        BoyAraligi(go, out minY, out maxY);
        if (maxY > minY) go.transform.position += new Vector3(0f, 0.05f - minY, 0f);
        return go;
    }

    static void AnimasyonBagla(GameObject go, RuntimeAnimatorController controller, Avatar avatar)
    {
        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        if (avatar != null) anim.avatar = avatar;
        anim.applyRootMotion = false;
        anim.runtimeAnimatorController = controller;
    }

    static List<AnimationClip> KlipListesi(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__")).ToList();
    }

    static AnimationClip Bul(List<AnimationClip> clips, string anahtar, string tercih = "")
    {
        return clips.Where(c => c.name.ToLowerInvariant().Contains(anahtar))
            .OrderByDescending(c => c.name.ToLowerInvariant().Contains(tercih))
            .ThenBy(c => c.name.Length)
            .FirstOrDefault();
    }

    // Oyuncunun PlayerAnimator.controller'ına Punch (Attack) ve Death (Die/Revive) state'lerini ekler.
    // Özellik başına idempotent: her biri yalnız eksikse eklenir.
    static void PlayerAnimasyonTamamla(AnimationClip yumruk, AnimationClip olum)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/PlayerAnimator.controller");
        if (c == null) { Debug.LogWarning("[Dusman] PlayerAnimator.controller yok — oyuncu savaş animasyonları eklenemedi."); return; }

        var sm = c.layers[0].stateMachine;
        bool degisti = false;

        System.Func<string, AnimatorState> stateBul = ad =>
            System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(sm.states, s => s.state.name == ad), s => s.state).FirstOrDefault();
        void ParamEkle(string ad, AnimatorControllerParameterType tip)
        {
            if (System.Linq.Enumerable.All(c.parameters, p => p.name != ad)) { c.AddParameter(ad, tip); degisti = true; }
        }
        ParamEkle("Attack", AnimatorControllerParameterType.Trigger);
        ParamEkle("Die", AnimatorControllerParameterType.Trigger);
        ParamEkle("Revive", AnimatorControllerParameterType.Trigger);

        // --- Punch ---
        var sPunch = stateBul("Punch");
        if (sPunch != null && yumruk != null && (sPunch.motion as AnimationClip) != yumruk)
        {
            sPunch.motion = yumruk; // klip tercihi değiştiyse mevcut state'i güncelle
            degisti = true;
            Debug.Log($"[Dusman] Punch klibi güncellendi: {yumruk.name}");
        }
        if (sPunch == null && yumruk != null)
        {
            sPunch = sm.AddState("Punch", new Vector3(460, 280));
            sPunch.motion = yumruk; sPunch.speed = 1.4f;
            AnimatorState hedefWalk = null;
            foreach (var cs in sm.states)
            {
                if (cs.state.name.Contains("Walk")) hedefWalk = cs.state;
                if (cs.state == sPunch ||
                    !(cs.state.name.Contains("Idle") || cs.state.name.Contains("Walk") || cs.state.name.Contains("Jog"))) continue;
                var t = cs.state.AddTransition(sPunch);
                t.hasExitTime = false; t.duration = 0.1f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            }
            if (hedefWalk != null)
            {
                var cikis = sPunch.AddTransition(hedefWalk);
                cikis.hasExitTime = true; cikis.exitTime = 1f; cikis.duration = 0.15f;
            }
            degisti = true;
            Debug.Log("[Dusman] Oyuncuya Punch state'i eklendi.");
        }

        // --- Death (Die ile girilir, Revive ile Idle'a dönülür) ---
        var sDeath = stateBul("Death");
        // Idle state'in adı "Idle_Loop" gibi olabilir — önce tam eşleşme, sonra içerir araması
        var sIdle = stateBul("Idle");
        if (sIdle == null)
            sIdle = System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Where(sm.states, s => s.state.name.Contains("Idle") && !s.state.name.Contains("Talking")),
                s => s.state).FirstOrDefault();
        if (sDeath == null && olum != null)
        {
            sDeath = sm.AddState("Death", new Vector3(460, 380));
            sDeath.motion = olum;
            foreach (var cs in sm.states)
            {
                if (cs.state == sDeath ||
                    !(cs.state.name.Contains("Idle") || cs.state.name.Contains("Walk") || cs.state.name.Contains("Jog") || cs.state.name.Contains("Punch"))) continue;
                var t = cs.state.AddTransition(sDeath);
                t.hasExitTime = false; t.duration = 0.15f;
                t.AddCondition(AnimatorConditionMode.If, 0, "Die");
            }
            if (sIdle != null)
            {
                var don = sDeath.AddTransition(sIdle);
                don.hasExitTime = false; don.duration = 0.1f;
                don.AddCondition(AnimatorConditionMode.If, 0, "Revive");
            }
            degisti = true;
            Debug.Log("[Dusman] Oyuncuya Death state'i eklendi (Die/Revive).");
        }

        // Revive bağlantısını her koşuda garanti et — eski kurulumda Idle adı tam
        // eşleşmediği için bu geçiş hiç eklenmemişti ve oyuncu ölü pozda kalıyordu
        if (sDeath != null && sIdle != null)
        {
            bool bagli = false;
            foreach (var t in sDeath.transitions)
                if (t.destinationState == sIdle) { bagli = true; break; }
            if (!bagli)
            {
                var don = sDeath.AddTransition(sIdle);
                don.hasExitTime = false; don.duration = 0.1f;
                don.AddCondition(AnimatorConditionMode.If, 0, "Revive");
                degisti = true;
                Debug.Log($"[Dusman] Death → {sIdle.name} (Revive) geçişi onarıldı.");
            }
        }

        if (degisti) EditorUtility.SetDirty(c);
    }

    // Sil-yeniden-kur: Idle/Walk + (Attack -> Punch) + (Die -> Death) + (Hit -> lokma)
    static AnimatorController ControllerKur(string path, AnimationClip idle, AnimationClip walk, AnimationClip saldiri,
        AnimationClip olum, AnimationClip lokma, float walkHizi, float saldiriHizi)
    {
        AssetDatabase.DeleteAsset(path);
        var c = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = c.layers[0].stateMachine;

        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        var sIdle = sm.AddState("Idle", new Vector3(250, 40));  if (idle != null) sIdle.motion = idle;
        sm.defaultState = sIdle; // varsayılan state yoksa animator hiçbir şey oynatmaz
        var sWalk = sm.AddState("Walk", new Vector3(250, 160)); if (walk != null) { sWalk.motion = walk; sWalk.speed = walkHizi; }
        var sPunch = sm.AddState("Punch", new Vector3(460, 100));
        if (saldiri != null) { sPunch.motion = saldiri; sPunch.speed = saldiriHizi; }
        var sDeath = sm.AddState("Death", new Vector3(460, 220));
        if (olum != null) sDeath.motion = olum;
        var sHit = sm.AddState("Hit", new Vector3(460, 340));
        if (lokma != null) { sHit.motion = lokma; sHit.speed = 1.6f; }

        Gecis(sIdle, sWalk, AnimatorConditionMode.Greater, 0.1f, "Speed");
        Gecis(sWalk, sIdle, AnimatorConditionMode.Less, 0.1f, "Speed");

        var i2p = sIdle.AddTransition(sPunch); i2p.hasExitTime = false; i2p.duration = 0.1f; i2p.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        var w2p = sWalk.AddTransition(sPunch); w2p.hasExitTime = false; w2p.duration = 0.1f; w2p.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        var p2w = sPunch.AddTransition(sWalk); p2w.hasExitTime = true; p2w.exitTime = 1f; p2w.duration = 0.15f;

        foreach (var kaynak in new[] { sIdle, sWalk })
        {
            var t = kaynak.AddTransition(sDeath);
            t.hasExitTime = false; t.duration = 0.15f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Die");
        }

        foreach (var kaynak in new[] { sIdle, sWalk, sPunch })
        {
            var ht = kaynak.AddTransition(sHit);
            ht.hasExitTime = false; ht.duration = 0.08f;
            ht.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        }
        var h2w = sHit.AddTransition(sWalk); h2w.hasExitTime = true; h2w.exitTime = 1f; h2w.duration = 0.1f;

        EditorUtility.SetDirty(c);
        return c;
    }

    static void Gecis(AnimatorState from, AnimatorState to, AnimatorConditionMode mod, float esik, string param)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false; t.duration = 0.15f;
        t.AddCondition(mod, esik, param);
    }

    static Material MaterialKur(string path, Color renk)
    {
        var mevcut = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mevcut != null)
        {
            mevcut.color = renk;
            EditorUtility.SetDirty(mevcut);
            return mevcut;
        }
        var mat = new Material(Shader.Find("Standard")) { color = renk };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Hatchet'in material'ine texture'sız kalırsa T_Pallet bağla (editörde kalıcı)
    static void HatchetTextureDuzelt()
    {
        var hatchet = AssetDatabase.LoadAssetAtPath<GameObject>(HatchetFbx);
        if (hatchet == null) { Debug.LogWarning("[Dusman] Hatchet.fbx bulunamadı — balta düşmeyecek."); return; }
        var pallet = AssetDatabase.FindAssets("T_Pallet t:Texture2D").FirstOrDefault();
        var tex = string.IsNullOrEmpty(pallet) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(pallet));
        foreach (var r in hatchet.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null && m.mainTexture == null && tex != null)
                {
                    m.mainTexture = tex;
                    EditorUtility.SetDirty(m);
                }
    }

    // Quaternius FBX import'larında loopTime hiç yoktur → klipler bir tur oynayıp donar.
    // Döngü niteliği taşıyan kliplere (loop/idle/walk/gallop/eating) loopTime verip reimport eder.
    static void LoopGaranti(string fbxPath)
    {
        var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (imp == null) { Debug.LogWarning("[Dusman] Model importer yok: " + fbxPath); return; }

        bool degisti = false;
        var klipler = imp.clipAnimations;
        if (klipler == null || klipler.Length == 0)
        {
            klipler = imp.defaultClipAnimations; // import ayarları boşsa default'lardan türet
            degisti = true;
        }
        foreach (var k in klipler)
        {
            var ad = k.name.ToLowerInvariant();
            bool dongu = ad.Contains("loop") || ad.Contains("idle") || ad.Contains("walk") ||
                         ad.Contains("gallop") || ad.Contains("eating");
            if (dongu && !k.loopTime) { k.loopTime = true; degisti = true; }
        }
        if (degisti)
        {
            imp.clipAnimations = klipler;
            imp.SaveAndReimport();
            Debug.Log($"[Dusman] loopTime ayarlandı + reimport: {System.IO.Path.GetFileName(fbxPath)}");
        }
    }

    // Köpek refakatçi: oyuncuyu takip eden husky. Dusmanlari Kur bu nesneyi silmez;
    // bu menü her çalıştırmada eskisini silip yeniden kurar.
    [MenuItem("Yazlab/Kopegi Kur")]
    public static void KopekRun()
    {
        var player = GameObject.Find(PlayerName);
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogError("[Kopek] Sahnede oyuncu yok — önce Yazlab/Kurulumu Tamamla."); return; }

        LoopGaranti(HuskyFbx);
        var klip = KlipListesi(HuskyFbx);
        var kIdle = Bul(klip, "idle", "loop");
        var kWalk = Bul(klip, "walk");
        var kGallop = Bul(klip, "gallop");
        if (kWalk == null) kWalk = kIdle;
        Debug.Log($"[Kopek] Klipler: Idle={(kIdle != null ? kIdle.name : "YOK")} Walk={(kWalk != null ? kWalk.name : "YOK")} Gallop={(kGallop != null ? kGallop.name : "YOK")}");

        // Speed değerine göre Idle ↔ Walk ↔ Gallop
        AssetDatabase.DeleteAsset(KopekControllerPath);
        var c = AnimatorController.CreateAnimatorControllerAtPath(KopekControllerPath);
        var sm = c.layers[0].stateMachine;
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sIdle = sm.AddState("Idle", new Vector3(250, 40));  if (kIdle != null) sIdle.motion = kIdle;
        sm.defaultState = sIdle;
        var sWalk = sm.AddState("Walk", new Vector3(250, 160)); if (kWalk != null) { sWalk.motion = kWalk; sWalk.speed = 1.3f; }
        var sGallop = sm.AddState("Gallop", new Vector3(460, 160)); if (kGallop != null) sGallop.motion = kGallop;
        Gecis(sIdle, sWalk, AnimatorConditionMode.Greater, 0.1f, "Speed");
        Gecis(sWalk, sIdle, AnimatorConditionMode.Less, 0.1f, "Speed");
        Gecis(sWalk, sGallop, AnimatorConditionMode.Greater, 3f, "Speed");
        Gecis(sGallop, sWalk, AnimatorConditionMode.Less, 3f, "Speed");
        EditorUtility.SetDirty(c);

        var kopekMat = MaterialKur(MatKopekPath, new Color(0.72f, 0.74f, 0.78f, 1f)); // açık gri — husky

        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == KopekAdi)
                Object.DestroyImmediate(go);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(HuskyFbx);
        if (model == null) { Debug.LogError("[Kopek] Husky.fbx bulunamadı: " + HuskyFbx); return; }
        var pos = player.transform.position + player.transform.forward * 2f + player.transform.right * 1.5f;
        pos.y = 0.05f;
        var kopek = DusmanYerlestir(model, KopekAdi, pos, player.transform.eulerAngles.y + 180f, KopekHedefBoy);
        AnimasyonBagla(kopek, c, null); // Generic rig: avatar gerekmez

        var cc = kopek.GetComponent<CharacterController>();
        if (cc == null) cc = kopek.AddComponent<CharacterController>();
        // Kök ölçeğine böl (dünya boyutu sabit) + stepOffset=0 — yoksa Unity CC'yi kapatıyor
        float s = Mathf.Max(0.0001f, kopek.transform.localScale.x);
        cc.height = 1.0f / s; cc.radius = 0.3f / s; cc.center = new Vector3(0f, 0.5f / s, 0f);
        cc.stepOffset = 0f; cc.skinWidth = Mathf.Min(0.08f, cc.radius * 0.1f);

        kopek.AddComponent<CompanionController>();

        foreach (var r in kopek.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(_ => kopekMat).ToArray();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Kopek] Köpek kuruldu — oyuncuyu takip eder.");
    }

    static void UiKur(PlayerHealth playerHealth = null)
    {
        var canvasGo = new GameObject(UiAdi, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var mesajPaneli = canvasGo.AddComponent<MesajPaneli>();

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // HP yazısı — sol alt
        var hpGo = new GameObject("HPText", typeof(Text));
        hpGo.transform.SetParent(canvasGo.transform, false);
        var hp = hpGo.GetComponent<Text>();
        hp.font = font; hp.fontSize = 34; hp.color = Color.white; hp.text = "HP: 100";
        var hrt = hp.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 0f);
        hrt.pivot = new Vector2(0f, 0f); hrt.anchoredPosition = new Vector2(24f, 20f);
        hrt.sizeDelta = new Vector2(300f, 60f);

        if (playerHealth != null)
        {
            playerHealth.HpText = hp;
        }

        // Mesaj satırı — üst orta
        var msgGo = new GameObject("MsgText", typeof(Text));
        msgGo.transform.SetParent(canvasGo.transform, false);
        var msg = msgGo.GetComponent<Text>();
        msg.font = font; msg.fontSize = 30; msg.color = new Color(1f, 0.9f, 0.5f); msg.text = "";
        msg.alignment = TextAnchor.UpperCenter;
        msg.verticalOverflow = VerticalWrapMode.Overflow;
        var mrt = msg.rectTransform;
        mrt.anchorMin = new Vector2(0.5f, 1f); mrt.anchorMax = new Vector2(0.5f, 1f);
        mrt.pivot = new Vector2(0.5f, 1f); mrt.anchoredPosition = new Vector2(0f, -60f);
        mrt.sizeDelta = new Vector2(900f, 120f);

        mesajPaneli.MessageText = msg;
    }

    static void HumanoidGaranti(string path)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp != null && imp.animationType != (ModelImporterAnimationType)3)
        {
            imp.animationType = (ModelImporterAnimationType)3;
            imp.SaveAndReimport();
        }
    }

    // FinishSetup ile aynı ölçüm — lossyScale ölçümden sonra; lossyScale 1 iken çağır
    static float BoyOlc(GameObject go)
    {
        float minY, maxY;
        BoyAraligi(go, out minY, out maxY);
        return maxY > minY ? maxY - minY : 0f;
    }

    static void BoyAraligi(GameObject go, out float minY, out float maxY)
    {
        minY = float.MaxValue; maxY = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            minY = Mathf.Min(minY, r.bounds.min.y);
            maxY = Mathf.Max(maxY, r.bounds.max.y);
        }
    }

    // Kurulumun tüm halkasını editörde denetler
    [MenuItem("Yazlab/Dusmanlari Test Et")]
    public static void TestEt()
    {
        var player = GameObject.Find(PlayerName);
        if (player == null) { Debug.LogError("[Test] Oyuncu yok — önce Kurulumu Tamamla."); return; }

        Debug.Log($"[Test] OYUNCU: tag={player.tag}, PlayerHealth={player.GetComponent<PlayerHealth>() != null}, " +
                  $"PlayerCombat={player.GetComponent<PlayerCombat>() != null}");
        var pa = player.GetComponentInChildren<Animator>();
        var pac = pa != null ? pa.runtimeAnimatorController as AnimatorController : null;
        if (pac != null)
        {
            var paramlar = string.Join(",", System.Linq.Enumerable.Select(pac.parameters, p => p.name));
            var punchVar = System.Linq.Enumerable.Any(pac.layers[0].stateMachine.states, s => s.state.name == "Punch");
            Debug.Log($"[Test] Oyuncu controller: {pac.name}, paramlar=[{paramlar}], Punch state={punchVar}");
        }
        else Debug.LogError("[Test] Oyuncuda Animator/controller YOK!");

        var dusmanlar = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        Debug.Log($"[Test] {dusmanlar.Length} düşman sahnede.");
        foreach (var d in dusmanlar)
        {
            var anim = d.GetComponentInChildren<Animator>();
            var dc = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;
            string durum;
            if (dc == null) durum = "CONTROLLER YOK!";
            else
            {
                var paramlar = string.Join(",", System.Linq.Enumerable.Select(dc.parameters, p => p.name));
                var stateListe = dc.layers[0].stateMachine.states;
                string KlipAdi(string ad) =>
                    System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(stateListe, s => s.state.name == ad), s => s.state.motion).FirstOrDefault() is AnimationClip k ? k.name : "YOK";
                durum = $"{dc.name} paramlar=[{paramlar}] IdleKlip={KlipAdi("Idle")} WalkKlip={KlipAdi("Walk")} PunchKlip={KlipAdi("Punch")} DeathKlip={KlipAdi("Death")}";
            }
            float mesafe = Vector3.Distance(d.transform.position, player.transform.position);
            Debug.Log($"[Test] {d.name}: aktif={d.gameObject.activeInHierarchy} can={d.can} mesafe={mesafe:0.0} m, {durum}");
        }
        Debug.Log("[Test] BİTTİ.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Bölüm sahneleri — ormandan sonraki bölgeler. (Sahne geçişi/LoadScene
    // arkadaşın kısmı; burada yalnız sahneler kuruluyor.)
    // "Yazlab/Bolumleri Kur": Bolum2_Gecit (kayalık geçit) + Bolum3_Koy (köy)
    // sahnesini sıfırdan kurar ve Build Settings'e yazar. GameScene'e DOKUNMAZ.
    // Controller'lar yeniden OLUŞTURULMAZ (GUID değişirse orman sahnesinin
    // referansları kırılır) — var olanlar yüklenir; yoksa hata verip durur.
    // Tekrar çalıştırılabilir: her seferinde iki sahneyi baştan yazar.
    // ─────────────────────────────────────────────────────────────────────
    const string Bolum2SahneAdi = "Bolum2_Gecit";
    const string Bolum3SahneAdi = "Bolum3_Koy";
    const string PlayerControllerPath = "Assets/PlayerAnimator.controller";
    const string NatureUnity = "Assets/Nature/FBX (Unity)";
    const string JsFbx = "Assets/Just_Survive/Just Survive/FBX/";
    const string CampfireFbx = "Assets/Just_Survive/Just Survive/FBX/Campfire.fbx";
    const string TentFbx = "Assets/Just_Survive/Just Survive/FBX/TentGreen.fbx";
    const string Tent2Fbx = "Assets/Just_Survive/Just Survive/FBX/TentBlue.fbx";
    const string MatReisPath = "Assets/Mat_Reis.mat";
    static Material toprakMat; // patika/yama materyali — sahne kurulumları arasında tek örnek

    [MenuItem("Yazlab/Bolumleri Kur")]
    public static void BolumleriKur()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var playerC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
        var sefC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var kurtC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KurtControllerPath);
        var kopekC = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KopekControllerPath);
        var kurtMat = AssetDatabase.LoadAssetAtPath<Material>(MatKurtPath);
        var kopekMat = AssetDatabase.LoadAssetAtPath<Material>(MatKopekPath);
        if (playerC == null || sefC == null || kurtC == null || kopekC == null || kurtMat == null || kopekMat == null)
        {
            Debug.LogError("[Bolum] Animator/Materyal assetleri eksik — önce Yazlab/Dusmanlari Kur ve Yazlab/Kopegi Kur.");
            return;
        }

        var kurtModel = AssetDatabase.LoadAssetAtPath<GameObject>(WolfFbx);
        var huskyModel = AssetDatabase.LoadAssetAtPath<GameObject>(HuskyFbx);
        var insanModel = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
        var hatchet = AssetDatabase.LoadAssetAtPath<GameObject>(HatchetFbx);
        HatchetTextureDuzelt();
        if (kurtModel == null || huskyModel == null || insanModel == null)
        {
            Debug.LogError("[Bolum] Model FBX'leri bulunamadı (Wolf/Husky/SurviverMale).");
            return;
        }

        // ── BÖLÜM 2: Kayalık Geçit — karanlık, sisli, soğuk ──
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.24f, 0.26f, 0.33f);
        RenderSettings.fogStartDistance = 14f;
        RenderSettings.fogEndDistance = 70f;
        RenderSettings.ambientLight = new Color(0.36f, 0.38f, 0.47f);

        ZeminMatesi("Assets/Mat_Zemin_Gecit.mat", new Color(0.85f, 0.83f, 0.80f), "T_DirtGround", 40f);
        GunesKur(new Vector3(55f, -40f, 0f), new Color(0.75f, 0.82f, 1f), 0.75f); // dik, soğuk, soluk

        var b2Player = OyuncuKur(playerC);
        KameraKur(b2Player);
        UiKur(b2Player.GetComponent<PlayerHealth>());
        KopekEkle(huskyModel, kopekC, kopekMat, b2Player);

        // Toprak patika — doğuştan geçide kadar sahnenin omurgası
        ToprakYol("Patika", new Vector3(0f, 0f, 2f), 60f, ToprakMatesi());

        // Koridor: sol-sağ kayalar (duvarların hemen dışında), ortada DÜZ yol taşları —
        // yüksek taş koyunca karakter/kurt içine giriyor, o yüzden levha (0.15).
        // Boylar ve açılar rastgele: eşit aralıklı "kaya çiti" hissi olmasın.
        for (int i = 0; i < 11; i++)
        {
            float z = -4f - i * 5.2f;
            float sol = 8.5f + (i % 3) * 1.6f;
            float sag = 8.5f + ((i + 1) % 3) * 1.6f;
            var kSol = DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_" + ((i % 3) + 1) + ".fbx"),
                "Kaya_Sol" + i,
                new Vector3(-sol + (Rastgele(i, 101f) - 0.5f), 0.05f, z + (Rastgele(i, 102f) - 0.5f) * 1.6f),
                Rastgele(i, 103f) * 360f, 2.6f + Rastgele(i, 104f) * 1.9f);
            Cesitle(kSol, 0.8f, 1.35f, i * 7 + 1);
            var kSag = DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_" + (((i + 1) % 3) + 1) + ".fbx"),
                "Kaya_Sag" + i,
                new Vector3(sag + (Rastgele(i, 105f) - 0.5f), 0.05f, z + (Rastgele(i, 106f) - 0.5f) * 1.6f),
                Rastgele(i, 107f) * 360f, 2.6f + Rastgele(i, 108f) * 1.9f);
            Cesitle(kSag, 0.8f, 1.35f, i * 7 + 2);
            DusmanYerlestir(Yukle(NatureUnity + "/RockPath_Round_Wide.fbx"),
                "Yol_Tasi" + i,
                new Vector3(((i % 2) * 2f - 1f) * 1.2f + (Rastgele(i, 109f) - 0.5f) * 0.8f, 0.05f, z - 2.5f),
                Rastgele(i, 110f) * 360f, 0.15f);
        }

        // Koridor kenarı çalılık/çakıl — yol kenarları bomboş kalmasın
        for (int i = 0; i < 18; i++)
        {
            float z = -2f - i * 3.4f;
            float taraf = i % 2 == 0 ? -1f : 1f;
            float x = taraf * (4.5f + Rastgele(i, 6f) * 2f);
            string caliTuru = i % 3 == 0 ? "/Bush_Common.fbx" : i % 3 == 1 ? "/Fern_1.fbx" : "/Pebble_Round_" + ((i % 5) + 1) + ".fbx";
            var cali = DusmanYerlestir(Yukle(NatureUnity + caliTuru),
                "Cali_" + i, new Vector3(x, 0.05f, z), Rastgele(i, 7f) * 360f, 0.5f + Rastgele(i, 8f) * 0.7f);
            Cesitle(cali, 0.75f, 1.3f, i * 5 + 3);
        }

        // Koridor dışı ormanı KÜMELER halinde döşe — sıra/halka hissi olmasın
        for (int k = 0; k < 30; k++)
        {
            float cx = (k % 2 == 0 ? -1f : 1f) * (13f + Rastgele(k, 51f) * 14f);
            float cz = 6f - Rastgele(k, 52f) * 74f;
            int adet = 2 + (int)(Rastgele(k, 53f) * 4f); // kümede 2-5 ağaç
            for (int j = 0; j < adet; j++)
            {
                float x = cx + (Rastgele(k * 10 + j, 54f) - 0.5f) * 7f;
                float z = cz + (Rastgele(k * 10 + j, 55f) - 0.5f) * 7f;
                float secim = Rastgele(k * 10 + j, 56f);
                string tur = secim < 0.45f ? "/Pine_" : secim < 0.72f ? "/TwistedTree_" : "/DeadTree_";
                var agac = DusmanYerlestir(Yukle(NatureUnity + tur + ((k + j) % 5 + 1) + ".fbx"),
                    "Orman_" + k + "_" + j, new Vector3(x, 0.05f, z), Rastgele(k * 10 + j, 58f) * 360f,
                    4.5f + Rastgele(k * 10 + j, 59f) * 5.5f);
                Cesitle(agac, 0.85f, 1.2f, k * 100 + j);
            }
        }
        for (int i = 0; i < 14; i++) // kümeler arasına tek tük ağaç
        {
            float x = (i % 2 == 0 ? -1f : 1f) * (12f + Rastgele(i, 61f) * 16f);
            float z = 6f - Rastgele(i, 62f) * 74f;
            var agac = DusmanYerlestir(Yukle(NatureUnity + "/Pine_" + ((i % 5) + 1) + ".fbx"),
                "Orman_Tek_" + i, new Vector3(x, 0.05f, z), Rastgele(i, 63f) * 360f, 5f + Rastgele(i, 64f) * 5f);
            Cesitle(agac, 0.85f, 1.15f, i * 13 + 7);
        }

        // Kurt sürüsü — koridorda ikişerli dört gözetim noktası (orman kurtlarından sert)
        var b2Kurt = new[]
        {
            new Vector3(-5f, 0.05f, -18f), new Vector3(5f, 0.05f, -19f),
            new Vector3(-5f, 0.05f, -30f), new Vector3(5f, 0.05f, -31f),
            new Vector3(-6f, 0.05f, -42f), new Vector3(6f, 0.05f, -43f),
            new Vector3(-6f, 0.05f, -53f), new Vector3(6f, 0.05f, -54f)
        };
        for (int i = 0; i < b2Kurt.Length; i++)
            KurtEkle(kurtModel, kurtC, kurtMat, OnekAd + "Kurt" + (i + 1), b2Kurt[i], i * 45f, 3, 10, 20f, 3.8f, b2Player);

        // Görünmez duvarlar — yolun dışına çıkış kapalı (kayaların iç yüzünde; -z ucu açık kalsın,
        // sahne geçişi oradan yapılacak). Kurtlar da duvardan dışarı çıkamaz → kayalara girmezler.
        DuvarKur(new Vector3(-7.5f, 3f, -27f), new Vector3(1f, 6f, 72f));
        DuvarKur(new Vector3(7.5f, 3f, -27f), new Vector3(1f, 6f, 72f));
        DuvarKur(new Vector3(0f, 3f, 7f), new Vector3(16f, 6f, 1f)); // doğuş arkası

        // HERO: anıtsal geçit kapısı — koridorun sonundaki odak noktası
        // (görsel kapı; sahne geçiş tetikleyecisini arkadaş buranın önüne koyabilir)
        DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_1.fbx"), "Kapi_Kule_Sol", new Vector3(-4.2f, 0.05f, -58f), 15f, 9.5f);
        DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_3.fbx"), "Kapi_Kule_Sag", new Vector3(4.2f, 0.05f, -58f), 205f, 10.5f);
        var kemer = DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_2.fbx"), "Kapi_Kemer", new Vector3(0f, 0.05f, -58f), 0f, 5f);
        kemer.transform.position += new Vector3(0f, 8.3f, 0f);
        kemer.transform.localScale = Vector3.Scale(kemer.transform.localScale, new Vector3(1.7f, 0.75f, 0.9f));
        AtesKur(new Vector3(-2.9f, 0.05f, -56.2f), 0.8f);
        AtesKur(new Vector3(2.9f, 0.05f, -56.2f), 0.8f);

        // Anıt kaya kümesi — koridorun ortasında doğal engel (dolana kadar manzara)
        DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_2.fbx"), "Anit_Kaya", new Vector3(-4.2f, 0.05f, -36f), 10f, 6.2f);
        DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_1.fbx"), "Anit_Kaya_Kucuk", new Vector3(-2.6f, 0.05f, -37.6f), 140f, 3.2f);

        // Devrik kütük/dallar — patikanın kenarında kuru ağırlık
        YatayKutuk("Kuru_Kutuk", new Vector3(-3.5f, 0f, -8f), 15f, 3.4f, Yukle(JsFbx + "TreeTrunk.fbx"));
        YatayKutuk("Kuru_Dal_1", new Vector3(3.7f, 0f, -21f), 60f, 3f, Yukle(JsFbx + "Branch001.fbx"));
        YatayKutuk("Kuru_Dal_2", new Vector3(-3.9f, 0f, -44.5f), -25f, 2.6f, Yukle(JsFbx + "Branch001.fbx"));

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/" + Bolum2SahneAdi + ".unity");
        Debug.Log("[Bolum] Bolum2_Gecit kuruldu + kaydedildi (8 kurt, duvarlı koridor, kümeli orman, anıtsal kapı).");

        // ── BÖLÜM 3: Köy Yerleşkesi — golden hour, sıcak, final boss ──
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.85f, 0.72f, 0.55f);
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 150f;
        RenderSettings.ambientLight = new Color(0.56f, 0.49f, 0.40f);

        ZeminMatesi("Assets/Mat_Zemin_Cayir.mat", new Color(0.95f, 0.97f, 0.9f), "T_GrassGround", 42f);
        GunesKur(new Vector3(18f, 55f, 0f), new Color(1f, 0.82f, 0.6f), 1.15f); // alçak sıcak güneş → uzun gölgeler

        var b3Player = OyuncuKur(playerC);
        KameraKur(b3Player);
        UiKur(b3Player.GetComponent<PlayerHealth>());
        KopekEkle(huskyModel, kopekC, kopekMat, b3Player);

        // Toprak patika — doğuştan köy girişine + köy içinde meydana
        ToprakYol("Patika", new Vector3(0f, 0f, 2f), 34f, ToprakMatesi());
        ToprakYama("Meydan", new Vector3(0f, 0.02f, -39f), 9f, 9f, 0f, ToprakMatesi());

        // Köyü saran orman: kümeler halinde, giriş koridoru (90°) açık — sıra halkası hissi yok
        for (int k = 0; k < 34; k++)
        {
            float derece = Rastgele(k, 81f) * 360f;
            float r = 26f + Rastgele(k, 83f) * 42f; // 26..68
            // giriş koridorunu açık tut: 90°±24 içinde ve r<50 olan kümeleri at
            if (Mathf.Abs(Mathf.DeltaAngle(derece, 90f)) < 24f && r < 50f) continue;
            float cx = Mathf.Cos(derece * Mathf.Deg2Rad) * r;
            float cz = Mathf.Sin(derece * Mathf.Deg2Rad) * r - 38f;
            int adet = 2 + (int)(Rastgele(k, 84f) * 4f);
            for (int j = 0; j < adet; j++)
            {
                float x = cx + (Rastgele(k * 10 + j, 85f) - 0.5f) * 7f;
                float z = cz + (Rastgele(k * 10 + j, 86f) - 0.5f) * 7f;
                float secim = Rastgele(k * 10 + j, 87f);
                string tur = secim < 0.55f ? "/Pine_" : "/CommonTree_";
                var agac = DusmanYerlestir(Yukle(NatureUnity + tur + ((k + j) % 5 + 1) + ".fbx"),
                    "Orman_" + k + "_" + j, new Vector3(x, 0.05f, z), Rastgele(k * 10 + j, 88f) * 360f,
                    5f + Rastgele(k * 10 + j, 89f) * 5f);
                Cesitle(agac, 0.85f, 1.2f, k * 100 + j);
            }
        }
        for (int k = 0; k < 7; k++) // iç vurgu kümeleri — çit ile ağaç duvarı arası boşluğu doldurur
        {
            float derece = Rastgele(k, 91f) * 360f;
            if (Mathf.Abs(Mathf.DeltaAngle(derece, 90f)) < 30f) continue;
            float aderece = (Rastgele(k, 92f) - 0.5f) * 22f;
            float ar = 26f + Rastgele(k, 93f) * 3f;
            var m2 = new Vector3(Mathf.Cos((derece + aderece) * Mathf.Deg2Rad) * ar, 0.05f,
                Mathf.Sin((derece + aderece) * Mathf.Deg2Rad) * ar - 38f);
            int adet = 2 + (int)(Rastgele(k, 94f) * 2f);
            for (int j = 0; j < adet; j++)
            {
                var agac = DusmanYerlestir(Yukle(NatureUnity + (j % 2 == 0 ? "/Pine_" : "/CommonTree_") + ((k + j) % 5 + 1) + ".fbx"),
                    "Ic_Kume_" + k + "_" + j,
                    m2 + new Vector3((Rastgele(k * 10 + j, 95f) - 0.5f) * 4f, 0f, (Rastgele(k * 10 + j, 96f) - 0.5f) * 4f),
                    Rastgele(k * 10 + j, 97f) * 360f, 5.5f + Rastgele(k * 10 + j, 98f) * 3.5f);
                Cesitle(agac, 0.85f, 1.15f, k * 77 + j);
            }
        }

        // Çit halkası — köyün çekirdeğini sarar (giriş koridoru açık); hafif düzensiz aralık/rot
        var citModel = Yukle(JsFbx + "Fence001.fbx");
        for (int i = 0; i < 36; i++)
        {
            float derece = i * 10f + (Rastgele(i, 111f) - 0.5f) * 4f;
            if (Mathf.Abs(Mathf.DeltaAngle(derece, 90f)) < 24f) continue;
            TekstuBagla(DusmanYerlestir(citModel, "Cit_" + i, YonelPos(derece, 22f + (Rastgele(i, 112f) - 0.5f) * 1.2f),
                derece + 90f + (Rastgele(i, 113f) - 0.5f) * 8f, 1.05f + Rastgele(i, 114f) * 0.25f), "T_Shelter");
        }

        // Zemin serpinti — YAMALAR halinde çim/çiçek/yonca/mantar (tekdüze serpme değil)
        string[] otlar = { "/Grass_Common_Short.fbx", "/Grass_Common_Tall.fbx", "/Clover_1.fbx", "/Clover_2.fbx",
            "/Flower_3_Single.fbx", "/Flower_4_Single.fbx", "/Mushroom_Common.fbx" };
        for (int p = 0; p < 20; p++)
        {
            var pMerkez = YonelPos(Rastgele(p, 121f) * 360f, 4f + Rastgele(p, 122f) * 19f);
            int adet = 3 + (int)(Rastgele(p, 123f) * 4f);
            for (int j = 0; j < adet; j++)
                DusmanYerlestir(Yukle(NatureUnity + otlar[(p + j) % 7]), "Ot_" + p + "_" + j,
                    pMerkez + new Vector3((Rastgele(p * 10 + j, 124f) - 0.5f) * 4.5f, 0f, (Rastgele(p * 10 + j, 125f) - 0.5f) * 4.5f),
                    Rastgele(p * 10 + j, 126f) * 360f, 0.3f + Rastgele(p * 10 + j, 127f) * 0.5f);
        }
        for (int i = 0; i < 10; i++)
            DusmanYerlestir(Yukle(NatureUnity + "/Pebble_Round_" + ((i % 5) + 1) + ".fbx"), "Cakil_" + i,
                YonelPos(Rastgele(i, 31f) * 360f, 5f + Rastgele(i, 32f) * 19f),
                Rastgele(i, 33f) * 360f, 0.3f + Rastgele(i, 34f) * 0.4f);

        // Yaban mersini çalıları + köy dekoru: ocak köşesi, odun yığınları, çadır eşyaları
        for (int i = 0; i < 8; i++)
        {
            float derece = 130f + i * 35f + (Rastgele(i, 41f) - 0.5f) * 14f;
            TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "BlueberryBush.fbx"), "Mersin_" + i,
                YonelPos(derece, 18f + Rastgele(i, 42f) * 4f), Rastgele(i, 43f) * 360f,
                0.8f + Rastgele(i, 44f) * 0.35f), "T_Shelter");
        }
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "CoockingRack.fbx"), "Ocak_Raf", new Vector3(2.2f, 0.05f, -37.2f), -30f, 1.5f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "PotLarge.fbx"), "Ocak_Tencere", new Vector3(1.4f, 0.05f, -38.2f), 0f, 0.5f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "ChopedWood.fbx"), "Odun_1", new Vector3(2.8f, 0.05f, -40.2f), 25f, 0.7f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "ChopedWood.fbx"), "Odun_2", new Vector3(-8.8f, 0.05f, -31.6f), -15f, 0.7f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "Backpack.fbx"), "Canta_1", new Vector3(8.6f, 0.05f, -33.4f), 40f, 0.6f), "T_Pallet");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "SleepingBag.fbx"), "Uyku_1", new Vector3(-6.6f, 0.05f, -42.6f), 120f, 0.4f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "Bucket.fbx"), "Kova", new Vector3(-2.2f, 0.05f, -35.6f), 0f, 0.5f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "Rock001.fbx"), "Kaya_Koy_1", new Vector3(5.5f, 0.05f, -43.5f), 70f, 0.9f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(JsFbx + "Rock002.fbx"), "Kaya_Koy_2", new Vector3(-5.8f, 0.05f, -30.4f), -130f, 1.2f), "T_Shelter");

        // Köy: çadırlar (rot/ölçek çeşitli) + ışıklı kamp ateşleri
        TekstuBagla(DusmanYerlestir(Yukle(TentFbx), "Cadir_1", new Vector3(-8f, 0.05f, -32f), 30f, 3f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(Tent2Fbx), "Cadir_2", new Vector3(8f, 0.05f, -34f), -25f, 3.1f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(TentFbx), "Cadir_3", new Vector3(-7f, 0.05f, -44f), 150f, 2.9f), "T_Shelter");
        TekstuBagla(DusmanYerlestir(Yukle(Tent2Fbx), "Cadir_4", new Vector3(7f, 0.05f, -46f), -155f, 3.05f), "T_Shelter");
        AtesKur(new Vector3(0f, 0.05f, -39f), 1.2f);
        AtesKur(new Vector3(-4.5f, 0.05f, -32.5f), 0.9f);

        // HERO: merkez ateş çemberi — kütük oturaklar + meşaleler (köyün kalbi)
        for (int i = 0; i < 4; i++)
            YatayKutuk("Kutuk_" + i, YonelPos(45f + i * 90f, 3.4f), 45f + i * 90f, 2.1f, Yukle(JsFbx + "TreeTrunk.fbx"));
        MesaleKur(YonelPos(10f, 5f), "Mesale_1");
        MesaleKur(YonelPos(170f, 5f), "Mesale_2");
        MesaleKur(YonelPos(250f, 5f), "Mesale_3");

        // Muhafız kurtlar
        var b3Kurt = new[]
        {
            new Vector3(-6f, 0.05f, -34f), new Vector3(6f, 0.05f, -35f),
            new Vector3(-6f, 0.05f, -43f), new Vector3(6f, 0.05f, -44f)
        };
        for (int i = 0; i < b3Kurt.Length; i++)
            KurtEkle(kurtModel, kurtC, kurtMat, OnekAd + "Kurt" + (i + 1), b3Kurt[i], i * 60f + 30f, 3, 10, 18f, 3.6f, b3Player);

        // HERO: şef avlusu — reisin alanı toprak avlu + kaya kuleleri + meşalelerle çerçevelenir
        ToprakYama("Sef_Avlu", new Vector3(0f, 0.02f, -53.5f), 7f, 7f, 0f, ToprakMatesi());
        DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_1.fbx"), "Sef_Kule_Sol", new Vector3(-3.6f, 0.05f, -56f), 40f, 4.6f);
        DusmanYerlestir(Yukle(NatureUnity + "/Rock_Medium_3.fbx"), "Sef_Kule_Sag", new Vector3(3.6f, 0.05f, -56f), 220f, 4.9f);
        MesaleKur(new Vector3(-2.4f, 0.05f, -50.6f), "Sef_Mesale_1");
        MesaleKur(new Vector3(2.4f, 0.05f, -50.6f), "Sef_Mesale_2");

        // KABİLE REİSİ — final: büyük, sert; ölünce balta + zafer mesajı
        var reisMat = MaterialKur(MatReisPath, new Color(0.16f, 0.09f, 0.22f, 1f)); // koyu mor boyalı
        InsanDusmanEkle(insanModel, sefC, reisMat, OnekAd + "Reis",
            new Vector3(0f, 0.05f, -52f), 0f, HedefBoy * 1.3f,
            20f, 3.2f, 2.6f, 1.8f, 20, 12, hatchet,
            "Kabile reisi yenildi! Tüm bölgeler temizlendi — OYUN TAMAMLANDI!", b3Player);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/" + Bolum3SahneAdi + ".unity");
        Debug.Log("[Bolum] Bolum3_Koy kuruldu + kaydedildi (4 muhafız + Reis; kümeli orman, toprak patika, ateş çemberi, şef avlusu).");

        // Sahne adlarını Build Settings'e yaz — geçişi arkadaş bunların üstüne kurar
        var sahneListesi = new List<EditorBuildSettingsScene>();
        foreach (var p in new[] { "Assets/Scenes/GameScene.unity", "Assets/Scenes/" + Bolum2SahneAdi + ".unity", "Assets/Scenes/" + Bolum3SahneAdi + ".unity" })
            if (System.IO.File.Exists(p)) sahneListesi.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = sahneListesi.ToArray();
        AssetDatabase.SaveAssets(); // kirli asset'leri diske göm: Mat_Prop_*, Reis rengi, balta/oyuncu dokuları

        Debug.Log("[Bolum] TAMAM — 3 sahne Build Settings'te. Aktif sahne Bolum3_Koy; ormana dönmek için GameScene.unity aç.");
    }

    static GameObject Yukle(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) Debug.LogWarning("[Bolum] Asset yok: " + path);
        return go;
    }

    // Deterministik sözde-rastgele 0..1 — menü her koştuğunda aynı düzen çıkar
    static float Rastgele(int i, float t)
    {
        return Mathf.Repeat(Mathf.Sin(i * 12.9898f + t * 78.233f) * 43758.5453f, 1f);
    }

    // Köy merkezi (0,-38) etrafında derece/yarıçap → dünya pozisyonu (y=0.05)
    static Vector3 YonelPos(float derece, float r)
    {
        float a = derece * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r - 38f);
    }

    // Görünmez çarpışma duvarı (renderer yok, yalnız BoxCollider) — CharacterController bunlara çarpar
    static void DuvarKur(Vector3 merkez, Vector3 boyut)
    {
        var go = new GameObject("Gorunmez_Duvar");
        go.transform.position = merkez;
        go.AddComponent<BoxCollider>().size = boyut;
    }

    // Dokulu zemin: kalıcı materyal asset (Default-Material'ı kirletmemek için) + plane
    static void ZeminMatesi(string matPath, Color tint, string dokuAdi, float tile)
    {
        var zemin = GameObject.CreatePrimitive(PrimitiveType.Plane);
        zemin.name = "Zemin";
        zemin.transform.localScale = new Vector3(20f, 1f, 20f); // 200×200 m
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, matPath); }
        var bul = AssetDatabase.FindAssets(dokuAdi + " t:Texture2D").FirstOrDefault();
        var tex = string.IsNullOrEmpty(bul) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(bul));
        if (tex != null)
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(tile, tile);
        }
        mat.color = tint;
        EditorUtility.SetDirty(mat);
        zemin.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Patika/yama ortak materyali (tek örnek — iki sahnede de aynı asset)
    static Material ToprakMatesi()
    {
        if (toprakMat != null) return toprakMat;
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Mat_ToprakYol.mat");
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, "Assets/Mat_ToprakYol.mat"); }
        var bul = AssetDatabase.FindAssets("T_DirtGround t:Texture2D").FirstOrDefault();
        var tex = string.IsNullOrEmpty(bul) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(bul));
        if (tex != null)
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(1.5f, 2f);
        }
        mat.color = new Color(0.95f, 0.92f, 0.88f);
        EditorUtility.SetDirty(mat);
        toprakMat = mat;
        return mat;
    }

    // -z yönüne uzanan toprak patika: örtüşen, hafif kıvrılan segmentler
    static void ToprakYol(string ad, Vector3 baslangic, float uzunluk, Material mat)
    {
        int adim = Mathf.CeilToInt(uzunluk / 5f);
        for (int i = 0; i < adim; i++)
            ToprakYama(ad + i,
                baslangic + new Vector3(Mathf.Sin(i * 1.9f) * 0.7f, 0.02f + (i % 2) * 0.004f, -i * 5f),
                3.6f, 6.5f, Mathf.Sin(i * 1.3f) * 9f, mat);
    }

    // Yükseltisiz toprak yaması — collider'ı da gölgesi de yok (CC takılmasın)
    static GameObject ToprakYama(string ad, Vector3 pos, float boyX, float boyZ, float rotY, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = ad;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rotY, 0f));
        go.transform.localScale = new Vector3(boyX / 10f, 1f, boyZ / 10f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    // Işıklı meşale (Torch.fbx + PointLight) — meydan/avluyu çerçeveleyen sıcak noktalar
    static void MesaleKur(Vector3 pos, string ad)
    {
        var t = DusmanYerlestir(Yukle(JsFbx + "Torch.fbx"), ad, pos, 0f, 1.7f);
        TekstuBagla(t, "T_Shelter");
        var l = t.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.65f, 0.35f);
        l.range = 8f; l.intensity = 1.2f;
    }

    // Yatay kütük: uzunluğa ölçekleyip yatırır (oturak/devrik dal) — yere sabitler
    static GameObject YatayKutuk(string ad, Vector3 pos, float rotY, float hedefUzunluk, GameObject model)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = ad;
        go.transform.position = pos;
        float boy = BoyOlc(go);
        go.transform.localScale = Vector3.one * (boy > 0.001f ? hedefUzunluk / boy : 1f);
        go.transform.eulerAngles = new Vector3(90f, rotY, 0f);
        float minY, maxY;
        BoyAraligi(go, out minY, out maxY);
        if (maxY > minY) go.transform.position += new Vector3(0f, 0.06f - minY, 0f);
        TekstuBagla(go, "T_Shelter");
        return go;
    }

    // Prop'a x/z dünya-ölçeği çeşitliliği (y dokunulmaz — yerden sabitlik bozulmaz)
    static void Cesitle(GameObject go, float minM, float maxM, int seed)
    {
        var s = go.transform.localScale;
        s.x *= Mathf.Lerp(minM, maxM, Rastgele(seed, 71f));
        s.z *= Mathf.Lerp(minM, maxM, Rastgele(seed, 72f));
        go.transform.localScale = s;
    }

    static void GunesKur(Vector3 acilar, Color renk, float siddet)
    {
        var go = new GameObject("Gunes", typeof(Light));
        var l = go.GetComponent<Light>();
        l.type = LightType.Directional; l.color = renk; l.intensity = siddet; l.shadows = LightShadows.Soft;
        go.transform.eulerAngles = acilar;
    }

    // Oyuncuyu FinishSetup'ın kalibrasyonuyla yeni sahneye koyar (spawn 0,0,2 — -z yönüne bakar)
    static GameObject OyuncuKur(RuntimeAnimatorController playerC)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<Avatar>().FirstOrDefault();
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx));
        player.name = PlayerName;
        player.transform.position = new Vector3(0f, 0.05f, 2f);
        player.transform.eulerAngles = new Vector3(0f, 180f, 0f);
        player.transform.localScale = Vector3.one;
        float hamBoy = BoyOlc(player);
        player.transform.localScale = Vector3.one * (hamBoy > 0.001f ? HedefBoy / hamBoy : 2.2f);
        AnimasyonBagla(player, playerC, avatar);

        var cc = player.GetComponent<CharacterController>();
        if (cc == null) cc = player.AddComponent<CharacterController>();
        cc.height = 4f; cc.radius = 0.66f; cc.center = new Vector3(0f, 2f, 0f);

        var pir = player.GetComponent<Yazlab.Player.PlayerInputReader>();
        if (pir == null) pir = player.AddComponent<Yazlab.Player.PlayerInputReader>();

        var pc = player.GetComponent<PlayerController>();
        if (pc == null) pc = player.AddComponent<PlayerController>();
        pc.walkSpeed = 4.5f; pc.runSpeed = 12f;

        var ph = player.GetComponent<PlayerHealth>();
        if (ph == null) ph = player.AddComponent<PlayerHealth>();
        ph.maxCan = 100; ph.can = 100;

        var pcb = player.GetComponent<PlayerCombat>();
        if (pcb == null) pcb = player.AddComponent<PlayerCombat>();
        pcb.InputReader = pir;
        pcb.menzil = 3.2f; pcb.aciDerece = 90f; pcb.vurusBeklemesi = 0.55f;
        pcb.vurulmaGecikmesi = 0.22f; pcb.hitStopSuresi = 0.06f;
        pcb.tabanHasar = 1; pcb.hasar = 1; pcb.baltaHasarCarpani = 2; pcb.debugKayit = true;

        player.tag = "Player";

        // Texture'sız kalan material'lara palet dokusu (FBX dış referanslı)
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
        return player;
    }

    static void KameraKur(GameObject oyuncu)
    {
        var camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        // Ormandakine göre biraz daha yakın: sahne dolu ve okunur görünür
        camGo.transform.position = new Vector3(0f, 7f, -9.5f);
        camGo.transform.eulerAngles = new Vector3(54f, 0f, 0f);
        var cam = camGo.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 500f;
        var follow = camGo.AddComponent<CameraFollow>();
        follow.target = oyuncu.transform;
        follow.offset = new Vector3(0f, 7f, -9.5f);
    }

    // KopekRun ile aynı kurulum; mevcut controller/materyali yükler
    static void KopekEkle(GameObject huskyModel, RuntimeAnimatorController c, Material mat, GameObject player)
    {
        var pos = player.transform.position + player.transform.forward * 2f + player.transform.right * 1.5f;
        pos.y = 0.05f;
        var kopek = DusmanYerlestir(huskyModel, KopekAdi, pos, player.transform.eulerAngles.y + 180f, KopekHedefBoy);
        AnimasyonBagla(kopek, c, null);
        var cc = kopek.GetComponent<CharacterController>();
        if (cc == null) cc = kopek.AddComponent<CharacterController>();
        float s = Mathf.Max(0.0001f, kopek.transform.localScale.x);
        cc.height = 1.0f / s; cc.radius = 0.3f / s; cc.center = new Vector3(0f, 0.5f / s, 0f);
        cc.stepOffset = 0f; cc.skinWidth = Mathf.Min(0.08f, cc.radius * 0.1f);
        kopek.AddComponent<CompanionController>();
        foreach (var r in kopek.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
    }

    static GameObject KurtEkle(GameObject model, RuntimeAnimatorController c, Material mat, string ad, Vector3 pos,
        float rotY, int can, int hasar, float algilama, float hiz, GameObject player = null)
    {
        var kurt = DusmanYerlestir(model, ad, pos, rotY, KurtHedefBoy);
        AnimasyonBagla(kurt, c, null);
        var cc = kurt.GetComponent<CharacterController>();
        if (cc == null) cc = kurt.AddComponent<CharacterController>();
        float ks = Mathf.Max(0.0001f, kurt.transform.localScale.x);
        cc.height = 1.5f / ks; cc.radius = 0.35f / ks; cc.center = new Vector3(0f, 0.75f / ks, 0f);
        cc.stepOffset = 0f; cc.skinWidth = Mathf.Min(0.08f, cc.radius * 0.1f);
        var ec = kurt.AddComponent<EnemyController>();
        ec.algilamaMesafesi = algilama; ec.hiz = hiz; ec.saldiriMesafesi = 1.6f;
        ec.saldiriBeklemesi = 1.4f; ec.hasar = hasar; ec.can = can; ec.debugKayit = true;
        if (player != null) ec.SetPlayerTarget(player.transform);
        foreach (var r in kurt.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
        return kurt;
    }

    // İnsan düşman (şef/reis) — CC ve animasyon deseni orman şefiyle aynı
    static GameObject InsanDusmanEkle(GameObject model, RuntimeAnimatorController c, Material mat, string ad, Vector3 pos,
        float rotY, float hedefBoy, float algilama, float hiz, float saldiriMesafe, float bekleme, int hasar, int can,
        GameObject drop, string olumMesaji, GameObject player = null)
    {
        var avatar = AssetDatabase.LoadAllAssetsAtPath(CharFbx).OfType<Avatar>().FirstOrDefault();
        var go = DusmanYerlestir(model, ad, pos, rotY, hedefBoy);
        AnimasyonBagla(go, c, avatar);
        var cc = go.GetComponent<CharacterController>();
        if (cc == null) cc = go.AddComponent<CharacterController>();
        float s = Mathf.Max(0.0001f, go.transform.localScale.x);
        cc.height = 4f / s; cc.radius = 0.66f / s; cc.center = new Vector3(0f, 2f / s, 0f);
        cc.stepOffset = 0f; cc.skinWidth = Mathf.Min(0.08f, cc.radius * 0.1f);
        var ec = go.AddComponent<EnemyController>();
        ec.algilamaMesafesi = algilama; ec.hiz = hiz; ec.saldiriMesafesi = saldiriMesafe;
        ec.saldiriBeklemesi = bekleme; ec.hasar = hasar; ec.can = can; ec.debugKayit = true;
        ec.olumDrop = drop; ec.olumMesaji = olumMesaji;
        if (player != null) ec.SetPlayerTarget(player.transform);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
        return go;
    }

    static void AtesKur(Vector3 pos, float boy)
    {
        var ates = DusmanYerlestir(Yukle(CampfireFbx), "Kamp_Atesi", pos, 0f, boy);
        TekstuBagla(ates, "T_Campfire");
        var isik = ates.AddComponent<Light>();
        isik.type = LightType.Point; isik.color = new Color(1f, 0.6f, 0.3f);
        isik.range = 12f * boy; isik.intensity = 1.8f;
    }

    static readonly System.Collections.Generic.Dictionary<string, Material> propMatCache =
        new System.Collections.Generic.Dictionary<string, Material>();

    static Material PropMatesi(Material kaynak, Texture2D tex)
    {
        if (AssetDatabase.Contains(kaynak) && !AssetDatabase.IsSubAsset(kaynak))
        {
            if (kaynak.mainTexture == null) { kaynak.mainTexture = tex; EditorUtility.SetDirty(kaynak); }
            return kaynak;
        }
        string ad = "Mat_Prop_" + kaynak.name.Replace(' ', '_');
        Material m;
        if (propMatCache.TryGetValue(ad, out m) && m != null) return m;
        m = AssetDatabase.LoadAssetAtPath<Material>("Assets/" + ad + ".mat");
        if (m == null)
        {
            m = new Material(kaynak);
            m.name = ad;
            AssetDatabase.CreateAsset(m, "Assets/" + ad + ".mat");
        }
        if (m.mainTexture == null) { m.mainTexture = tex; EditorUtility.SetDirty(m); }
        propMatCache[ad] = m;
        return m;
    }

    static void TekstuBagla(GameObject go, string dokuAdi)
    {
        var bul = AssetDatabase.FindAssets(dokuAdi + " t:Texture2D").FirstOrDefault();
        var tex = string.IsNullOrEmpty(bul) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(bul));
        if (tex == null) { Debug.LogWarning("[Bolum] Doku bulunamadı: " + dokuAdi); return; }
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            bool degisti = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.mainTexture == null || AssetDatabase.IsSubAsset(m))
                {
                    mats[i] = PropMatesi(m, tex);
                    degisti = true;
                }
            }
            if (degisti) r.sharedMaterials = mats;
        }
    }
}
