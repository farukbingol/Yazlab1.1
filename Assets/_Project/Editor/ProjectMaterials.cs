using UnityEditor;
using UnityEngine;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Kurulum araçlarının kullandığı düz renk materyal asset'leri. Paket modellerinin materyalleri palet dokusudur;
    /// düz küpe uygulanınca renkli karo görünür, bu yüzden kutu geometriler için dokusuz materyal üretilir.
    /// </summary>
    internal static class ProjectMaterials
    {
        public const string Folder = "Assets/_Project/Materials";
        public const string WoodPath = Folder + "/Mat_FinalGate.mat";
        public const string RockPath = Folder + "/Mat_RockPlain.mat";

        public static readonly Color WoodColor = new Color(0.33f, 0.21f, 0.12f, 1f);
        public static readonly Color RockColor = new Color(0.42f, 0.33f, 0.25f, 1f);

        /// <summary>Düz koyu ahşap (kapı kanatları).</summary>
        public static Material Wood => LoadOrCreate(WoodPath, WoodColor);

        /// <summary>Düz toprak/kaya (tünel çatısı).</summary>
        public static Material Rock => LoadOrCreate(RockPath, RockColor);

        /// <summary>
        /// Varsa asset'i döndürür; yoksa şef materyalinin shader'ıyla (render pipeline uyumlu) dokusuz materyal oluşturur.
        /// </summary>
        public static Material LoadOrCreate(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Materials");
            }

            Material template = null;
            GameObject sef = GameObject.Find("Dusman_Sef");
            if (sef != null)
            {
                Renderer r = sef.GetComponentInChildren<Renderer>();
                template = r != null ? r.sharedMaterial : null;
            }

            var mat = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.mainTexture = null;
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
