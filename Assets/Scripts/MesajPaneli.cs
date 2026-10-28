using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Yazlab.Core;

// Üst ortadaki mesaj satırı. IMessageService üzerinden veya geriye dönük
// MesajPaneli.Goster("...") ile çağrılabilir; süresi dolunca kendiliğinden kaybolur.
/// <summary>
/// Ekranın üst orta kısmında geçici bilgilendirme mesajları gösteren UI bileşeni.
/// <see cref="IMessageService"/> arayüzünü uygular ve <see cref="Time.unscaledTime"/> kullanır.
/// </summary>
[DisallowMultipleComponent]
public class MesajPaneli : MonoBehaviour, IMessageService
{
    [FormerlySerializedAs("yazi")]
    [SerializeField] private Text messageText;

    [SerializeField] private float defaultDuration = 4f;

    [Tooltip("Mesaj varken görünen arka plan paneli (isteğe bağlı; metin bunun içinde olabilir).")]
    [SerializeField] private GameObject _background;

    private readonly System.Collections.Generic.List<MessageEntry> _activeMessages = new System.Collections.Generic.List<MessageEntry>(4);

    private struct MessageEntry
    {
        public string Text;
        public float ExpireAtUnscaledTime;
    }

    /// <summary>
    /// Mesajların yazıldığı UI metin bileşeni.
    /// </summary>
    public Text MessageText
    {
        get => messageText;
        set => messageText = value;
    }

    private void Awake()
    {
        ServiceRegistry.Register<IMessageService>(this);
        ServiceRegistry.Register(this);
    }

    private void Start()
    {
        ResolveTextIfNeeded();
        if (messageText != null)
        {
            messageText.verticalOverflow = VerticalWrapMode.Overflow;
            Display(string.Empty);
        }
    }

    private void OnDestroy()
    {
        ServiceRegistry.Unregister<IMessageService>(this);
        ServiceRegistry.Unregister(this);
    }

    private void Update()
    {
        if (messageText == null || _activeMessages.Count == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        bool changed = false;
        for (int i = _activeMessages.Count - 1; i >= 0; i--)
        {
            if (now >= _activeMessages[i].ExpireAtUnscaledTime)
            {
                _activeMessages.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            RefreshDisplayText();
        }
    }

    /// <inheritdoc />
    public void Show(string text, float duration = 4f)
    {
        ResolveTextIfNeeded();
        if (messageText == null)
        {
            Debug.Log("[Mesaj] " + text);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _activeMessages.Clear();
            Display(string.Empty);
            return;
        }

        float effectiveDuration = duration > 0f ? duration : defaultDuration;
        float expireAt = Time.unscaledTime + Mathf.Max(0.1f, effectiveDuration);

        // Aynı metin zaten listedeyse süresini yenile
        bool updatedExisting = false;
        for (int i = 0; i < _activeMessages.Count; i++)
        {
            if (_activeMessages[i].Text == text)
            {
                _activeMessages[i] = new MessageEntry { Text = text, ExpireAtUnscaledTime = expireAt };
                updatedExisting = true;
                break;
            }
        }

        if (!updatedExisting)
        {
            if (_activeMessages.Count >= 3)
            {
                _activeMessages.RemoveAt(0);
            }

            _activeMessages.Add(new MessageEntry { Text = text, ExpireAtUnscaledTime = expireAt });
        }

        RefreshDisplayText();
    }

    private void RefreshDisplayText()
    {
        if (messageText == null)
        {
            return;
        }

        if (_activeMessages.Count == 0)
        {
            Display(string.Empty);
            return;
        }

        if (_activeMessages.Count == 1)
        {
            Display(_activeMessages[0].Text);
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < _activeMessages.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            sb.Append(_activeMessages[i].Text);
        }

        Display(sb.ToString());
    }

    private void Display(string text)
    {
        messageText.text = text;
        if (_background != null)
        {
            _background.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    /// <summary>
    /// Geriye dönük uyumluluk için <see cref="IMessageService"/> üzerinden mesaj gösterir.
    /// </summary>
    /// <param name="s">Gösterilecek mesaj metni.</param>
    public static void Goster(string s)
    {
        if (ServiceRegistry.TryGet<IMessageService>(out var service))
        {
            service.Show(s);
            return;
        }

        Debug.Log("[Mesaj] " + s);
    }

    private void ResolveTextIfNeeded()
    {
        if (messageText != null)
        {
            return;
        }

        Transform child = transform.Find("MsgText");
        if (child != null && child.TryGetComponent(out messageText))
        {
            return;
        }

        GameObject found = GameObject.Find("MsgText");
        if (found != null)
        {
            messageText = found.GetComponent<Text>();
        }
    }
}
