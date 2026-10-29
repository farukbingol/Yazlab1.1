using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ComponentUtility = UnityEditorInternal.ComponentUtility;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Bolum3_Koy sahnesindeki <c>Dusman_Reis</c> (Kabile Reisi — eski kırmızı SurviverMale prefab'ı) modelini
    /// <c>Karakterler/Brute.fbx</b> (MaleBruteA — elinde balta) ile değiştirir (idempotent). Reis'in ölçülmüş boyu
    /// korunur (dev patron, eski ölçek 2.976); <see cref="EnemyController"/> değerleri (ReisEnemyConfig, hasar 20,
    /// Hatchet drop, "Kabile reisi yenildi!..." mesajı) serileştirilmiş alan kopyasıyla taşınır;
    /// CharacterController yeni kök ölçeğinden yeniden türetilir (CC transform ölçeğini takip etmez);
    /// Animator'a Brute'un Humanoid avatarı bağlanır (DusmanAnimator.controller klipleri retarget ile oturur).
    /// FBX'in doku bağlantıları kopuk exportlandığı için Brute_Dokular/ altındaki atlaslarla kalıcı
    /// Mat_Brute_* materyalleri kurulur. BossHealthBar._boss referansı yeni bileşene yeniden bağlanır.
    /// Sahne kaydedilir.
    /// </summary>
    public static class DusmanReisSwap
    {
        private const string YeniFbx = "Assets/Karakterler/Brute.fbx";
        private const string ReisAd = "Dusman_Reis";
        private const string SahneYolu = "Assets/Scenes/Bolum3_Koy.unity";
        private const string MatKlasor = "Assets/Karakterler";
        private const string DokuKlasor = MatKlasor + "/Brute_Dokular";
        private const float YedekBoy = 4.807f; // eski reis boyu (ölçüm başarısızsa)

        // FBX materyal adı → (diffuse, normal). Bağlantılar FBX'te kopuk (video→materyal bağlantısı YOK);
        // gömülü dokular FBX'ten ayrıştırıldı: file27=gövde, file28=gövde normali, file29=yüz/göz/diş atlası,
        // file32=ayakkabı, F2=balta, kalanları adlı atlaslar (Hair/Bottom/Shoes/Moustache F=diffuse BM=normal F9=spec).
        private static readonly Dictionary<string, (string diffuse, string normal)> SlotDokulari =
            new Dictionary<string, (string, string)>
            {
                { "Body_MAT4", ("MaleBruteA_Body_diffuse", "MaleBruteA_StdNM") },
                { "EyeSpec_MAT2", ("Brute_Yuz", null) }, // gövde altındaki yüz/diş alt mesh'i
                { "EyeSpec_MAT4", ("Brute_Yuz", null) }, // göz küreleri
                { "Lashes_MAT4", ("Brute_Yuz", null) }, // kirpik şeridi aynı atlasın içinde
                { "MaleBruteA_Hair6", ("MaleBruteA_Hair_diffuse", "MaleBruteA_Hair_normal") },
                { "MaleBruteA_Bottom5", ("MaleBruteA_Bottom_diffuse1", "MaleBruteA_Bottom_normal") },
                { "MaleBruteA_Shoes6", ("MaleBruteA_Shoes_diffuse1", "MaleBruteA_Shoes_normal") },
                { "MaleBruteA_Moustache5", ("MaleBruteA_Moustache_diffuse", "MaleBruteA_Moustache_normal") },
                { "phong4", ("F2", null) }, // küpeler — balta metaliyle kaplanır
                { "blinn7", ("F2", "axe_NM2") }, // BattleAxe
            };

        /// <summary>Bolum3_Koy'daki reisi Brute modeliyle değiştirir ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Reis Modeli Degistir (Brute)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[ReisDegisim] Play modunda sahne değiştirilemez; Play'den çık ve tekrar çalıştır.");
                return;
            }

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != SahneYolu)
            {
                EditorSceneManager.OpenScene(SahneYolu);
            }

            // 1) FBX'i Humanoid'e çevir (Unity 6'da enum üyesi yok → sabit 3).
            var imp = AssetImporter.GetAtPath(YeniFbx) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError($"[ReisDegisim] FBX bulunamadı: {YeniFbx}");
                return;
            }
            if (imp.animationType != (ModelImporterAnimationType)3)
            {
                imp.animationType = (ModelImporterAnimationType)3;
                imp.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(YeniFbx);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(YeniFbx).OfType<Avatar>().FirstOrDefault();
            if (model == null || avatar == null)
            {
                Debug.LogError("[ReisDegisim] Model veya Humanoid avatar bulunamadı.");
                return;
            }

            var eski = GameObject.Find(ReisAd);
            if (eski == null)
            {
                Debug.LogError($"[ReisDegisim] Sahnedeki '{ReisAd}' bulunamadı — Bolum3_Koy açılamadı mı?");
                return;
            }

            // 2) Yeni instance: poz/rot eski reisten; boy eski reisin ölçülmüş boyuna ölçeklenir
            // (dev patron büyüklüğü korunur, denge bozulmaz).
            var yeni = (GameObject)PrefabUtility.InstantiatePrefab(model);
            yeni.transform.SetPositionAndRotation(eski.transform.position, eski.transform.rotation);
            yeni.transform.localScale = Vector3.one;
            float hedefBoy = BoyOlc(eski);
            if (hedefBoy <= 0.01f) hedefBoy = YedekBoy;
            float hamBoy = BoyOlc(yeni);
            float olcek = hamBoy > 0.001f ? hedefBoy / hamBoy : 1f;
            yeni.transform.localScale = Vector3.one * olcek;
            Debug.Log($"[ReisDegisim] Eski boy {hedefBoy:0.00}, Brute ham boy {hamBoy:0.00} → ölçek {olcek:0.000}");

            // 3) Bileşen taşı: Animator (FBX'te hazır → değer kopyala: controller DusmanAnimator), EnemyController
            // (değerlerle yeni ekle: config/drop/mesaj serileştirilmiş alanlardan gelir). CharacterController
            // KOPYALANMAZ — yeni ölçekten yeniden türetilir (adım 4).
            foreach (var c in eski.GetComponents<Component>())
            {
                if (c == null || c is Transform || c is CharacterController) continue;
                ComponentUtility.CopyComponent(c);
                var mevcut = yeni.GetComponent(c.GetType());
                if (mevcut != null) ComponentUtility.PasteComponentValues(mevcut);
                else ComponentUtility.PasteComponentAsNew(yeni);
            }

            // 4) CharacterController: eski instance'ın dünyadaki hedefi — 4 boy / 0.66 yarıçap / 2 merkez
            // (eski ham değerler × 2.976 ölçek tam olarak bunları veriyordu); CC kök ölçeğini takip etmediği
            // için değerler yeni ölçeğe bölünür.
            var rcc = yeni.GetComponent<CharacterController>();
            if (rcc == null) rcc = yeni.AddComponent<CharacterController>();
            float s = Mathf.Max(0.0001f, yeni.transform.localScale.x);
            rcc.height = 4f / s;
            rcc.radius = 0.66f / s;
            rcc.center = new Vector3(0f, 2f / s, 0f);
            rcc.stepOffset = 0f;
            rcc.skinWidth = Mathf.Min(0.08f, rcc.radius * 0.1f);

            // 5) Animator: kopyalanan eski avatar yerine Brute avatarı (controller kopyadan gelir).
            var anim = yeni.GetComponent<Animator>();
            if (anim == null)
            {
                Debug.LogError("[ReisDegisim] Yeni modelde Animator yok — işlem geri alınıyor.");
                Object.DestroyImmediate(yeni);
                return;
            }
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            if (anim.runtimeAnimatorController == null)
                Debug.LogError("[ReisDegisim] Controller kopyalanmadı — eski reiste Animator controller'ı yoktu mu?");

            // 6) Ad/tag/layer: IntegrationSetup'ın GameObject.Find("Dusman_Reis") araması bozulmasın.
            yeni.name = ReisAd;
            yeni.tag = eski.tag;
            foreach (var t in yeni.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = eski.layer;

            // 7) Materyaller: FBX slot adına göre atlas eşleşmesi (normaller dahil).
            MateryalleriKur(yeni);

            // 8) BossHealthBar._boss sahnedeki eski bileşeni işaret ediyordu — yeni EnemyController'a bağla.
            var yeniReis = yeni.GetComponent<EnemyController>();
            var bossBar = Object.FindFirstObjectByType<Yazlab.UI.BossHealthBar>();
            if (bossBar != null && yeniReis != null)
            {
                var so = new SerializedObject(bossBar);
                so.FindProperty("_boss").objectReferenceValue = yeniReis;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (yeniReis == null)
            {
                Debug.LogError("[ReisDegisim] EnemyController taşınamadı — boss barı ve ölüm akışı çalışmaz!");
            }

            Object.DestroyImmediate(eski);
            EditorUtility.SetDirty(yeni);
            EditorSceneManager.MarkSceneDirty(yeni.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ReisDegisim] Dusman_Reis Brute ile değiştirildi (boy {hedefBoy:0.00}, avatar {avatar.name}). Sahne kaydedildi.");
        }

        // Her renderer slotunu, prefab kaynağından okunan FBX materyal adına göre kalıcı materyale bağlar.
        private static void MateryalleriKur(GameObject go)
        {
            NormalleriAyarla();

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var kaynak = PrefabUtility.GetCorrespondingObjectFromSource(r);
                var orijinaller = kaynak != null ? kaynak.sharedMaterials : r.sharedMaterials;
                var mats = r.sharedMaterials;
                bool degisti = false;
                for (int i = 0; i < mats.Length && i < orijinaller.Length; i++)
                {
                    string slotAd = orijinaller[i] != null ? orijinaller[i].name
                                  : (mats[i] != null && AssetDatabase.IsSubAsset(mats[i]) ? mats[i].name : null);
                    if (slotAd == null) continue;

                    Material hedef = SlotDokulari.TryGetValue(slotAd, out var dokuAdi)
                        ? KaliciMateryal("Mat_Brute_" + slotAd, dokuAdi.diffuse, dokuAdi.normal)
                        : (mats[i] == null || mats[i].mainTexture == null || mats[i].name == "Mat_Brute"
                            ? KaliciMateryal("Mat_Brute_Yedek", "MaleBruteA_Body_diffuse", null) // bilinmeyen/dokusuz slot
                            : null);
                    if (hedef != null && mats[i] != hedef) { mats[i] = hedef; degisti = true; }
                }
                if (degisti) r.sharedMaterials = mats;
            }
        }

        // Normal dokuların import tipini NormalMap'e çevirir (Standard _BumpMap bunu ister).
        private static void NormalleriAyarla()
        {
            foreach (var ad in new[]
            {
                "MaleBruteA_StdNM", "MaleBruteA_Bottom_normal", "MaleBruteA_Hair_normal",
                "MaleBruteA_Shoes_normal", "MaleBruteA_Moustache_normal", "axe_NM2",
            })
            {
                string yol = DokuKlasor + "/" + ad + ".png";
                var ti = AssetImporter.GetAtPath(yol) as TextureImporter;
                if (ti == null)
                {
                    Debug.LogWarning($"[ReisDegisim] Normal dokusu yok: {yol} (ilk importta bir kez daha çalıştır)");
                    continue;
                }
                if (ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
        }

        // Diffuse (+opsiyonel normal) dokulu kalıcı materyal — sub-asset mutasyonu kalıcı olmadığından asset.
        private static Material KaliciMateryal(string matAd, string diffuseAd, string normalAd)
        {
            string yol = MatKlasor + "/" + matAd + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (m != null) return m;

            m = new Material(Shader.Find("Standard")) { name = matAd };
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/" + diffuseAd + ".png");
            if (diffuse == null) // ayakkabı/pantolon diffuse'ları jpg kaynaklı
                diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/" + diffuseAd + ".jpg");
            if (diffuse != null) m.mainTexture = diffuse;
            if (!string.IsNullOrEmpty(normalAd))
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/" + normalAd + ".png");
                if (normal != null) m.SetTexture("_BumpMap", normal);
            }
            m.SetFloat("_Glossiness", 0.15f); // deri/kumaş için kısık parlama
            AssetDatabase.CreateAsset(m, yol);
            Debug.Log($"[ReisDegisim] Materyal kuruldu: {matAd} (diffuse: {diffuseAd}{(normalAd != null ? ", normal: " + normalAd : "")})");
            return m;
        }

        // Tüm renderer'ların y-ekseni toplam boyu.
        private static float BoyOlc(GameObject go)
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
}
