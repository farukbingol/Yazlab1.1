using UnityEngine;
using UnityEngine.UI;
using Yazlab.Core;

namespace Yazlab.UI
{
    /// <summary>
    /// Oyun açılışındaki başlangıç/kontroller kartı: açıkken oyun duraklatılır (<see cref="ITimeScaleService"/>) ve oyuncu
    /// girdisi kilitlenir; "Başla", Enter veya Space ile kapanır.
    /// </summary>
    [DisallowMultipleComponent]
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _startButton;
        [SerializeField] private bool _showOnStart = true;

        private bool _lockHeld;
        private bool _paused;

        /// <summary>Kart açık mı.</summary>
        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (_startButton != null)
            {
                _startButton.onClick.AddListener(Close);
            }

            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void Start()
        {
            if (_showOnStart)
            {
                Open();
            }
        }

        private void OnDisable()
        {
            Restore();
        }

        private void Update()
        {
            if (IsOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                Close();
            }
        }

        /// <summary>Kartı gösterir, oyunu duraklatır.</summary>
        public void Open()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.SetActive(true);
            if (!_lockHeld)
            {
                InputLock.Acquire();
                _lockHeld = true;
            }

            if (!_paused && ServiceRegistry.TryGet(out ITimeScaleService time))
            {
                time.SetPaused(true);
                _paused = true;
            }
        }

        /// <summary>Kartı kapatır, oyunu sürdürür.</summary>
        public void Close()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }

            Restore();
        }

        private void Restore()
        {
            if (_lockHeld)
            {
                InputLock.Release();
                _lockHeld = false;
            }

            if (_paused)
            {
                if (ServiceRegistry.TryGet(out ITimeScaleService time))
                {
                    time.SetPaused(false);
                }

                _paused = false;
            }
        }
    }
}
