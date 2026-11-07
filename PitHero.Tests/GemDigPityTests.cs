using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Config;
using PitHero.Services;
using PitHero.Services.Replay;
using RolePlayingFramework.Equipment;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>Gem dig pity (issue #452): the odds ramp, counter resets, the sim-side dig and the v42 save section.</summary>
    [TestClass]
    public class GemDigPityTests
    {
        private const int Rare = 1, Epic = 2, Legendary = 3;

        [TestMethod]
        public void Multiplier_IsOneUntilSoftStart_ThenClimbs_ThenCaps()
        {
            int soft = GemDigConfig.PitySoftStartDigs[Rare];
            Assert.AreEqual(1.0, GemDigConfig.GetPityMultiplier(Rare, 0));
            Assert.AreEqual(1.0, GemDigConfig.GetPityMultiplier(Rare, soft));
            double a = GemDigConfig.GetPityMultiplier(Rare, soft + 10);
            double b = GemDigConfig.GetPityMultiplier(Rare, soft + 20);
            Assert.IsTrue(a > 1.0 && b > a, "odds climb past the soft start");
            Assert.AreEqual(GemDigConfig.PityMaxMultiplier[Rare], GemDigConfig.GetPityMultiplier(Rare, int.MaxValue), 1e-9);
        }

        [TestMethod]
        public void ZeroCounters_GiveTheBaseOdds()
        {
            var chance = new double[5];
            GemDigConfig.GetTierChances(new int[GemDigConfig.PityTierCount], chance);
            for (int t = 0; t < 5; t++)
                Assert.AreEqual(GemDigConfig.GetBaseTierChance((ItemRarity)t), chance[t], 1e-12, ((ItemRarity)t).ToString());
        }

        [TestMethod]
        public void MaxedCounters_StayAProbability()
        {
            var chance = new double[5];
            GemDigConfig.GetTierChances(new[] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue }, chance);
            double sum = 0;
            for (int t = 0; t < 5; t++)
            {
                Assert.IsTrue(chance[t] >= 0, ((ItemRarity)t).ToString());
                sum += chance[t];
            }
            Assert.AreEqual(1.0, sum, 1e-12);
            Assert.IsTrue(chance[(int)ItemRarity.Rare] > GemDigConfig.GetBaseTierChance(ItemRarity.Rare));
            Assert.IsTrue(chance[(int)ItemRarity.Normal] >= 1.0 - GemDigConfig.MaxNonCommonChance - 1e-12);
        }

        [TestMethod]
        public void AdvancePity_ResetsFoundTierAndBelow_CountsBetterTiers()
        {
            var pity = new[] { 5, 50, 500, 5000 };
            GemDigConfig.AdvancePity(pity, DigFind.OfGem(GemType.Agate));
            CollectionAssert.AreEqual(new[] { 6, 51, 501, 5001 }, pity, "a Common find counts every tier");

            GemDigConfig.AdvancePity(pity, DigFind.Nothing);
            GemDigConfig.AdvancePity(pity, DigFind.OfJunk(DigJunkType.Spoon));
            CollectionAssert.AreEqual(new[] { 8, 53, 503, 5003 }, pity, "empty and junk digs count as digs without a gem");

            GemDigConfig.AdvancePity(pity, DigFind.OfGem(GemType.Ruby)); // Epic
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 5004 }, pity, "an Epic resets Uncommon..Epic");

            pity = new[] { 7, 70, 700, 7000 };
            GemDigConfig.AdvancePity(pity, DigFind.OfGem(GemType.Diamond));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, pity, "a Diamond resets everything");
        }

        [TestMethod]
        public void Resolve_LowestRollIsTheBestTier_HighestIsTheBasePool()
        {
            var zero = new int[GemDigConfig.PityTierCount];
            Assert.AreEqual(GemType.Diamond, GemDigConfig.Resolve(0, zero).Gem);
            var last = GemDigConfig.Resolve(GemDigConfig.RollRange - 1, zero);
            Assert.IsTrue(!last.IsGem || GemDigConfig.GetRarity(last.Gem) == ItemRarity.Normal, "top roll lands in the base pool: " + last);
            // Out-of-range rolls clamp instead of throwing
            Assert.AreEqual(GemType.Diamond, GemDigConfig.Resolve(-1, zero).Gem);
            GemDigConfig.Resolve(long.MaxValue, zero);
        }

        [TestMethod]
        public void Dig_IsDeterministicForTheSameRolls()
        {
            var a = new GameStateService();
            var b = new GameStateService();
            var random = new SeedableRandom(452);
            for (int i = 0; i < 50_000; i++)
            {
                long roll = random.NextInt64(GemDigConfig.RollRange);
                Assert.AreEqual(a.Dig(roll).OutcomeIndex, b.Dig(roll).OutcomeIndex);
            }
            CollectionAssert.AreEqual(a.CopyGemPityArray(), b.CopyGemPityArray());
        }

        [TestMethod]
        public void Pity_RaisesHighTierRates_KeepsDiamondRarest_AndLeavesEmptyAndJunkCommon()
        {
            const int Digs = 20_000_000;
            var state = new GameStateService();
            var random = new SeedableRandom(7);
            var gems = new long[GemDigConfig.GemCount];
            var tiers = new long[5];
            long gold = 0, nothing = 0, junk = 0;
            for (int i = 0; i < Digs; i++)
            {
                var find = state.Dig(random.NextInt64(GemDigConfig.RollRange));
                if (find.IsGem)
                {
                    gems[find.Index]++;
                    tiers[(int)GemDigConfig.GetRarity(find.Gem)]++;
                    gold += GemDigConfig.GetValue(find);
                }
                else if (find.Kind == DigFindKind.Nothing) nothing++;
                else junk++;
            }

            double rareWait = (double)Digs / (tiers[2] + tiers[3] + tiers[4]);
            double epicWait = (double)Digs / (tiers[3] + tiers[4]);
            double diamondWait = (double)Digs / tiers[4];
            Assert.IsTrue(rareWait > 17 && rareWait < 20, "Rare or better every " + rareWait + " (base 21.4)");
            Assert.IsTrue(epicWait > 60 && epicWait < 73, "Epic or better every " + epicWait + " (base 78)");
            Assert.IsTrue(diamondWait > 760 && diamondWait < 920, "Diamond every " + diamondWait + " (base 1,000)");

            // Each gem is still rarer than the one listed before it, so Diamond stays the rarest
            for (int g = 1; g < GemDigConfig.GemCount; g++)
                Assert.IsTrue(gems[g] < gems[g - 1], ((GemType)g) + " should be rarer than " + ((GemType)(g - 1)));

            Assert.AreEqual(0.35, (double)nothing / Digs, 0.01, "empty digs stay about 35%");
            Assert.AreEqual(0.30, (double)junk / Digs, 0.01, "junk stays about 30%");

            double perDig = (double)gold / Digs;
            Assert.IsTrue(perDig > 48 && perDig < 54, "gold per dig " + perDig + " (base 43.5)");
        }

        [TestMethod]
        public void SetGemPity_ClampsAndToleratesShortArrays()
        {
            var state = new GameStateService();
            state.SetGemPity(new[] { -3, 9 });
            CollectionAssert.AreEqual(new[] { 0, 9, 0, 0 }, state.CopyGemPityArray());
            state.ResetGemPity();
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, state.CopyGemPityArray());
        }

        // ── Save section 56 (v42) ───────────────────────────────────────────────

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

        /// <summary>Bytes of section 56: a count int plus one int per pity tier.</summary>
        private const int V42Tail = 4 + 4 * GemDigConfig.PityTierCount;

        [TestMethod]
        public void SaveData_V42_GemPity_RoundTrips()
        {
            var loaded = Read(Write(new SaveData { HeroName = "Digger", GemPity = new[] { 3, 140, 900, 12000 } }));
            CollectionAssert.AreEqual(new[] { 3, 140, 900, 12000 }, loaded.GemPity);

            var state = new GameStateService();
            state.SetGemPity(loaded.GemPity);
            Assert.AreEqual(900, state.GetGemPity(Epic));
            Assert.AreEqual(12000, state.GetGemPity(Legendary));
        }

        /// <summary>
        /// Backwards compatibility for the v42 bump: a v41 file ends before section 56, so the hero
        /// starts with no bad-luck streak. Built by cutting section 56 off a v42 stream and patching
        /// the version header down to 41.
        /// </summary>
        [TestMethod]
        public void SaveData_V41_File_LoadsZeroGemPity()
        {
            var original = new SaveData { HeroName = "OldDigger", Level = 7, GemPity = new[] { 1, 2, 3, 4 } };
            byte[] v42 = Write(original);
            var v41 = new byte[v42.Length - V42Tail];
            Array.Copy(v42, v41, v41.Length);
            v41[0] = 41; v41[1] = 0; v41[2] = 0; v41[3] = 0;

            var loaded = Read(v41);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, loaded.GemPity);
            Assert.AreEqual("OldDigger", loaded.HeroName);
            Assert.AreEqual(7, loaded.Level);
        }
    }
}
