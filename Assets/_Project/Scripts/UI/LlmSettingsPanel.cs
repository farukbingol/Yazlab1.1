using UnityEngine;
using UnityEngine.UI;
using Yazlab.Core;
using Yazlab.LLM;

namespace Yazlab.UI
{
    /// <summary>
    /// Oyun içi LLM ayar paneli (varsayılan F2): Ollama adresi (ör. ekip arkadaşının LAN IP'si) ve model adı değiştirilir,
    /// <see cref="ILlmRuntime.TryReconfigure"/> ile uygulanır. Yerel/LAN olmayan adres reddedilir. Açıkken oyuncu girdisi kilitlidir.
    /// Değişiklik yalnız çalışma anı kopyasına uygulanır; config asset'i değişmez.
    /// </summary>
    [DisallowMultipleComponent]
    public class LlmSettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _window;
        [SerializeField] private InputField _urlField;
        [SerializeField] private InputField _modelField;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _infoText;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F2;
        [SerializeField] private Color _errorColor = new Color(1f, 0.45f, 0.4f);
        [SerializeField] private Color _infoColor = new Color(0.85f, 0.85f, 0.82f);

        private ILlmRuntime _runtime;
        private bool _lockHeld;

        /// <summary>Panel açık mı.</summary>
        public bool IsOpen => _window != null && _window.activeSelf;

        private void Awake()
        {
            if (_applyButton != null)
            {
                _applyButton.onClick.AddListener(Apply);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_window != null)
            {
                _window.SetActive(false);
            }
        }

        private void Start()
        {
            if (ServiceRegistry.TryGet(out _runtime))
            {
                _runtime.StatusChanged += OnStatusChanged;
            }
        }

        private void OnDisable()
        {
            ReleaseLock();
        }

        private void OnDestroy()
        {
            if (_runtime != null)
            {
                _runtime.StatusChanged -= OnStatusChanged;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        /// <summary>Paneli mevcut değerlerle açar.</summary>
        public void Open()
        {
            if (_window == null)
            {
                return;
            }

            if (_runtime != null && _runtime.Config != null)
            {
                SetField(_urlField, _runtime.Config.BaseUrl);
                SetField(_modelField, _runtime.Config.ModelName);
                ShowInfo(_runtime.StatusText, false);
            }
            else
            {
                ShowInfo("LLM servisi bulunamadı.", true);
            }

            _window.SetActive(true);
            if (!_lockHeld)
            {
                InputLock.Acquire();
                _lockHeld = true;
            }
        }

        /// <summary>Paneli kapatır.</summary>
        public void Close()
        {
            if (_window != null)
            {
                _window.SetActive(false);
            }

            ReleaseLock();
        }

        private void Apply()
        {
            if (_runtime == null)
            {
                ShowInfo("LLM servisi bulunamadı.", true);
                return;
            }

            string url = _urlField != null ? _urlField.text : string.Empty;
            string model = _modelField != null ? _modelField.text : string.Empty;
            if (_runtime.TryReconfigure(url, model, out string error))
            {
                ShowInfo("Uygulandı, bağlantı kontrol ediliyor...", false);
            }
            else
            {
                ShowInfo($"Uygulanmadı: {error}", true);
            }
        }

        private void OnStatusChanged(LlmHealthState state, string text)
        {
            if (IsOpen)
            {
                ShowInfo(text, state == LlmHealthState.Unavailable);
            }
        }

        private void ShowInfo(string text, bool isError)
        {
            if (_infoText == null)
            {
                return;
            }

            _infoText.text = text;
            _infoText.color = isError ? _errorColor : _infoColor;
        }

        private static void SetField(InputField field, string value)
        {
            if (field != null)
            {
                field.text = value ?? string.Empty;
            }
        }

        private void ReleaseLock()
        {
            if (_lockHeld)
            {
                InputLock.Release();
                _lockHeld = false;
            }
        }
    }
}
