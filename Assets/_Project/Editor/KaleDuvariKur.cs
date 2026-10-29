using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Setup_Bolum2/Walls altındaki toprak kayalık duvar görsellerini (<c>DirtCliff001</c> parçaları) LowPoly
    /// Medieval MegaKit'in <c>Rampart_Wall</c> parçalarıyla değiştirir (idempotent). Görünmez <c>*_Collider</c>
    /// kutularına (oyun sınırı) ve NavMesh'e DOKUNULMAZ; yalnızca <c>_Cliff</c>/<c>_Rampart</c> işaretli görseller
    /// silinip collider kutularının boyutlarına göre yeniden dizilir. Parça boyutları sahnede ölçülür (import
    /// yönü değişirse yükseklik ekseni doğrulanır); mazgallı duvarda bindirme çift mazgal çizdiği için parçalar
    /// bindirme yerine milimetrik esnetmeyle uç uca dizilir. Materyal: tek palet dokusuyla kalıcı Mat_Kale_Duvar.
    /// </summary>
    internal static class KaleDuvariKur
    {
        public const string RampartPath = "Assets/Kale/Rampart_Wall.fbx";
        public const string EskiMarker = "_Cliff";
        public const string PieceMarker = "_Rampart";
        private const string SahneYolu = "Assets/Scenes/GameScene.unity";
        private const string WallsYolu = "Setup_Bolum2/Walls";
        private const string DokuKlasor = "Assets/Kale/Dokular";
        private const string MatKlasor = "Assets/Kale";
        private const float ZeminBatu = 0.05f; // taban zemine tam otursun diye hafif gömme

        /// <summary>GameScene'deki Bolum2 duvarlarını rampart ile yeniden dizer.</summary>
        [MenuItem("Yazlab/Kurulum/Bolum2 Kale Duvari (Rampart)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[KaleDuvari] Play modunda sahne değiştirilemez; Play'den çık ve tekrar çalıştır.");
                return;
            }

            var walls = GameObject.Find(WallsYolu);
            if (walls == null)
            {
                EditorSceneManager.OpenScene(SahneYolu);
                walls = GameObject.Find(WallsYolu);
            }
            if (walls == null)
            {
                Debug.LogError($"[KaleDuvari] '{WallsYolu}' bulunamadı — GameScene'de Setup_Bolum2 var mı?");
                return;
            }

            int kutuSayi = RebuildAll(walls.transform);
            MarkStatic(walls);
            EditorSceneManager.MarkSceneDirty(walls.scene);
            Debug.Log($"[KaleDuvari] Bolum2 duvarları rampart ile yeniden dizildi ({kutuSayi} kutu). Kaydedilmedi — görünüme bak, beğenirsen Ctrl+S.");
        }

        /// <summary>
        /// <paramref name="wallsRoot"/> altındaki görselleri silip BoxCollider'lı görünmez kutulardan rampart
        /// duvarı kurar. Collider/NavMesh'e dokunmaz; kutu sayısını döndürür.
        /// </summary>
        public static int RebuildAll(Transform wallsRoot)
        {
            for (int i = wallsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = wallsRoot.GetChild(i);
                if (child.name.Contains(EskiMarker) || child.name.Contains(PieceMarker))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RampartPath);
            if (model == null)
            {
                Debug.LogError($"[KaleDuvari] Rampart modeli bulunamadı: {RampartPath} (ilk Unity odaklanmasında import edilir; sonra tekrar çalıştır)");
                return 0;
            }

            var materyal = KaliciMateryal();

            // Parça boyutlarını prova instance'tan ölç: yükseklik y ekseninde olmalı (FBX PreRotation -90X ile
            // dikilir). Ölçüm beklentiyi tutmazsa çık — yarım doğru dizilimden iyisi net hata.
            var prova = (GameObject)PrefabUtility.InstantiatePrefab(model);
            foreach (Collider c in prova.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var provaR = prova.GetComponentInChildren<Renderer>();
            Vector3 ham = provaR != null ? provaR.bounds.size : new Vector3(2f, 5.41f, 1.635f);
            Object.DestroyImmediate(prova);
            if (ham.y < Mathf.Max(ham.x, ham.z))
            {
                Debug.LogError($"[KaleDuvari] Model beklenmedik yönde import oldu (boyutlar {ham}) — import ayarlarını kontrol et.");
                return 0;
            }
            // Yatay eksenlerden uzun olan duvar uzunluğu (mazgallı yüz), kısa olan kalınlık.
            bool zUzun = ham.z >= ham.x;
            float hamUzunluk = zUzun ? ham.z : ham.x;
            float hamKalinlik = zUzun ? ham.x : ham.z;

            // BoxCollider'lı görünmez kutuları önce listeye al (döngü içinde çocuk ekleniyor).
            var kutular = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in wallsRoot)
            {
                if (child.GetComponent<BoxCollider>() != null)
                {
                    kutular.Add(child);
                }
            }

            foreach (Transform box in kutular)
            {
                Vector3 size = box.localScale;
                bool alongX = size.x >= size.z;
                float length = alongX ? size.x : size.z;
                float thickness = alongX ? size.z : size.x;
                float height = size.y;
                Vector3 baseCenter = new Vector3(box.position.x, box.position.y - size.y * 0.5f, box.position.z);

                // Parça sayısı en yakın tam sayı; kalan fark esnetmeyle kapatılır (bindirme yok — mazgal çift çizmez).
                int adet = Mathf.Max(1, Mathf.RoundToInt(length / hamUzunluk));
                float parcaUzunluk = length / adet;
                Vector3 eksen = alongX ? Vector3.right : Vector3.forward;
                Vector3 baslangic = baseCenter - eksen * (length * 0.5f);

                for (int i = 0; i < adet; i++)
                {
                    var pivot = new GameObject(box.name.EndsWith("_Collider")
                        ? box.name.Substring(0, box.name.Length - "_Collider".Length) + PieceMarker + i
                        : box.name + PieceMarker + i).transform;
                    pivot.SetParent(wallsRoot, false);
                    pivot.position = baslangic + eksen * (parcaUzunluk * (i + 0.5f));
                    // Modelin uzun ekseni dünya z: x boyunca uzanan duvar için 90° döndür.
                    pivot.rotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f);
                    pivot.localScale = new Vector3(thickness / hamKalinlik, height / ham.y, parcaUzunluk / hamUzunluk);

                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    foreach (Collider c in piece.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    var r = piece.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        pivot.position += Vector3.up * (baseCenter.y - r.bounds.min.y - ZeminBatu);
                    }
                    foreach (var rr in piece.GetComponentsInChildren<Renderer>()) rr.sharedMaterial = materyal;
                }
            }

            return kutular.Count;
        }

        // Palet dokulu kalıcı materyal — sub-asset mutasyonu kalıcı olmadığından asset (idempotent).
        private static Material KaliciMateryal()
        {
            const string yol = MatKlasor + "/Mat_Kale_Duvar.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (m != null) return m;

            m = new Material(Shader.Find("Standard")) { name = "Mat_Kale_Duvar" };
            var palet = AssetDatabase.LoadAssetAtPath<Texture2D>(DokuKlasor + "/props_palette.png");
            if (palet != null) m.mainTexture = palet;
            else Debug.LogWarning("[KaleDuvari] Palet dokusu yok: " + DokuKlasor + "/props_palette.png");
            m.SetFloat("_Glossiness", 0.1f); // mat taş
            AssetDatabase.CreateAsset(m, yol);
            Debug.Log("[KaleDuvari] Materyal kuruldu: Mat_Kale_Duvar (props_palette)");
            return m;
        }

        // EnvironmentSetup.MarkStatic ile aynı bayraklar (ışık hariç, batching + occlusion).
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
    }
}
