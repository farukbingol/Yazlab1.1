using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yazlab.Core;
using Yazlab.LLM;

namespace Yazlab.UI
{
    /// <summary>
    /// uGUI diyalog penceresi: konuşma geçmişi, duygu/güven rozeti, "Düşünüyor..." göstergesi, giriş alanı ve
    /// Gönder / Tekrar Dene / Kapat butonları. Açıkken <see cref="InputLock"/> tutar (WASD/saldırı kapalı).
    /// Animasyon <see cref="Time.unscaledTime"/> ile çalışır; <c>timeScale</c> ve kare hızından bağımsızdır.
    /// Metinler zengin metin (rich text) olarak yorumlanmaz; oyuncu/model etiket enjekte edemez.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueView : MonoBehaviour, IDialogueView
    {
        private const string ThinkingBase = "Düşünüyor";
        private const string SystemPrefix = "[Sistem]";

        [Header("Bağlantılar")]
        [Tooltip("Açılıp kapanan pencere kökü.")]
        [SerializeField] private GameObject _windowRoot;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _badgeText;
        [SerializeField] private Image _badgeBackground;
        [SerializeField] private ScrollRect _scrollRect;
        [Tooltip("Geçmiş satırlarının ekleneceği içerik (VerticalLayoutGroup).")]
        [SerializeField] private RectTransform _historyContent;
        [Tooltip("Pasif satır şablonu; her mesajda kopyalanır.")]
        [SerializeField] private Text _messageTemplate;
        [SerializeField] private Text _thinkingText;
        [SerializeField] private InputField _inputField;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _closeButton;

        [Header("Davranış")]
        [Tooltip("Geçmişte tutulacak en fazla satır.")]
        [SerializeField, Min(5)] private int _maxLines = 40;
        [Tooltip("Düşünüyor noktalarının değişme aralığı (gerçek saniye).")]
        [SerializeField, Min(0.05f)] private float _thinkingDotInterval = 0.4f;
        [SerializeField] private string _playerLabel = "Sen";

        [Header("Renkler")]
        [SerializeField] private Color _playerColor = new Color(0.62f, 0.82f, 1f);
        [SerializeField] private Color _systemColor = new Color(1f, 0.92f, 0.3f);
        [SerializeField] private Color _neutralColor = new Color(0.88f, 0.88f, 0.86f);
        [SerializeField] private Color _friendlyColor = new Color(0.55f, 0.9f, 0.55f);
        [SerializeField] private Color _suspiciousColor = new Color(0.95f, 0.6f, 0.3f);
        [SerializeField] private Color _angryColor = new Color(1f, 0.42f, 0.38f);

        private readonly Queue<Text> _lines = new Queue<Text>();
        private readonly string[] _thinkingFrames = new string[3];

        private string _npcName = string.Empty;
        private bool _lockHeld;
        private bool _isThinking;
        private bool _scrollPending;
        private int _thinkingFrame = -1;

        /// <inheritdoc />
        public event Action<string> MessageSubmitted;

        /// <inheritdoc />
        public event Action RetryRequested;

        /// <inheritdoc />
        public event Action Closed;

        /// <inheritdoc />
        public bool IsVisible => _windowRoot != null && _windowRoot.activeSelf;

        /// <summary>Düşünme göstergesi açık mı.</summary>
        public bool IsThinking => _isThinking;

        private void Awake()
        {
            for (int i = 0; i < _thinkingFrames.Length; i++)
            {
                _thinkingFrames[i] = ThinkingBase + new string('.', i + 1);
            }

            if (_messageTemplate != null)
            {
                _messageTemplate.gameObject.SetActive(false);
            }

            if (_sendButton != null)
            {
                _sendButton.onClick.AddListener(Submit);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(OnRetryClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(RequestClose);
            }

            if (_windowRoot != null)
            {
                _windowRoot.SetActive(false);
            }

            SetRetryVisible(false);
            ApplyThinkingState(false);
        }

        private void OnDisable()
        {
            // Pencere nesnesi yok edilir/devre dışı kalırsa kilit asılı kalmasın.
            ReleaseLock();
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                RequestClose();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Submit();
            }

            if (_isThinking)
            {
                AnimateThinking();
            }
        }

        private void LateUpdate()
        {
            if (!_scrollPending || _scrollRect == null)
            {
                return;
            }

            _scrollPending = false;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        /// <inheritdoc />
        public void Show(string npcName)
        {
            _npcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName.Trim();
            if (_titleText != null)
            {
                _titleText.text = _npcName;
            }

            ClearHistory();
            SetBadge(NpcEmotion.Neutral, -1);
            SetRetryVisible(false);
            ApplyThinkingState(false);

            if (_windowRoot != null)
            {
                _windowRoot.SetActive(true);
            }

            AcquireLock();
            FocusInput();
        }

        /// <inheritdoc />
        public void Hide()
        {
            ApplyThinkingState(false);
            if (_windowRoot != null)
            {
                _windowRoot.SetActive(false);
            }

            ReleaseLock();
        }

        /// <inheritdoc />
        public void AppendPlayerMessage(string text)
        {
            AddLine($"{_playerLabel}: {text}", _playerColor, TextAnchor.UpperRight);
        }

        /// <inheritdoc />
        public void AppendNpcMessage(string text, NpcEmotion emotion, int currentTrust)
        {
            AddLine($"{_npcName}: {text}", ColorFor(emotion), TextAnchor.UpperLeft);
            SetBadge(emotion, currentTrust);
        }

        /// <inheritdoc />
        public void AppendSystemError(string text, bool canRetry)
        {
            string message = string.IsNullOrEmpty(text) ? SystemPrefix : text.Trim();
            if (!message.StartsWith(SystemPrefix, StringComparison.Ordinal))
            {
                message = $"{SystemPrefix} {message}";
            }

            // NPC satırlarından ayrışsın: ortalı, italik, parlak sarı ve [Sistem] önekli.
            Text line = AddLine(message, _systemColor, TextAnchor.UpperCenter);
            if (line != null)
            {
                line.fontStyle = FontStyle.Italic;
            }

            SetRetryVisible(canRetry);
        }

        /// <inheritdoc />
        public void SetThinking(bool isThinking)
        {
            ApplyThinkingState(isThinking);
            if (isThinking)
            {
                SetRetryVisible(false);
            }
            else
            {
                FocusInput();
            }
        }

        private void Submit()
        {
            if (!IsVisible || _isThinking || _inputField == null)
            {
                return;
            }

            string text = _inputField.text != null ? _inputField.text.Trim() : string.Empty;
            if (text.Length == 0)
            {
                FocusInput();
                return;
            }

            _inputField.text = string.Empty;
            SetRetryVisible(false);
            MessageSubmitted?.Invoke(text);
            FocusInput();
        }

        private void OnRetryClicked()
        {
            if (_isThinking)
            {
                return;
            }

            SetRetryVisible(false);
            RetryRequested?.Invoke();
        }

        private void RequestClose()
        {
            if (!IsVisible)
            {
                return;
            }

            Hide();
            Closed?.Invoke();
        }

        private void ApplyThinkingState(bool isThinking)
        {
            _isThinking = isThinking;
            _thinkingFrame = -1;

            if (_thinkingText != null)
            {
                _thinkingText.gameObject.SetActive(isThinking);
                if (isThinking)
                {
                    AnimateThinking();
                }
            }

            if (_inputField != null)
            {
                _inputField.interactable = !isThinking;
            }

            if (_sendButton != null)
            {
                _sendButton.interactable = !isThinking;
            }
        }

        /// <summary>Kare hızından ve timeScale'den bağımsız: kare, gerçek zamanın aralığa bölümünden seçilir.</summary>
        private void AnimateThinking()
        {
            if (_thinkingText == null)
            {
                return;
            }

            int frame = (int)(Time.unscaledTime / _thinkingDotInterval) % _thinkingFrames.Length;
            if (frame == _thinkingFrame)
            {
                return;
            }

            _thinkingFrame = frame;
            _thinkingText.text = $"{_npcName} {_thinkingFrames[frame].ToLowerInvariant()}";
        }

        private Text AddLine(string message, Color color, TextAnchor alignment)
        {
            if (_messageTemplate == null || _historyContent == null)
            {
                Debug.LogWarning($"[DialogueView] Satır şablonu bağlı değil: {message}", this);
                return null;
            }

            Text line = Instantiate(_messageTemplate, _historyContent);
            line.gameObject.SetActive(true);
            line.supportRichText = false;
            line.text = message;
            line.color = color;
            line.alignment = alignment;
            _lines.Enqueue(line);

            while (_lines.Count > _maxLines)
            {
                Text oldest = _lines.Dequeue();
                if (oldest != null)
                {
                    Destroy(oldest.gameObject);
                }
            }

            _scrollPending = true;
            return line;
        }

        private void ClearHistory()
        {
            while (_lines.Count > 0)
            {
                Text line = _lines.Dequeue();
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }
        }

        private void SetBadge(NpcEmotion emotion, int trust)
        {
            if (_badgeText != null)
            {
                _badgeText.text = trust >= 0 ? $"{LabelFor(emotion)} · Güven {trust}/100" : LabelFor(emotion);
            }

            if (_badgeBackground != null)
            {
                Color c = ColorFor(emotion);
                _badgeBackground.color = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.9f);
            }
        }

        private void SetRetryVisible(bool visible)
        {
            if (_retryButton != null)
            {
                _retryButton.gameObject.SetActive(visible);
            }
        }

        private void FocusInput()
        {
            if (_inputField != null && _inputField.interactable && IsVisible)
            {
                _inputField.ActivateInputField();
            }
        }

        private void AcquireLock()
        {
            if (_lockHeld)
            {
                return;
            }

            InputLock.Acquire();
            _lockHeld = true;
        }

        private void ReleaseLock()
        {
            if (!_lockHeld)
            {
                return;
            }

            InputLock.Release();
            _lockHeld = false;
        }

        private Color ColorFor(NpcEmotion emotion)
        {
            switch (emotion)
            {
                case NpcEmotion.Friendly:
                    return _friendlyColor;
                case NpcEmotion.Suspicious:
                    return _suspiciousColor;
                case NpcEmotion.Angry:
                    return _angryColor;
                default:
                    return _neutralColor;
            }
        }

        private static string LabelFor(NpcEmotion emotion)
        {
            switch (emotion)
            {
                case NpcEmotion.Friendly:
                    return "Dostça";
                case NpcEmotion.Suspicious:
                    return "Şüpheci";
                case NpcEmotion.Angry:
                    return "Öfkeli";
                default:
                    return "Nötr";
            }
        }
    }
}
