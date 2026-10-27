using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Yazlab.Core;

namespace Yazlab.LLM
{
    /// <summary>Diyalog oturumunun durumu.</summary>
    public enum DialogueState
    {
        /// <summary>Başlamadı.</summary>
        Idle,

        /// <summary>Oyuncu mesajı bekleniyor.</summary>
        Active,

        /// <summary>LLM yanıtı bekleniyor (yeni mesaj kabul edilmez).</summary>
        AwaitingResponse,

        /// <summary>Konuşma bitti.</summary>
        Ended
    }

    /// <summary>
    /// Kapı bekçisiyle tek bir konuşmanın durumu: mesaj geçmişi (son N mesaj), birikimli güven (0–100) ve tur akışı.
    /// Her turda system prompt güncel oyun durumuyla yeniden üretilir. Ağ çağrısı yapmaz; isteği hazırlar
    /// (<see cref="TryBeginTurn"/>), sonucu işler (<see cref="CompleteTurn"/> / <see cref="FailTurn"/>).
    /// Başarısız tur geçmişi değiştirmez; aynı mesaj yeniden denenebilir.
    /// </summary>
    public sealed class DialogueSession
    {
        /// <summary>Varsayılan geçmiş uzunluğu (mesaj sayısı; system prompt hariç).</summary>
        public const int DefaultMaxHistoryMessages = 12;

        /// <summary>Oyuncu mesajının varsayılan en büyük uzunluğu (karakter).</summary>
        public const int DefaultMaxPlayerMessageLength = 300;

        /// <summary>İlk selam turunda kullanıcı rolüyle gönderilen sahne notu (oyuncu sözü değildir).</summary>
        public const string OpeningCue = "[Sahne: yolcu kapıya yaklaştı, henüz bir şey söylemedi.]";

        private const int MinTrust = 0;
        private const int MaxTrust = 100;

        private readonly NpcPersona _persona;
        private readonly IGameStateProvider _stateProvider;
        private readonly SystemPromptBuilder _promptBuilder = new SystemPromptBuilder();
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private readonly JObject _schema = NpcResponseSchema.Build();
        private readonly int _maxHistoryMessages;
        private readonly int _maxPlayerMessageLength;

        private ChatMessage? _pendingUserMessage;

        /// <summary>Oturum durumu.</summary>
        public DialogueState State { get; private set; } = DialogueState.Idle;

        /// <summary>NPC'nin oyuncuya güveni (0–100).</summary>
        public int CurrentTrust { get; private set; }

        /// <summary>Kapı açma güven eşiği.</summary>
        public int OpenThreshold { get; }

        /// <summary>Güven eşiği aşıldı mı (kapı açma aksiyonu için koşullardan biri).</summary>
        public bool IsTrustEnoughToOpen => CurrentTrust >= OpenThreshold;

        /// <summary>Tamamlanan tur sayısı (selam dahil).</summary>
        public int TurnCount { get; private set; }

        /// <summary>Tamamlanmış mesaj geçmişi (system prompt hariç).</summary>
        public IReadOnlyList<ChatMessage> History => _history;

        /// <summary>Son hazırlanan system prompt (log ve rapor için).</summary>
        public string LastSystemPrompt { get; private set; } = string.Empty;

        /// <summary>Son turda yakalanan oyun durumu (alınamadıysa <c>null</c>).</summary>
        public GameStateSnapshot? LastSnapshot { get; private set; }

        /// <summary>Güven değiştiğinde (eski, yeni) tetiklenir.</summary>
        public event Action<int, int> TrustChanged;

        /// <summary>Durum değiştiğinde tetiklenir.</summary>
        public event Action<DialogueState> StateChanged;

        public DialogueSession(
            NpcPersona persona,
            int openThreshold,
            IGameStateProvider stateProvider,
            int maxHistoryMessages = DefaultMaxHistoryMessages,
            int maxPlayerMessageLength = DefaultMaxPlayerMessageLength)
        {
            _persona = persona;
            _stateProvider = stateProvider;
            OpenThreshold = Mathf.Clamp(openThreshold, MinTrust, MaxTrust);
            _maxHistoryMessages = Mathf.Max(2, maxHistoryMessages);
            _maxPlayerMessageLength = Mathf.Max(20, maxPlayerMessageLength);
        }

        /// <summary>Konuşmayı baştan başlatır: geçmiş temizlenir, güven persona başlangıç değerine döner.</summary>
        public void Start()
        {
            _history.Clear();
            _pendingUserMessage = null;
            TurnCount = 0;
            SetTrust(_persona != null ? _persona.InitialTrust : 0);
            SetState(DialogueState.Active);
        }

        /// <summary>Konuşmayı bitirir; bekleyen tur iptal edilir.</summary>
        public void End()
        {
            _pendingUserMessage = null;
            SetState(DialogueState.Ended);
        }

        /// <summary>
        /// İlk selam isteğini hazırlar (yalnızca hiç tur yapılmadıysa). Selam metnini model üretir.
        /// </summary>
        public bool TryBeginOpening(out LlmRequest request)
        {
            request = null;
            if (State != DialogueState.Active || TurnCount > 0)
            {
                return false;
            }

            _pendingUserMessage = ChatMessage.User(OpeningCue);
            request = BuildRequest(_pendingUserMessage.Value, isOpening: true);
            SetState(DialogueState.AwaitingResponse);
            return true;
        }

        /// <summary>
        /// Oyuncu mesajıyla yeni tur isteği hazırlar. Konuşma aktif değilse, önceki yanıt beklenirken veya mesaj boşsa
        /// <c>false</c> döner ve <paramref name="error"/> oyuncuya gösterilebilecek açıklama içerir.
        /// Mesaj <see cref="DefaultMaxPlayerMessageLength"/> karakterden uzunsa kırpılır.
        /// </summary>
        public bool TryBeginTurn(string playerMessage, out LlmRequest request, out string error)
        {
            request = null;
            if (State == DialogueState.AwaitingResponse)
            {
                error = "Muhafız henüz cevap vermedi.";
                return false;
            }

            if (State != DialogueState.Active)
            {
                error = "Konuşma aktif değil.";
                return false;
            }

            string text = playerMessage?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                error = "Boş mesaj gönderilemez.";
                return false;
            }

            if (text.Length > _maxPlayerMessageLength)
            {
                text = text.Substring(0, _maxPlayerMessageLength);
            }

            _pendingUserMessage = ChatMessage.User(text);
            request = BuildRequest(_pendingUserMessage.Value, isOpening: false);
            SetState(DialogueState.AwaitingResponse);
            error = null;
            return true;
        }

        /// <summary>
        /// Doğrulanmış NPC yanıtını işler: oyuncu mesajı ve yanıt geçmişe eklenir, güven
        /// <c>clamp(güven + TrustDelta, 0, 100)</c> olur. Aksiyonun uygulanması çağıranın işidir.
        /// </summary>
        /// <returns>(önceki güven, yeni güven).</returns>
        public (int Previous, int Current) CompleteTurn(NpcResponse response)
        {
            int previous = CurrentTrust;
            if (State != DialogueState.AwaitingResponse || response == null || !_pendingUserMessage.HasValue)
            {
                return (previous, previous);
            }

            _history.Add(_pendingUserMessage.Value);
            _history.Add(ChatMessage.Assistant(SerializeResponse(response)));
            _pendingUserMessage = null;
            TrimHistory();

            TurnCount++;
            SetTrust(previous + response.TrustDelta);
            SetState(DialogueState.Active);
            return (previous, CurrentTrust);
        }

        /// <summary>Tur başarısız oldu (ağ/parse hatası): bekleyen mesaj atılır, geçmiş ve güven değişmez.</summary>
        public void FailTurn()
        {
            if (State != DialogueState.AwaitingResponse)
            {
                return;
            }

            _pendingUserMessage = null;
            SetState(DialogueState.Active);
        }

        private LlmRequest BuildRequest(ChatMessage pending, bool isOpening)
        {
            LastSnapshot = CaptureState();
            LastSystemPrompt = _promptBuilder.Build(_persona, LastSnapshot, CurrentTrust, OpenThreshold, isOpening);

            var messages = new List<ChatMessage>(_history.Count + 2) { ChatMessage.System(LastSystemPrompt) };
            messages.AddRange(_history);
            messages.Add(pending);
            return new LlmRequest(messages, _schema);
        }

        private GameStateSnapshot? CaptureState()
        {
            if (_stateProvider == null)
            {
                return null;
            }

            try
            {
                return _stateProvider.Capture();
            }
            catch (Exception e)
            {
                // Sağlayıcı yok edilmiş olabilir; prompt "durum alınamadı" moduna geçer.
                Debug.LogWarning($"[Dialogue] Oyun durumu alınamadı: {e.Message}");
                return null;
            }
        }

        /// <summary>Geçmişi en eski oyuncu/NPC çiftinden başlayarak sınırda tutar.</summary>
        private void TrimHistory()
        {
            while (_history.Count > _maxHistoryMessages)
            {
                _history.RemoveRange(0, Math.Min(2, _history.Count));
            }
        }

        /// <summary>Modelin geçmişte kendi biçimini görmesi için yanıt şema JSON'u olarak saklanır.</summary>
        private static string SerializeResponse(NpcResponse response)
        {
            var json = new JObject
            {
                [NpcResponseSchema.ReplyField] = response.Reply,
                [NpcResponseSchema.EmotionField] = NpcResponseSchema.ToWireName(response.Emotion),
                [NpcResponseSchema.TrustDeltaField] = response.TrustDelta,
                [NpcResponseSchema.ActionField] = NpcResponseSchema.ToWireName(response.Action)
            };
            return json.ToString(Formatting.None);
        }

        private void SetTrust(int value)
        {
            int clamped = Mathf.Clamp(value, MinTrust, MaxTrust);
            if (clamped == CurrentTrust)
            {
                return;
            }

            int previous = CurrentTrust;
            CurrentTrust = clamped;
            TrustChanged?.Invoke(previous, clamped);
        }

        private void SetState(DialogueState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
