using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Regression tests for the hero-stuck-after-promotion deadlock: when the pit shrinks on a
    /// death reset (SetPitLevel to a lower width), the old pit's inner-wall collision column must
    /// be cleared. Previously SetPitLevel overestimated the new right edge (+2), skipped the
    /// shrink cleanup, and the stranded wall column sealed the pit mouth so the hero and
    /// mercenaries could never path back to the pit edge.
    /// </summary>
    [TestClass]
    public class PitShrinkStaleCollisionTests
    {
        private static (VirtualPitWidthManager manager, VirtualTiledMapService map) CreatePitManager()
        {
            var worldState = new VirtualWorldState();
            var tiledMapService = new VirtualTiledMapService(worldState);
            var manager = new VirtualPitWidthManager(tiledMapService);
            manager.Initialize();
            return (manager, tiledMapService);
        }

        private static bool HasCollision(VirtualTiledMapService map, int x, int y)
        {
            var tile = map.CurrentMap.GetLayer("Collision").GetTile(x, y);
            return tile != null && tile.Gid != 0;
        }

        // ── Edge formula parity ───────────────────────────────────────────────────

        [TestMethod]
        public void CalculateRightEdgeForLevel_KnownValues()
        {
            int baseEdge = GameConfig.PitRectX + GameConfig.PitRectWidth; // 13

            Assert.AreEqual(baseEdge, PitWidthManager.CalculateRightEdgeForLevel(1), "Levels 1-9 have no extension");
            Assert.AreEqual(baseEdge, PitWidthManager.CalculateRightEdgeForLevel(9), "Levels 1-9 have no extension");
            Assert.AreEqual(baseEdge + 2, PitWidthManager.CalculateRightEdgeForLevel(10), "Level 10 extends 2 tiles");
            Assert.AreEqual(baseEdge + 4, PitWidthManager.CalculateRightEdgeForLevel(25), "Level 25 extends 4 tiles");
            Assert.AreEqual(baseEdge + 20, PitWidthManager.CalculateRightEdgeForLevel(100), "Level 100 extends 20 tiles");
            Assert.AreEqual(baseEdge + 20, PitWidthManager.CalculateRightEdgeForLevel(150), "Expansion caps at level 100");
        }

        [TestMethod]
        public void CalculateRightEdgeForLevel_MatchesRegeneratedEdgeInEveryTier()
        {
            // The shrink cleanup in SetPitLevel relies on this prediction matching what RegeneratePitWidth
            // actually produces. The width follows the level within the cycle, so the tier never changes it.
            int[] levels = { 1, 5, 7, 10, 15, 25 };
            int[] tiers = { 1, 2, 3 };

            for (int t = 0; t < tiers.Length; t++)
            {
                for (int l = 0; l < levels.Length; l++)
                {
                    var (manager, _) = CreatePitManager();
                    manager.SetPitTier(tiers[t]);
                    manager.SetPitLevel(levels[l]);

                    Assert.AreEqual(PitWidthManager.CalculateRightEdgeForLevel(levels[l]), manager.CurrentPitRightEdge,
                        $"Predicted edge must match regenerated edge for level {levels[l]} tier {tiers[t]}");
                }
            }
        }

        // ── A new cycle starts at the base width ──────────────────────────────────

        [TestMethod]
        public void NewCycle_ResetsToBaseWidthAndClearsTheOldWall()
        {
            // Tier 1 level 25 (edge 17) wraps to tier 2 level 1: the pit is back to the base width and the
            // old inner-wall column must be gone, or it seals the pit mouth (the #333 deadlock).
            var (manager, map) = CreatePitManager();

            manager.SetPitLevel(25);
            Assert.AreEqual(17, manager.CurrentPitRightEdge, "Tier 1 level 25 pit right edge");
            Assert.IsTrue(HasCollision(map, 16, 5), "Level 25 pit should have its inner wall at x=16");

            manager.IncrementPitTier(1);
            manager.SetPitLevel(1);
            Assert.AreEqual(GameConfig.PitRectX + GameConfig.PitRectWidth, manager.CurrentPitRightEdge,
                "Tier 2 level 1 pit is back to the base width");

            for (int x = 14; x <= 17; x++)
            {
                for (int y = 1; y <= 11; y++)
                {
                    Assert.IsFalse(HasCollision(map, x, y), $"Stale extension collision must be cleared at ({x},{y})");
                }
            }
            Assert.IsTrue(HasCollision(map, 12, 5), "Base pit inner wall at x=12 must survive the reset");
        }

        [TestMethod]
        public void TierOneDeathReset_ClearsExtensionEntirely()
        {
            // Tier 1: level 35 pit (edge 19) resets to level 1 (no extension, edge 13).
            var (manager, map) = CreatePitManager();

            manager.SetPitLevel(35);
            Assert.AreEqual(19, manager.CurrentPitRightEdge, "Tier 1 level 35 pit right edge");
            Assert.IsTrue(HasCollision(map, 18, 5), "Level 35 pit should have its inner wall at x=18");

            manager.SetPitLevel(1);
            Assert.AreEqual(GameConfig.PitRectX + GameConfig.PitRectWidth, manager.CurrentPitRightEdge,
                "Tier 1 level 1 pit right edge is the base pit boundary");

            // Entire extension region must be free of collision
            for (int x = 14; x <= 19; x++)
            {
                for (int y = 1; y <= 11; y++)
                {
                    Assert.IsFalse(HasCollision(map, x, y), $"Stale extension collision must be cleared at ({x},{y})");
                }
            }

            // Sanity: the base pit's own inner wall at x=12 survives the restore
            Assert.IsTrue(HasCollision(map, 12, 5), "Base pit inner wall at x=12 must survive the reset");
        }

        [TestMethod]
        public void RepeatedGrowAndShrink_NeverLeavesStaleCollision()
        {
            // Cycle the pit through several grow/shrink transitions and verify no stale
            // collision ever survives past the current right edge.
            var (manager, map) = CreatePitManager();
            manager.SetPitTier(2);

            int[] levelSequence = { 7, 1, 15, 1, 25, 7, 1 };
            for (int i = 0; i < levelSequence.Length; i++)
            {
                manager.SetPitLevel(levelSequence[i]);
                int edge = manager.CurrentPitRightEdge;

                for (int x = edge + 1; x <= 33; x++)
                {
                    for (int y = 1; y <= 11; y++)
                    {
                        Assert.IsFalse(HasCollision(map, x, y),
                            $"Stale collision at ({x},{y}) after SetPitLevel({levelSequence[i]}) with edge {edge}");
                    }
                }
            }
        }
    }
}
