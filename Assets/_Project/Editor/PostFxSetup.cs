using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// İki sahneye Post Processing v2 kurar (Built-in pipeline): ana kameraya <see cref="PostProcessLayer"/>,
    /// "PostFX" katmanında global <see cref="PostProcessVolume"/>. Profil: hafif kontrast/doygunluk ve sıcak ton,
    /// yalnız parlak noktalarda bloom, yumuşak vinyet, ağaç/kaya diplerinde ambient occlusion. Köy biraz daha sıcak.
    /// Idempotent; profiller <c>Assets/_Project/PostFX</c> altında. Görsel menülerin önerilen sırası:
    /// Yaprak ve Bitki Malzemeleri → Cevre Duzeni → UI Temasi → Post Processing.
    /// </summary>
    public static class PostFxSetup
    {
        private const string LayerName = "PostFX";
        private const string ProfileDir = "Assets/_Project/PostFX";
        private const string VolumeName = "Setup_PostFX";

        [MenuItem("Yazlab/Gorsel/Post Processing (Iki Sahne)")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int layer = EnsureLayer();
            if (layer < 0)
            {
                Debug.LogError("[Gorsel] Boş kullanıcı katmanı yok; PostFX kurulmadı.");
                return;
            }

            string start = EditorSceneManager.GetActiveScene().path;
            Apply("Assets/Scenes/GameScene.unity", layer, "PP_Forest", 4f, 8f);
            Apply("Assets/Scenes/Bolum3_Koy.unity", layer, "PP_Village", 12f, 6f);
            if (!string.IsNullOrEmpty(start))
            {
                EditorSceneManager.OpenScene(start);
            }
        }

        private static void Apply(string scenePath, int layer, string profileName, float temperature, float saturation)
        {
            // Profil sahne açıldıktan sonra yüklenir: OpenScene kullanılmayan varlıkları boşaltır, önceki referans ölür.
            EditorSceneManager.OpenScene(scenePath);
            PostProcessProfile profile = Profile(profileName, temperature, saturation);
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"[Gorsel] {scenePath}: ana kamera yok.");
                return;
            }

            PostProcessLayer ppLayer = cam.TryGetComponent(out PostProcessLayer l) ? l : cam.gameObject.AddComponent<PostProcessLayer>();
            string[] guids = AssetDatabase.FindAssets("t:PostProcessResources");
            if (guids.Length > 0)
            {
                ppLayer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(guids[0])));
            }

            ppLayer.volumeTrigger = cam.transform;
            ppLayer.volumeLayer = 1 << layer;
            ppLayer.antialiasingMode = PostProcessLayer.Antialiasing.None; // MSAA kalite ayarından geliyor
            EditorUtility.SetDirty(ppLayer);

            GameObject volumeGo = GameObject.Find(VolumeName) ?? new GameObject(VolumeName);
            volumeGo.layer = layer;
            PostProcessVolume volume = volumeGo.TryGetComponent(out PostProcessVolume v) ? v : volumeGo.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Gorsel] Post processing kuruldu: {scenePath} ({profile.name}).");
        }

        private static PostProcessProfile Profile(string name, float temperature, float saturation)
        {
            System.IO.Directory.CreateDirectory(ProfileDir);
            string path = $"{ProfileDir}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            ColorGrading grading = Get<ColorGrading>(profile);
            grading.gradingMode.Override(GradingMode.LowDefinitionRange); // gamma renk uzayı → LDR
            grading.temperature.Override(temperature);
            grading.saturation.Override(saturation);
            grading.contrast.Override(12f);
            grading.brightness.Override(2f);

            Bloom bloom = Get<Bloom>(profile);
            bloom.intensity.Override(1.1f);
            bloom.threshold.Override(1.05f);
            bloom.softKnee.Override(0.5f);
            bloom.diffusion.Override(6f);

            Vignette vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            vignette.roundness.Override(1f);

            AmbientOcclusion ao = Get<AmbientOcclusion>(profile);
            ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
            ao.intensity.Override(0.55f);
            ao.thicknessModifier.Override(1.5f);

            EditorUtility.SetDirty(profile);
            foreach (PostProcessEffectSettings s in profile.settings)
            {
                EditorUtility.SetDirty(s);
            }

            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Get<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
        {
            if (profile.TryGetSettings(out T settings))
            {
                settings.enabled.Override(true);
                return settings;
            }

            settings = profile.AddSettings<T>();
            settings.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(settings, profile);
            settings.enabled.Override(true);
            return settings;
        }

        /// <summary>"PostFX" kullanıcı katmanını bulur, yoksa ilk boş slota ekler.</summary>
        private static int EnsureLayer()
        {
            int existing = LayerMask.NameToLayer(LayerName);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = LayerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            return -1;
        }
    }
}
