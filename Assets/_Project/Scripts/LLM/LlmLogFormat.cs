using System;

namespace Yazlab.LLM
{
    /// <summary>
    /// <c>llm_log.csv</c> satır biçimi (oyun içi logger ve senaryo testleri aynı sütunları yazar).
    /// </summary>
    public static class LlmLogFormat
    {
        /// <summary>Sütun adları.</summary>
        public static readonly string[] Header =
        {
            "SessionId", "Timestamp", "Model", "ScenarioTag", "PlayerInput", "RawOutput", "ParseSuccess", "Emotion",
            "TrustDelta", "FinalTrust", "RequestedAction", "Action", "CorrectionUsed", "LatencyMs", "ErrorKind", "Error"
        };

        /// <summary><paramref name="row"/> dizisini (uzunluğu <see cref="Header"/> kadar) kayıttan doldurur.</summary>
        public static void Fill(string[] row, DialogueTurnRecord record, string sessionId, string scenarioTag)
        {
            row[0] = sessionId;
            row[1] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            row[2] = record.Model;
            row[3] = scenarioTag;
            row[4] = record.PlayerMessage;
            row[5] = record.RawResponse;
            row[6] = record.ParseOk ? "1" : "0";
            row[7] = record.ParseOk ? NpcResponseSchema.ToWireName(record.Emotion) : string.Empty;
            row[8] = record.ParseOk ? record.TrustDelta.ToString() : string.Empty;
            row[9] = record.Trust.ToString();
            row[10] = NpcResponseSchema.ToWireName(record.RequestedAction);
            row[11] = NpcResponseSchema.ToWireName(record.AppliedAction);
            row[12] = record.CorrectionUsed ? "1" : "0";
            row[13] = record.LatencyMs.ToString();
            row[14] = record.ErrorKind.ToString();
            row[15] = record.Error;
        }
    }
}
