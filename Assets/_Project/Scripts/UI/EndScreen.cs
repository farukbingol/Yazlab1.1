using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yazlab.Core;

namespace Yazlab.UI
{
    /// <summary>
    /// Oyun tamamlandığında (Victory) açılan zafer ekranı kontrolcüsü.
    /// Başlık, özet metin, "Yeniden Oyna" ve "Çıkış" butonlarını yönetir.
    /// </summary>
    [DisallowMultipleComponent]
    public class EndScreen : MonoBehaviour
    {
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        private bool _isVisible;
        private bool _acquiredInputLock;

        /// <summary>
        /// Zafer panelinin kök GameObject referansı.
        /// </summary>
        public GameObject RootPanel
        {
            get => rootPanel;
            set => rootPanel = value;
        }

        /// <summary>
        /// Başlık metni bileşeni.
        /// </summary>
        public Text TitleText
        {
            get => titleText;
            set => titleText = value;
        }

        /// <summary>
        /// Özet istatistik metni bileşeni.
        /// </summary>
        public Text SummaryText
        {
            get => summaryText;
            set => summaryText = value;
        }

        /// <summary>
        /// "Yeniden Oyna" butonu referansı.
        /// </summary>
        public Button RestartButton
        {
            get => restartButton;
            set => restartButton = value;
        }

        /// <summary>
        /// "Çıkış" butonu referansı.
        /// </summary>
        public Button QuitButton
        {
            get => quitButton;
            set => quitButton = value;
        }

        /// <summary>
        /// Ekranın şu anda açık olup olmadığını belirtir.
        /// </summary>
        public bool IsVisible => _isVisible;

        private void Awake()
        {
            ServiceRegistry.Register(this);

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitClicked);
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            if (!_isVisible && rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            ReleaseLockIfHeld();
            ServiceRegistry.Unregister(this);
        }

        /// <summary>
        /// Zafer ekranını verilen başlık ve oyun durumu özetiyle gösterir, girdi kilidini devreye alır.
        /// </summary>
        /// <param name="snapshot">Oyun bitiş anındaki durum özeti.</param>
        /// <param name="customTitle">Özel başlık (boş bırakılırsa varsayılan kullanılır).</param>
        public void ShowVictory(GameStateSnapshot snapshot, string customTitle = null)
        {
            if (!_acquiredInputLock)
            {
                InputLock.Acquire();
                _acquiredInputLock = true;
            }

            _isVisible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(customTitle)
                    ? "TEBRİKLER — BÖLÜM TAMAMLANDI!"
                    : customTitle;
            }

            if (summaryText != null)
            {
                string bossStatus = snapshot.BossDefeated ? "Yenildi" : "Hayatta";
                string axeStatus = snapshot.HasAxe ? "Kuşanıldı" : "Alınmadı";
                string gateStatus = snapshot.CompanionGateOpened ? "Açıldı (Companion)" : "Kapalı";
                string doorStatus = snapshot.FinalDoorOpened ? "Açıldı" : "Kapalı";

                summaryText.text =
                    $"Senaryo başarıyla tamamlandı!\n\n" +
                    $"• Kabile Şefi: {bossStatus}\n" +
                    $"• Balta (Hatchet): {axeStatus}\n" +
                    $"• Kalan Kurt Sayısı: {snapshot.WolvesAliveCount}\n" +
                    $"• Fiziksel Engel Geçidi: {gateStatus}\n" +
                    $"• Son Kapı: {doorStatus}\n" +
                    $"• Kalan Oyuncu Canı: {snapshot.PlayerHp}";
            }

            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Zafer ekranını gizler ve tutulan girdi kilidini serbest bırakır.
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            ReleaseLockIfHeld();

            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Oyunu ilk sahneden (Build index 0) baştan başlatır ("Yeniden Oyna" butonu aksiyonu; köy sahnesinden de başa döner).
        /// </summary>
        public void OnRestartClicked()
        {
            ReleaseLockIfHeld();
            InputLock.Reset();

            if (ServiceRegistry.TryGet<ITimeScaleService>(out var timeScaleService))
            {
                timeScaleService.ResetTimeScale();
            }
            else
            {
                Time.timeScale = 1f;
            }

            if (SceneManager.sceneCountInBuildSettings > 0)
            {
                SceneManager.LoadScene(0);
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        /// <summary>
        /// Oyundan çıkar; Editor içindeyse Play Mode'u durdurur ("Çıkış" butonu aksiyonu).
        /// </summary>
        public void OnQuitClicked()
        {
            ReleaseLockIfHeld();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ReleaseLockIfHeld()
        {
            if (_acquiredInputLock)
            {
                InputLock.Release();
                _acquiredInputLock = false;
            }
        }
    }
}
