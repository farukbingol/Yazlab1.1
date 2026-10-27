using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Yazlab.LLM;

namespace Yazlab.Tests
{
    /// <summary>
    /// <see cref="JsonNpcResponseParser"/> birim testleri: geçerli, sarmalanmış, eksik, bozuk, kesik ve aralık dışı yanıtlar.
    /// Raporda "bozuk JSON testi" kanıtı olarak kullanılır.
    /// </summary>
    public class JsonNpcResponseParserTests
    {
        private JsonNpcResponseParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new JsonNpcResponseParser(logWarnings: false);
        }

        private NpcResponse ParseOk(string raw)
        {
            bool ok = _parser.TryParse(raw, out NpcResponse response, out string error);
            Assert.IsTrue(ok, $"Ayrıştırma başarısız: {error}");
            Assert.IsNotNull(response);
            Assert.IsNull(error);
            return response;
        }

        private string ParseFail(string raw)
        {
            bool ok = true;
            NpcResponse response = null;
            string error = null;
            Assert.DoesNotThrow(() => ok = _parser.TryParse(raw, out response, out error));
            Assert.IsFalse(ok, "Başarısız olması gerekirdi.");
            Assert.IsNull(response);
            Assert.IsFalse(string.IsNullOrEmpty(error), "Hata açıklaması boş.");
            return error;
        }

        // --- Geçerli yanıtlar ---

        [Test]
        public void ValidJson_ParsesAllFields()
        {
            NpcResponse r = ParseOk(
                "{\"reply\":\"Geçebilirsin, yolcu.\",\"emotion\":\"friendly\",\"trust_delta\":12,\"action\":\"open_door\"}");

            Assert.AreEqual("Geçebilirsin, yolcu.", r.Reply);
            Assert.AreEqual(NpcEmotion.Friendly, r.Emotion);
            Assert.AreEqual(12, r.TrustDelta);
            Assert.AreEqual(NpcAction.OpenDoor, r.Action);
            Assert.IsFalse(r.HasWarnings);
        }

        [Test]
        public void MarkdownWrappedJson_IsExtracted()
        {
            NpcResponse r = ParseOk(
                "İşte yanıtım:\n```json\n{\"reply\":\"Dur!\",\"emotion\":\"angry\",\"trust_delta\":-5,\"action\":\"attack\"}\n```\nUmarım işine yarar.");

            Assert.AreEqual("Dur!", r.Reply);
            Assert.AreEqual(NpcEmotion.Angry, r.Emotion);
            Assert.AreEqual(-5, r.TrustDelta);
            Assert.AreEqual(NpcAction.Attack, r.Action);
        }

        [Test]
        public void BracesAndQuotesInsideReply_DoNotBreakExtraction()
        {
            NpcResponse r = ParseOk(
                "{\"reply\":\"Şifre {gizli} ve \\\"kapı\\\" değil.\",\"emotion\":\"suspicious\",\"trust_delta\":0,\"action\":\"none\"} sonra {çöp}");

            Assert.AreEqual("Şifre {gizli} ve \"kapı\" değil.", r.Reply);
            Assert.AreEqual(NpcEmotion.Suspicious, r.Emotion);
        }

        [Test]
        public void EnumValues_AreCaseAndSeparatorInsensitive()
        {
            NpcResponse r = ParseOk("{\"reply\":\"Tamam.\",\"emotion\":\"FRIENDLY\",\"trust_delta\":1,\"action\":\"End Conversation\"}");

            Assert.AreEqual(NpcEmotion.Friendly, r.Emotion);
            Assert.AreEqual(NpcAction.EndConversation, r.Action);
            Assert.IsFalse(r.HasWarnings);
        }

        // --- Eksik alanlar ---

        [Test]
        public void MissingReply_Fails()
        {
            StringAssert.Contains("reply", ParseFail("{\"emotion\":\"neutral\",\"trust_delta\":0,\"action\":\"none\"}"));
        }

        [Test]
        public void EmptyOrWhitespaceReply_Fails()
        {
            ParseFail("{\"reply\":\"   \",\"emotion\":\"neutral\",\"trust_delta\":0,\"action\":\"none\"}");
        }

        [Test]
        public void NonStringReply_Fails()
        {
            ParseFail("{\"reply\":[\"a\"],\"emotion\":\"neutral\",\"trust_delta\":0,\"action\":\"none\"}");
        }

        [Test]
        public void MissingOptionalFields_UseSafeDefaultsWithWarnings()
        {
            NpcResponse r = ParseOk("{\"reply\":\"Hmm.\"}");

            Assert.AreEqual(NpcEmotion.Neutral, r.Emotion);
            Assert.AreEqual(0, r.TrustDelta);
            Assert.AreEqual(NpcAction.None, r.Action);
            Assert.AreEqual(3, r.Warnings.Count);
        }

        // --- Bozuk / kesik JSON ---

        [TestCase("{bozuk")]
        [TestCase("{\"reply\":\"Merhaba")]
        [TestCase("{\"reply\": }")]
        [TestCase("tamamen düz metin, JSON yok")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void BrokenOrTruncatedInput_FailsWithoutException(string raw)
        {
            ParseFail(raw);
        }

        // --- Aralık dışı ve bilinmeyen değerler ---

        [TestCase(999, 20)]
        [TestCase(-999, -20)]
        [TestCase(20, 20)]
        [TestCase(-20, -20)]
        public void TrustDelta_IsClamped(int input, int expected)
        {
            NpcResponse r = ParseOk($"{{\"reply\":\"x\",\"emotion\":\"neutral\",\"trust_delta\":{input},\"action\":\"none\"}}");
            Assert.AreEqual(expected, r.TrustDelta);
            Assert.AreEqual(input != expected, r.HasWarnings);
        }

        [Test]
        public void TrustDelta_FloatIsRounded_StringIsAccepted()
        {
            Assert.AreEqual(6, ParseOk("{\"reply\":\"x\",\"emotion\":\"neutral\",\"trust_delta\":5.6,\"action\":\"none\"}").TrustDelta);
            Assert.AreEqual(-7, ParseOk("{\"reply\":\"x\",\"emotion\":\"neutral\",\"trust_delta\":\"-7\",\"action\":\"none\"}").TrustDelta);
        }

        [Test]
        public void TrustDelta_NonNumeric_DefaultsToZero()
        {
            NpcResponse r = ParseOk("{\"reply\":\"x\",\"emotion\":\"neutral\",\"trust_delta\":\"çok\",\"action\":\"none\"}");
            Assert.AreEqual(0, r.TrustDelta);
            Assert.IsTrue(r.HasWarnings);
        }

        [Test]
        public void UnknownAction_FallsBackToNone()
        {
            NpcResponse r = ParseOk("{\"reply\":\"Uçalım!\",\"emotion\":\"friendly\",\"trust_delta\":3,\"action\":\"fly\"}");
            Assert.AreEqual(NpcAction.None, r.Action);
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("fly")));
        }

        [Test]
        public void UnknownEmotion_FallsBackToNeutral()
        {
            NpcResponse r = ParseOk("{\"reply\":\"x\",\"emotion\":\"ecstatic\",\"trust_delta\":0,\"action\":\"none\"}");
            Assert.AreEqual(NpcEmotion.Neutral, r.Emotion);
            Assert.IsTrue(r.HasWarnings);
        }

        [Test]
        public void ReplyText_IsNeverScannedForActions()
        {
            // Replik "kapıyı aç" dese bile aksiyon yalnızca 'action' alanından gelir.
            NpcResponse r = ParseOk("{\"reply\":\"Tamam, kapıyı açıyorum! open_door\",\"emotion\":\"friendly\",\"trust_delta\":5,\"action\":\"none\"}");
            Assert.AreEqual(NpcAction.None, r.Action);
        }

        // --- Şema ---

        [Test]
        public void Schema_RequiresAllFieldsAndMatchesEnums()
        {
            JObject schema = NpcResponseSchema.Build();
            var required = schema["required"].Values<string>().ToArray();
            CollectionAssert.AreEquivalent(new[] { "reply", "emotion", "trust_delta", "action" }, required);

            var actions = schema["properties"]["action"]["enum"].Values<string>().ToArray();
            Assert.AreEqual(System.Enum.GetValues(typeof(NpcAction)).Length, actions.Length);
            Assert.AreEqual("open_door", NpcResponseSchema.ToWireName(NpcAction.OpenDoor));

            var emotions = schema["properties"]["emotion"]["enum"].Values<string>().ToArray();
            Assert.AreEqual(System.Enum.GetValues(typeof(NpcEmotion)).Length, emotions.Length);

            Assert.AreEqual(NpcResponseSchema.TrustDeltaMin, (int)schema["properties"]["trust_delta"]["minimum"]);
            Assert.AreEqual(NpcResponseSchema.TrustDeltaMax, (int)schema["properties"]["trust_delta"]["maximum"]);
        }

        [Test]
        public void SchemaRoundTrip_EveryWireValueParses()
        {
            foreach (NpcAction action in System.Enum.GetValues(typeof(NpcAction)))
            {
                foreach (NpcEmotion emotion in System.Enum.GetValues(typeof(NpcEmotion)))
                {
                    string raw = $"{{\"reply\":\"x\",\"emotion\":\"{NpcResponseSchema.ToWireName(emotion)}\"," +
                                 $"\"trust_delta\":0,\"action\":\"{NpcResponseSchema.ToWireName(action)}\"}}";
                    NpcResponse r = ParseOk(raw);
                    Assert.AreEqual(action, r.Action);
                    Assert.AreEqual(emotion, r.Emotion);
                }
            }
        }
    }
}
