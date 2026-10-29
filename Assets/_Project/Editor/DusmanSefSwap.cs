using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ComponentUtility = UnityEditorInternal.ComponentUtility;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// GameScene'deki <c>Dusman_Sef</c> (kabile şefi — eski kırmızı SurviverMale klonu) modelini
    /// <c>Ch05_nonPBR.fbx</c> ile değiştirir (idempotent). Şefin ölçülmüş boyu korunur (≈4.8 —
    /// oyuncunun 1.15 katı); <see cref="EnemyController"/> değerleri (can 6, hasar 15, olumDrop =
    /// Hatchet, "Bölge temizlendi!" mesajı, oyuncu hedefi) serileştirilmiş alan kopyasıyla taşınır;
    /// CharacterController yeni kök ölçeğinden yeniden türetilir (CC transform ölçeğini takip etmez);
    /// Animator'a yeni Humanoid avatar bağlanır (DusmanAnimator.controller klipleri retarget ile
    /// oturur). FBX'in materyal→doku bağlantıları kopuk exportlandığı için doku eşleşmesi elle
    /// yapıldı (Ch05_Dokular/ altındaki ayrıştırılmış atlaslar) ve kalıcı materyallere yazılır.
    /// Sahne kaydedilir.
    /// </summary>
    public static class DusmanSefSwap
    {
        private const string YeniFbx = "Assets/Just_Survive/Karakter/Ch05_nonPBR.fbx";
        private const string SefAd = "Dusman_Sef";
        private const string MatKlasor = "Assets/Just_Survive/Karakter";
        private const string DokuKlasor = MatKlasor + "/Ch05_Dokular";
        private const float YedekBoy = 4.807f; // 4.18 × 1.15 — eski şef boyu (ölçüm başarısızsa)

        // FBX materyal adı → (diffuse, normal). Bağlantılar FBX'te kopuk (materyal: YOK);
        // atlaslar görsel olarak ayıklandı: file3=gövde, file5=giyim/ekipman, file18=kafa/maske.
        private static readonly Dictionary<string, (string diffuse, string normal)> SlotDokulari =
            new Dictionary<string, (string, string)>
            {
                { "Ch05_body", ("Ch05_Body", "Ch05_Body_Normal") },
                { "Ch05_body1", ("Ch05_Giyim", "Ch05_Giyim_Normal") },
                { "Ch05_body2", ("Ch05_Kafa", "Ch05_Kafa_Normal") },
            };

        /// <summary>Aktif sahnede şefi Ch05 modeliyle değiştirir ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Sef Modeli Degistir (Ch05)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[SefDegisim] Play modunda sahne değiştirilemez; Play'den çık ve tekrar çalıştır.");
                return;
            }

            // 1) FBX'i Humanoid'e çevir (Unity 6'da enum üyesi yok → sabit 3).
            var imp = AssetImporter.GetAtPath(YeniFbx) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError($"[SefDegisim] FBX bulunamadı: {YeniFbx} (ilk Unity odaklanmasında import edilir).");
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
                Debug.LogError("[SefDegisim] Model veya Humanoid avatar bulunamadı (import tamamlanmış mı?).");
                return;
            }

            var eski = GameObject.Find(SefAd);
            if (eski == null)
            {
                Debug.LogError($"[SefDegisim] Sahnedeki '{SefAd}' bulunamadı — GameScene açık mı? ('Yazlab/Dusmanlari Kur' çalıştırılmış mı?)");
                return;
            }

            // 2) Yeni instance: poz/rot eski şeften; boy eski şefin ölçülmüş boyuna ölçeklenir
            // (boss büyüklüğü — oyuncunun 1.15 katı — korunur, denge bozulmaz).
            var yeni = (GameObject)PrefabUtility.InstantiatePrefab(model);
            yeni.transform.SetPositionAndRotation(eski.transform.position, eski.transform.rotation);
            yeni.transform.localScale = Vector3.one;
            float hedefBoy = BoyOlc(eski);
            if (hedefBoy <= 0.01f) hedefBoy = YedekBoy;
            float hamBoy = BoyOlc(yeni);
            float olcek = hamBoy > 0.001f ? hedefBoy / hamBoy : 1f;
            yeni.transform.localScale = Vector3.one * olcek;
            Debug.Log($"[SefDegisim] Eski boy {hedefBoy:0.00}, Ch05 ham boy {hamBoy:0.00} → ölçek {olcek:0.000}");

            // 3) Bileşen taşı: Animator (FBX'te hazır → değer kopyala), EnemyController (değerlerle
            // yeni ekle: can/hasar/olumDrop/olumMesajı/oyuncu hedefi serileştirilmiş alanlardan gelir).
            // CharacterController KOPYALANMAZ — yeni ölçekten yeniden türetilir (adım 4).
            foreach (var c in eski.GetComponents<Component>())
            {
                if (c == null || c is Transform || c is CharacterController) continue;
                ComponentUtility.CopyComponent(c);
                var mevcut = yeni.GetComponent(c.GetType());
                if (mevcut != null) ComponentUtility.PasteComponentValues(mevcut);
                else ComponentUtility.PasteComponentAsNew(yeni);
            }

            // 4) CharacterController: DusmanKur'un formülü — CC kök ölçeğini takip etmediği için
            // değerler ölçeğe bölünür (hedef: ölçekli düzende 4 boy / 0.66 yarıçap / 2 merkez).
            var scc = yeni.GetComponent<CharacterController>();
            if (scc == null) scc = yeni.AddComponent<CharacterController>();
            float s = Mathf.Max(0.0001f, yeni.transform.localScale.x);
            scc.height = 4f / s;
            scc.radius = 0.66f / s;
            scc.center = new Vector3(0f, 2f / s, 0f);
            scc.stepOffset = 0f;
            scc.skinWidth = Mathf.Min(0.08f, scc.radius * 0.1f);

            // 5) Animator: kopyalanan eski avatar yerine Ch05 avatarı (controller kopyadan gelir).
            var anim = yeni.GetComponent<Animator>();
            if (anim == null)
            {
                Debug.LogError("[SefDegisim] Yeni modelde Animator yok — işlem geri alınıyor.");
                Object.DestroyImmediate(yeni);
                return;
            }
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            if (anim.runtimeAnimatorController == null)
                Debug.LogError("[SefDegisim] Controller kopyalanmadı — eski şefte Animator controller'ı yoktu mu?");

            // 6) Ad/tag/layer: diğer scriptlerin Find'ları bozulmasın.
            yeni.name = SefAd;
            yeni.tag = eski.tag;
            foreach (var t in yeni.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = eski.layer;

            // 7) Materyaller: FBX slot adına göre atlas eşleşmesi (normaller dahil).
            MateryalleriKur(yeni);

            Object.DestroyImmediate(eski);
            EditorUtility.SetDirty(yeni);
            EditorSceneManager.MarkSceneDirty(yeni.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SefDegisim] Dusman_Sef Ch05 ile değiştirildi (boy {hedefBoy:0.00}, avatar {avatar.name}). Sahne kaydedildi.");
        }

        // Her renderer slotunu, prefab kaynağından okunan FBX materyal adına göre kalıcı materyale bağlar.
        // (Önceki değişimden kalan flat Mat_Ch05 slotları da kaynak adı üzerinden düzelir.)
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
                        ? KaliciMateryal("Mat_Ch05_" + slotAd, dokuAdi.diffuse, dokuAdi.normal)
                        : (mats[i] == null || mats[i].mainTexture == null || mats[i].name == "Mat_Ch05"
                            ? KaliciMateryal("Mat_Ch05_Yedek", "Ch05_Body", null) // bilinmeyen/dokusuz/eski düz mat slotu
                            : null);
                    if (hedef != null && mats[i] != hedef) { mats[i] = hedef; degisti = true; }
                }
                if (degisti) r.sharedMaterials = mats;
            }
        }

        // Normal dokuların import tipini NormalMap'e çevirir (Standard _BumpMap bunu ister).
        private static void NormalleriAyarla()
        {
            foreach (var ad in new[] { "Ch05_Body_Normal", "Ch05_Giyim_Normal", "Ch05_Kafa_Normal" })
            {
                string yol = DokuKlasor + "/" + ad + ".png";
                var ti = AssetImporter.GetAtPath(yol) as TextureImporter;
                if (ti == null)
                {
                    Debug.LogWarning($"[SefDegisim] Normal dokusu yok: {yol} (ilk importta bir kez daha çalıştır)");
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
            if (diffuse != null) m.mainTexture = diffuse;
            if (!string.IsNullOrEmpty(normalAd))
            {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/" + normalAd + ".png");
                if (normal != null) m.SetTexture("_BumpMap", normal);
            }
            m.SetFloat("_Glossiness", 0.15f); // deri/kumaş için kısık parlama
            AssetDatabase.CreateAsset(m, yol);
            Debug.Log($"[SefDegisim] Materyal kuruldu: {matAd} (diffuse: {diffuseAd}{(normalAd != null ? ", normal: " + normalAd : "")})");
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
