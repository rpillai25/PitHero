using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Regression tests for dropped-crop pickup. Drops used to be created at the worker's
    /// inside-the-building deposit position, where TryClaimFromQueue discards them forever
    /// (tile occupied by the building footprint and impassable to A*). The coordinator now
    /// relocates such drops to the nearest reachable tile when populating the pickup queue.
    /// </summary>
    [TestClass]
    public class DroppedCropPickupTests
    {
        private const int MapW = 240;
        private const int MapH = 12;

        private TileStateService _tileState;
        private BuildingService _buildingService;
        private DroppedCropService _drops;
        private FarmTaskCoordinator _coordinator;

        [TestInitialize]
        public void Setup()
        {
            _tileState = new TileStateService();
            _buildingService = new BuildingService();
            _drops = new DroppedCropService();
            _coordinator = new FarmTaskCoordinator(_tileState, _buildingService, MapW, MapH);
            _coordinator.SetDroppedCropService(_drops);
        }

        [TestCleanup]
        public void Cleanup() => _coordinator.Detach();

        private PlacedBuilding PlaceStorage(int x, int y)
        {
            var b = new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = x,
                TileY = y,
                UniqueId = _buildingService.AllocateId(),
            };
            _buildingService.AddBuilding(b);
            return b;
        }

        /// <summary>
        /// A drop on the storage's walk-in tile (inside the footprint, an A* wall) must be
        /// relocated to a reachable tile and become claimable — this is the exact state older
        /// saves are in when a full storage forced a worker to drop its harvest.
        /// </summary>
        [TestMethod]
        public void DropInsideStorageFootprint_IsRelocatedAndClaimed()
        {
            var storage = PlaceStorage(160, 5); // footprint rows 3-6, walls rows 5-6, door (160,7)
            var badTile = new Point(160, 6);    // walk-in position: one tile north of the door
            _drops.Drop(CropType.Corn, 3, badTile);

            Assert.IsTrue(_coordinator.TryClaimAction(out var action), "pickup was not claimable");
            Assert.AreEqual(FarmActionType.PickupDrop, action.Type);

            var tile = action.TargetTile;
            Assert.AreNotEqual(badTile, tile, "drop was not relocated off the footprint tile");
            Assert.IsTrue(_coordinator.Pathfinder.IsPassable(tile), "relocated onto an impassable tile");
            Assert.IsFalse(_buildingService.IsTileOccupied(tile.X, tile.Y), "relocated onto an occupied tile");

            Assert.IsFalse(_drops.TryGetAt(badTile, out _), "drop still registered at the old tile");
            Assert.IsTrue(_drops.TryGetAt(tile, out var drop), "drop not registered at the claimed tile");
            Assert.AreEqual(CropType.Corn, drop.Type);
            Assert.AreEqual(3, drop.Count);
        }

        [TestMethod]
        public void DropOnOpenTile_IsClaimedInPlace()
        {
            PlaceStorage(160, 5);
            var tile = new Point(170, 9);
            _drops.Drop(CropType.Wheat, 2, tile);

            Assert.IsTrue(_coordinator.TryClaimAction(out var action));
            Assert.AreEqual(FarmActionType.PickupDrop, action.Type);
            Assert.AreEqual(tile, action.TargetTile);
        }

        [TestMethod]
        public void MoveDrop_UpdatesTileLookup()
        {
            var oldTile = new Point(150, 5);
            var newTile = new Point(151, 5);
            _drops.Drop(CropType.Tomato, 1, oldTile);
            Assert.IsTrue(_drops.TryGetAt(oldTile, out var drop));

            _drops.MoveDrop(drop, newTile);

            Assert.IsFalse(_drops.TryGetAt(oldTile, out _));
            Assert.IsTrue(_drops.TryGetAt(newTile, out var moved));
            Assert.AreEqual(CropType.Tomato, moved.Type);
        }
    }
}
