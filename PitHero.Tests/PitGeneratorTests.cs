using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using PitHero;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest unit tests for PitGenerator functionality
    /// </summary>
    [TestClass]
    public class PitGeneratorTests
    {
        /// <summary>
        /// Validates cave levels 1-4 only generate tier 1 enemies.
        /// </summary>
        [TestMethod]
        public void CaveEnemies_Pit1To4_OnlyTier1()
        {
            var generator = new PitGenerator((Nez.Scene)null!);
            string[] allowed = { "Slime", "Bat", "Rat", "CaveMushroom", "StoneBeetle" };

            for (int level = 1; level <= 4; level++)
            {
                for (int sample = 0; sample < 50; sample++)
                {
                    var result = generator.CreateEnemyForPitLevel(level);
                    string enemyType = result.enemy.GetType().Name;
                    Assert.IsTrue(IsAllowedEnemy(enemyType, allowed),
                        $"Level {level} generated unexpected enemy '{enemyType}'");
                }
            }
        }

        /// <summary>
        /// Validates cave levels 6-9 generate only tier 1 and tier 2 enemies.
        /// </summary>
        [TestMethod]
        public void CaveEnemies_Pit6To9_Tier1AndTier2()
        {
            var generator = new PitGenerator((Nez.Scene)null!);
            string[] allowed = { "Slime", "Bat", "Rat", "CaveMushroom", "StoneBeetle", "Goblin", "Spider", "Snake", "ShadowImp", "TunnelWorm", "FireLizard" };

            for (int level = 6; level <= 9; level++)
            {
                for (int sample = 0; sample < 50; sample++)
                {
                    var result = generator.CreateEnemyForPitLevel(level);
                    string enemyType = result.enemy.GetType().Name;
                    Assert.IsTrue(IsAllowedEnemy(enemyType, allowed),
                        $"Level {level} generated unexpected enemy '{enemyType}'");
                }
            }
        }

        /// <summary>
        /// Validates cave levels 11-24 include tier 3 enemies in the spawn pool.
        /// </summary>
        [TestMethod]
        public void CaveEnemies_Pit11Plus_IncludesTier3()
        {
            var generator = new PitGenerator((Nez.Scene)null!);
            int[] levels = { 11, 16, 21, 24 };

            for (int index = 0; index < levels.Length; index++)
            {
                int level = levels[index];
                bool sawSkeleton = false;
                bool sawOrc = false;
                bool sawWraith = false;

                for (int sample = 0; sample < 300; sample++)
                {
                    var result = generator.CreateEnemyForPitLevel(level);
                    string enemyType = result.enemy.GetType().Name;

                    if (enemyType == "Skeleton")
                    {
                        sawSkeleton = true;
                    }
                    else if (enemyType == "Orc")
                    {
                        sawOrc = true;
                    }
                    else if (enemyType == "Wraith")
                    {
                        sawWraith = true;
                    }
                }

                Assert.IsTrue(sawSkeleton, $"Level {level} should include Skeleton in pool");
                Assert.IsTrue(sawOrc, $"Level {level} should include Orc in pool");
                Assert.IsTrue(sawWraith, $"Level {level} should include Wraith in pool");
            }
        }

        /// <summary>
        /// Validates cave boss floors spawn the configured boss for each floor and expected scaled level.
        /// </summary>
        [TestMethod]
        public void CaveEnemies_BossFloors_SpawnConfiguredBossWithExpectedLevel()
        {
            var generator = new PitGenerator((Nez.Scene)null!);
            int[] bossFloors = { 5, 10, 15, 20, 25 };
            string[] expectedBosses = { "StoneGuardian", "PitLord", "EarthElemental", "MoltenTitan", "AncientWyrm" };
            int[] expectedLevels = { 10, 10, 28, 38, 46 };

            for (int index = 0; index < bossFloors.Length; index++)
            {
                int floor = bossFloors[index];
                var result = generator.CreateEnemyForPitLevel(floor);

                Assert.AreEqual(expectedBosses[index], result.enemy.GetType().Name,
                    $"Boss floor {floor} should spawn {expectedBosses[index]}");
                Assert.AreEqual(expectedLevels[index], result.enemy.Level,
                    $"Boss floor {floor} should spawn level {expectedLevels[index]}");
            }
        }

        /// <summary>
        /// Validates non-boss cave floors never spawn the PitLord boss.
        /// </summary>
        [TestMethod]
        public void CaveEnemies_NonBossFloors_NoPitLord()
        {
            var generator = new PitGenerator((Nez.Scene)null!);

            for (int level = 1; level <= 25; level++)
            {
                if (PitHero.Config.CaveBiomeConfig.IsBossFloor(level))
                {
                    continue;
                }

                for (int sample = 0; sample < 60; sample++)
                {
                    var result = generator.CreateEnemyForPitLevel(level);
                    Assert.AreNotEqual("PitLord", result.enemy.GetType().Name,
                        $"Non-boss level {level} should never spawn PitLord");
                }
            }
        }

        /// <summary>
        /// Validates generated enemy level scaling follows CaveBiomeConfig for all cave levels.
        /// </summary>
        [TestMethod]
        public void CaveEnemies_LevelScaling_FollowsBalanceConfig()
        {
            var generator = new PitGenerator((Nez.Scene)null!);

            for (int level = 1; level <= 25; level++)
            {
                int expected = PitHero.Config.CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level);

                for (int sample = 0; sample < 10; sample++)
                {
                    var result = generator.CreateEnemyForPitLevel(level);
                    Assert.IsTrue(result.enemy.Level >= 1 && result.enemy.Level <= expected,
                        $"Level {level} should generate enemies between level 1 and scaled level {expected}");
                }
            }
        }

        [TestMethod]
        public void PitGenerator_Constants_ShouldBeValidValues()
        {
            // Arrange & Act - Test that we can access the constants
            var obstacleTag = GameConfig.TAG_OBSTACLE;
            var treasureTag = GameConfig.TAG_TREASURE;
            var monsterTag = GameConfig.TAG_MONSTER;
            var wizardOrbTag = GameConfig.TAG_WIZARD_ORB;
            
            // Assert
            Assert.IsTrue(obstacleTag > 0, "Obstacle tag should be greater than 0");
            Assert.IsTrue(treasureTag > 0, "Treasure tag should be greater than 0");
            Assert.IsTrue(monsterTag > 0, "Monster tag should be greater than 0");
            Assert.IsTrue(wizardOrbTag > 0, "Wizard orb tag should be greater than 0");
        }

        [TestMethod]
        public void GameConfig_EntityTagConstants_ShouldHaveCorrectValues()
        {
            // Arrange & Act
            var obstacleTag = GameConfig.TAG_OBSTACLE;
            var treasureTag = GameConfig.TAG_TREASURE;
            var monsterTag = GameConfig.TAG_MONSTER;
            var wizardOrbTag = GameConfig.TAG_WIZARD_ORB;
            
            // Assert
            Assert.AreEqual(4, obstacleTag, "Obstacle tag should be 4");
            Assert.AreEqual(5, treasureTag, "Treasure tag should be 5");
            Assert.AreEqual(6, monsterTag, "Monster tag should be 6");
            Assert.AreEqual(7, wizardOrbTag, "Wizard orb tag should be 7");
        }

        [TestMethod]
        public void GoapConstants_PitInitialized_ShouldExistWithCorrectValue()
        {
            // Arrange & Act
            var pitInitializedConstant = PitHero.AI.GoapConstants.PitInitialized;
            
            // Assert
            Assert.AreEqual("PitInitialized", pitInitializedConstant, "PitInitialized constant should have correct value");
        }

        [TestMethod]
        public void GameConfig_PitBounds_ShouldHaveCorrectConfiguration()
        {
            // Arrange & Act
            var pitRectX = GameConfig.PitRectX;
            var pitRectY = GameConfig.PitRectY;
            var pitRectWidth = GameConfig.PitRectWidth;
            var pitRectHeight = GameConfig.PitRectHeight;
            
            // Assert
            Assert.AreEqual(1, pitRectX, "Pit rect X should be 1");
            Assert.AreEqual(2, pitRectY, "Pit rect Y should be 2");
            Assert.AreEqual(12, pitRectWidth, "Pit rect width should be 12");
            Assert.AreEqual(9, pitRectHeight, "Pit rect height should be 9");
        }

        [TestMethod]
        public void PitGenerator_ValidPlacementArea_ShouldCalculateCorrectly()
        {
            // Arrange - Calculate valid placement area (excluding 1-tile perimeter)
            var validMinX = GameConfig.PitRectX + 1; // Should be 2
            var validMinY = GameConfig.PitRectY + 1; // Should be 3
            var validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // Should be 11
            var validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // Should be 9
            
            // Act
            var validWidth = validMaxX - validMinX + 1; // Should be 10
            var validHeight = validMaxY - validMinY + 1; // Should be 7
            var totalSpots = validWidth * validHeight; // Should be 70
            
            // Assert
            Assert.AreEqual(10, validWidth, "Valid placement width should be 10");
            Assert.AreEqual(7, validHeight, "Valid placement height should be 7");
            Assert.AreEqual(70, totalSpots, "Total valid placement spots should be 70");
        }

        [TestMethod]
        public void PitGenerator_EntityCountRequirements_ShouldBeCorrect()
        {
            // Arrange & Act
            int obstacles = 10;
            int treasures = 2;
            int monsters = 2;
            int wizardOrbs = 1;
            int totalEntities = obstacles + treasures + monsters + wizardOrbs;
            
            // Assert
            Assert.AreEqual(15, totalEntities, "Total entities should be 15 (10+2+2+1)");
        }

        [TestMethod]
        public void PitGenerator_LevelBasedFormulas_ShouldCalculateCorrectAmounts()
        {
            // Test the formulas from the problem statement
            
            // Level 1: should be minimum values (2, 2, 5-10)
            ValidateFormulasForLevel(1, expectedMaxMonsters: 2, expectedMaxChests: 2, 
                                   expectedMinObstacles: 5, expectedMaxObstacles: 10);
            
            // Level 10: should be minimum values (2, 2, 5-10) 
            ValidateFormulasForLevel(10, expectedMaxMonsters: 2, expectedMaxChests: 2,
                                   expectedMinObstacles: 5, expectedMaxObstacles: 10);
            
            // Level 55: should be mid-range values 
            ValidateFormulasForLevel(55, expectedMaxMonsters: 6, expectedMaxChests: 6,
                                   expectedMinObstacles: 22, expectedMaxObstacles: 30);
            
            // Level 100: should be maximum values (10, 10, 40-50)
            ValidateFormulasForLevel(100, expectedMaxMonsters: 10, expectedMaxChests: 10,
                                   expectedMinObstacles: 40, expectedMaxObstacles: 50);
        }

        private void ValidateFormulasForLevel(int level, int expectedMaxMonsters, int expectedMaxChests, 
                                            int expectedMinObstacles, int expectedMaxObstacles)
        {
            // Calculate using the formulas from the problem statement
            int actualMaxMonsters = Math.Clamp(
                (int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
            
            int actualMaxChests = Math.Clamp(
                (int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
            
            int actualMinObstacles = Math.Clamp(
                (int)Math.Round(5 + 35 * Math.Max(level - 10, 0) / 90.0), 5, 40);
            
            int actualMaxObstacles = Math.Clamp(
                (int)Math.Round(10 + 40 * Math.Max(level - 10, 0) / 90.0), 10, 50);
            
            // Assert the calculated values match expectations
            Assert.AreEqual(expectedMaxMonsters, actualMaxMonsters, 
                $"MaxMonsters for level {level} should be {expectedMaxMonsters}");
            Assert.AreEqual(expectedMaxChests, actualMaxChests, 
                $"MaxChests for level {level} should be {expectedMaxChests}");
            Assert.AreEqual(expectedMinObstacles, actualMinObstacles, 
                $"MinObstacles for level {level} should be {expectedMinObstacles}");
            Assert.AreEqual(expectedMaxObstacles, actualMaxObstacles, 
                $"MaxObstacles for level {level} should be {expectedMaxObstacles}");
        }

        /// <summary>
        /// Checks if an enemy name is allowed for a level band.
        /// </summary>
        private bool IsAllowedEnemy(string enemyType, string[] allowed)
        {
            for (int index = 0; index < allowed.Length; index++)
            {
                if (allowed[index] == enemyType)
                {
                    return true;
                }
            }

            return false;
        }

        [TestMethod]
        public void PitGenerator_FormulaConstraints_ShouldRespectMinMaxLimits()
        {
            // Test extreme values to ensure clamping works
            
            // Level 0: should clamp to minimums  
            ValidateFormulasForLevel(0, expectedMaxMonsters: 2, expectedMaxChests: 2,
                                   expectedMinObstacles: 5, expectedMaxObstacles: 10);
            
            // Level 1000: should clamp to maximums
            ValidateFormulasForLevel(1000, expectedMaxMonsters: 10, expectedMaxChests: 10,
                                   expectedMinObstacles: 40, expectedMaxObstacles: 50);
        }
    }
}