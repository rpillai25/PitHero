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

        [TestMethod]
        public void LogFarmingLifecycleEvents_WriteExpectedTypesAndFields()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogCropPlanted("Turnip", 3, 4, "Slime A", "Slime");
            AnalyticsService.LogCropGrown("Turnip", 3, 4);
            AnalyticsService.LogCropWatered("Turnip", 3, 4, "Slime A", "Slime", 7);
            AnalyticsService.LogCropHarvested("Turnip", 3, 4, 5, "Slime A", "Slime");
            AnalyticsService.LogCropStored("Turnip", 5, "Slime A", "Slime");
            AnalyticsService.LogCropDropped("Turnip", 2, 6, 9);
            AnalyticsService.LogCropDestroyed("Turnip", 3, 4, "Slime A", "Slime");

            var lines = ReadAllEventLines();
            Assert.AreEqual(7, lines.Length);

            var expectedTypes = new[] { "crop_planted", "crop_grown", "crop_watered", "crop_harvested",
                "crop_stored", "crop_dropped", "crop_destroyed" };
            for (int i = 0; i < lines.Length; i++)
            {
                using var lineDoc = JsonDocument.Parse(lines[i]);
                Assert.AreEqual(expectedTypes[i], lineDoc.RootElement.GetProperty("e").GetString());
                Assert.AreEqual("Turnip", lineDoc.RootElement.GetProperty("crop").GetString());
            }

            using var planted = JsonDocument.Parse(lines[0]);
            Assert.AreEqual(3, planted.RootElement.GetProperty("x").GetInt32());
            Assert.AreEqual(4, planted.RootElement.GetProperty("y").GetInt32());
            Assert.AreEqual("Slime A", planted.RootElement.GetProperty("monster").GetString());
            Assert.AreEqual("Slime", planted.RootElement.GetProperty("monsterType").GetString());

            using var watered = JsonDocument.Parse(lines[2]);
            Assert.AreEqual(7, watered.RootElement.GetProperty("waterLeft").GetInt32());

            using var harvested = JsonDocument.Parse(lines[3]);
            Assert.AreEqual(5, harvested.RootElement.GetProperty("qty").GetInt32());

            using var stored = JsonDocument.Parse(lines[4]);
            Assert.AreEqual(5, stored.RootElement.GetProperty("qty").GetInt32());

            using var dropped = JsonDocument.Parse(lines[5]);
            Assert.AreEqual(2, dropped.RootElement.GetProperty("qty").GetInt32());
            Assert.AreEqual(6, dropped.RootElement.GetProperty("x").GetInt32());
            Assert.AreEqual(9, dropped.RootElement.GetProperty("y").GetInt32());
        }

        [TestMethod]
        public void LogCropWatered_NullCrop_WritesNullCropField()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogCropWatered(null, 1, 2, "Slime A", "Slime", 3);

            var lines = ReadAllEventLines();
            using var doc = JsonDocument.Parse(lines[0]);
            Assert.AreEqual("crop_watered", doc.RootElement.GetProperty("e").GetString());
            Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("crop").ValueKind);
        }

        [TestMethod]
        public void LogSeedPurchasedAndCropSold_WriteSourceAndGoldFields()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogSeedPurchased("Turnip", 4, 80, "manual", 920);
            AnalyticsService.LogSeedPurchased("Wheat", 2, 30, "auto", 890);
            AnalyticsService.LogCropSold("Turnip", 10, 250, "manual");
            AnalyticsService.LogCropSold("Wheat", 8, 120, "auto");

            var lines = ReadAllEventLines();
            Assert.AreEqual(4, lines.Length);

            using var manualBuy = JsonDocument.Parse(lines[0]);
            Assert.AreEqual("seed_purchased", manualBuy.RootElement.GetProperty("e").GetString());
            Assert.AreEqual("Turnip", manualBuy.RootElement.GetProperty("crop").GetString());
            Assert.AreEqual(4, manualBuy.RootElement.GetProperty("qty").GetInt32());
            Assert.AreEqual(80, manualBuy.RootElement.GetProperty("goldSpent").GetInt32());
            Assert.AreEqual("manual", manualBuy.RootElement.GetProperty("source").GetString());
            Assert.AreEqual(920, manualBuy.RootElement.GetProperty("currentGold").GetInt32());

            using var autoBuy = JsonDocument.Parse(lines[1]);
            Assert.AreEqual("auto", autoBuy.RootElement.GetProperty("source").GetString());

            using var manualSell = JsonDocument.Parse(lines[2]);
            Assert.AreEqual("crop_sold", manualSell.RootElement.GetProperty("e").GetString());
            Assert.AreEqual(10, manualSell.RootElement.GetProperty("qty").GetInt32());
            Assert.AreEqual(250, manualSell.RootElement.GetProperty("gold").GetInt32());
            Assert.AreEqual("manual", manualSell.RootElement.GetProperty("source").GetString());

            using var autoSell = JsonDocument.Parse(lines[3]);
            Assert.AreEqual("auto", autoSell.RootElement.GetProperty("source").GetString());
        }

        [TestMethod]
        public void LogBuildingEvents_WriteCoordinatesAndType()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogBuildingCreated("CropStorage", 10, 5, 500);
            AnalyticsService.LogBuildingMoved("MonsterHouse", 3, 4, 8, 9);

            var lines = ReadAllEventLines();
            Assert.AreEqual(2, lines.Length);

            using var created = JsonDocument.Parse(lines[0]);
            Assert.AreEqual("building_created", created.RootElement.GetProperty("e").GetString());
            Assert.AreEqual("CropStorage", created.RootElement.GetProperty("buildingType").GetString());
            Assert.AreEqual(10, created.RootElement.GetProperty("x").GetInt32());
            Assert.AreEqual(5, created.RootElement.GetProperty("y").GetInt32());
            Assert.AreEqual(500, created.RootElement.GetProperty("cost").GetInt32());

            using var moved = JsonDocument.Parse(lines[1]);
            Assert.AreEqual("building_moved", moved.RootElement.GetProperty("e").GetString());
            Assert.AreEqual("MonsterHouse", moved.RootElement.GetProperty("buildingType").GetString());
            Assert.AreEqual(3, moved.RootElement.GetProperty("fromX").GetInt32());
            Assert.AreEqual(4, moved.RootElement.GetProperty("fromY").GetInt32());
            Assert.AreEqual(8, moved.RootElement.GetProperty("toX").GetInt32());
            Assert.AreEqual(9, moved.RootElement.GetProperty("toY").GetInt32());
        }

        [TestMethod]
        public void LogWateringCanFilled_WritesMonsterAndLocation()
        {
            AnalyticsService.Initialize(_tempDir);
            AnalyticsService.LogWateringCanFilled("Slime A", "Slime", 118, 5);

            var lines = ReadAllEventLines();
            using var doc = JsonDocument.Parse(lines[0]);
            Assert.AreEqual("watering_can_filled", doc.RootElement.GetProperty("e").GetString());
            Assert.AreEqual("Slime A", doc.RootElement.GetProperty("monster").GetString());
            Assert.AreEqual("Slime", doc.RootElement.GetProperty("monsterType").GetString());
            Assert.AreEqual(118, doc.RootElement.GetProperty("x").GetInt32());
            Assert.AreEqual(5, doc.RootElement.GetProperty("y").GetInt32());
        }
    }
}
#endif
