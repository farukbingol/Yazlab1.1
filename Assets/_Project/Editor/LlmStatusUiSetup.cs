using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yazlab.LLM;
using Yazlab.UI;
using static Yazlab.EditorTools.DialogueUiSetup;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// LLM çalışma anı servisini ve arayüzünü kurar (idempotent):
    /// sahne kökünde <c>LlmRuntime</c> (+ Editor hata kısayolları <see cref="LlmFaultInjector"/>), <c>OyuncuUI/LlmStatusUI</c>
    /// altında sağ-alt durum rozeti ve F2 ayar penceresi, kapı bekçisine <see cref="LlmInteractionLogger"/>.
    /// Bileşenler korunur (referanslar kopmaz), UI alt objeleri yeniden kurulur.
    /// </summary>
    public static class LlmStatusUiSetup
    {
        private const string RuntimeName = "LlmRuntime";
        private const string HudName = "OyuncuUI";
        private const string UiRootName = "LlmStatusUI";
        private const string NpcPath = "Setup_Bolum3/Npc_KapiBekcisi";
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/LlmConfig.asset";
        private const int SortingOrder = 40;

        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.1f, 0.88f);
        private static readonly Color ButtonColor = new Color(0.27f, 0.22f, 0.16f, 1f);
        private static readonly Color TextColor = new Color(0.9f, 0.9f, 0.88f);

        /// <summary>Kurulumu yapar ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/LLM Durum Isigi ve Log")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Kurulum] Play modunda sahneye kalıcı yazılamaz; Play'den çık ve tekrar çalıştır.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<LlmConfig>(ConfigPath);
            GameObject hud = GameObject.Find(HudName);
            if (config == null || hud == null)
            {
                Debug.LogError("[Kurulum] LlmConfig ('Yazlab/Kurulum/LLM Config') veya OyuncuUI bulunamadı.");
                return;
            }

            BuildRuntime(config);
            BuildUi(hud);
            bool logger = AddLoggerToNpc();

            EditorSceneManager.MarkSceneDirty(hud.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] LLM durum ışığı + F2 ayar paneli + LlmRuntime kuruldu; NPC log: {(logger ? "OK" : "NPC yok")}. Sahne kaydedildi.");
        }

        private static void BuildRuntime(LlmConfig config)
        {
            GameObject go = GameObject.Find(RuntimeName);
            if (go == null)
            {
                go = new GameObject(RuntimeName);
            }

            if (!go.TryGetComponent(out LlmRuntime runtime))
            {
                runtime = go.AddComponent<LlmRuntime>();
            }

            if (!go.TryGetComponent(out LlmFaultInjector _))
            {
                go.AddComponent<LlmFaultInjector>();
            }

            var so = new SerializedObject(runtime);
            so.FindProperty("_configAsset").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
        }

        private static void BuildUi(GameObject hud)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DefaultControls.Resources res = LoadResources();

            Transform existing = hud.transform.Find(UiRootName);
            GameObject root = existing != null ? existing.gameObject : new GameObject(UiRootName, typeof(RectTransform));
            root.transform.SetParent(hud.transform, false);
            Stretch((RectTransform)root.transform, 0f);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            if (!root.TryGetComponent(out Canvas canvas))
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            if (!root.TryGetComponent(out GraphicRaycaster _))
            {
                root.AddComponent<GraphicRaycaster>();
            }

            // Rozet (sağ alt: üst-orta mesaj alanı ve alt-orta diyalog penceresiyle çakışmaz).
            RectTransform badge = NewUi("Badge", root.transform);
            SetRect(badge, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(250f, 48f));
            AddImage(badge.gameObject, res.standard, PanelColor);

            RectTransform dot = NewUi("Dot", badge);
            SetRect(dot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(16f, 16f));
            Image dotImage = AddImage(dot.gameObject, res.knob, new Color(1f, 0.82f, 0.2f));
            dotImage.type = Image.Type.Simple;

            Text label = NewText("Label", badge, font, 14, FontStyle.Normal, TextColor, TextAnchor.MiddleLeft, "Ollama'ya bağlanılıyor...");
            Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(36f, 2f);
            label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            // F2 ayar penceresi (ortada, kapalı başlar).
            RectTransform window = NewUi("SettingsWindow", root.transform);
            SetRect(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470f, 250f));
            AddImage(window.gameObject, res.standard, PanelColor);

            Text title = NewText("Title", window, font, 19, FontStyle.Bold, new Color(0.96f, 0.84f, 0.6f), TextAnchor.UpperLeft, "LLM Ayarları (yalnız yerel / LAN Ollama)");
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 26f));

            InputField urlField = LabeledField(window, res, font, "Sunucu adresi", -50f);
            InputField modelField = LabeledField(window, res, font, "Model", -104f);

            Text info = NewText("Info", window, font, 14, FontStyle.Italic, TextColor, TextAnchor.UpperLeft, string.Empty);
            SetRect(info.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(-32f, 36f));
            info.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button apply = NewButton("ApplyButton", window, res, font, "Uygula", ButtonColor);
            SetRect((RectTransform)apply.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-132f, 14f), new Vector2(110f, 36f));
            Button close = NewButton("CloseButton", window, res, font, "Kapat", ButtonColor);
            SetRect((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 14f), new Vector2(110f, 36f));

            SetLayerRecursive(root, hud.layer);

            LlmStatusIndicator indicator = GetOrAddWithDefaults<LlmStatusIndicator>(root);
            var indicatorSo = new SerializedObject(indicator);
            indicatorSo.FindProperty("_dot").objectReferenceValue = dotImage;
            indicatorSo.FindProperty("_label").objectReferenceValue = label;
            indicatorSo.ApplyModifiedPropertiesWithoutUndo();

            LlmSettingsPanel panel = GetOrAddWithDefaults<LlmSettingsPanel>(root);
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("_window").objectReferenceValue = window.gameObject;
            panelSo.FindProperty("_urlField").objectReferenceValue = urlField;
            panelSo.FindProperty("_modelField").objectReferenceValue = modelField;
            panelSo.FindProperty("_applyButton").objectReferenceValue = apply;
            panelSo.FindProperty("_closeButton").objectReferenceValue = close;
            panelSo.FindProperty("_infoText").objectReferenceValue = info;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            window.gameObject.SetActive(false);
            EditorUtility.SetDirty(root);
        }

        private static InputField LabeledField(Transform window, DefaultControls.Resources res, Font font, string caption, float y)
        {
            Text label = NewText(caption.Replace(' ', '_') + "_Label", window, font, 14, FontStyle.Normal, TextColor, TextAnchor.MiddleLeft, caption);
            SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, y), new Vector2(120f, 34f));

            GameObject go = DefaultControls.CreateInputField(res);
            go.name = caption.Replace(' ', '_') + "_Field";
            var rt = (RectTransform)go.transform;
            rt.SetParent(window, false);
            SetRect(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(62f, y), new Vector2(-156f, 34f));
            InputField field = go.GetComponent<InputField>();
            field.lineType = InputField.LineType.SingleLine;
            field.textComponent.font = font;
            field.textComponent.fontSize = 15;
            field.textComponent.supportRichText = false;
            ((Text)field.placeholder).font = font;
            return field;
        }

        /// <summary>Bileşeni korur (dış referanslar kopmaz) ama ayarlarını kod varsayılanlarına döndürür.</summary>
        private static T GetOrAddWithDefaults<T>(GameObject go) where T : MonoBehaviour
        {
            if (!go.TryGetComponent(out T component))
            {
                return go.AddComponent<T>();
            }

            var temp = new GameObject("Defaults_" + typeof(T).Name) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                EditorUtility.CopySerialized(temp.AddComponent<T>(), component);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            return component;
        }

        private static bool AddLoggerToNpc()
        {
            GameObject npc = GameObject.Find(NpcPath);
            if (npc == null || !npc.TryGetComponent(out NpcDialogueController _))
            {
                Debug.LogWarning("[Kurulum] Npc_KapiBekcisi bulunamadı; önce 'Yazlab/Kurulum/Bolum 3 NPC ve Son Kapi'.");
                return false;
            }

            if (!npc.TryGetComponent(out LlmInteractionLogger _))
            {
                npc.AddComponent<LlmInteractionLogger>();
                EditorUtility.SetDirty(npc);
            }

            return true;
        }
    }
}
