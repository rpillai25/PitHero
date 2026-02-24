using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    [TestClass]
    public class CaveBiomeConfigTests
    {
        [TestMethod]
        public void CaveBiome_IsCaveLevel_ShouldMatchRange1To25()
        {
            for (int level = 1; level <= 25; level++)
            {
                Assert.IsTrue(CaveBiomeConfig.IsCaveLevel(level), $"Level {level} should be Cave biome");
            }

            Assert.IsFalse(CaveBiomeConfig.IsCaveLevel(0));
            Assert.IsFalse(CaveBiomeConfig.IsCaveLevel(26));
        }

        [TestMethod]
        public void CaveBiome_IsBossFloor_ShouldMatchExpectedCadence()
        {
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(5));
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(10));
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(15));
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(20));
            Assert.IsTrue(CaveBiomeConfig.IsBossFloor(25));

            for (int level = 1; level <= 25; level++)
            {
                bool expected = level == 5 || level == 10 || level == 15 || level == 20 || level == 25;
                Assert.AreEqual(expected, CaveBiomeConfig.IsBossFloor(level), $"Unexpected boss-floor state for level {level}");
            }
        }

        [TestMethod]
        public void CaveBiome_GetEnemyPoolForLevel_ShouldBeEmptyOnBossFloorsOnly()
        {
            for (int level = 1; level <= 25; level++)
            {
                var pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                if (CaveBiomeConfig.IsBossFloor(level))
                {
                    Assert.AreEqual(0, pool.Length, $"Boss floor {level} should not have regular pool entries");
                }
                else
                {
                    Assert.IsTrue(pool.Length > 0, $"Non-boss floor {level} should have at least one enemy option");
                }
            }
        }

        [TestMethod]
        public void CaveBiome_DetermineCaveTreasureLevel_ShouldStayWithinOneToThree()
        {
            // Pit levels 1-15 must stay in band [1, 2]; levels 16-25 may also return 3 (Rare) on low rolls.
            for (int level = 1; level <= 25; level++)
            {
                var levelLowRoll = CaveBiomeConfig.DetermineCaveTreasureLevel(level, 0.0f);
                var levelMidRoll = CaveBiomeConfig.DetermineCaveTreasureLevel(level, 0.5f);
                var levelHighRoll = CaveBiomeConfig.DetermineCaveTreasureLevel(level, 0.99f);

                Assert.IsTrue(levelLowRoll >= 1 && levelLowRoll <= 3,
                    $"Level {level} low roll produced out-of-range treasure level {levelLowRoll}");
                Assert.IsTrue(levelMidRoll >= 1 && levelMidRoll <= 3,
                    $"Level {level} mid roll produced out-of-range treasure level {levelMidRoll}");
                Assert.IsTrue(levelHighRoll >= 1 && levelHighRoll <= 3,
                    $"Level {level} high roll produced out-of-range treasure level {levelHighRoll}");

                // Levels 1-15 must never produce level 3.
                if (level <= 15)
                {
                    Assert.IsTrue(levelLowRoll <= 2,
                        $"Level {level} (<=15) must not produce treasure level 3, got {levelLowRoll}");
                    Assert.IsTrue(levelMidRoll <= 2,
                        $"Level {level} (<=15) must not produce treasure level 3, got {levelMidRoll}");
                    Assert.IsTrue(levelHighRoll <= 2,
                        $"Level {level} (<=15) must not produce treasure level 3, got {levelHighRoll}");
                }
            }

            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(5, 0.25f));
            Assert.AreEqual(2, CaveBiomeConfig.DetermineCaveTreasureLevel(15, 0.5f));
            Assert.AreEqual(1, CaveBiomeConfig.DetermineCaveTreasureLevel(15, 0.8f));
            // Boss floor 20: roll 0.0f should give level 3 (20% chance).
            Assert.AreEqual(3, CaveBiomeConfig.DetermineCaveTreasureLevel(20, 0.0f));
            // Boss floor 20: roll 0.5f should give level 2 (50% chance band).
            Assert.AreEqual(2, CaveBiomeConfig.DetermineCaveTreasureLevel(20, 0.5f));
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveBossFloor_ShouldGenerateBossMarker()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            context.PitGenerator.RegenerateForLevel(5);

            Assert.AreEqual(1, world.LastGeneratedBossMonsterCount);
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveNonBossFloor_ShouldNotGenerateBossMarker()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            context.PitGenerator.RegenerateForLevel(6);

            Assert.AreEqual(0, world.LastGeneratedBossMonsterCount);
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveTreasureLevels_ShouldStayInCaveBand()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            for (int level = 1; level <= 25; level++)
            {
                context.PitGenerator.RegenerateForLevel(level);
                for (int i = 0; i < world.LastGeneratedTreasureLevels.Count; i++)
                {
                    var treasureLevel = world.LastGeneratedTreasureLevels[i];
                    // Levels 1-15: max treasure level 2. Levels 16-25: max treasure level 3 (Rare possible).
                    int maxAllowed = level >= 16 ? 3 : 2;
                    Assert.IsTrue(treasureLevel >= 1 && treasureLevel <= maxAllowed,
                        $"Cave pit {level} generated invalid treasure level {treasureLevel} (max allowed: {maxAllowed})");
                }
            }
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveMonsterTypes_ShouldMatchEnemyPool()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            // Test non-boss floors
            for (int level = 1; level <= 25; level++)
            {
                if (CaveBiomeConfig.IsBossFloor(level))
                    continue; // Skip boss floors

                context.PitGenerator.RegenerateForLevel(level);

                string[] expectedPool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.IsTrue(expectedPool.Length > 0, $"Level {level} should have non-empty enemy pool");

                // All spawned monsters should be from the pool
                foreach (string monsterType in world.LastGeneratedMonsterTypes)
                {
                    bool isInPool = false;
                    for (int i = 0; i < expectedPool.Length; i++)
                    {
                        if (expectedPool[i] == monsterType)
                        {
                            isInPool = true;
                            break;
                        }
                    }

                    Assert.IsTrue(isInPool, 
                        $"Level {level} spawned {monsterType} which is not in expected pool: {string.Join(", ", expectedPool)}");
                }
            }
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveBossFloors_ShouldSpawnBossType()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            int[] bossFloors = { 5, 10, 15, 20, 25 };

            for (int i = 0; i < bossFloors.Length; i++)
            {
                int level = bossFloors[i];
                context.PitGenerator.RegenerateForLevel(level);

                Assert.AreEqual(1, world.LastGeneratedBossMonsterCount, 
                    $"Boss floor {level} should spawn exactly 1 boss");
                
                Assert.IsTrue(world.LastGeneratedMonsterTypes.Count > 0, 
                    $"Boss floor {level} should have monster types tracked");

                // At least one monster should be tracked (the boss)
                string firstMonster = world.LastGeneratedMonsterTypes[0];
                Assert.IsFalse(string.IsNullOrEmpty(firstMonster), 
                    $"Boss floor {level} should have valid boss type");
            }
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveEquipmentTypes_ShouldBeTracked()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            for (int level = 1; level <= 25; level++)
            {
                context.PitGenerator.RegenerateForLevel(level);

                // Equipment types should match treasure count
                Assert.AreEqual(world.LastGeneratedTreasureLevels.Count, 
                    world.LastGeneratedEquipmentTypes.Count,
                    $"Level {level} should track equipment type for each treasure");

                // All equipment types should be non-empty strings
                for (int i = 0; i < world.LastGeneratedEquipmentTypes.Count; i++)
                {
                    string equipmentType = world.LastGeneratedEquipmentTypes[i];
                    Assert.IsFalse(string.IsNullOrEmpty(equipmentType), 
                        $"Level {level} treasure {i} should have valid equipment type");
                }
            }
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveMonsterPool_ShouldHaveCorrectSize()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            // Test expected pool sizes for each pit level band
            // Updated to match 10-monster sliding window system
            
            // Pool 1 (Pit 1-4): 5 monsters (Slime, Bat, Rat, Cave Mushroom, Stone Beetle)
            for (int level = 1; level <= 4; level++)
            {
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.AreEqual(5, pool.Length, $"Pit {level} should have 5 monsters in pool");
            }

            // Pool 2 (Pit 6-9): 10 monsters (Pool 1 + 5 new types)
            for (int level = 6; level <= 9; level++)
            {
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.AreEqual(10, pool.Length, $"Pit {level} should have 10 monsters in pool");
            }

            // Pool 3 (Pit 11-14): 10 monsters (sliding window)
            for (int level = 11; level <= 14; level++)
            {
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.AreEqual(10, pool.Length, $"Pit {level} should have 10 monsters in pool");
            }

            // Pool 4 (Pit 16-19): 10 monsters (sliding window)
            for (int level = 16; level <= 19; level++)
            {
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.AreEqual(10, pool.Length, $"Pit {level} should have 10 monsters in pool");
            }

            // Pool 5 (Pit 21-24): 10 monsters (sliding window)
            for (int level = 21; level <= 24; level++)
            {
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.AreEqual(10, pool.Length, $"Pit {level} should have 10 monsters in pool");
            }

            // Boss floors (5, 10, 15, 20, 25): empty pool
            int[] bossFloors = { 5, 10, 15, 20, 25 };
            for (int i = 0; i < bossFloors.Length; i++)
            {
                int level = bossFloors[i];
                string[] pool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                Assert.AreEqual(0, pool.Length, $"Boss floor {level} should have empty pool");
            }
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveTreasureDistribution_Pit1To10_OnlyLevel1()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            // Test pit levels 1-10 should only have level 1 treasure
            for (int level = 1; level <= 10; level++)
            {
                context.PitGenerator.RegenerateForLevel(level);

                for (int i = 0; i < world.LastGeneratedTreasureLevels.Count; i++)
                {
                    int treasureLevel = world.LastGeneratedTreasureLevels[i];
                    Assert.AreEqual(1, treasureLevel, 
                        $"Pit {level} should only spawn level 1 treasure");
                }
            }
        }

        [TestMethod]
        public void VirtualPitGenerator_CaveTreasureDistribution_Pit11To25_MixedLevels()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            // Test pit levels 11-25 should have both level 1 and 2 treasure
            bool foundLevel1 = false;
            bool foundLevel2 = false;

            for (int level = 11; level <= 25; level++)
            {
                context.PitGenerator.RegenerateForLevel(level);

                for (int i = 0; i < world.LastGeneratedTreasureLevels.Count; i++)
                {
                    int treasureLevel = world.LastGeneratedTreasureLevels[i];
                    if (treasureLevel == 1) foundLevel1 = true;
                    if (treasureLevel == 2) foundLevel2 = true;
                }
            }

            Assert.IsTrue(foundLevel1, "Pit 11-25 should spawn some level 1 treasure");
            Assert.IsTrue(foundLevel2, "Pit 11-25 should spawn some level 2 treasure");
        }

        [TestMethod]
        public void VirtualPitGenerator_ClearEntities_ShouldResetTrackingLists()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            // Generate level 10
            context.PitGenerator.RegenerateForLevel(10);

            // Verify data was generated
            Assert.IsTrue(world.LastGeneratedMonsterTypes.Count > 0, "Should have monster types");
            Assert.IsTrue(world.LastGeneratedEquipmentTypes.Count > 0, "Should have equipment types");
            Assert.IsTrue(world.LastGeneratedTreasureLevels.Count > 0, "Should have treasure levels");

            // Clear entities
            world.ClearAllEntities();

            // Verify tracking lists were cleared
            Assert.AreEqual(0, world.LastGeneratedMonsterTypes.Count, "Monster types should be cleared");
            Assert.AreEqual(0, world.LastGeneratedEquipmentTypes.Count, "Equipment types should be cleared");
            Assert.AreEqual(0, world.LastGeneratedTreasureLevels.Count, "Treasure levels should be cleared");
            Assert.AreEqual(0, world.LastGeneratedBossMonsterCount, "Boss count should be cleared");
        }
    }
}
