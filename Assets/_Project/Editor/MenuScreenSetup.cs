using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yazlab.UI;
using static Yazlab.EditorTools.DialogueUiSetup;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Giriş (başlangıç kartı, GameScene) ve bitiş (zafer, iki sahne) ekranlarını SHADOW Series piksel çerçeveleri ve
    /// Quadlet piksel fontuyla yeniden kurar: 9-slice çerçeveler nokta filtreli ve 2× ölçekli, LLM durumu animasyonlu
    /// yükleme ikonuyla gösterilir. Sahnede Montserrat yedek font olarak kalır; Quadlet oyun açılırken
    /// <see cref="PixelFontText"/> ile uygulanır (lisansı açık repoya konmasına izin vermiyor, dosya git dışında).
    /// Idempotent; <see cref="UiThemeSetup"/> sonunda da çalışır.
    /// </summary>
    public static class MenuScreenSetup
    {
        private const string ShadowDir = "Assets/SHADOW Series - Free Ui";
        private const string LoadingStrip = ShadowDir + "/Loading Icons/Sprite/Loading Icons 1.png";
        private const string PixelFontAsset = "Assets/_Project/LocalOnly/Resources/" + PixelFontText.DefaultFontPath + ".ttf";
        private const string HudName = "OyuncuUI";
        private const string StartCardName = "StartCardUI";
        private const string EndScreenName = "Setup_EndScreenUI";
        private const string GameTitle = "KURT ORMANI";
        private const string GameSubtitle = "KÖYÜN KAPISI";
        private static readonly string[] Scenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/Bolum3_Koy.unity" };

        // "border N.png" için 9-slice kenarları (sol, alt, sağ, üst): yalnız tekdüze kenar şeridi esner, köşe süsleri korunur.
        private static readonly Vector4[] BorderSlices =
        {
            new(12f, 19f, 71f, 24f), new(23f, 14f, 32f, 17f), new(9f, 12f, 9f, 35f), new(24f, 14f, 51f, 15f)
        };

        // Bir sprite pikseli = 2 UI birimi (Quadlet'in 22/33 punto boyutlarıyla aynı piksel ızgarası).
        private const float PixelScale = 2f;

        private static readonly Color Teal = new Color32(138, 198, 200, 255);
        private static readonly Color TextMain = new Color32(230, 244, 242, 255);
        private static readonly Color TextMuted = new Color32(150, 182, 182, 255);
        private static readonly Color OverlayColor = new Color32(6, 10, 12, 205);
        private static readonly Color PanelFill = new Color32(8, 16, 18, 238);
        private static readonly Color BoxFill = new Color32(14, 26, 28, 220);
        private static readonly Color PrimaryFill = new Color32(10, 30, 32, 240);
        private static readonly Color DangerFill = new Color32(36, 14, 16, 240);
        private static readonly Color DangerFrame = new Color32(230, 140, 140, 255);
        private static readonly Color DangerText = new Color32(245, 205, 205, 255);

        private static Sprite[] _borders;
        private static Texture2D _loading;
        private static Font _medium;
        private static Font _bold;
        private static Font _extraBold;

        [MenuItem("Yazlab/Gorsel/Giris ve Bitis Ekranlari (SHADOW UI)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Gorsel] Giriş/bitiş ekranı kurulumu Play modunda çalışmaz; önce Play'den çıkın.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string startScene = EditorSceneManager.GetActiveScene().path;
            foreach (string path in Scenes)
            {
                EditorSceneManager.OpenScene(path);
                ApplyToOpenScene();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log($"[Gorsel] Giriş/bitiş ekranları güncellendi: {path}");
            }

            if (!string.IsNullOrEmpty(startScene))
            {
                EditorSceneManager.OpenScene(startScene);
            }
        }

        /// <summary>Açık sahnedeki başlangıç kartını ve zafer ekranını yeniden kurar.</summary>
        public static void ApplyToOpenScene()
        {
            if (!LoadAssets())
            {
                return;
            }

            GameObject hud = GameObject.Find(HudName);
            Transform card = hud != null ? hud.transform.Find(StartCardName) : null;
            if (card != null && card.TryGetComponent(out StartScreen startScreen))
            {
                BuildStartScreen(startScreen);
            }

            GameObject end = GameObject.Find(EndScreenName);
            if (end != null && end.TryGetComponent(out EndScreen endScreen))
            {
                BuildEndScreen(endScreen);
            }
        }

        // ---------------------------------------------------------------- giriş

        private static void BuildStartScreen(StartScreen screen)
        {
            Transform root = screen.transform;
            ClearChildren(root);

            RectTransform overlay = NewUi("Overlay", root);
            Stretch(overlay, 0f);
            AddImage(overlay.gameObject, null, OverlayColor);

            Text title = PixelText("Title", overlay, _extraBold, 80, 99, TextMain, TextAnchor.MiddleCenter, GameTitle);
            SetRect(title.rectTransform, Center, Center, Center, new Vector2(0f, 370f), new Vector2(1600f, 130f));
            AddShadow(title.gameObject, 5f);

            Text subtitle = PixelText("Subtitle", overlay, _bold, 34, 44, Teal, TextAnchor.MiddleCenter, GameSubtitle);
            SetRect(subtitle.rectTransform, Center, Center, Center, new Vector2(0f, 278f), new Vector2(1200f, 60f));
            AddShadow(subtitle.gameObject, 3f);

            RectTransform panel = Framed("Panel", overlay, 0, PanelFill, Color.white);
            SetRect(panel, Center, Center, Center, new Vector2(0f, -5f), new Vector2(960f, 430f));

            Text header = PixelText("Header", panel, _extraBold, 26, 33, Teal, TextAnchor.MiddleCenter, "KONTROLLER");
            SetRect(header.rectTransform, TopLeft, TopRight, Top, new Vector2(0f, -36f), new Vector2(-80f, 44f));

            // ControlsText = tuşlar + boş satır + görev cümlesi; görev cümlesi ayrı, sönük satırda.
            string[] parts = IntegrationSetup.ControlsText.Split(new[] { "\n\n" }, 2, System.StringSplitOptions.None);
            Text body = PixelText("Body", panel, _medium, 22, 33, TextMain, TextAnchor.UpperCenter, parts[0], 1.25f, 1.25f);
            SetRect(body.rectTransform, TopLeft, TopRight, Top, new Vector2(0f, -110f), new Vector2(-80f, 240f));

            if (parts.Length > 1)
            {
                Text goal = PixelText("Goal", panel, _medium, 18, 22, TextMuted, TextAnchor.LowerCenter, parts[1], 1.2f, 1.3f);
                SetRect(goal.rectTransform, BottomLeft, BottomRight, Bottom, new Vector2(0f, 30f), new Vector2(-120f, 64f));
            }

            BuildLlmStatus(overlay);

            Button start = FramedButton("StartButton", overlay, "BAŞLA  [ENTER]", PrimaryFill, Color.white, TextMain);
            SetRect((RectTransform)start.transform, Center, Center, Center, new Vector2(-170f, -370f), new Vector2(300f, 80f));
            Button quit = FramedButton("QuitButton", overlay, "ÇIKIŞ", DangerFill, DangerFrame, DangerText);
            SetRect((RectTransform)quit.transform, Center, Center, Center, new Vector2(170f, -370f), new Vector2(300f, 80f));

            SetLayerRecursive(root.gameObject, root.gameObject.layer);
            SetFields(screen, ("_panel", overlay.gameObject), ("_startButton", start), ("_quitButton", quit));
            overlay.gameObject.SetActive(false);
        }

        private static void BuildLlmStatus(Transform parent)
        {
            RectTransform row = NewUi("LlmStatus", parent);
            SetRect(row, Center, Center, Center, new Vector2(0f, -274f), new Vector2(1200f, 36f));
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image dot = AddImage(NewUi("Dot", row).gameObject, null, Teal);
            dot.raycastTarget = false;
            Fixed(dot.gameObject, 12f, 12f);

            RectTransform busy = NewUi("BusyIcon", row);
            RawImage icon = busy.gameObject.AddComponent<RawImage>();
            icon.texture = _loading;
            icon.color = Teal;
            icon.raycastTarget = false;
            Fixed(busy.gameObject, 14f * PixelScale, 14f * PixelScale);
            busy.gameObject.AddComponent<UiStripAnimator>();

            PixelText("Prefix", row, _bold, 18, 22, Teal, TextAnchor.MiddleLeft, "KAEL (LLM):");
            Text label = PixelText("Label", row, _medium, 18, 22, TextMuted, TextAnchor.MiddleLeft, "Ollama'ya bağlanılıyor...");

            LlmStatusIndicator indicator = row.gameObject.AddComponent<LlmStatusIndicator>();
            SetFields(indicator, ("_dot", dot), ("_label", label), ("_busyIcon", busy.gameObject));
            var so = new SerializedObject(indicator);
            so.FindProperty("_hint").stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- bitiş

        private static void BuildEndScreen(EndScreen screen)
        {
            Transform root = screen.transform;
            ClearChildren(root);

            RectTransform overlay = NewUi("VictoryPanel", root);
            Stretch(overlay, 0f);
            AddImage(overlay.gameObject, null, OverlayColor);

            RectTransform card = Framed("Card", overlay, 0, PanelFill, Color.white);
            SetRect(card, Center, Center, Center, Vector2.zero, new Vector2(1040f, 740f));

            Text title = PixelText("TitleText", card, _extraBold, 64, 88, TextMain, TextAnchor.MiddleCenter, "TEBRİKLER!");
            SetRect(title.rectTransform, TopLeft, TopRight, Center, new Vector2(0f, -90f), new Vector2(-80f, 110f));
            AddShadow(title.gameObject, 4f);

            Text subtitle = PixelText("Subtitle", card, _bold, 28, 33, Teal, TextAnchor.MiddleCenter, "BÖLÜM TAMAMLANDI");
            SetRect(subtitle.rectTransform, TopLeft, TopRight, Center, new Vector2(0f, -170f), new Vector2(-80f, 44f));

            RectTransform box = Framed("SummaryBox", card, 3, BoxFill, Color.white);
            SetRect(box, Center, Center, Center, new Vector2(0f, -20f), new Vector2(800f, 340f));
            Text summary = PixelText("SummaryText", box, _medium, 24, 33, TextMain, TextAnchor.UpperLeft, "• Kabile Şefi: Yenildi", 1.25f, 1.3f);
            Stretch(summary.rectTransform, 0f);
            summary.rectTransform.offsetMin = new Vector2(48f, 30f);
            summary.rectTransform.offsetMax = new Vector2(-48f, -36f);

            Button restart = FramedButton("RestartButton", card, "YENİDEN OYNA", PrimaryFill, Color.white, TextMain);
            SetRect((RectTransform)restart.transform, Center, Center, Center, new Vector2(-170f, -270f), new Vector2(300f, 80f));
            Button quit = FramedButton("QuitButton", card, "ÇIKIŞ", DangerFill, DangerFrame, DangerText);
            SetRect((RectTransform)quit.transform, Center, Center, Center, new Vector2(170f, -270f), new Vector2(300f, 80f));

            SetLayerRecursive(root.gameObject, root.gameObject.layer);
            screen.RootPanel = overlay.gameObject;
            screen.TitleText = title;
            screen.SummaryText = summary;
            screen.RestartButton = restart;
            screen.QuitButton = quit;
            EditorUtility.SetDirty(screen);
            overlay.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- parçalar

        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Vector2 Top = new(0.5f, 1f);
        private static readonly Vector2 Bottom = new(0.5f, 0f);
        private static readonly Vector2 TopLeft = new(0f, 1f);
        private static readonly Vector2 TopRight = new(1f, 1f);
        private static readonly Vector2 BottomLeft = new(0f, 0f);
        private static readonly Vector2 BottomRight = new(1f, 0f);

        /// <summary>Koyu dolgu + üstte SHADOW çerçevesi. İçerik bu objenin çocuğu olarak eklenir (çerçevenin üstünde çizilir).</summary>
        private static RectTransform Framed(string name, Transform parent, int borderIndex, Color fill, Color frameTint)
        {
            RectTransform root = NewUi(name, parent);

            RectTransform fillRt = NewUi("Fill", root);
            Stretch(fillRt, 3f * PixelScale); // çerçevenin pahlı köşelerinden taşmasın
            AddImage(fillRt.gameObject, null, fill);

            RectTransform frameRt = NewUi("Frame", root);
            Stretch(frameRt, 0f);
            Image frame = AddImage(frameRt.gameObject, _borders[borderIndex], frameTint);
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1f / PixelScale;
            frame.raycastTarget = false;
            return root;
        }

        private static Button FramedButton(string name, Transform parent, string label, Color fill, Color frameTint, Color textColor)
        {
            RectTransform root = Framed(name, parent, 1, fill, frameTint);
            Image fillImg = root.Find("Fill").GetComponent<Image>();
            fillImg.raycastTarget = true;

            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fillImg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(2.2f, 2.2f, 2.2f, 1f);
            colors.selectedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            Text text = PixelText("Label", root, _bold, 24, 33, textColor, TextAnchor.MiddleCenter, label);
            Stretch(text.rectTransform, 0f);
            AddShadow(text.gameObject, 2f);
            return button;
        }

        /// <summary>Yedek fontlu metin + oyun açılınca Quadlet'e geçiren <see cref="PixelFontText"/>.</summary>
        private static Text PixelText(string name, Transform parent, Font fallback, int fallbackSize, int pixelSize, Color color,
            TextAnchor alignment, string value, float fallbackLineSpacing = 1f, float pixelLineSpacing = 1f)
        {
            Text text = NewText(name, parent, fallback, fallbackSize, FontStyle.Normal, color, alignment, value);
            text.lineSpacing = fallbackLineSpacing;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            PixelFontText pixel = text.gameObject.AddComponent<PixelFontText>();
            var so = new SerializedObject(pixel);
            so.FindProperty("_pixelFontSize").intValue = pixelSize;
            so.FindProperty("_pixelLineSpacing").floatValue = pixelLineSpacing;
            so.ApplyModifiedPropertiesWithoutUndo();
            return text;
        }

        private static void AddShadow(GameObject go, float distance)
        {
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(distance, -distance);
        }

        private static void Fixed(GameObject go, float width, float height)
        {
            LayoutElement element = go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        private static void SetFields(Object target, params (string Name, Object Value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string fieldName, Object value) in fields)
            {
                SerializedProperty p = so.FindProperty(fieldName);
                if (p != null)
                {
                    p.objectReferenceValue = value;
                }
                else
                {
                    Debug.LogWarning($"[Gorsel] {target.GetType().Name}.{fieldName} bulunamadı.");
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- varlıklar

        private static bool LoadAssets()
        {
            _borders = new Sprite[BorderSlices.Length];
            for (int i = 0; i < BorderSlices.Length; i++)
            {
                _borders[i] = ImportPixelSprite($"{ShadowDir}/borders/Sprite/border {i + 1}.png", BorderSlices[i]);
            }

            Sprite loading = ImportPixelSprite(LoadingStrip, Vector4.zero);
            _loading = loading != null ? loading.texture : null;
            ImportPixelFont();

            _medium = UiThemeSetup.LoadFont("Montserrat-Medium.otf");
            _bold = UiThemeSetup.LoadFont("Montserrat-Bold.otf");
            _extraBold = UiThemeSetup.LoadFont("Montserrat-ExtraBold.otf");

            if (System.Array.IndexOf(_borders, null) >= 0 || _loading == null)
            {
                Debug.LogError($"[Gorsel] SHADOW UI sprite'ları bulunamadı: {ShadowDir}");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<Font>(PixelFontAsset) == null)
            {
                Debug.LogWarning($"[Gorsel] Piksel font yok ({PixelFontAsset}); ekranlar Montserrat ile çalışır.");
            }

            return true;
        }

        private static Sprite ImportPixelSprite(string path, Vector4 border)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point ||
                importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; // 9-slice için
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ImportPixelFont()
        {
            if (AssetImporter.GetAtPath(PixelFontAsset) is TrueTypeFontImporter importer &&
                importer.fontRenderingMode != FontRenderingMode.HintedRaster)
            {
                importer.fontRenderingMode = FontRenderingMode.HintedRaster; // kenar yumuşatma yok, pikseller keskin
                importer.fontTextureCase = FontTextureCase.Dynamic;
                importer.SaveAndReimport();
            }
        }
    }
}
