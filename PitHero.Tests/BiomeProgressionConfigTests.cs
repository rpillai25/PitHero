using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for BiomeProgressionConfig helper methods:
    /// round-trips between (pitLevel, pitTier) and cumulative depth, boundary values,
    /// and tier-cap behaviour.
    /// </summary>
    [TestClass]
    public class BiomeProgressionConfigTests
    {
        // MaxBiomeLevel is derived from CaveBiomeConfig.CaveEndLevel = 25.
        private const int MaxLevel = 25;

        // ── GetEffectiveDepth ─────────────────────────────────────────────────────

        [TestMethod]
        public void GetEffectiveDepth_Level1_Tier1_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetEffectiveDepth(1, 1));
        }

        [TestMethod]
        public void GetEffectiveDepth_Level25_Tier1_Returns25()
        {
            Assert.AreEqual(25, BiomeProgressionConfig.GetEffectiveDepth(25, 1));
        }

        [TestMethod]
        public void GetEffectiveDepth_Level1_Tier2_Returns26()
        {
            Assert.AreEqual(26, BiomeProgressionConfig.GetEffectiveDepth(1, 2));
        }

        [TestMethod]
        public void GetEffectiveDepth_Level19_Tier5_Returns119()
        {
            Assert.AreEqual(119, BiomeProgressionConfig.GetEffectiveDepth(19, 5));
        }

        [TestMethod]
        public void GetEffectiveDepth_ClampsLowTier()
        {
            // Tier 0 and below should be treated as tier 1.
            Assert.AreEqual(BiomeProgressionConfig.GetEffectiveDepth(5, 1),
                            BiomeProgressionConfig.GetEffectiveDepth(5, 0));
            Assert.AreEqual(BiomeProgressionConfig.GetEffectiveDepth(5, 1),
                            BiomeProgressionConfig.GetEffectiveDepth(5, -99));
        }

        [TestMethod]
        public void GetEffectiveDepth_ClampsAtMaxTier()
        {
            int depth99 = BiomeProgressionConfig.GetEffectiveDepth(1, 99);
            int depth100 = BiomeProgressionConfig.GetEffectiveDepth(1, 100);
            Assert.AreEqual(depth99, depth100);
        }

        // ── GetDisplayedLevelForDepth ─────────────────────────────────────────────

        [TestMethod]
        public void GetDisplayedLevelForDepth_Depth1_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(1));
        }

        [TestMethod]
        public void GetDisplayedLevelForDepth_Depth25_Returns25()
        {
            Assert.AreEqual(25, BiomeProgressionConfig.GetDisplayedLevelForDepth(25));
        }

        [TestMethod]
        public void GetDisplayedLevelForDepth_Depth26_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(26));
        }

        [TestMethod]
        public void GetDisplayedLevelForDepth_Depth119_Returns19()
        {
            Assert.AreEqual(19, BiomeProgressionConfig.GetDisplayedLevelForDepth(119));
        }

        [TestMethod]
        public void GetDisplayedLevelForDepth_GuardDepthLessThan1_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(0));
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(-5));
        }

        // ── GetTierForDepth ───────────────────────────────────────────────────────

        [TestMethod]
        public void GetTierForDepth_Depth1_ReturnsTier1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(1));
        }

        [TestMethod]
        public void GetTierForDepth_Depth25_ReturnsTier1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(25));
        }

        [TestMethod]
        public void GetTierForDepth_Depth26_ReturnsTier2()
        {
            Assert.AreEqual(2, BiomeProgressionConfig.GetTierForDepth(26));
        }

        [TestMethod]
        public void GetTierForDepth_Depth119_ReturnsTier5()
        {
            Assert.AreEqual(5, BiomeProgressionConfig.GetTierForDepth(119));
        }

        [TestMethod]
        public void GetTierForDepth_CapAt99()
        {
            // Depth 2475 = 99 * 25; depth 2476 = 100 * 25 + 1 — both should return 99.
            Assert.AreEqual(99, BiomeProgressionConfig.GetTierForDepth(2475));
            Assert.AreEqual(99, BiomeProgressionConfig.GetTierForDepth(2476));
            Assert.AreEqual(99, BiomeProgressionConfig.GetTierForDepth(99999));
        }

        [TestMethod]
        public void GetTierForDepth_GuardDepthLessThan1_Returns1()
        {
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(0));
            Assert.AreEqual(1, BiomeProgressionConfig.GetTierForDepth(-1));
        }

        // ── Round-trip ────────────────────────────────────────────────────────────

        [TestMethod]
        public void RoundTrip_Depth25_Tier1_Level25()
        {
            int depth = 25;
            Assert.AreEqual(25, BiomeProgressionConfig.GetDisplayedLevelForDepth(depth));
            Assert.AreEqual(1,  BiomeProgressionConfig.GetTierForDepth(depth));
        }

        [TestMethod]
        public void RoundTrip_Depth26_Tier2_Level1()
        {
            int depth = 26;
            Assert.AreEqual(1, BiomeProgressionConfig.GetDisplayedLevelForDepth(depth));
            Assert.AreEqual(2, BiomeProgressionConfig.GetTierForDepth(depth));
        }

        [TestMethod]
        public void RoundTrip_Depth119_Tier5_Level19()
        {
            int depth = 119;
            Assert.AreEqual(19, BiomeProgressionConfig.GetDisplayedLevelForDepth(depth));
            Assert.AreEqual(5,  BiomeProgressionConfig.GetTierForDepth(depth));
        }

        [TestMethod]
        public void GetEffectiveDepth_ReconstitutesDepth()
        {
            // GetEffectiveDepth(GetDisplayedLevel(d), GetTier(d)) == d for valid depths.
            int[] testDepths = { 1, 25, 26, 50, 119, 200, 2475 };
            for (int i = 0; i < testDepths.Length; i++)
            {
                int d = testDepths[i];
                int level = BiomeProgressionConfig.GetDisplayedLevelForDepth(d);
                int tier  = BiomeProgressionConfig.GetTierForDepth(d);
                int reconstructed = BiomeProgressionConfig.GetEffectiveDepth(level, tier);
                Assert.AreEqual(d, reconstructed, $"Round-trip failed for depth {d}");
            }
        }

        // ── MaxBiomeLevel derivation ──────────────────────────────────────────────

        [TestMethod]
        public void MaxBiomeLevel_MatchesCaveEndLevel()
        {
            Assert.AreEqual(CaveBiomeConfig.CaveEndLevel, BiomeProgressionConfig.MaxBiomeLevel);
        }
    }
}
