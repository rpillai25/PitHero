using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Config;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>Stats window lifetime totals (issue #456): recording rules and the v44 save section.</summary>
    [TestClass]
    public class LifetimeStatsTests
    {
        private static SaveData RoundTrip(SaveData original, int? patchVersion = null)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] bytes = ms.ToArray();
            if (patchVersion.HasValue)
            {
                bytes[0] = (byte)patchVersion.Value; bytes[1] = 0; bytes[2] = 0; bytes[3] = 0;
            }
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }

        private static SaveData WithStats()
        {
            var data = new SaveData();
            data.HeroId = 4560;
            var s = data.Stats;
            s.GoldEarned = 5_000_000_000L; // above int range
            s.PlayedTicksBase = 216_000;
            s.MonstersDefeated = 11;
            s.TreasuresFound = 12;
            s.MercenariesHired = 3;
            s.HeroDefeats = 2;
            s.MercenaryDeaths = 4;
            s.PitLevelsCleared = 25;
            s.RecordCropSale(CropType.Corn, 30, 450);
            s.RecordSeedPurchase(CropType.Corn, 100);
            s.RecordDishGold(DishType.ApplePie, 77);
            s.RecordFishCatch(FishCatchType.GoldenKoi, 265);
            s.RecordFishCatch(FishCatchType.GoldenKoi, 265);
            s.RecordGemFind(GemType.Diamond, 5000);
            s.RecordGemFind(GemType.Agate, 10);
            return data;
        }

        [TestMethod]
        public void SaveData_V44_Stats_RoundTrip()
        {
            var loaded = RoundTrip(WithStats()).Stats;

            Assert.AreEqual(5_000_000_000L, loaded.GoldEarned);
            Assert.AreEqual(216_000, loaded.PlayedTicksBase);
            Assert.AreEqual(11, loaded.MonstersDefeated);
            Assert.AreEqual(12, loaded.TreasuresFound);
            Assert.AreEqual(3, loaded.MercenariesHired);
            Assert.AreEqual(2, loaded.HeroDefeats);
            Assert.AreEqual(4, loaded.MercenaryDeaths);
            Assert.AreEqual(25, loaded.PitLevelsCleared);
            Assert.AreEqual(30, loaded.CropUnitsSold[(int)CropType.Corn]);
            Assert.AreEqual(350, loaded.GetCropProfit(CropType.Corn), "profit = sales minus seeds");
            Assert.AreEqual(77, loaded.DishGold[(int)DishType.ApplePie]);
            Assert.AreEqual(2, loaded.FishCaught[(int)FishCatchType.GoldenKoi]);
            Assert.AreEqual(530, loaded.FishGold[(int)FishCatchType.GoldenKoi]);
            Assert.AreEqual(1, loaded.GemsFound[(int)GemType.Diamond]);
            Assert.AreEqual(5000, loaded.GemGold[(int)GemType.Diamond]);
            Assert.AreEqual(5010L, loaded.TotalGemGold);
        }

        [TestMethod]
        public void SaveData_V43_File_ReadsWithZeroStats()
        {
            var loaded = RoundTrip(WithStats(), patchVersion: 43);

            Assert.AreEqual(4560, loaded.HeroId);
            Assert.AreEqual(0L, loaded.Stats.GoldEarned);
            Assert.AreEqual(0, loaded.Stats.MonstersDefeated);
            Assert.AreEqual(0, loaded.Stats.FishCaught[(int)FishCatchType.GoldenKoi]);
            Assert.AreEqual(FishingConfig.CatchCount, loaded.Stats.FishCaught.Length);
        }

        [TestMethod]
        public void SaveData_V44_File_KeepsStats_ReadsWithZeroGems()
        {
            var loaded = RoundTrip(WithStats(), patchVersion: 44);

            Assert.AreEqual(4560, loaded.HeroId);
            Assert.AreEqual(11, loaded.Stats.MonstersDefeated, "the v44 section still reads");
            Assert.AreEqual(0, loaded.Stats.GemsFound[(int)GemType.Diamond], "a v44 file has dug up nothing on record");
            Assert.AreEqual(0L, loaded.Stats.TotalGemGold);
        }

        [TestMethod]
        public void RecordGold_SkipsRefundsAndBuildingSales()
        {
            var s = new LifetimeStats();
            s.RecordGold(100, "battle");
            s.RecordGold(50, "refund");
            s.RecordGold(70, "sell_building");
            s.RecordGold(-5, "battle");
            Assert.AreEqual(100L, s.GoldEarned);
        }

        [TestMethod]
        public void GameState_AddFunds_FeedsGoldEarned_AndClearResets()
        {
            var gs = new GameStateService();
            gs.AddFunds(40, "fish_catch");
            gs.Stats.MonstersDefeated = 9;
            Assert.AreEqual(40L, gs.Stats.GoldEarned);

            gs.ClearProgressCounters();
            Assert.AreEqual(0L, gs.Stats.GoldEarned);
            Assert.AreEqual(0, gs.Stats.MonstersDefeated);
        }

        [TestMethod]
        public void GoldPerHour_UsesLifetimeTicks()
        {
            var s = new LifetimeStats();
            Assert.AreEqual(0f, s.GoldPerHour, "no time played yet");
            s.GoldEarned = 1200;
            s.PlayedTicksBase = (long)(1800f / GameConfig.SimulationFixedStepSeconds); // half an hour
            Assert.AreEqual(2400f, s.GoldPerHour, 1f);
        }

        [TestMethod]
        public void CloneForSave_CopiesEverything()
        {
            var original = WithStats().Stats;
            var copy = original.CloneForSave();
            original.FishCaught[(int)FishCatchType.GoldenKoi] = 99; // the copy owns its arrays
            Assert.AreEqual(2, copy.FishCaught[(int)FishCatchType.GoldenKoi]);
            Assert.AreEqual(original.TotalPlayedTicks, copy.PlayedTicksBase);
            Assert.AreEqual(12, copy.TreasuresFound);
        }
    }
}
