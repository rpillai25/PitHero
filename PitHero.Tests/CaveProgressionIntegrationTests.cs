using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Integration tests for complete Cave progression through levels 1-25.
    /// </summary>
    [TestClass]
    public class CaveProgressionIntegrationTests
    {
        /// <summary>
        /// Simulates a full cave progression pass and validates parity checkpoints.
        /// </summary>
        [TestMethod]
        public void CaveProgression_CompletePlaythrough_Levels1To25()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            int[] bossFloors = { 5, 10, 15, 20, 25 };
            int bossFloorIndex = 0;

            for (int level = 1; level <= 25; level++)
            {
                context.PitGenerator.RegenerateForLevel(level);

                var entities = world.GetEntityPositions();
                Assert.IsTrue(entities.ContainsKey("WizardOrb"), $"Level {level} should generate wizard orb");
                Assert.IsTrue(entities.ContainsKey("Obstacles"), $"Level {level} should generate obstacles");
                Assert.IsTrue(entities.ContainsKey("Treasures"), $"Level {level} should generate treasures");

                Assert.IsTrue(world.LastGeneratedMonsterTypes.Count > 0,
                    $"Level {level} should generate at least one monster type");
                Assert.AreEqual(world.LastGeneratedTreasureLevels.Count, world.LastGeneratedEquipmentTypes.Count,
                    $"Level {level} should track equipment types for every treasure");

                for (int treasureIndex = 0; treasureIndex < world.LastGeneratedTreasureLevels.Count; treasureIndex++)
                {
                    int treasureLevel = world.LastGeneratedTreasureLevels[treasureIndex];
                    Assert.IsTrue(treasureLevel == 1 || treasureLevel == 2,
                        $"Level {level} generated invalid cave treasure level {treasureLevel}");
                }

                bool isBossFloor = CaveBiomeConfig.IsBossFloor(level);
                if (isBossFloor)
                {
                    Assert.AreEqual(1, world.LastGeneratedBossMonsterCount,
                        $"Boss floor {level} should have one boss marker");
                    Assert.IsTrue(world.LastGeneratedMonsterTypes.Count > 0,
                        $"Boss floor {level} should include tracked boss type");

                    Assert.AreEqual(bossFloors[bossFloorIndex], level,
                        "Boss floors should follow 5-level cadence");
                    bossFloorIndex++;
                }
                else
                {
                    Assert.AreEqual(0, world.LastGeneratedBossMonsterCount,
                        $"Non-boss floor {level} should have zero boss markers");
                }

                Assert.IsTrue(world.PitBounds.Width >= GameConfig.PitRectWidth,
                    $"Level {level} should maintain a valid pit width");
            }

            Assert.AreEqual(5, bossFloorIndex, "Progression should encounter exactly 5 boss floors");
        }
    }
}
