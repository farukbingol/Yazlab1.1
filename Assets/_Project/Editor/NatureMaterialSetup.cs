using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Doğa modellerinin (Quaternius Nature, Just_Survive) yaprak/çiçek malzemelerini düzeltir. FBX'e gömülü
    /// Standard malzemeler Opaque modda geldiği için yaprak dokularının saydam kısımları siyah şerit olarak
    /// çiziliyordu. Saydamlığı olan her gömülü malzeme için <c>Assets/_Project/Materials/Nature</c> altında Cutout
    /// modda bir kopya üretir ve modelin içe aktarma ayarında ona eşler (tüm sahnelerdeki örnekler birlikte düzelir).
    /// Kırmızı "TwistedTree" yaprakları paketteki boyanabilir beyaz dokuyla sonbahar sarısına çevrilir. Idempotent.
    /// </summary>
    public static class NatureMaterialSetup
    {
        private const string OutDir = "Assets/_Project/Materials/Nature";
        private static readonly string[] ModelFolders = { "Assets/Nature", "Assets/Just_Survive/Just Survive/FBX" };
        private static readonly string[] MaterialFolders = { "Assets/Nature", "Assets/Just_Survive" };
        private const string TwistedLeavesWhite = "Assets/Nature/Textures/Leaves_TwistedTree.png";
        private static readonly Color AutumnTint = new Color(0.9f, 0.62f, 0.2f, 1f);

        [MenuItem("Yazlab/Gorsel/Yaprak ve Bitki Malzemeleri")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            var cache = new Dictionary<string, Material>();
            int remapped = 0;
            int models = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", ModelFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                {
                    continue;
                }

                // Önceki sürümün palet dokulu eşyalar için yaptığı gereksiz eşlemeleri geri al. Eşlenmiş gömülü malzeme
                // alt varlık olarak listelenmediğinden eşleme tablosundan bakılır.
                bool changed = false;
                foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> pair in importer.GetExternalObjectMap())
                {
                    if (pair.Value is Material mapped && AssetDatabase.GetAssetPath(mapped).StartsWith(OutDir) &&
                        mapped.mainTexture != null && mapped.mainTexture.name.Contains("Pallet"))
                    {
                        importer.RemoveRemap(pair.Key);
                        changed = true;
                    }
                }

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is Material embedded))
                    {
                        continue;
                    }

                    if (!NeedsCutout(embedded))
                    {
                        continue;
                    }

                    Material fixedMat = GetOrCreate(embedded, cache);
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name);
                    if (importer.GetExternalObjectMap().TryGetValue(id, out Object current) && current == fixedMat)
                    {
                        continue;
                    }

                    importer.AddRemap(id, fixedMat);
                    changed = true;
                    remapped++;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    models++;
                }
            }

            // Dış .mat dosyaları (ör. T_PineTree): yerinde Cutout'a çevir.
            int external = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", MaterialFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat"))
                {
                    continue;
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && NeedsCutout(mat) && mat.GetFloat("_Mode") != 1f)
                {
                    MakeCutout(mat);
                    EditorUtility.SetDirty(mat);
                    external++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Gorsel] Yaprak malzemeleri: {cache.Count} Cutout malzeme, {remapped} eşleme ({models} model), {external} dış .mat düzeltildi.");
        }

        private static bool NeedsCutout(Material mat)
        {
            if (mat.shader == null || !mat.shader.name.StartsWith("Standard") || !(mat.mainTexture is Texture2D tex))
            {
                return false;
            }

            string texPath = AssetDatabase.GetAssetPath(tex);
            if (tex.name.Contains("Pallet"))
            {
                return false; // renk paleti atlası: saydamlık kullanılmıyor, Cutout gereksiz ve riskli
            }

            return AssetImporter.GetAtPath(texPath) is TextureImporter ti && ti.DoesSourceTextureHaveAlpha() && HasRealTransparency(texPath);
        }

        private static readonly Dictionary<string, bool> TransparencyCache = new Dictionary<string, bool>();

        /// <summary>Alfa kanalı var ama tamamen opak olan dokuları (ör. kabuk) ele.</summary>
        private static bool HasRealTransparency(string texPath)
        {
            if (TransparencyCache.TryGetValue(texPath, out bool cached))
            {
                return cached;
            }

            var tex = new Texture2D(2, 2);
            bool result = false;
            if (tex.LoadImage(File.ReadAllBytes(texPath)))
            {
                Color32[] px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i += 7)
                {
                    if (px[i].a < 128)
                    {
                        result = true;
                        break;
                    }
                }
            }

            Object.DestroyImmediate(tex);
            TransparencyCache[texPath] = result;
            return result;
        }

        private static Material GetOrCreate(Material embedded, Dictionary<string, Material> cache)
        {
            string key = $"{embedded.name}__{embedded.mainTexture.name}";
            if (cache.TryGetValue(key, out Material mat))
            {
                return mat;
            }

            string path = $"{OutDir}/{Sanitize(key)}.mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(embedded) { name = Sanitize(key) };
                AssetDatabase.CreateAsset(mat, path);
            }

            MakeCutout(mat);
            mat.SetFloat("_Glossiness", 0.08f);
            mat.SetFloat("_Metallic", 0f);
            if (embedded.name.StartsWith("Leaves_TwistedTree"))
            {
                var white = AssetDatabase.LoadAssetAtPath<Texture2D>(TwistedLeavesWhite);
                if (white != null)
                {
                    mat.mainTexture = white;
                    mat.color = AutumnTint;
                }
            }

            EditorUtility.SetDirty(mat);
            cache[key] = mat;
            return mat;
        }

        private static void MakeCutout(Material mat)
        {
            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", 0.45f);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Replace(' ', '_');
        }
    }
}
