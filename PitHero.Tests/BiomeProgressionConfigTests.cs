using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for BiomeProgressionConfig helper methods (issue #473: a 100-level cycle of four
    /// 25-level biomes): round-trips between (pitLevel, pitTier) and cumulative depth, biome and
    /// biome-level folding, scaling tier, legacy save conversion, boundary values and tier caps.
    /// </summary>
    [TestClass]
    public class BiomeProgressionConfigTests
    {
        // ── Derivation ────────────────────────────────────────────────────────────

        [TestMethod]
        public void BiomeLength_MatchesCaveEndLevel()
        {
            Assert.AreEqual(CaveBiomeConfig.CaveEndLevel, BiomeProgressionConfig.BiomeLength);
        }

        [TestMethod]
        public void CycleLength_IsFourBiomes()
        {
            Assert.AreEqual(4, BiomeProgressionConfig.BiomeCount);
            Assert.AreEqual(100, BiomeProgressionConfig.CycleLength);
        }

        // ── GetEffectiveDepth ─────────────────────────────────────────────────────

        [TestMethod]
        public void GetEffectiveDepth_Tier1_IsTheDisplayedLevel()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetEffectiveDepth(1, 1));
            Assert.AreEqual(26, BiomeProgressionConfig.GetEffectiveDepth(26, 1));
            Assert.AreEqual(100, BiomeProgressionConfig.GetEffectiveDepth(100, 1));
        }

        [TestMethod]
        public void GetEffectiveDepth_Level1_Tier2_Returns101()
        {
            Assert.AreEqual(101, BiomeProgressionConfig.GetEffectiveDepth(1, 2));
        }

        [TestMethod]
        public void GetEffectiveDepth_Level19_Tier3_Returns219()
        {
            Assert.AreEqual(219, BiomeProgressionConfig.GetEffectiveDepth(19, 3));
        }

        [TestMethod]
        public void GetEffectiveDepth_ClampsLowTier()
        {
            Assert.AreEqual(BiomeProgressionConfig.GetEffectiveDepth(5, 1), BiomeProgressionConfig.GetEffectiveDepth(5, 0));
            Assert.AreEqual(BiomeProgressionConfig.GetEffectiveDepth(5, 1), BiomeProgressionConfig.GetEffectiveDepth(5, -99));
        }

        [TestMethod]
        public void GetEffectiveDepth_ClampsAtMaxTier()
        {
            Assert.AreEqual(BiomeProgressionConfig.GetEffectiveDepth(1, 99), BiomeProgressionConfig.GetEffectiveDepth(1, 100));
        }

        // ── GetDisplayedLevelForDepth / GetTierForDepth ───────────────────────────

        [TestMethod]
        public void GetDisplayedLevelForDepth_WrapsPastTheCycle()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(1));
            Assert.AreEqual(26, BiomeProgressionConfig.GetDisplayedLevelForDepth(26));
            Assert.AreEqual(100, BiomeProgressionConfig.GetDisplayedLevelForDepth(100));
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(101));
            Assert.AreEqual(19, BiomeProgressionConfig.GetDisplayedLevelForDepth(219));
        }

        [TestMethod]
        public void GetDisplayedLevelForDepth_GuardDepthLessThan1_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(0));
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(-5));
        }

        [TestMethod]
        public void GetTierForDepth_IncrementsOnlyPastLevel100()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(1));
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(26));
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(100));
            Assert.AreEqual(2, BiomeProgressionConfig.GetTierForDepth(101));
            Assert.AreEqual(3, BiomeProgressionConfig.GetTierForDepth(219));
        }

        [TestMethod]
        public void GetTierForDepth_CapAt99()
        {
            Assert.AreEqual(99, BiomeProgressionConfig.GetTierForDepth(9900));
            Assert.AreEqual(99, BiomeProgressionConfig.GetTierForDepth(9901));
            Assert.AreEqual(99, BiomeProgressionConfig.GetTierForDepth(999999));
        }

        [TestMethod]
        public void GetTierForDepth_GuardDepthLessThan1_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(0));
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(-1));
        }

        [TestMethod]
        public void GetEffectiveDepth_ReconstitutesDepth()
        {
            int[] testDepths = { 1, 25, 26, 50, 100, 101, 119, 200, 201, 9900 };
            for (int i = 0; i < testDepths.Length; i++)
            {
                int d = testDepths[i];
                int level = BiomeProgressionConfig.GetDisplayedLevelForDepth(d);
                int tier = BiomeProgressionConfig.GetTierForDepth(d);
                Assert.AreEqual(d, BiomeProgressionConfig.GetEffectiveDepth(level, tier), $"Round-trip failed for depth {d}");
            }
        }

        // ── Biomes ────────────────────────────────────────────────────────────────

        [TestMethod]
        public void GetBiome_ChangesEvery25Levels()
        {
            Assert.AreEqual(PitBiome.Cave, BiomeProgressionConfig.GetBiome(1));
            Assert.AreEqual(PitBiome.Cave, BiomeProgressionConfig.GetBiome(25));
            Assert.AreEqual(PitBiome.Forest, BiomeProgressionConfig.GetBiome(26));
            Assert.AreEqual(PitBiome.Forest, BiomeProgressionConfig.GetBiome(50));
            Assert.AreEqual(PitBiome.Castle, BiomeProgressionConfig.GetBiome(51));
            Assert.AreEqual(PitBiome.Castle, BiomeProgressionConfig.GetBiome(75));
            Assert.AreEqual(PitBiome.Underworld, BiomeProgressionConfig.GetBiome(76));
            Assert.AreEqual(PitBiome.Underworld, BiomeProgressionConfig.GetBiome(100));
            Assert.AreEqual(PitBiome.Cave, BiomeProgressionConfig.GetBiome(0));
        }

        [TestMethod]
        public void GetBiomeLevel_FoldsEachBiomeOnto1To25()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetBiomeLevel(1));
            Assert.AreEqual(25, BiomeProgressionConfig.GetBiomeLevel(25));
            Assert.AreEqual(1, BiomeProgressionConfig.GetBiomeLevel(26));
            Assert.AreEqual(5, BiomeProgressionConfig.GetBiomeLevel(55));
            Assert.AreEqual(25, BiomeProgressionConfig.GetBiomeLevel(100));
            Assert.AreEqual(1, BiomeProgressionConfig.GetBiomeLevel(0));
        }

        [TestMethod]
        public void IsBossFloor_RepeatsInEveryBiome()
        {
            for (int level = 1; level <= 100; level++)
            {
                bool expected = level % 5 == 0;
                Assert.AreEqual(expected, BiomeProgressionConfig.IsBossFloor(level), $"level {level}");
            }
        }

        // ── Scaling tier (gear +N, chest gold) ────────────────────────────────────

        [TestMethod]
        public void GetScalingTier_StepsOncePerBiomeOfDepth()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetScalingTier(1, 1));
            Assert.AreEqual(1, BiomeProgressionConfig.GetScalingTier(25, 1));
            Assert.AreEqual(2, BiomeProgressionConfig.GetScalingTier(26, 1));
            Assert.AreEqual(4, BiomeProgressionConfig.GetScalingTier(100, 1));
            Assert.AreEqual(5, BiomeProgressionConfig.GetScalingTier(1, 2));
            Assert.AreEqual(99, BiomeProgressionConfig.GetScalingTier(100, 99));
        }

        [TestMethod]
        public void GetScalingDepthDelta_IsOneBiomePerStep()
        {
            Assert.AreEqual(0, BiomeProgressionConfig.GetScalingDepthDelta(1));
            Assert.AreEqual(25, BiomeProgressionConfig.GetScalingDepthDelta(2));
            Assert.AreEqual(75, BiomeProgressionConfig.GetScalingDepthDelta(4));
        }

        // ── Legacy save conversion ────────────────────────────────────────────────

        [TestMethod]
        public void ConvertLegacyPitPosition_PreservesEffectiveDepth()
        {
            BiomeProgressionConfig.ConvertLegacyPitPosition(7, 1, out int level, out int tier);
            Assert.AreEqual(7, level); Assert.AreEqual(1, tier);

            BiomeProgressionConfig.ConvertLegacyPitPosition(1, 2, out level, out tier);
            Assert.AreEqual(26, level); Assert.AreEqual(1, tier);

            BiomeProgressionConfig.ConvertLegacyPitPosition(25, 4, out level, out tier);
            Assert.AreEqual(100, level); Assert.AreEqual(1, tier);

            BiomeProgressionConfig.ConvertLegacyPitPosition(3, 5, out level, out tier);
            Assert.AreEqual(3, level); Assert.AreEqual(2, tier);

            BiomeProgressionConfig.ConvertLegacyPitPosition(0, 0, out level, out tier);
            Assert.AreEqual(1, level); Assert.AreEqual(1, tier);
        }
    }
}
