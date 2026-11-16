using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Artifacts;
using PitHero.Config;
using PitHero.Services;
using PitHero.Services.Replay;
using PitHero.Util;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// The Digging Field's shared 500-marble dig bag on <see cref="DiggingFieldService"/>, and save
    /// compatibility after the v42 pity counters were retired in v43.
    /// </summary>
    [TestClass]
    public class GemDigBagTests
    {
        [TestMethod]
        public void TryDig_DrawsFromTheFieldBag_AndResetRefillsIt()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding { Type = BuildingType.DiggingField, TileX = 130, TileY = 6, UniqueId = 1 });
            float now = 0f;
            var field = new DiggingFieldService(buildings, () => now);
            var state = new GameStateService();
            state.GrantLocalArtifact(ArtifactType.ProspectorsMap);
            var random = new SeedableRandom(452);

            int diamonds = 0, nothing = 0, junk = 0, gems = 0;
            long gold = 0;
            for (int d = 0; d < GemDigConfig.BagSize; d++)
            {
                now += GameConfig.DiggingFieldCooldownSeconds; // every tile ready again
                long fundsBefore = state.Funds;
                Assert.IsTrue(field.TryDig(state, 126, 4, random.NextInt64(GemDigConfig.RollRange), "gem_dig", out var find));
                gold += state.Funds - fundsBefore;
                if (find.IsGem) gems++;
                else if (find.Kind == DigFindKind.Junk) junk++;
                else nothing++;
                if (find.IsGem && find.Gem == GemType.Diamond) diamonds++;
                Assert.AreEqual(GemDigConfig.BagSize - d - 1, field.MarblesRemaining);
            }
            Assert.AreEqual(1, diamonds, "exactly one Diamond per 500 digs");
            Assert.AreEqual(135, nothing);
            Assert.AreEqual(115, junk);
            Assert.AreEqual(250, gems);
            Assert.AreEqual(28_280, gold, "a full bag pays its exact total");

            now += GameConfig.DiggingFieldCooldownSeconds;
            Assert.IsTrue(field.TryDig(state, 126, 4, 0, "gem_dig", out _));
            Assert.AreEqual(GemDigConfig.BagSize - 1, field.MarblesRemaining, "an empty bag refills on the next dig");
            field.Reset();
            Assert.AreEqual(GemDigConfig.BagSize, field.MarblesRemaining);
        }

        [TestMethod]
        public void TryDig_RefusedDigsDrawNoMarble()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding { Type = BuildingType.DiggingField, TileX = 130, TileY = 6, UniqueId = 1 });
            var field = new DiggingFieldService(buildings, () => 0f);
            var state = new GameStateService();
            Assert.IsFalse(field.TryDig(state, 126, 4, 0, "gem_dig", out _), "no map");
            Assert.AreEqual(GemDigConfig.BagSize, field.MarblesRemaining);
        }

        // ── Save compatibility (v42 pity section retired in v43) ───────────────────

        private static byte[] Write(SaveData data)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(data);
            return ms.ToArray();
        }

        private static SaveData Read(byte[] bytes)
        {
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }

        private static void PatchVersion(byte[] bytes, int version)
        {
            var v = BitConverter.GetBytes(version);
            Array.Copy(v, 0, bytes, 0, 4);
        }

        [TestMethod]
        public void SaveData_V42_File_SkipsThePityCounters()
        {
            // A v42 file is a v43 stream plus section 56: a count (4) and four pity counters
            byte[] v43 = Write(new SaveData { HeroName = "Digger", Level = 9 });
            var ms = new MemoryStream();
            ms.Write(v43, 0, v43.Length);
            foreach (int v in new[] { 4, 3, 140, 900, 12000 })
                ms.Write(BitConverter.GetBytes(v), 0, 4);
            byte[] v42 = ms.ToArray();
            PatchVersion(v42, 42);

            var loaded = Read(v42);
            Assert.AreEqual("Digger", loaded.HeroName);
            Assert.AreEqual(9, loaded.Level);
        }

        [TestMethod]
        public void SaveData_V41_File_StillLoads()
        {
            byte[] v41 = Write(new SaveData { HeroName = "OldDigger", Level = 7 });
            PatchVersion(v41, 41);
            var loaded = Read(v41);
            Assert.AreEqual("OldDigger", loaded.HeroName);
            Assert.AreEqual(7, loaded.Level);
        }
    }
}
