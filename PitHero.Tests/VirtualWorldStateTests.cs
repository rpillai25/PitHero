using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests VirtualWorldState cave parity behavior for Phase 4.
    /// </summary>
    [TestClass]
    public class VirtualWorldStateTests
    {
        /// <summary>
        /// Validates cave pit generation tracks entities and boss markers correctly.
        /// </summary>
        [TestMethod]
        public void VirtualWorld_CavePit_TracksAllEntities()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            context.PitGenerator.RegenerateForLevel(10);

            var entities = world.GetEntityPositions();
            Assert.IsTrue(entities.ContainsKey("WizardOrb"), "Wizard orb should be tracked");
            Assert.IsTrue(entities.ContainsKey("Obstacles"), "Obstacles should be tracked");
            Assert.IsTrue(entities.ContainsKey("Treasures"), "Treasures should be tracked");
            Assert.IsTrue(entities.ContainsKey("BossMonsters"), "Boss monster should be tracked on level 10");

            Assert.AreEqual(1, world.LastGeneratedBossMonsterCount, "Boss count should be one on level 10");
            Assert.IsTrue(world.LastGeneratedMonsterTypes.Count > 0, "Monster type tracking should not be empty");
            Assert.IsTrue(world.LastGeneratedEquipmentTypes.Count > 0, "Equipment type tracking should not be empty");
            Assert.IsTrue(world.LastGeneratedTreasureLevels.Count > 0, "Treasure level tracking should not be empty");
        }

        /// <summary>
        /// Validates cave treasure level tracking stays in level 1-2 band.
        /// </summary>
        [TestMethod]
        public void VirtualWorld_CaveTreasure_TracksTreasureLevels()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            int[] sampleLevels = { 1, 6, 11, 15, 20, 25 };

            for (int levelIndex = 0; levelIndex < sampleLevels.Length; levelIndex++)
            {
                int level = sampleLevels[levelIndex];
                context.PitGenerator.RegenerateForLevel(level);

                for (int itemIndex = 0; itemIndex < world.LastGeneratedTreasureLevels.Count; itemIndex++)
                {
                    int treasureLevel = world.LastGeneratedTreasureLevels[itemIndex];
                    Assert.IsTrue(treasureLevel == 1 || treasureLevel == 2,
                        $"Cave level {level} generated invalid treasure level {treasureLevel}");
                }
            }
        }

        /// <summary>
        /// Validates virtual cave pit bounds follow dynamic width progression.
        /// </summary>
        [TestMethod]
        public void VirtualWorld_CaveBounds_MatchDynamicWidth()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            int[] sampleLevels = { 1, 10, 20, 25 };

            for (int levelIndex = 0; levelIndex < sampleLevels.Length; levelIndex++)
            {
                int level = sampleLevels[levelIndex];
                context.PitGenerator.RegenerateForLevel(level);

                Assert.IsTrue(world.PitBounds.Width >= GameConfig.PitRectWidth,
                    $"Pit bounds width should remain valid at level {level}");
            }
        }
    }
}
