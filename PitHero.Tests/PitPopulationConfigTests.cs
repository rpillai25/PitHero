using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Config;

namespace PitHero.Tests
{
    /// <summary>
    /// The pit monster curve (issue #422): a floor's monster count ramps from the first floor's
    /// handful up to at least ten by the biome's last floor, and never inverts.
    /// </summary>
    [TestClass]
    public class PitPopulationConfigTests
    {
        [TestMethod]
        [TestCategory("PitGeneration")]
        public void FirstFloor_StaysModest()
        {
            PitPopulationConfig.GetMonsterRange(1, out var min, out var max);
            Assert.AreEqual(GameConfig.PitMonsterMinAtFirstFloor, min, "floor 1 min");
            Assert.AreEqual(GameConfig.PitMonsterMaxAtFirstFloor, max, "floor 1 max");
        }

        [TestMethod]
        [TestCategory("PitGeneration")]
        public void LastFloor_GuaranteesAtLeastTenMonsters()
        {
            PitPopulationConfig.GetMonsterRange(CaveBiomeConfig.CaveEndLevel, out var min, out var max);
            Assert.IsTrue(min >= 10, $"floor {CaveBiomeConfig.CaveEndLevel} must always spawn at least 10 monsters, got min {min}");
            Assert.IsTrue(max >= min, "max must not fall below min");
        }

        [TestMethod]
        [TestCategory("PitGeneration")]
        public void Range_NeverDecreasesAcrossTheBiome()
        {
            int prevMin = int.MinValue;
            int prevMax = int.MinValue;
            for (int level = 1; level <= CaveBiomeConfig.CaveEndLevel; level++)
            {
                PitPopulationConfig.GetMonsterRange(level, out var min, out var max);
                Assert.IsTrue(min >= prevMin, $"min must not fall going from floor {level - 1} to {level}");
                Assert.IsTrue(max >= prevMax, $"max must not fall going from floor {level - 1} to {level}");
                Assert.IsTrue(max >= min, $"floor {level}: max {max} must be at least min {min}");
                prevMin = min;
                prevMax = max;
            }
        }

        [TestMethod]
        [TestCategory("PitGeneration")]
        public void LevelsOutsideTheBiome_ClampToItsEnds()
        {
            PitPopulationConfig.GetMonsterRange(1, out var firstMin, out var firstMax);
            PitPopulationConfig.GetMonsterRange(0, out var belowMin, out var belowMax);
            Assert.AreEqual(firstMin, belowMin, "levels below 1 behave as floor 1");
            Assert.AreEqual(firstMax, belowMax, "levels below 1 behave as floor 1");

            PitPopulationConfig.GetMonsterRange(CaveBiomeConfig.CaveEndLevel, out var lastMin, out var lastMax);
            PitPopulationConfig.GetMonsterRange(CaveBiomeConfig.CaveEndLevel + 20, out var pastMin, out var pastMax);
            Assert.AreEqual(lastMin, pastMin, "levels past the biome end behave as the last floor");
            Assert.AreEqual(lastMax, pastMax, "levels past the biome end behave as the last floor");
        }

        [TestMethod]
        [TestCategory("PitGeneration")]
        public void EveryFloor_HoldsMoreMonstersThanTheOldFormula()
        {
            // The retired formula was clamp(round(2 + 8*max(level-10,0)/90), 2, 10) with the actual
            // count rolled in [max/2, max] — never more than 3 on any cave floor.
            for (int level = 1; level <= CaveBiomeConfig.CaveEndLevel; level++)
            {
                int oldMax = System.Math.Clamp((int)System.Math.Round(2 + 8 * System.Math.Max(level - 10, 0) / 90.0), 2, 10);
                PitPopulationConfig.GetMonsterRange(level, out var min, out _);
                Assert.IsTrue(min > oldMax, $"floor {level}: new minimum {min} must beat the old maximum {oldMax}");
            }
        }
    }
}
