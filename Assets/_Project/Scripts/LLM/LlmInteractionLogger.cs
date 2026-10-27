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
        [SerializeField] private string _fileName = "llm_log.csv";
        [Tooltip("Satırlara yazılan senaryo etiketi (oyun içinde 'play'; test koşucusu değiştirir).")]
        [SerializeField] private string _scenarioTag = "play";

        private readonly string[] _row = new string[LlmLogFormat.Header.Length];
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
            _csv = new CsvLogger(FilePath, LlmLogFormat.Header, append: true);
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
            LlmLogFormat.Fill(_row, record, _sessionId, _scenarioTag);
            _csv?.AppendRow(_row);
            RowsWritten++;
        }
    }
}
