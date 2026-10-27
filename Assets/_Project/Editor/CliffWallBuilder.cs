using UnityEditor;
using UnityEngine;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Duvar boyunca DirtCliff001 kayalık parçalarını görsel olarak dizer (collider'sız). FBX kökü varsayılan olarak
    /// 100 ölçek ve x=270° dönüşle gelir (cm birimli model); bu yüzden parça dünya eksenine hizalı bir pivotun altına
    /// kendi yerel dönüş/ölçeği korunarak konur, boyut ve yön pivotta ayarlanır, alt kenarı zemine oturtulur.
    /// </summary>
    internal static class CliffWallBuilder
    {
        public const string CliffPath = "Assets/Just_Survive/Just Survive/FBX/DirtCliff001.fbx";
        public const string PieceMarker = "_Cliff";

        // Varsayılan dönüşle dünya boyutları: x = kalınlık, y = yükseklik, z = uzunluk.
        private const float CliffThickness = 4.378f;
        private const float CliffHeight = 2.276f;
        private const float CliffLength = 9.781f;
        private const float GroundSink = 0.05f;

        /// <summary>
        /// <paramref name="baseCenter"/> (zemin seviyesindeki orta nokta) etrafında duvarı kaplayan parçaları oluşturur.
        /// </summary>
        /// <param name="alongX"><c>true</c>: duvar dünya x ekseni boyunca uzanır; <c>false</c>: z boyunca.</param>
        public static void Build(Transform parent, string name, Vector3 baseCenter, float length, float thickness, float height, bool alongX)
        {
            var cliff = AssetDatabase.LoadAssetAtPath<GameObject>(CliffPath);
            if (cliff == null)
            {
                Debug.LogWarning($"[Kurulum] Kayalık modeli bulunamadı: {CliffPath}");
                return;
            }

            int count = Mathf.Max(1, Mathf.CeilToInt(length / CliffLength));
            float pieceLength = length / count;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Vector3 start = baseCenter - axis * (length * 0.5f);

            for (int i = 0; i < count; i++)
            {
                var pivot = new GameObject($"{name}{PieceMarker}{i}").transform;
                pivot.SetParent(parent, false);
                pivot.position = start + axis * (pieceLength * (i + 0.5f));
                // Modelin uzun ekseni dünya z: x boyunca uzanan duvar için 90° döndür.
                pivot.rotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f);
                pivot.localScale = new Vector3(thickness / CliffThickness, height / CliffHeight, pieceLength / CliffLength);

                var piece = (GameObject)PrefabUtility.InstantiatePrefab(cliff, pivot);
                foreach (Collider c in piece.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(c);
                }

                Renderer renderer = piece.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    pivot.position += Vector3.up * (baseCenter.y - renderer.bounds.min.y - GroundSink);
                }
            }
        }

        /// <summary>
        /// <paramref name="wallsRoot"/> altındaki eski kayalık parçalarını silip, görünmez duvar kutularının
        /// (adı <paramref name="colliderSuffix"/> ile biten) boyutlarından yeniden kurar. Collider/NavMesh'e dokunmaz.
        /// </summary>
        public static int RebuildFromColliders(Transform wallsRoot, string colliderSuffix)
        {
            for (int i = wallsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = wallsRoot.GetChild(i);
                if (child.name.Contains(PieceMarker))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            int built = 0;
            var boxes = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in wallsRoot)
            {
                if (child.name.EndsWith(colliderSuffix))
                {
                    boxes.Add(child);
                }
            }

            foreach (Transform box in boxes)
            {
                Vector3 size = box.localScale;
                bool alongX = size.x >= size.z;
                string baseName = box.name.Substring(0, box.name.Length - colliderSuffix.Length);
                Vector3 baseCenter = new Vector3(box.position.x, box.position.y - size.y * 0.5f, box.position.z);
                Build(wallsRoot, baseName, baseCenter, alongX ? size.x : size.z, alongX ? size.z : size.x, size.y, alongX);
                built++;
            }

            return built;
        }
    }
}
