using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yazlab.Core;
using Yazlab.UI;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Sahneye <see cref="GameManager"/>, <see cref="TimeScaleService"/> ve <see cref="EndScreen"/>
    /// zafer ekranı panelini idempotent olarak kuran Editor menü aracı.
    /// </summary>
    public static class GameManagerSetup
    {
        private const string GameManagerObjectName = "GameManager";
        private const string EndScreenCanvasName = "Setup_EndScreenUI";

        /// <summary>
        /// Sahneye <see cref="GameManager"/> ve <see cref="EndScreen"/> UI hiyerarşisini kurar.
        /// Tekrar çalıştırıldığında mevcut kurulumu temizleyip yeniden oluşturur.
        /// </summary>
        [MenuItem("Yazlab/Kurulum/GameManager ve Bitis Ekrani")]
        public static void Run()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject rootGo in activeScene.GetRootGameObjects())
            {
                if (rootGo.name == GameManagerObjectName || rootGo.name == EndScreenCanvasName)
                {
                    Object.DestroyImmediate(rootGo);
                }
            }

            EnsureEventSystem();

            EndScreen endScreen = CreateEndScreenHierarchy();
            GameManager gameManager = CreateGameManagerObject(endScreen);

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] {gameManager.name} ve {EndScreenCanvasName} başarıyla kuruldu ve sahne kaydedildi.");
        }

        private static GameManager CreateGameManagerObject(EndScreen endScreen)
        {
            GameObject gmGo = new GameObject(GameManagerObjectName);
            if (!gmGo.TryGetComponent<TimeScaleService>(out _))
            {
                gmGo.AddComponent<TimeScaleService>();
            }

            GameManager gm = gmGo.AddComponent<GameManager>();
            PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            PlayerCombat playerCombat = playerHealth != null
                ? playerHealth.GetComponent<PlayerCombat>()
                : Object.FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Exclude);

            gm.PlayerHealthRef = playerHealth;
            gm.PlayerCombatRef = playerCombat;
            gm.EndScreenRef = endScreen;

            EditorUtility.SetDirty(gmGo);
            return gm;
        }

        private static EndScreen CreateEndScreenHierarchy()
        {
            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGo = new GameObject(
                EndScreenCanvasName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EndScreen endScreen = canvasGo.AddComponent<EndScreen>();

            // Karartma arka plan paneli (RootPanel)
            GameObject panelGo = new GameObject("VictoryPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            Image overlayImg = panelGo.GetComponent<Image>();
            overlayImg.color = new Color(0.06f, 0.08f, 0.10f, 0.88f);

            // İç kart kutusu
            GameObject cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(panelGo.transform, false);
            RectTransform cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(760f, 520f);
            cardRt.anchoredPosition = Vector2.zero;

            Image cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.14f, 0.17f, 0.21f, 0.96f);

            // Başlık (TitleText)
            Text titleText = CreateText(
                cardGo.transform,
                "TitleText",
                builtinFont,
                38,
                FontStyle.Bold,
                new Color(1f, 0.85f, 0.35f, 1f),
                TextAnchor.MiddleCenter,
                new Vector2(0f, 185f),
                new Vector2(680f, 70f));
            titleText.text = "TEBRİKLER — BÖLÜM TAMAMLANDI!";

            // Özet (SummaryText)
            Text summaryText = CreateText(
                cardGo.transform,
                "SummaryText",
                builtinFont,
                24,
                FontStyle.Normal,
                new Color(0.92f, 0.94f, 0.96f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0f, 15f),
                new Vector2(620f, 240f));
            summaryText.text = "Senaryo başarıyla tamamlandı!";

            // Butonlar
            Button restartBtn = CreateButton(
                cardGo.transform,
                "RestartButton",
                builtinFont,
                "Yeniden Oyna",
                new Color(0.20f, 0.55f, 0.32f, 1f),
                new Vector2(-150f, -180f),
                new Vector2(240f, 64f));

            Button quitBtn = CreateButton(
                cardGo.transform,
                "QuitButton",
                builtinFont,
                "Çıkış",
                new Color(0.62f, 0.22f, 0.22f, 1f),
                new Vector2(150f, -180f),
                new Vector2(240f, 64f));

            endScreen.RootPanel = panelGo;
            endScreen.TitleText = titleText;
            endScreen.SummaryText = summaryText;
            endScreen.RestartButton = restartBtn;
            endScreen.QuitButton = quitBtn;

            // Başlangıçta gizli
            panelGo.SetActive(false);

            EditorUtility.SetDirty(canvasGo);
            return endScreen;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            return txt;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Font font,
            string label,
            Color bgColor,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = btnGo.GetComponent<Image>();
            img.color = bgColor;

            Button btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;

            Text labelText = CreateText(
                btnGo.transform,
                "Label",
                font,
                24,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                sizeDelta);
            labelText.text = label;

            return btn;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorUtility.SetDirty(esGo);
        }
    }
}
