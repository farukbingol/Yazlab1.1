using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yazlab.UI;
using static Yazlab.EditorTools.DialogueUiSetup;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Oyunun tüm UI'ına tek bir görsel tema uygular (iki sahnede de): Montserrat fontu, yuvarlak köşeli ve
    /// altın kenarlı koyu paneller, degrade butonlar, koyu giriş kutuları, ekrana göre ölçeklenen HUD, sol altta
    /// can barı + balta rozeti ve arka planlı mesaj kutusu. Sprite'lar kodla üretilir (dış görsel yok).
    /// Yalnız görünüm değişir; bileşen referansları ve oyun mantığı korunur. Idempotent: diğer UI kurulum
    /// menüleri yeniden çalıştırılırsa bu menü de tekrar çalıştırılmalıdır.
    /// </summary>
    public static class UiThemeSetup
    {
        private const string SpriteDir = "Assets/_Project/UI/Sprites";
        private const string FontDir = "Assets/_Project/Fonts";
        private const string HudName = "OyuncuUI";
        private const string EndScreenName = "Setup_EndScreenUI";
        private static readonly string[] Scenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/Bolum3_Koy.unity" };

        // Tema renkleri.
        internal static readonly Color Gold = Hex("E8C27A");
        internal static readonly Color TextMain = Hex("EDE6D6");
        internal static readonly Color TextMuted = Hex("B3AC9C");
        internal static readonly Color PanelTint = new Color(1f, 1f, 1f, 0.94f);
        internal static readonly Color ButtonPrimary = Hex("3E7448");
        internal static readonly Color ButtonNeutral = Hex("4A3F33");
        internal static readonly Color ButtonWarning = Hex("8C6420");
        internal static readonly Color ButtonDanger = Hex("8A3434");
        internal static readonly Color InputBg = Hex("1C1F25");
        internal static readonly Color HealthRed = Hex("D93A44");

        private static Sprite _panel;
        private static Sprite _round;
        private static Sprite _button;
        private static Sprite _bar;
        private static Font _medium;
        private static Font _bold;
        private static Font _extraBold;

        [MenuItem("Yazlab/Gorsel/UI Temasi (Iki Sahne)")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string startScene = EditorSceneManager.GetActiveScene().path;
            LoadAssets();
            foreach (string path in Scenes)
            {
                EditorSceneManager.OpenScene(path);
                ApplyToOpenScene();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log($"[Gorsel] UI teması uygulandı: {path}");
            }

            if (!string.IsNullOrEmpty(startScene))
            {
                EditorSceneManager.OpenScene(startScene);
            }
        }

        /// <summary>Açık sahnenin HUD ve bitiş ekranına temayı uygular.</summary>
        public static void ApplyToOpenScene()
        {
            LoadAssets();
            GameObject hud = GameObject.Find(HudName);
            if (hud != null)
            {
                ConfigureScaler(hud);
                BuildPlayerHud(hud);
                BuildMessageBox(hud);
                StyleTree(hud.transform);
                LayoutObjective(hud);
                RefreshControlsCard(hud);
            }

            SetupSensorToggle();

            GameObject end = GameObject.Find(EndScreenName);
            if (end != null)
            {
                StyleTree(end.transform);
            }
        }

        // ---------------------------------------------------------------- yapı

        private static void ConfigureScaler(GameObject hud)
        {
            if (hud.TryGetComponent(out CanvasScaler scaler))
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                EditorUtility.SetDirty(scaler);
            }
        }

        private static void BuildPlayerHud(GameObject hud)
        {
            GameObject player = GameObject.FindWithTag("Player");
            Transform oldHp = hud.transform.Find("HPText");
            if (oldHp != null)
            {
                oldHp.gameObject.SetActive(false); // PlayerHealth yazmaya devam eder; görünmez.
            }

            Transform existing = hud.transform.Find("PlayerHUD");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            RectTransform root = NewUi("PlayerHUD", hud.transform);
            SetRect(root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(380f, 74f));
            Image frame = AddImage(root.gameObject, _panel, PanelTint);
            frame.type = Image.Type.Sliced;

            Text title = NewText("Title", root, _extraBold, 15, FontStyle.Normal, Gold, TextAnchor.MiddleLeft, "CAN");
            SetRect(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(60f, 30f));

            RectTransform barBg = NewUi("BarBg", root);
            SetRect(barBg, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(74f, 0f), new Vector2(-94f, 26f));
            Image bg = AddImage(barBg.gameObject, _round, Hex("2A1517"));
            bg.type = Image.Type.Sliced;

            Image trail = FillImage("Trail", barBg, Hex("F2DDB8"));
            Image fill = FillImage("Fill", barBg, HealthRed);
            Text label = NewText("Value", barBg, _bold, 15, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter, "100 / 100");
            Stretch(label.rectTransform, 0f);
            AddShadow(label.gameObject, 0.75f);

            RectTransform badge = NewUi("AxeBadge", root);
            SetRect(badge, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(150f, 30f));
            Image badgeImg = AddImage(badge.gameObject, _panel, PanelTint);
            badgeImg.type = Image.Type.Sliced;
            Text badgeText = NewText("Text", badge, _bold, 14, FontStyle.Normal, Gold, TextAnchor.MiddleCenter, "BALTA  ·  hasar ×2");
            Stretch(badgeText.rectTransform, 0f);
            badge.gameObject.SetActive(false);

            SetLayerRecursive(root.gameObject, hud.layer);
            PlayerHealthBar bar = root.gameObject.AddComponent<PlayerHealthBar>();
            Set(bar,
                ("_health", player != null ? player.GetComponent<PlayerHealth>() : null),
                ("_combat", player != null ? player.GetComponent<PlayerCombat>() : null),
                ("_fill", fill), ("_trail", trail), ("_label", label), ("_axeBadge", badge.gameObject));
            root.SetSiblingIndex(0);
        }

        private static Image FillImage(string name, Transform parent, Color color)
        {
            RectTransform rt = NewUi(name, parent);
            Stretch(rt, 3f);
            Image img = AddImage(rt.gameObject, _bar, color);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        private static void BuildMessageBox(GameObject hud)
        {
            MesajPaneli panel = hud.GetComponent<MesajPaneli>();
            Transform msg = hud.transform.Find("MsgText") ?? hud.transform.Find("MsgBox/MsgText");
            if (panel == null || msg == null)
            {
                return;
            }

            Transform box = hud.transform.Find("MsgBox");
            if (box == null)
            {
                box = NewUi("MsgBox", hud.transform);
            }

            var boxRt = (RectTransform)box;
            SetRect(boxRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 56f));
            Image img = box.TryGetComponent(out Image existingImg) ? existingImg : AddImage(box.gameObject, _panel, PanelTint);
            img.sprite = _panel;
            img.type = Image.Type.Sliced;
            img.color = PanelTint;
            img.raycastTarget = false;

            VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(box.gameObject);
            layout.padding = new RectOffset(28, 28, 12, 12);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(box.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            msg.SetParent(box, false);
            Text text = msg.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.fontSize = 22;
            text.color = TextMain;
            text.lineSpacing = 1.15f;
            text.raycastTarget = false;

            SetLayerRecursive(box.gameObject, hud.layer);
            Set(panel, ("_background", box.gameObject));
            box.gameObject.SetActive(false);
        }

        private static void LayoutObjective(GameObject hud)
        {
            Transform objective = hud.transform.Find("ObjectiveUI");
            if (objective == null)
            {
                return;
            }

            SetRect((RectTransform)objective, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(700f, 50f));
            Transform label = objective.Find("Label");
            if (label != null && label.TryGetComponent(out Text text))
            {
                text.rectTransform.offsetMin = new Vector2(18f, 0f);
                text.rectTransform.offsetMax = new Vector2(-18f, 0f);
                text.fontSize = 20;
                text.resizeTextMaxSize = 20;
                text.resizeTextMinSize = 14;
                text.color = Gold;
            }
        }

        private static void RefreshControlsCard(GameObject hud)
        {
            Transform body = hud.transform.Find("StartCardUI/Overlay/Panel/Body");
            if (body != null && body.TryGetComponent(out Text text))
            {
                text.text = IntegrationSetup.ControlsText;
                text.lineSpacing = 1.25f;
            }
        }

        private static void SetupSensorToggle()
        {
            Yazlab.Companion.RaycastSensor sensor = Object.FindFirstObjectByType<Yazlab.Companion.RaycastSensor>();
            if (sensor == null)
            {
                return;
            }

            Yazlab.Companion.SensorViewToggle toggle = GetOrAdd<Yazlab.Companion.SensorViewToggle>(sensor.gameObject);
            Set(toggle, ("_sensor", sensor));
        }

        // ---------------------------------------------------------------- stil

        private static void StyleTree(Transform root)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                StyleText(text);
            }

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                StyleImage(image);
            }

            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
                colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                colors.selectedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
                colors.colorMultiplier = 1.2f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
                EditorUtility.SetDirty(button);
            }

            foreach (InputField field in root.GetComponentsInChildren<InputField>(true))
            {
                if (field.textComponent != null)
                {
                    field.textComponent.color = TextMain;
                }

                if (field.placeholder is Text placeholder)
                {
                    placeholder.color = new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.6f);
                }

                field.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
                field.caretColor = Gold;
                field.customCaretColor = true;
                EditorUtility.SetDirty(field);
            }
        }

        private static void StyleText(Text text)
        {
            bool title = text.name.Contains("Title") || text.fontSize >= 26;
            bool bold = text.fontStyle == FontStyle.Bold || text.fontStyle == FontStyle.BoldAndItalic;
            bool italic = text.fontStyle == FontStyle.Italic || text.fontStyle == FontStyle.BoldAndItalic;
            bool inButton = text.GetComponentInParent<Button>(true) != null;

            text.font = title ? _extraBold : bold || inButton ? _bold : _medium;
            text.fontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
            if (title)
            {
                text.color = Gold;
            }
            else if (text.color.r > 0.85f && text.color.g > 0.85f && text.color.b > 0.8f && !inButton)
            {
                text.color = TextMain;
            }

            // Montserrat Arial'dan geniş: sabit kutularda taşmasın.
            if (text.GetComponentInParent<ScrollRect>(true) == null && text.GetComponentInParent<LayoutGroup>(true) == null &&
                text.GetComponentInParent<InputField>(true) == null)
            {
                text.resizeTextForBestFit = true;
                text.resizeTextMaxSize = text.fontSize;
                text.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(text.fontSize * 0.7f));
            }

            if (inButton || title || text.name == "Label" || text.name == "Value")
            {
                AddShadow(text.gameObject, 0.55f);
            }

            EditorUtility.SetDirty(text);
        }

        private static void StyleImage(Image image)
        {
            string n = image.name;
            if (image.GetComponent<Mask>() != null || n == "Dot" || n.StartsWith("Fill") || n == "Trail" || n == "BarBg")
            {
                return;
            }

            Button button = image.GetComponent<Button>();
            if (button != null)
            {
                Color current = image.color;
                image.sprite = _button;
                image.type = Image.Type.Sliced;
                image.color = n.Contains("Quit") ? ButtonDanger
                    : n.Contains("Retry") ? ButtonWarning
                    : n.Contains("Start") || n.Contains("Restart") || n.Contains("Send") || n.Contains("Apply") ? ButtonPrimary
                    : current.g > current.r + 0.15f ? ButtonPrimary
                    : ButtonNeutral;
                EditorUtility.SetDirty(image);
                return;
            }

            if (image.GetComponent<InputField>() != null)
            {
                image.sprite = _round;
                image.type = Image.Type.Sliced;
                image.color = InputBg;
                EditorUtility.SetDirty(image);
                return;
            }

            if (n == "Overlay" || n == "VictoryPanel")
            {
                image.sprite = null;
                image.color = new Color(0.03f, 0.04f, 0.05f, 0.66f);
            }
            else if (n == "Header")
            {
                image.sprite = _round;
                image.type = Image.Type.Sliced;
                image.color = new Color(0f, 0f, 0f, 0.28f);
            }
            else if (n == "History")
            {
                image.sprite = _round;
                image.type = Image.Type.Sliced;
                image.color = new Color(0f, 0f, 0f, 0.3f);
            }
            else if (n.StartsWith("Scrollbar"))
            {
                image.sprite = _round;
                image.type = Image.Type.Sliced;
                image.color = new Color(1f, 1f, 1f, 0.06f);
            }
            else if (n == "Handle")
            {
                image.sprite = _round;
                image.type = Image.Type.Sliced;
                image.color = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            }
            else if (n == "Badge" && image.transform.parent != null && image.transform.parent.name == "Header")
            {
                image.sprite = _round;
                image.type = Image.Type.Sliced;
            }
            else if (n is "Window" or "Panel" or "Card" or "Badge" or "SettingsWindow" or "ObjectiveUI" or "Bar")
            {
                image.sprite = _panel;
                image.type = Image.Type.Sliced;
                image.color = PanelTint;
            }
            else
            {
                return;
            }

            EditorUtility.SetDirty(image);
        }

        private static void AddShadow(GameObject go, float alpha)
        {
            Shadow shadow = GetOrAdd<Shadow>(go);
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // ---------------------------------------------------------------- varlıklar

        private static void LoadAssets()
        {
            _medium = LoadFont("Montserrat-Medium.otf");
            _bold = LoadFont("Montserrat-Bold.otf");
            _extraBold = LoadFont("Montserrat-ExtraBold.otf");

            // Koyu dolgu + 2 px altın kenar + üstte ince parlama.
            _panel = MakeSprite("ui_panel", 64, 14, 2, Hex("15171C"), Hex("C9A060"), true, false);
            // Düz beyaz yuvarlak (renk Image'dan).
            _round = MakeSprite("ui_round", 32, 8, 0, Color.white, Color.white, false, false);
            // Dikey degrade buton (renk Image'dan).
            _button = MakeSprite("ui_button", 48, 10, 1, Color.white, new Color(1f, 1f, 1f, 0.55f), false, true);
            // Can/boss barı dolgusu: köşesiz dikey degrade.
            _bar = MakeSprite("ui_bar", 16, 0, 0, Color.white, Color.white, false, true);
        }

        /// <summary>Tema fontu (diğer kurulum araçları için).</summary>
        internal static Font LoadFont(string file) => AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{file}");

        private static Sprite MakeSprite(string name, int size, int radius, int border, Color fill, Color borderColor,
            bool topSheen, bool gradient)
        {
            string path = $"{SpriteDir}/{name}.png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(SpriteDir);
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius); // < 0 içeride
                        float alpha = Mathf.Clamp01(0.5f - d);
                        Color c = border > 0 && d > -border ? borderColor : fill;
                        if (gradient)
                        {
                            float t = (float)y / (size - 1);
                            c *= Mathf.Lerp(0.78f, 1f, t);
                        }

                        if (topSheen && d <= -border && y >= size - border - 3)
                        {
                            c = Color.Lerp(c, Color.white, 0.06f);
                        }

                        c.a *= alpha;
                        pixels[y * size + x] = c;
                    }
                }

                tex.SetPixels(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            int b = Mathf.Max(radius, border) + 1;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder.x != b)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spriteBorder = radius > 0 ? new Vector4(b, b, b, b) : Vector4.zero;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static float RoundedDistance(float x, float y, int size, int radius)
        {
            if (radius <= 0)
            {
                return -Mathf.Min(Mathf.Min(x, size - x), Mathf.Min(y, size - y));
            }

            float cx = Mathf.Clamp(x, radius, size - radius);
            float cy = Mathf.Clamp(y, radius, size - radius);
            return Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - radius;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T component) ? component : go.AddComponent<T>();

        private static void Set(Object target, params (string Name, Object Value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string name, Object value) in fields)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p != null)
                {
                    p.objectReferenceValue = value;
                }
                else
                {
                    Debug.LogWarning($"[Gorsel] {target.GetType().Name}.{name} bulunamadı.");
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;
    }
}
