#if DEBUG
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.Services.Analytics;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using System;
using System.IO;
using System.Text.Json;

namespace PitHero.Tests
{
    /// <summary>End-to-end tests for the analytics logging service (issue #289).</summary>
    [TestClass]
    public class AnalyticsServiceTests
    {
        private string _tempDir;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PitHeroAnalyticsTests_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            AnalyticsService.Shutdown();
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private string[] ReadAllEventLines()
        {
            AnalyticsService.Flush();
            var files = Directory.GetFiles(_tempDir, "session_*.jsonl");
            Assert.AreEqual(1, files.Length, "Expected exactly one session file");
            return ReadLinesSharedWithWriter(files[0]);
        }

        private static string[] ReadLinesSharedWithWriter(string path)
        {
            // The service keeps its StreamWriter open, so the reader must allow concurrent write access
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            return content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        [TestMethod]
        public void LogSessionStart_WritesValidEventWithTimestamp()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogSessionStart("new_game", 100);

            var lines = ReadAllEventLines();
            Assert.AreEqual(1, lines.Length);

            using var doc = JsonDocument.Parse(lines[0]);
            Assert.AreEqual("session_start", doc.RootElement.GetProperty("e").GetString());
            Assert.AreEqual("new_game", doc.RootElement.GetProperty("mode").GetString());
            Assert.AreEqual(100, doc.RootElement.GetProperty("gold").GetInt32());

            var timestamp = doc.RootElement.GetProperty("t").GetString();
            Assert.IsFalse(string.IsNullOrEmpty(timestamp));
            DateTimeOffset.Parse(timestamp); // throws if malformed
        }

        [TestMethod]
        public void MultipleEvents_WriteOneValidJsonLineEachInOrder()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogSessionStart("new_game", 0);
            AnalyticsService.LogPitGenerated(3, false, 5, 2);
            AnalyticsService.LogInnSleep(10, 90);
            AnalyticsService.LogAttack("Hero", "hero", "physical", "Slime", "monster", 7, 20, 13, false);
            AnalyticsService.LogHeal("Hero", "heal", "Hero", 15, 40);
            AnalyticsService.LogMercLeft("Boris", "tavern_left");

            var lines = ReadAllEventLines();
            Assert.AreEqual(6, lines.Length);

            var expectedTypes = new[] { "session_start", "pit_generated", "inn_sleep", "attack", "heal", "merc_left" };
            for (int i = 0; i < lines.Length; i++)
            {
                using var doc = JsonDocument.Parse(lines[i]);
                Assert.AreEqual(expectedTypes[i], doc.RootElement.GetProperty("e").GetString());
                Assert.IsFalse(string.IsNullOrEmpty(doc.RootElement.GetProperty("t").GetString()));
            }
        }

        [TestMethod]
        public void LogGoldGained_TracksSessionRunningTotal()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogSessionStart("new_game", 0);
            AnalyticsService.LogGoldGained(10, "battle", 10);
            AnalyticsService.LogGoldGained(25, "sell_item", 30);

            var lines = ReadAllEventLines();
            using var first = JsonDocument.Parse(lines[1]);
            using var second = JsonDocument.Parse(lines[2]);

            Assert.AreEqual(10, first.RootElement.GetProperty("sessionTotal").GetInt64());
            Assert.AreEqual("battle", first.RootElement.GetProperty("source").GetString());
            Assert.AreEqual(35, second.RootElement.GetProperty("sessionTotal").GetInt64());
            Assert.AreEqual(30, second.RootElement.GetProperty("currentGold").GetInt32());
        }

        [TestMethod]
        public void LogCharacterKilled_WritesFullHeroSnapshotWithKiller()
        {
            AnalyticsService.Initialize(_tempDir);
            var hero = new Hero("TestHero", new Knight(), 5, new StatBlock(6, 4, 7, 2));
            var enemy = RolePlayingFramework.Enemies.EnemyFactory.Create(RolePlayingFramework.Enemies.EnemyId.Slime, 3);

            AnalyticsService.LogCharacterKilled(hero, enemy);

            var lines = ReadAllEventLines();
            using var doc = JsonDocument.Parse(lines[0]);
            var root = doc.RootElement;

            Assert.AreEqual("char_killed", root.GetProperty("e").GetString());
            Assert.AreEqual("TestHero", root.GetProperty("name").GetString());
            Assert.AreEqual("hero", root.GetProperty("type").GetString());
            Assert.AreEqual(5, root.GetProperty("level").GetInt32());
            Assert.IsTrue(root.GetProperty("maxHP").GetInt32() > 0);
            Assert.AreEqual(JsonValueKind.Array, root.GetProperty("skills").ValueKind);
            Assert.AreEqual(JsonValueKind.Object, root.GetProperty("gear").ValueKind);

            var killer = root.GetProperty("killer");
            Assert.IsFalse(string.IsNullOrEmpty(killer.GetProperty("name").GetString()));
            Assert.AreEqual(3, killer.GetProperty("level").GetInt32());
            Assert.IsTrue(killer.GetProperty("maxHP").GetInt32() > 0);
        }

        [TestMethod]
        public void DisabledService_WritesNothing()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.Enabled = false;
            AnalyticsService.LogSessionStart("new_game", 0);
            AnalyticsService.LogInnSleep(10, 90);
            AnalyticsService.Flush();

            Assert.IsFalse(Directory.Exists(_tempDir), "No output directory should be created when disabled");
        }

        [TestMethod]
        public void UninitializedService_DoesNotThrowAndWritesNothing()
        {
            AnalyticsService.Shutdown(); // guarantee un-initialized state
            AnalyticsService.LogInnSleep(10, 90);
            AnalyticsService.LogGoldGained(5, "battle", 5);
            AnalyticsService.Flush();

            Assert.IsFalse(Directory.Exists(_tempDir));
        }

        [TestMethod]
        public void SessionRestart_RotatesToNewFile()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogSessionStart("new_game", 0);
            AnalyticsService.LogInnSleep(10, 90);
            AnalyticsService.Flush();

            System.Threading.Thread.Sleep(1100); // ensure a distinct per-second filename
            AnalyticsService.LogSessionStart("load", 50);
            AnalyticsService.Flush();

            var files = Directory.GetFiles(_tempDir, "session_*.jsonl");
            Assert.AreEqual(2, files.Length, "A second session should rotate to a new file");
        }

        [TestMethod]
        public void Shutdown_FlushesRemainingBufferedEvents()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogSessionStart("new_game", 0);
            AnalyticsService.Shutdown();

            var files = Directory.GetFiles(_tempDir, "session_*.jsonl");
            Assert.AreEqual(1, files.Length);
            var lines = File.ReadAllLines(files[0]);
            Assert.AreEqual(1, lines.Length);
        }

        [TestMethod]
        public void GameStateService_AddFunds_IncrementsFunds()
        {
            var gameState = new GameStateService();
            gameState.Funds = 10;
            gameState.AddFunds(25, "battle");
            Assert.AreEqual(35, gameState.Funds);
        }
    }
}
#endif
