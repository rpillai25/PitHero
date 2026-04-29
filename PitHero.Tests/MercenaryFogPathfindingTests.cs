using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the mercenary fog-of-war pathfinding restriction feature.
    /// Verifies that IsFogOfWarTile correctly reports fog state via VirtualTiledMapService
    /// and that fog clears are reflected correctly.
    /// </summary>
    [TestClass]
    public class MercenaryFogPathfindingTests
    {
        private VirtualWorldState _worldState;
        private VirtualTiledMapService _tiledMapService;

        [TestInitialize]
        public void Setup()
        {
            _worldState = new VirtualWorldState();
            _tiledMapService = new VirtualTiledMapService(_worldState);
        }

        [TestMethod]
        public void IsFogOfWarTile_ReturnsFalse_WhenTileHasNoFog()
        {
            // Tile (0,0) is never assigned a fog tile by InitializeBasicPatterns
            bool result = _tiledMapService.IsFogOfWarTile(0, 0);
            Assert.IsFalse(result, "Tile at (0,0) should have no fog");
        }

        [TestMethod]
        public void IsFogOfWarTile_ReturnsTrue_WhenFogTileExists()
        {
            // InitializeBasicPatterns places fog at x=2..12, y=3..9
            bool result = _tiledMapService.IsFogOfWarTile(5, 5);
            Assert.IsTrue(result, "Tile at (5,5) should have fog after initialization");
        }

        [TestMethod]
        public void IsFogOfWarTile_ReturnsFalse_AfterFogCleared()
        {
            Assert.IsTrue(_tiledMapService.IsFogOfWarTile(5, 5), "Tile (5,5) should start covered");

            _tiledMapService.ClearFogOfWarTile(5, 5);

            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(5, 5),
                "Tile (5,5) should not be fog-covered after ClearFogOfWarTile");
        }

        [TestMethod]
        public void IsFogOfWarTile_ReturnsFalse_ForNegativeCoordinates()
        {
            bool result = _tiledMapService.IsFogOfWarTile(-1, -1);
            Assert.IsFalse(result, "Negative-coordinate tile should return false without throwing");
        }

        [TestMethod]
        public void IsFogOfWarTile_ReturnsFalse_ForLargeOutOfBoundsCoordinates()
        {
            bool result = _tiledMapService.IsFogOfWarTile(9999, 9999);
            Assert.IsFalse(result, "Far out-of-bounds tile should return false without throwing");
        }

        [TestMethod]
        public void IsFogOfWarTile_ReturnsCorrectCount_ForInitializedFogRegion()
        {
            // InitializeBasicPatterns places fog at x=2..12, y=3..9 => 11 * 7 = 77 tiles
            int fogCount = 0;
            for (int x = 2; x <= 12; x++)
            {
                for (int y = 3; y <= 9; y++)
                {
                    if (_tiledMapService.IsFogOfWarTile(x, y))
                        fogCount++;
                }
            }
            Assert.AreEqual(77, fogCount, "Expected 77 fog tiles in the initialized region (11 cols * 7 rows)");
        }

        [TestMethod]
        public void IsFogOfWarTile_AllTilesCleared_AfterClearAroundTile()
        {
            // ClearFogOfWarAroundTile with default radius 1 clears a 3x3 area around the center
            var heroComp = new PitHero.ECS.Components.HeroComponent();
            _tiledMapService.ClearFogOfWarAroundTile(5, 5, heroComp);

            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(5, 5), "Center (5,5) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(4, 4), "UL (4,4) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(5, 4), "U (5,4) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(6, 4), "UR (6,4) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(4, 5), "L (4,5) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(6, 5), "R (6,5) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(4, 6), "LL (4,6) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(5, 6), "LB (5,6) should be cleared");
            Assert.IsFalse(_tiledMapService.IsFogOfWarTile(6, 6), "LR (6,6) should be cleared");
        }
    }
}
