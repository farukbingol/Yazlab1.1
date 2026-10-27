using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Yazlab.LLM
{
    /// <summary>
    /// Modelin <c>message.content</c> metnini <see cref="NpcResponse"/>'a çevirir. Markdown kod bloğu veya önüne/arkasına
    /// eklenmiş metin varsa ilk dengeli <c>{...}</c> nesnesini ayıklar. Kurallar: <c>reply</c> boş/eksikse başarısız;
    /// <c>trust_delta</c> aralığa çekilir; bilinmeyen/eksik <c>emotion</c> → <see cref="NpcEmotion.Neutral"/>,
    /// <c>action</c> → <see cref="NpcAction.None"/> (uyarıyla). Hiçbir girdide istisna fırlatmaz.
    /// Yalnızca yapılandırılmış alanları okur; <c>reply</c> metninden aksiyon çıkarmaz.
    /// </summary>
    public sealed class JsonNpcResponseParser : INpcResponseParser
    {
        private readonly bool _logWarnings;

        /// <param name="logWarnings">Düzeltmeleri Console'a uyarı olarak da yaz.</param>
        public JsonNpcResponseParser(bool logWarnings = true)
        {
            _logWarnings = logWarnings;
        }

        /// <inheritdoc />
        public bool TryParse(string rawContent, out NpcResponse response, out string error)
        {
            response = null;
            try
            {
                return TryParseInternal(rawContent, out response, out error);
            }
            catch (Exception e)
            {
                // Beklenmeyen durum (ör. Newtonsoft iç hatası) — sözleşme gereği istisna dışarı sızmaz.
                error = $"Beklenmeyen ayrıştırma hatası: {e.Message}";
                return false;
            }
        }

        private bool TryParseInternal(string rawContent, out NpcResponse response, out string error)
        {
            response = null;
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                error = "Yanıt boş.";
                return false;
            }

            if (!TryExtractObject(rawContent, out string json, out error))
            {
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                error = $"Geçersiz JSON: {e.Message}";
                return false;
            }

            JToken replyToken = root[NpcResponseSchema.ReplyField];
            if (replyToken == null || replyToken.Type == JTokenType.Null)
            {
                error = $"'{NpcResponseSchema.ReplyField}' alanı eksik.";
                return false;
            }

            if (replyToken.Type != JTokenType.String)
            {
                error = $"'{NpcResponseSchema.ReplyField}' metin değil ({replyToken.Type}).";
                return false;
            }

            string reply = ((string)replyToken).Trim();
            if (reply.Length == 0)
            {
                error = $"'{NpcResponseSchema.ReplyField}' boş.";
                return false;
            }

            var warnings = new List<string>();
            NpcEmotion emotion = ReadEnum(root, NpcResponseSchema.EmotionField, NpcResponseSchema.EmotionValues,
                NpcEmotion.Neutral, warnings);
            NpcAction action = ReadEnum(root, NpcResponseSchema.ActionField, NpcResponseSchema.ActionValues,
                NpcAction.None, warnings);
            int trustDelta = ReadTrustDelta(root, warnings);

            if (_logWarnings)
            {
                foreach (string warning in warnings)
                {
                    Debug.LogWarning($"[NpcParser] {warning}");
                }
            }

            response = new NpcResponse(reply, emotion, trustDelta, action, warnings);
            error = null;
            return true;
        }

        /// <summary>
        /// Metindeki ilk dengeli JSON nesnesini (string içindeki süslü parantezleri ve kaçışları hesaba katarak) ayıklar.
        /// Kapanmayan nesne kesik yanıt sayılır.
        /// </summary>
        public static bool TryExtractObject(string text, out string json, out string error)
        {
            json = null;
            int start = text.IndexOf('{');
            if (start < 0)
            {
                error = "Yanıtta JSON nesnesi yok.";
                return false;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        json = text.Substring(start, i - start + 1);
                        error = null;
                        return true;
                    }
                }
            }

            error = "JSON nesnesi kapanmıyor (yanıt kesik).";
            return false;
        }

        private static T ReadEnum<T>(JObject root, string field, string[] wireValues, T fallback, List<string> warnings)
            where T : struct, Enum
        {
            JToken token = root[field];
            if (token == null || token.Type == JTokenType.Null)
            {
                warnings.Add($"'{field}' eksik → {wireValues[Convert.ToInt32(fallback)]}.");
                return fallback;
            }

            string raw = token.Type == JTokenType.String ? (string)token : token.ToString();
            string normalized = Normalize(raw);
            for (int i = 0; i < wireValues.Length; i++)
            {
                if (Normalize(wireValues[i]) == normalized)
                {
                    return (T)Enum.ToObject(typeof(T), i);
                }
            }

            warnings.Add($"Bilinmeyen '{field}' değeri '{raw}' → {wireValues[Convert.ToInt32(fallback)]}.");
            return fallback;
        }

        /// <summary>Büyük/küçük harf, boşluk, '-' ve '_' farkını yok sayar (ör. "Open Door" = "open_door").</summary>
        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var chars = new char[value.Length];
            int count = 0;
            foreach (char c in value)
            {
                if (c == '_' || c == '-' || char.IsWhiteSpace(c))
                {
                    continue;
                }

                chars[count++] = char.ToLowerInvariant(c);
            }

            return new string(chars, 0, count);
        }

        private static int ReadTrustDelta(JObject root, List<string> warnings)
        {
            string field = NpcResponseSchema.TrustDeltaField;
            JToken token = root[field];
            if (token == null || token.Type == JTokenType.Null)
            {
                warnings.Add($"'{field}' eksik → 0.");
                return 0;
            }

            double value;
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    value = token.Value<double>();
                    break;
                case JTokenType.String when double.TryParse(((string)token).Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double parsed):
                    value = parsed;
                    break;
                default:
                    warnings.Add($"'{field}' sayı değil ('{token}') → 0.");
                    return 0;
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                warnings.Add($"'{field}' geçersiz sayı → 0.");
                return 0;
            }

            double rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            if (rounded < NpcResponseSchema.TrustDeltaMin || rounded > NpcResponseSchema.TrustDeltaMax)
            {
                int clamped = rounded < NpcResponseSchema.TrustDeltaMin ? NpcResponseSchema.TrustDeltaMin : NpcResponseSchema.TrustDeltaMax;
                warnings.Add($"'{field}' {value.ToString(CultureInfo.InvariantCulture)} aralık dışı → {clamped}.");
                return clamped;
            }

            if (Math.Abs(rounded - value) > double.Epsilon)
            {
                warnings.Add($"'{field}' {value.ToString(CultureInfo.InvariantCulture)} tam sayıya yuvarlandı → {rounded}.");
            }

            return (int)rounded;
        }
    }
}
