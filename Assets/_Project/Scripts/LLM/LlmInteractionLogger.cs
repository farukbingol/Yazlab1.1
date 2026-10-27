using System;
using System.IO;
using UnityEngine;
using Yazlab.Core;

namespace Yazlab.LLM
{
    /// <summary>
    /// Aynı objedeki <see cref="NpcDialogueController"/>'ın her turunu (başarılı veya hatalı)
    /// <c>Application.persistentDataPath/llm_log.csv</c> dosyasına ekler. Dosya oturumlar boyunca büyür;
    /// <c>SessionId</c> sütunu oturumları ayırır. Rapordaki diyalog örnekleri ve model karşılaştırması buradan alınır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NpcDialogueController))]
    public class LlmInteractionLogger : MonoBehaviour
    {
        private static readonly string[] Header =
        {
            "SessionId", "Timestamp", "Model", "ScenarioTag", "PlayerInput", "RawOutput", "ParseSuccess", "Emotion",
            "TrustDelta", "FinalTrust", "RequestedAction", "Action", "CorrectionUsed", "LatencyMs", "ErrorKind", "Error"
        };

        [SerializeField] private string _fileName = "llm_log.csv";
        [Tooltip("Satırlara yazılan senaryo etiketi (oyun içinde 'play'; test koşucusu değiştirir).")]
        [SerializeField] private string _scenarioTag = "play";

        private readonly string[] _row = new string[Header.Length];
        private NpcDialogueController _controller;
        private CsvLogger _csv;
        private string _sessionId;

        /// <summary>CSV dosyasının tam yolu.</summary>
        public string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

        /// <summary>Sonraki satırlara yazılacak senaryo etiketi (ör. B23 'yalan', 'tehdit').</summary>
        public string ScenarioTag
        {
            get => _scenarioTag;
            set => _scenarioTag = string.IsNullOrWhiteSpace(value) ? "play" : value.Trim();
        }

        /// <summary>Bu oturumda yazılan satır sayısı.</summary>
        public int RowsWritten { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<NpcDialogueController>();
            _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _csv = new CsvLogger(FilePath, Header, append: true);
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.TurnCompleted += OnTurnCompleted;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.TurnCompleted -= OnTurnCompleted;
            }
        }

        private void OnTurnCompleted(DialogueTurnRecord record)
        {
            _row[0] = _sessionId;
            _row[1] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _row[2] = record.Model;
            _row[3] = _scenarioTag;
            _row[4] = record.PlayerMessage;
            _row[5] = record.RawResponse;
            _row[6] = record.ParseOk ? "1" : "0";
            _row[7] = record.ParseOk ? NpcResponseSchema.ToWireName(record.Emotion) : string.Empty;
            _row[8] = record.ParseOk ? record.TrustDelta.ToString() : string.Empty;
            _row[9] = record.Trust.ToString();
            _row[10] = NpcResponseSchema.ToWireName(record.RequestedAction);
            _row[11] = NpcResponseSchema.ToWireName(record.AppliedAction);
            _row[12] = record.CorrectionUsed ? "1" : "0";
            _row[13] = record.LatencyMs.ToString();
            _row[14] = record.ErrorKind.ToString();
            _row[15] = record.Error;
            _csv?.AppendRow(_row);
            RowsWritten++;
        }
    }
}
