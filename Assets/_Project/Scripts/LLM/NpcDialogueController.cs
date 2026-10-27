using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Yazlab.Actions;
using Yazlab.Core;
using Yazlab.Player;
using Yazlab.UI;

namespace Yazlab.LLM
{
    /// <summary>
    /// Kapı bekçisi NPC'nin diyalog denetleyicisi: oyuncu yakındayken E ile pencereyi açar, mesajı
    /// <see cref="DialogueSession"/> ile isteğe çevirir, <see cref="ILlmClient"/> ile gönderir (ana thread bloklanmaz),
    /// <see cref="INpcResponseParser"/> ile ayrıştırır (bozuk JSON'da 1 düzeltme isteği), güveni günceller ve yapılandırılmış
    /// aksiyonu güvenlik kontrolünden geçirip <see cref="GameActionDispatcher"/>'a verir.
    /// Hata durumlarında NPC adına replik üretmez; <c>[Sistem]</c> mesajı gösterir ve oyun devam eder.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcDialogueController : MonoBehaviour
    {
        /// <summary>Bozuk JSON sonrası modele gönderilen düzeltme isteği.</summary>
        public const string CorrectionPrompt = "Yanıtın geçerli JSON değildi, yalnızca şemaya uygun JSON döndür.";

        private const string ParseFailedMessage = "[Sistem] Model yanıtı ayrıştırılamadı.";
        private const string UnexpectedMessage = "[Sistem] Beklenmeyen hata; konuşma güvenli şekilde sürdürülüyor.";

        [Header("Bağlantılar")]
        [SerializeField] private LlmConfig _config;
        [SerializeField] private NpcPersona _persona;
        [Tooltip("IDialogueView uygulayan görünüm (OyuncuUI/DialogueUI üzerindeki DialogueView).")]
        [SerializeField] private MonoBehaviour _view;
        [Tooltip("Boşsa 'Player' etiketli obje bir kez aranır.")]
        [SerializeField] private Transform _player;

        [Header("Etkileşim")]
        [SerializeField, Min(0.5f)] private float _interactionRange = 3f;
        [Tooltip("Oyuncu menzile girince gösterilen ipucu ({0} = NPC adı).")]
        [SerializeField] private string _interactHint = "E: {0} ile konuş";
        [Tooltip("Konuşma kalıcı bitince (kapı/savaş) pencerenin kapanmasından önceki bekleme (gerçek sn).")]
        [SerializeField, Min(0f)] private float _closeDelay = 2.5f;
        [Tooltip("Başlangıçta sağlık kontrolü + kısa ısınma isteği at (ilk yanıt gecikmesini azaltır).")]
        [SerializeField] private bool _warmUpOnStart = true;
        [SerializeField] private bool _debugLog = true;

        private readonly GameActionDispatcher _dispatcher = new GameActionDispatcher();
        private readonly List<IGameAction> _actionBuffer = new List<IGameAction>();

        private IDialogueView _dialogueView;
        private ILlmClient _client;
        private INpcResponseParser _parser;
        private DialogueSession _session;
        private PlayerInputReader _input;
        private CancellationTokenSource _requestCts;
        private RetryKind _retryKind;
        private string _retryPlayerText;
        private bool _inRange;
        private bool _finished;
        private bool _closing;

        private enum RetryKind
        {
            None,
            Opening,
            PlayerTurn
        }

        /// <summary>Her tur tamamlandığında (başarılı veya hatalı) tetiklenir.</summary>
        public event Action<DialogueTurnRecord> TurnCompleted;

        /// <summary>Aktif oturum (konuşma hiç açılmadıysa <c>null</c>).</summary>
        public DialogueSession Session => _session;

        /// <summary>Konuşma kalıcı olarak bitti mi (kapı açıldı / savaş başladı).</summary>
        public bool IsFinished => _finished;

        /// <summary>Son sağlık kontrolü sonucu (B20 durum ışığı için; henüz yapılmadıysa <c>null</c>).</summary>
        public LlmResult LastHealth { get; private set; }

        /// <summary>Oyuncu etkileşim menzilinde mi.</summary>
        public bool IsPlayerInRange => _inRange;

        private void Awake()
        {
            _dialogueView = _view as IDialogueView;
            if (_view != null && _dialogueView == null)
            {
                Debug.LogError($"[NpcDialogue] {name}: görünüm IDialogueView uygulamıyor.", this);
            }

            if (_config == null || _persona == null)
            {
                Debug.LogError($"[NpcDialogue] {name}: LlmConfig veya NpcPersona atanmamış; NPC konuşamaz.", this);
            }

            _client = new OllamaClient(_config);
            _parser = new JsonNpcResponseParser();

            GetComponents(_actionBuffer);
            foreach (IGameAction action in _actionBuffer)
            {
                _dispatcher.Register(action);
            }
        }

        private void OnEnable()
        {
            if (_dialogueView != null)
            {
                _dialogueView.MessageSubmitted += OnMessageSubmitted;
                _dialogueView.RetryRequested += OnRetryRequested;
                _dialogueView.Closed += OnViewClosed;
            }
        }

        private void Start()
        {
            ResolvePlayer();
            if (_warmUpOnStart)
            {
                _ = WarmUpAsync();
            }
        }

        private void OnDisable()
        {
            if (_dialogueView != null)
            {
                _dialogueView.MessageSubmitted -= OnMessageSubmitted;
                _dialogueView.RetryRequested -= OnRetryRequested;
                _dialogueView.Closed -= OnViewClosed;
            }

            if (_input != null)
            {
                _input.InteractTriggered -= OnInteract;
            }

            CancelRequest();
        }

        private void Update()
        {
            if (_player == null || _finished)
            {
                return;
            }

            Vector3 delta = _player.position - transform.position;
            delta.y = 0f;
            bool inRange = delta.sqrMagnitude <= _interactionRange * _interactionRange;
            if (inRange && !_inRange && !IsViewVisible && ServiceRegistry.TryGet(out IMessageService messages))
            {
                messages.Show(string.Format(_interactHint, NpcName), 3f);
            }

            _inRange = inRange;
        }

        /// <summary>
        /// Oyuncu menzildeyse konuşmayı açar (E tuşu da bunu çağırır). İlk açılışta selamı model üretir;
        /// sonraki açılışlarda geçmiş ve güven korunur.
        /// </summary>
        public bool TryStartConversation()
        {
            if (_finished || _closing || _dialogueView == null || _config == null || _persona == null || IsViewVisible)
            {
                return false;
            }

            bool firstTime = _session == null || _session.State == DialogueState.Ended;
            if (firstTime)
            {
                _session = new DialogueSession(_persona, _config.OpenTrustThreshold, ResolveStateProvider());
                _session.Start();
            }

            _dialogueView.Show(NpcName);
            GameEvents.RaiseDialogueStarted();

            if (firstTime || _session.TurnCount == 0)
            {
                if (_session.TryBeginOpening(out LlmRequest opening))
                {
                    _ = RunTurnAsync(opening, DialogueSession.OpeningCue, RetryKind.Opening);
                }
            }
            else
            {
                ReplayHistory();
            }

            return true;
        }

        private bool IsViewVisible => _dialogueView != null && _dialogueView.IsVisible;

        private string NpcName => _persona != null ? _persona.NpcName : name;

        private void OnInteract()
        {
            if (_inRange)
            {
                TryStartConversation();
            }
        }

        private void OnMessageSubmitted(string text)
        {
            if (_session == null || _finished || _closing)
            {
                return;
            }

            if (!_session.TryBeginTurn(text, out LlmRequest request, out string error))
            {
                _dialogueView.AppendSystemError(error, false);
                return;
            }

            _dialogueView.AppendPlayerMessage(text);
            _ = RunTurnAsync(request, text, RetryKind.PlayerTurn);
        }

        private void OnRetryRequested()
        {
            if (_session == null || _finished)
            {
                return;
            }

            switch (_retryKind)
            {
                case RetryKind.Opening when _session.TryBeginOpening(out LlmRequest opening):
                    _ = RunTurnAsync(opening, DialogueSession.OpeningCue, RetryKind.Opening);
                    break;
                case RetryKind.PlayerTurn when _session.TryBeginTurn(_retryPlayerText, out LlmRequest request, out _):
                    _ = RunTurnAsync(request, _retryPlayerText, RetryKind.PlayerTurn);
                    break;
            }
        }

        private void OnViewClosed()
        {
            CancelRequest();
            _session?.FailTurn();
            GameEvents.RaiseDialogueEnded();
        }

        /// <summary>Tek tur: istek → (gerekirse 1 düzeltme) → ayrıştırma → güven → koruma → aksiyon.</summary>
        private async Awaitable RunTurnAsync(LlmRequest request, string playerText, RetryKind kind)
        {
            CancelRequest();
            _requestCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = _requestCts.Token;
            _retryKind = RetryKind.None;
            _dialogueView.SetThinking(true);

            long latency = 0;
            bool correctionUsed = false;
            string raw = null;
            try
            {
                LlmResult result = await _client.SendChatAsync(request, token);
                latency += result.LatencyMs;
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (!result.IsSuccess)
                {
                    FailTurn(kind, playerText, result.UserFriendlyError, result.ErrorKind != LlmErrorKind.InvalidConfig);
                    Report(playerText, null, false, result.ErrorKind, result.TechnicalDetail, NpcAction.None, NpcAction.None, latency, false);
                    return;
                }

                raw = result.RawContent;
                if (!_parser.TryParse(raw, out NpcResponse response, out string parseError))
                {
                    correctionUsed = true;
                    LogDebug($"Bozuk JSON ({parseError}); düzeltme isteği gönderiliyor. Ham: {raw}");
                    LlmResult retry = await _client.SendChatAsync(BuildCorrectionRequest(request, raw), token);
                    latency += retry.LatencyMs;
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (retry.IsSuccess)
                    {
                        raw = retry.RawContent;
                    }

                    if (!retry.IsSuccess || !_parser.TryParse(raw, out response, out parseError))
                    {
                        FailTurn(kind, playerText, retry.IsSuccess ? ParseFailedMessage : retry.UserFriendlyError, true);
                        Report(playerText, raw, false, retry.IsSuccess ? LlmErrorKind.InvalidResponse : retry.ErrorKind,
                            retry.IsSuccess ? parseError : retry.TechnicalDetail, NpcAction.None, NpcAction.None, latency, true);
                        return;
                    }
                }

                (int previousTrust, int trust) = _session.CompleteTurn(response);
                NpcAction applied = ApplyGuards(response.Action);
                _dialogueView.SetThinking(false);
                _dialogueView.AppendNpcMessage(response.Reply, response.Emotion, trust);

                LogDebug($"{latency} ms | güven {previousTrust}→{trust} | {response.Emotion} | " +
                         $"aksiyon {response.Action}{(applied != response.Action ? $"→{applied}" : string.Empty)}" +
                         $"{(correctionUsed ? " | düzeltme isteğiyle" : string.Empty)}");
                Report(playerText, raw, true, LlmErrorKind.None, null, response.Action, applied, latency, correctionUsed);

                await ApplyActionAsync(applied);
            }
            catch (OperationCanceledException)
            {
                // Diyalog kapandı veya obje yok edildi; tur zaten iptal.
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                FailTurn(kind, playerText, UnexpectedMessage, true);
                Report(playerText, raw, false, LlmErrorKind.Unexpected, e.Message, NpcAction.None, NpcAction.None, latency, correctionUsed);
            }
        }

        /// <summary>
        /// Model kararına körü körüne uyulmaz: <c>open_door</c> yalnızca şef gerçekten öldürüldüyse (oyun durumu) ve
        /// güven eşiği aşıldıysa uygulanır; aksi halde <see cref="NpcAction.None"/>.
        /// </summary>
        private NpcAction ApplyGuards(NpcAction requested)
        {
            if (requested != NpcAction.OpenDoor)
            {
                return requested;
            }

            bool bossDefeated = _session.LastSnapshot.HasValue && _session.LastSnapshot.Value.BossDefeated;
            bool trustOk = _session.IsTrustEnoughToOpen;
            if (bossDefeated && trustOk)
            {
                return NpcAction.OpenDoor;
            }

            Debug.Log($"[NpcDialogue] open_door reddedildi (şef öldü: {bossDefeated}, güven {_session.CurrentTrust} / eşik {_session.OpenThreshold}).");
            return NpcAction.None;
        }

        /// <summary>
        /// Konuşmayı sürdüren aksiyon hemen uygulanır. Konuşmayı kapatan/bitiren aksiyonda önce oyuncu son repliği okur,
        /// pencere kapanır (girdi kilidi kalkar), aksiyon ondan sonra uygulanır — ör. savaş başlarken oyuncu kilitli kalmaz.
        /// </summary>
        private async Awaitable ApplyActionAsync(NpcAction action)
        {
            if (!_dispatcher.TryGetOutcome(action, out DialogueOutcome outcome) || outcome == DialogueOutcome.Continue)
            {
                _dispatcher.TryDispatch(action, out _);
                return;
            }

            _closing = true;
            if (outcome == DialogueOutcome.Finish)
            {
                _finished = true;
                _session.End();
            }

            try
            {
                // Bekleme gerçek zamanla (timeScale'den bağımsız).
                float closeAt = Time.unscaledTime + _closeDelay;
                while (Time.unscaledTime < closeAt)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                if (IsViewVisible)
                {
                    _dialogueView.Hide();
                    GameEvents.RaiseDialogueEnded();
                }

                _dispatcher.TryDispatch(action, out _);
            }
            finally
            {
                _closing = false;
            }
        }

        private void FailTurn(RetryKind kind, string playerText, string message, bool canRetry)
        {
            _session.FailTurn();
            _retryKind = canRetry ? kind : RetryKind.None;
            _retryPlayerText = playerText;
            _dialogueView.SetThinking(false);
            _dialogueView.AppendSystemError(message, canRetry);
        }

        private static LlmRequest BuildCorrectionRequest(LlmRequest original, string badRaw)
        {
            var messages = new List<ChatMessage>(original.Messages.Count + 2);
            messages.AddRange(original.Messages);
            messages.Add(ChatMessage.Assistant(badRaw ?? string.Empty));
            messages.Add(ChatMessage.User(CorrectionPrompt));
            return new LlmRequest(messages, original.Format);
        }

        /// <summary>Pencere yeniden açıldığında önceki konuşmayı (model geçmişinden) ekrana geri yazar.</summary>
        private void ReplayHistory()
        {
            IReadOnlyList<ChatMessage> history = _session.History;
            for (int i = 0; i < history.Count; i++)
            {
                ChatMessage message = history[i];
                if (message.Role == ChatMessage.UserRole)
                {
                    if (message.Content != DialogueSession.OpeningCue)
                    {
                        _dialogueView.AppendPlayerMessage(message.Content);
                    }
                }
                else if (message.Role == ChatMessage.AssistantRole &&
                         _parser.TryParse(message.Content, out NpcResponse past, out _))
                {
                    _dialogueView.AppendNpcMessage(past.Reply, past.Emotion, _session.CurrentTrust);
                }
            }
        }

        private async Awaitable WarmUpAsync()
        {
            try
            {
                LastHealth = await _client.CheckHealthAsync(destroyCancellationToken);
                if (!LastHealth.IsSuccess)
                {
                    Debug.LogWarning($"[NpcDialogue] LLM hazır değil: {LastHealth.UserFriendlyError} ({LastHealth.TechnicalDetail})");
                    return;
                }

                var warmUp = new LlmRequest(new[] { ChatMessage.User("Hazır mısın? Tek kelimeyle cevap ver.") });
                LlmResult result = await _client.SendChatAsync(warmUp, destroyCancellationToken);
                LogDebug(result.IsSuccess
                    ? $"Model ısındı ({LastHealth.RawContent}, {result.LatencyMs} ms)."
                    : $"Isınma isteği başarısız: {result.ErrorKind} ({result.TechnicalDetail}).");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void Report(string playerText, string raw, bool parseOk, LlmErrorKind errorKind, string error,
            NpcAction requested, NpcAction applied, long latency, bool correctionUsed)
        {
            TurnCompleted?.Invoke(new DialogueTurnRecord(playerText, raw, parseOk, errorKind, error, requested, applied,
                _session != null ? _session.CurrentTrust : 0, latency, correctionUsed, _config != null ? _config.ModelName : string.Empty));
        }

        private void CancelRequest()
        {
            if (_requestCts == null)
            {
                return;
            }

            _requestCts.Cancel();
            _requestCts.Dispose();
            _requestCts = null;
        }

        private void ResolvePlayer()
        {
            if (_player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                _player = found != null ? found.transform : null;
            }

            if (_player != null && _player.TryGetComponent(out _input))
            {
                _input.InteractTriggered += OnInteract;
            }
        }

        private static IGameStateProvider ResolveStateProvider()
        {
            return ServiceRegistry.TryGet(out IGameStateProvider provider) ? provider : null;
        }

        private void LogDebug(string message)
        {
            if (_debugLog)
            {
                Debug.Log($"[NpcDialogue] {message}");
            }
        }
    }
}
