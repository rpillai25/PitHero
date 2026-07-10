#if DEBUG
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services.Analytics;
using System;
using System.Text;
using System.Text.Json;

namespace PitHero.Tests
{
    /// <summary>Tests for the AOT-safe JSONL writer used by the analytics service (issue #289).</summary>
    [TestClass]
    public class AnalyticsJsonLineBuilderTests
    {
        private static (StringBuilder sb, JsonLineBuilder json) CreateBuilder()
        {
            var sb = new StringBuilder(256);
            return (sb, new JsonLineBuilder(sb));
        }

        [TestMethod]
        public void SimpleEvent_ProducesValidJsonLine()
        {
            var (sb, json) = CreateBuilder();
            json.BeginEvent();
            json.Field("e", "test_event");
            json.Field("count", 42);
            json.Field("ratio", 1.5f);
            json.Field("flag", true);
            json.EndEvent();

            var line = sb.ToString();
            Assert.IsTrue(line.EndsWith("\n"));

            using var doc = JsonDocument.Parse(line);
            Assert.AreEqual("test_event", doc.RootElement.GetProperty("e").GetString());
            Assert.AreEqual(42, doc.RootElement.GetProperty("count").GetInt32());
            Assert.AreEqual(1.5, doc.RootElement.GetProperty("ratio").GetDouble(), 0.0001);
            Assert.IsTrue(doc.RootElement.GetProperty("flag").GetBoolean());
        }

        [TestMethod]
        public void StringEscaping_HandlesSpecialCharacters()
        {
            var (sb, json) = CreateBuilder();
            json.BeginEvent();
            json.Field("name", "Sword \"of\" Doom\\Slash");
            json.Field("multi", "line1\nline2\ttabbed\rreturn");
            json.Field("control", "lowchar");
            json.EndEvent();

            using var doc = JsonDocument.Parse(sb.ToString());
            Assert.AreEqual("Sword \"of\" Doom\\Slash", doc.RootElement.GetProperty("name").GetString());
            Assert.AreEqual("line1\nline2\ttabbed\rreturn", doc.RootElement.GetProperty("multi").GetString());
            Assert.AreEqual("lowchar", doc.RootElement.GetProperty("control").GetString());
        }

        [TestMethod]
        public void NullString_WritesJsonNull()
        {
            var (sb, json) = CreateBuilder();
            json.BeginEvent();
            json.Field("item", (string)null);
            json.EndEvent();

            using var doc = JsonDocument.Parse(sb.ToString());
            Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("item").ValueKind);
        }

        [TestMethod]
        public void NestedObjectsAndArrays_ProduceValidJson()
        {
            var (sb, json) = CreateBuilder();
            json.BeginEvent();
            json.Field("e", "party_snapshot");
            json.BeginArray("members");
            json.BeginArrayObject();
            json.Field("name", "Alice");
            json.BeginArray("skills");
            json.ArrayStringValue("slash");
            json.ArrayStringValue("guard");
            json.EndArray();
            json.BeginObject("gear");
            json.Field("weapon", "Bronze Sword");
            json.EndObject();
            json.EndObject();
            json.BeginArrayObject();
            json.Field("name", "Bob");
            json.EndObject();
            json.EndArray();
            json.Field("after", 1);
            json.EndEvent();

            using var doc = JsonDocument.Parse(sb.ToString());
            var members = doc.RootElement.GetProperty("members");
            Assert.AreEqual(2, members.GetArrayLength());
            Assert.AreEqual("Alice", members[0].GetProperty("name").GetString());
            Assert.AreEqual(2, members[0].GetProperty("skills").GetArrayLength());
            Assert.AreEqual("slash", members[0].GetProperty("skills")[0].GetString());
            Assert.AreEqual("Bronze Sword", members[0].GetProperty("gear").GetProperty("weapon").GetString());
            Assert.AreEqual("Bob", members[1].GetProperty("name").GetString());
            Assert.AreEqual(1, doc.RootElement.GetProperty("after").GetInt32());
        }

        [TestMethod]
        public void TimestampField_ProducesIso8601WithOffset()
        {
            var (sb, json) = CreateBuilder();
            json.BeginEvent();
            json.TimestampField("t", new DateTime(2026, 7, 9, 18, 30, 5, 123), new TimeSpan(-8, 0, 0));
            json.EndEvent();

            using var doc = JsonDocument.Parse(sb.ToString());
            Assert.AreEqual("2026-07-09T18:30:05.123-08:00", doc.RootElement.GetProperty("t").GetString());

            var parsed = DateTimeOffset.Parse(doc.RootElement.GetProperty("t").GetString());
            Assert.AreEqual(2026, parsed.Year);
            Assert.AreEqual(123, parsed.Millisecond);
        }

        [TestMethod]
        public void TimeField_ProducesPaddedClock()
        {
            var (sb, json) = CreateBuilder();
            json.BeginEvent();
            json.TimeField("gt", 6, 5);
            json.EndEvent();

            using var doc = JsonDocument.Parse(sb.ToString());
            Assert.AreEqual("06:05", doc.RootElement.GetProperty("gt").GetString());
        }

        [TestMethod]
        public void MultipleEvents_ProduceOneLineEach()
        {
            var (sb, json) = CreateBuilder();
            for (int i = 0; i < 3; i++)
            {
                json.BeginEvent();
                json.Field("i", i);
                json.EndEvent();
            }

            var lines = sb.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(3, lines.Length);
            for (int i = 0; i < 3; i++)
            {
                using var doc = JsonDocument.Parse(lines[i]);
                Assert.AreEqual(i, doc.RootElement.GetProperty("i").GetInt32());
            }
        }
    }
}
#endif
