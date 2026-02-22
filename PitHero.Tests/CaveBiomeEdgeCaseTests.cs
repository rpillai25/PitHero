using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests
{
    /// <summary>
    /// Edge-case and boundary tests for Cave biome progression rules.
    /// </summary>
    [TestClass]
    public class CaveBiomeEdgeCaseTests
    {
        /// <summary>
        /// Verifies level 0 is not in the Cave biome range.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Level0_NotCave()
        {
            Assert.IsFalse(CaveBiomeConfig.IsCaveLevel(0));
        }

        /// <summary>
        /// Verifies level 1 is the first Cave biome level.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Level1_IsCave_FirstLevel()
        {
            Assert.IsTrue(CaveBiomeConfig.IsCaveLevel(1));
        }

        /// <summary>
        /// Verifies level 25 is the last Cave biome level.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Level25_IsCave_LastLevel()
        {
            Assert.IsTrue(CaveBiomeConfig.IsCaveLevel(25));
            Assert.IsFalse(CaveBiomeConfig.IsCaveLevel(26));
        }

        /// <summary>
        /// Verifies level 26 is outside the Cave biome range.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Level26_NotCave()
        {
            Assert.IsFalse(CaveBiomeConfig.IsCaveLevel(26));
        }

        /// <summary>
        /// Verifies rarity transitions from Normal to Uncommon between levels 10 and 11.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Transitions_Level10To11_RarityChange()
        {
            Assert.AreEqual(ItemRarity.Normal, CaveBiomeConfig.GetCaveRarityBand(10));
            Assert.AreEqual(ItemRarity.Uncommon, CaveBiomeConfig.GetCaveRarityBand(11));

            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(10, 0.0f));
            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(10, 0.5f));
            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(10, 0.99f));

            Assert.AreEqual(2, CaveBiomeConfig.DetermineCaveTreasureLevel(11, 0.34f));
            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(11, 0.35f));
            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(11, 0.99f));
        }

        /// <summary>
        /// Verifies transition from level 4 to 5 enters a boss floor.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Transitions_Level4To5_BossFloor()
        {
            Assert.IsFalse(CaveBiomeConfig.IsBossFloor(4));
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(5));

            string[] level4Pool = CaveBiomeConfig.GetEnemyPoolForLevel(4);
            string[] level5Pool = CaveBiomeConfig.GetEnemyPoolForLevel(5);

            Assert.IsTrue(level4Pool.Length > 0);
            Assert.AreEqual(0, level5Pool.Length);
            Assert.IsTrue(ContainsEnemy(level4Pool, "Slime"));
            Assert.IsTrue(ContainsEnemy(level4Pool, "Bat"));
            Assert.IsTrue(ContainsEnemy(level4Pool, "Rat"));
        }

        /// <summary>
        /// Verifies transition from level 5 to 6 exits boss floor state.
        /// </summary>
        [TestMethod]
        public void CaveBiome_Transitions_Level5To6_NonBossFloor()
        {
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(5));
            Assert.IsFalse(CaveBiomeConfig.IsBossFloor(6));

            string[] level5Pool = CaveBiomeConfig.GetEnemyPoolForLevel(5);
            string[] level6Pool = CaveBiomeConfig.GetEnemyPoolForLevel(6);

            Assert.AreEqual(0, level5Pool.Length);
            Assert.IsTrue(level6Pool.Length > 0);
            Assert.IsTrue(ContainsEnemy(level6Pool, "Slime"));
            Assert.IsTrue(ContainsEnemy(level6Pool, "Bat"));
            Assert.IsTrue(ContainsEnemy(level6Pool, "Rat"));
            Assert.IsTrue(ContainsEnemy(level6Pool, "Goblin"));
            Assert.IsTrue(ContainsEnemy(level6Pool, "Spider"));
            Assert.IsTrue(ContainsEnemy(level6Pool, "Snake"));
        }

        /// <summary>
        /// Verifies out-of-range levels return empty enemy pools.
        /// </summary>
        [TestMethod]
        public void CaveBiome_GetEnemyPool_LevelOutOfRange_ReturnsEmpty()
        {
            int[] levels = { -100, -1, 0, 26, 999 };

            for (int i = 0; i < levels.Length; i++)
            {
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(levels[i]);
                Assert.AreEqual(0, pool.Length, $"Out-of-range level {levels[i]} should return empty enemy pool");
            }
        }

        /// <summary>
        /// Verifies out-of-range levels are never considered boss floors.
        /// </summary>
        [TestMethod]
        public void CaveBiome_IsBossFloor_LevelOutOfRange_ReturnsFalse()
        {
            int[] levels = { -100, -1, 0, 26, 999 };

            for (int i = 0; i < levels.Length; i++)
            {
                Assert.IsFalse(CaveBiomeConfig.IsBossFloor(levels[i]),
                    $"Out-of-range level {levels[i]} should not be a boss floor");
            }
        }

        /// <summary>
        /// Verifies scaled enemy level is always clamped to the valid [1, 99] range.
        /// </summary>
        [TestMethod]
        public void CaveBiome_ScaledLevel_AlwaysClamped_1To99()
        {
            for (int pitLevel = -500; pitLevel <= 500; pitLevel++)
            {
                int scaledLevel = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(pitLevel);
                Assert.IsTrue(scaledLevel >= 1 && scaledLevel <= 99,
                    $"Scaled level {scaledLevel} for pit {pitLevel} should be clamped to [1, 99]");
            }
        }

        /// <summary>
        /// Returns true if the enemy exists in the provided pool.
        /// </summary>
        private static bool ContainsEnemy(string[] pool, string enemyName)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == enemyName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
