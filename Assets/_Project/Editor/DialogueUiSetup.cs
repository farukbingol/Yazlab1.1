using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yazlab.UI;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// <c>OyuncuUI</c> Canvas'ı altına NPC diyalog penceresini kurar (idempotent). <c>DialogueUI</c> objesi ve üzerindeki
    /// <see cref="DialogueView"/> korunur (B18'deki referanslar kopmaz); yalnız alt objeler silinip yeniden kurulur.
    /// Hiyerarşi: DialogueUI (alt Canvas, sıra 50) → Window → Header{Title, Badge, CloseButton}, History (ScrollView),
    /// ThinkingText, InputRow{InputField, RetryButton, SendButton}.
    /// </summary>
    public static class DialogueUiSetup
    {
        private const string HudName = "OyuncuUI";
        private const string RootName = "DialogueUI";
        private const int SortingOrder = 50;
        private const float WindowWidth = 760f;
        private const float WindowHeight = 380f;
        private const float WindowBottomOffset = 90f;
        private const float Pad = 12f;

        private static readonly Color WindowColor = new Color(0.07f, 0.08f, 0.1f, 0.93f);
        private static readonly Color HeaderColor = new Color(0.13f, 0.11f, 0.09f, 1f);
        private static readonly Color TitleColor = new Color(0.96f, 0.84f, 0.6f);
        private static readonly Color HistoryColor = new Color(0f, 0f, 0f, 0.35f);
        private static readonly Color ButtonColor = new Color(0.27f, 0.22f, 0.16f, 1f);
        private static readonly Color RetryColor = new Color(0.55f, 0.36f, 0.08f, 1f);
        private static readonly Color MutedText = new Color(0.75f, 0.75f, 0.72f);

        /// <summary>Diyalog penceresini kurar ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Diyalog Paneli UI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[Kurulum] Play modunda sahneye kalıcı yazılamaz; Play'den çık ve tekrar çalıştır.");
                return;
            }

            GameObject hud = GameObject.Find(HudName);
            if (hud == null || hud.GetComponent<Canvas>() == null)
            {
                Debug.LogError($"[Kurulum] '{HudName}' Canvas'ı bulunamadı; önce 'Yazlab/Dusmanlari Kur' çalıştır.");
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            DefaultControls.Resources res = LoadResources();

            GameObject root = FindOrCreateRoot(hud.transform);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            // Pencere
            var window = NewUi("Window", root.transform);
            SetRect(window, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, WindowBottomOffset), new Vector2(WindowWidth, WindowHeight));
            AddImage(window.gameObject, res.standard, WindowColor);

            // Başlık şeridi
            var header = NewUi("Header", window);
            SetRect(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 44f));
            AddImage(header.gameObject, res.standard, HeaderColor);

            Text title = NewText("Title", header, font, 20, FontStyle.Bold, TitleColor, TextAnchor.MiddleLeft, "Muhafız");
            SetRect(title.rectTransform, Vector2.zero, new Vector2(0.55f, 1f), new Vector2(0f, 0.5f), new Vector2(Pad, 0f), new Vector2(-Pad, 0f));

            var badge = NewUi("Badge", header);
            SetRect(badge, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-52f, 0f), new Vector2(210f, 28f));
            Image badgeImage = AddImage(badge.gameObject, res.standard, new Color(0.3f, 0.3f, 0.3f, 0.9f));
            Text badgeText = NewText("BadgeText", badge, font, 15, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, "Nötr");
            Stretch(badgeText.rectTransform, 0f);
            badgeText.rectTransform.offsetMin = new Vector2(6f, 0f);
            badgeText.rectTransform.offsetMax = new Vector2(-6f, 0f);
            badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            badgeText.verticalOverflow = VerticalWrapMode.Overflow;

            Button close = NewButton("CloseButton", header, res, font, "X", ButtonColor);
            SetRect((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(34f, 30f));

            // Konuşma geçmişi
            GameObject scrollGo = DefaultControls.CreateScrollView(res);
            scrollGo.name = "History";
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(window, false);
            SetRect(scrollRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 21f), Vector2.zero);
            scrollRt.offsetMin = new Vector2(Pad, 96f);
            scrollRt.offsetMax = new Vector2(-Pad, -52f);
            scrollGo.GetComponent<Image>().color = HistoryColor;

            ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
            if (scroll.horizontalScrollbar != null)
            {
                Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
            }

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            RectTransform content = scroll.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text template = NewText("MessageTemplate", content, font, 17, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, "satır");
            template.horizontalOverflow = HorizontalWrapMode.Wrap;
            template.verticalOverflow = VerticalWrapMode.Overflow;
            template.supportRichText = false;
            template.gameObject.SetActive(false);

            // Düşünüyor göstergesi
            Text thinking = NewText("ThinkingText", window, font, 16, FontStyle.Italic, MutedText, TextAnchor.MiddleLeft, "Düşünüyor...");
            SetRect(thinking.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(-2f * Pad, 24f));
            thinking.gameObject.SetActive(false);

            // Giriş satırı
            var row = NewUi("InputRow", window);
            SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Pad), new Vector2(-2f * Pad, 46f));

            Button send = NewButton("SendButton", row, res, font, "Gönder", ButtonColor);
            SetRect((RectTransform)send.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 0f));

            Button retry = NewButton("RetryButton", row, res, font, "Tekrar Dene", RetryColor);
            SetRect((RectTransform)retry.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-118f, 0f), new Vector2(130f, 0f));
            retry.gameObject.SetActive(false);

            GameObject inputGo = DefaultControls.CreateInputField(res);
            inputGo.name = "InputField";
            var inputRt = (RectTransform)inputGo.transform;
            inputRt.SetParent(row, false);
            inputRt.anchorMin = Vector2.zero;
            inputRt.anchorMax = Vector2.one;
            inputRt.pivot = new Vector2(0f, 0.5f);
            inputRt.offsetMin = Vector2.zero;
            inputRt.offsetMax = new Vector2(-(110f + 8f + 130f + 8f), 0f);
            InputField input = inputGo.GetComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 300;
            input.textComponent.font = font;
            input.textComponent.fontSize = 17;
            input.textComponent.supportRichText = false;
            var placeholder = (Text)input.placeholder;
            placeholder.font = font;
            placeholder.fontSize = 16;
            placeholder.text = "Muhafıza bir şey yaz…  (Enter: gönder · Esc: kapat)";

            SetLayerRecursive(root, hud.layer);

            // Bağlantılar: bileşen korunur (dış referanslar kopmaz) ama ayarları koddaki varsayılanlara döner.
            DialogueView view = root.GetComponent<DialogueView>();
            ResetToDefaults(view);
            var so = new SerializedObject(view);
            so.FindProperty("_windowRoot").objectReferenceValue = window.gameObject;
            so.FindProperty("_titleText").objectReferenceValue = title;
            so.FindProperty("_badgeText").objectReferenceValue = badgeText;
            so.FindProperty("_badgeBackground").objectReferenceValue = badgeImage;
            so.FindProperty("_scrollRect").objectReferenceValue = scroll;
            so.FindProperty("_historyContent").objectReferenceValue = content;
            so.FindProperty("_messageTemplate").objectReferenceValue = template;
            so.FindProperty("_thinkingText").objectReferenceValue = thinking;
            so.FindProperty("_inputField").objectReferenceValue = input;
            so.FindProperty("_sendButton").objectReferenceValue = send;
            so.FindProperty("_retryButton").objectReferenceValue = retry;
            so.FindProperty("_closeButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Pencere sahnede kapalı başlar.
            window.gameObject.SetActive(false);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] Diyalog paneli hazır: {HudName}/{RootName} (DialogueView bağlı, pencere kapalı). Sahne kaydedildi.");
        }

        private static GameObject FindOrCreateRoot(Transform hud)
        {
            Transform existing = hud.Find(RootName);
            GameObject root = existing != null ? existing.gameObject : new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(hud, false);
            Stretch((RectTransform)root.transform, 0f);

            if (!root.TryGetComponent(out Canvas canvas))
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            if (root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
            }

            if (root.GetComponent<DialogueView>() == null)
            {
                root.AddComponent<DialogueView>();
            }

            return root;
        }

        internal static void ResetToDefaults(DialogueView view)
        {
            var temp = new GameObject("DialogueView_Defaults") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                EditorUtility.CopySerialized(temp.AddComponent<DialogueView>(), view);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        internal static DefaultControls.Resources LoadResources()
        {
            return new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
            };
        }

        internal static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        internal static Text NewText(string name, Transform parent, Font font, int size, FontStyle style, Color color,
            TextAnchor alignment, string text)
        {
            RectTransform rt = NewUi(name, parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.text = text;
            t.raycastTarget = false;
            return t;
        }

        internal static Button NewButton(string name, Transform parent, DefaultControls.Resources res, Font font, string label, Color color)
        {
            GameObject go = DefaultControls.CreateButton(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            Text text = go.GetComponentInChildren<Text>();
            text.font = font;
            text.fontSize = 16;
            text.color = Color.white;
            text.text = label;
            return go.GetComponent<Button>();
        }

        internal static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        internal static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
