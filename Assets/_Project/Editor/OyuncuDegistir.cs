using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ComponentUtility = UnityEditorInternal.ComponentUtility;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// GameScene'deki oyuncuyu (<c>SurviverMale</c> prefab instance'ı — tag Player) <c>Oyuncu/Dreyar.fbx</c>
    /// (Mixamo rig'li) ile değiştirir (idempotent). Oyuncunun ölçülmüş boyu korunur (dünya ≈4.17, eski
    /// ölçek 2.2896); <c>PlayerController</c>/<c>PlayerCombat</c>/<c>PlayerHealth</c>/<c>PlayerInputReader</c>/
    /// <c>CompanionCommander</c> vb. tüm bileşenler serileştirilmiş alan kopyasıyla taşınır;
    /// CharacterController yeni ölçekten yeniden türetilir (CC transform ölçeğini takip etmez; dünya hedefi
    /// 4 boy / 0.66 yarıçap / 2 merkez — eski instance'ın değerleri); Animator'a Dreyar'ın Humanoid avatarı
    /// bağlanır (PlayerAnimator.controller klipleri retarget ile oturur; AxePickup zaten "RightHand" adıyla
    /// mixamo kemiğini bulur). Sahnedeki TÜM serialized oyuncu referansları (_playerTransform, _target, _health,
    /// _combat...) eski→yeni eşlemesiyle yeniden bağlanır. Sahne kaydedilir.
    /// </summary>
    public static class OyuncuDegistir
    {
        private const string YeniFbx = "Assets/Oyuncu/Dreyar.fbx";
        private const string DokuKlasor = "Assets/Oyuncu/Dokular";
        private const string MatKlasor = "Assets/Oyuncu";

        /// <summary>GameScene'deki oyuncuyu Dreyar modeliyle değiştirir ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Oyuncu Modeli Degistir (Dreyar)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[OyuncuDegisim] Play modunda sahne değiştirilemez; Play'den çık ve tekrar çalıştır.");
                return;
            }

            // 1) FBX'i Humanoid'e çevir (Unity 6'da enum üyesi yok → sabit 3).
            var imp = AssetImporter.GetAtPath(YeniFbx) as ModelImporter;
            if (imp == null)
            {
                Debug.LogError($"[OyuncuDegisim] FBX bulunamadı: {YeniFbx} (ilk Unity odaklanmasında import edilir).");
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
                Debug.LogError("[OyuncuDegisim] Model veya Humanoid avatar bulunamadı.");
                return;
            }

            var eski = GameObject.FindGameObjectWithTag("Player");
            if (eski == null) eski = GameObject.Find("SurviverMale");
            if (eski == null)
            {
                Debug.LogError("[OyuncuDegisim] Tag'i Player olan nesne bulunamadı — GameScene açık mı?");
                return;
            }

            // 2) Yeni instance: poz/rot eski oyuncudan; boy eski oyuncunun ölçülmüş boyuna ölçeklenir.
            var yeni = (GameObject)PrefabUtility.InstantiatePrefab(model);
            yeni.transform.SetPositionAndRotation(eski.transform.position, eski.transform.rotation);
            yeni.transform.localScale = Vector3.one;
            float hedefBoy = BoyOlc(eski);
            float hamBoy = BoyOlc(yeni);
            float olcek = hamBoy > 0.001f && hedefBoy > 0.01f ? hedefBoy / hamBoy : 1f;
            yeni.transform.localScale = Vector3.one * olcek;
            Debug.Log($"[OyuncuDegisim] Eski boy {hedefBoy:0.00}, Dreyar ham boy {hamBoy:0.00} → ölçek {olcek:0.000}");

            // 3) Bileşen taşı + eski→yeni eşlemesi kur (referans rewiring bunu kullanır).
            var esle = new Dictionary<Object, Object> { { eski.transform, yeni.transform } };
            foreach (var c in eski.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                ComponentUtility.CopyComponent(c);
                var mevcut = yeni.GetComponent(c.GetType());
                if (mevcut != null) ComponentUtility.PasteComponentValues(mevcut);
                else ComponentUtility.PasteComponentAsNew(yeni);
                esle[c] = yeni.GetComponent(c.GetType()); // CharacterController dahil — CC referansı veren varsa
            }

            // 4) CharacterController: dünya hedefi 4 boy / 0.66 yarıçap / 2 merkez (eski instance ile aynı);
            // CC kök ölçeğini takip etmediği için değerler yeni ölçeğe bölünür.
            var pcc = yeni.GetComponent<CharacterController>();
            if (pcc == null) pcc = yeni.AddComponent<CharacterController>();
            float s = Mathf.Max(0.0001f, yeni.transform.localScale.x);
            pcc.height = 4f / s;
            pcc.radius = 0.66f / s;
            pcc.center = new Vector3(0f, 2f / s, 0f);
            pcc.stepOffset = 0f;
            pcc.skinWidth = Mathf.Min(0.08f, pcc.radius * 0.1f);

            // 5) Animator: kopyalanan eski avatar yerine Dreyar avatarı (controller kopyadan gelir).
            var anim = yeni.GetComponent<Animator>();
            if (anim == null)
            {
                Debug.LogError("[OyuncuDegisim] Yeni modelde Animator yok — işlem geri alınıyor.");
                Object.DestroyImmediate(yeni);
                return;
            }
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            if (anim.runtimeAnimatorController == null)
                Debug.LogError("[OyuncuDegisim] Controller kopyalanmadı — eski oyuncuda Animator controller'ı yoktu mu?");

            // 6) Ad/tag/layer: AxePickup'ın GameObject.Find("SurviverMale") yedeği ve tag aramaları bozulmasın.
            yeni.name = eski.name;
            yeni.tag = eski.tag;
            foreach (var t in yeni.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = eski.layer;

            // 7) Materyaller: tek slot (dreyar_M3) → palet yerine kendi diffuse/normal dokuları.
            MateryalleriKur(yeni);

            // 8) Sahnedeki tüm serialized referansları eski oyuncudan yeniye çevir (kamera, HUD, GameManager,
            // düşmanların _playerTransform'ları...).
            int rewired = ReferanslariTasi(esle);

            Object.DestroyImmediate(eski);
            EditorUtility.SetDirty(yeni);
            EditorSceneManager.MarkSceneDirty(yeni.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[OyuncuDegisim] Oyuncu Dreyar ile değiştirildi (boy {hedefBoy:0.00}, avatar {avatar.name}, {rewired} bileşende referans yenilendi). Sahne kaydedildi.");
        }

        // Yüklenen sahnede tüm MonoBehaviour'ların serialized alanlarını gez; eski→yeni eşlemesine denk
        // gelen her objectReference'i değiştir (Transform dahil, dizi elemanları dahil).
        private static int ReferanslariTasi(Dictionary<Object, Object> esle)
        {
            int adet = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool degisti = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null)
                    {
                        continue;
                    }
                    if (esle.TryGetValue(it.objectReferenceValue, out var yeniDeger))
                    {
                        it.objectReferenceValue = yeniDeger;
                        degisti = true;
                    }
                }
                if (degisti)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    adet++;
                }
            }
            return adet;
        }

        // Tek materyallı model: her renderer slotuna kalıcı Dreyar materyalini bağlar (normaller dahil).
        private static void MateryalleriKur(GameObject go)
        {
            string yol = DokuKlasor + "/Dreyar_normal.png";
            var ti = AssetImporter.GetAtPath(yol) as TextureImporter;
            if (ti == null) Debug.LogWarning($"[OyuncuDegisim] Normal dokusu yok: {yol}");
            else if (ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }

            var materyal = KaliciMateryal();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = materyal;
                r.sharedMaterials = mats;
            }
        }

        // Diffuse + normal (+specular yok sayılır) dokulu kalıcı materyal — idempotent.
        private static Material KaliciMateryal()
        {
            const string matYol = MatKlasor + "/Mat_Dreyar.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matYol);
            if (m != null) return m;

            m = new Material(Shader.Find("Standard")) { name = "Mat_Dreyar" };
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/Dreyar_diffuse.png");
            if (diffuse != null) m.mainTexture = diffuse;
            else Debug.LogWarning("[OyuncuDegisim] Dreyar_diffuse.png yok — materyal dokusuz kuruldu.");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/Dreyar_normal.png");
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetFloat("_Glossiness", 0.15f);
            AssetDatabase.CreateAsset(m, matYol);
            Debug.Log("[OyuncuDegisim] Materyal kuruldu: Mat_Dreyar (diffuse + normal)");
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
