using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Regression tests for the harvest-delivery teleport bug: when the path from a crop tile to a
    /// Crop Storage approach tile fails, FarmingMonsterStateMachine skips the carry walk and
    /// teleport-deposits instead. Root cause was NotifyBuildingMoved not firing BuildingsChanged,
    /// leaving the farm pathfinder with walls at the building's pre-move position.
    /// </summary>
    [TestClass]
    public class StorageDoorPathTests
    {
        private const int MapW = 240;
        private const int MapH = 12;
        private const int MinX = 120; // TillMinTileX in BuildingModeOverlay

        private static BuildingService NewServiceWith(params (BuildingType type, int x, int y)[] buildings)
        {
            var service = new BuildingService();
            foreach (var b in buildings)
                service.AddBuilding(new PlacedBuilding
                {
                    Type = b.type,
                    TileX = b.x,
                    TileY = b.y,
                    UniqueId = service.AllocateId(),
                });
            return service;
        }

        private static bool DoorReachable(FarmPathfinder pf, Point start, Point door)
        {
            if (!pf.IsPassable(door))
                return false;
            if (start == door)
                return true;
            return pf.Search(start, door) != null;
        }

        /// <summary>
        /// Moving a storage must rebuild the coordinator's pathfinder walls. Before the fix, moving
        /// a storage up one row left stale walls at the old rows, and the new door tile — which
        /// coincides with the old bottom wall row — was impassable, so every delivery teleported.
        /// </summary>
        [TestMethod]
        public void MovingStorageUpOneRow_DoorStaysReachable()
        {
            int x = 160;
            var service = NewServiceWith((BuildingType.CropStorage, x, 3));
            var coordinator = new FarmTaskCoordinator(new TileStateService(), service, MapW, MapH);
            try
            {
                var storage = service.GetAll()[0];
                var cropTile = new Point(x, 9);

                // Sanity: reachable at the original position (door row 5)
                var door = BuildingConfig.GetDoorTile(BuildingType.CropStorage, new Point(x, 3));
                Assert.IsTrue(DoorReachable(coordinator.Pathfinder, cropTile, door),
                    "door unreachable before move");

                // Move up one row (the placement UI path: mutate tile, then notify)
                storage.TileY = 2;
                service.NotifyBuildingMoved(storage);

                door = BuildingConfig.GetDoorTile(BuildingType.CropStorage, new Point(x, 2));
                Assert.IsTrue(coordinator.Pathfinder.IsPassable(door),
                    "new door tile blocked by stale wall from the pre-move position");
                Assert.IsTrue(DoorReachable(coordinator.Pathfinder, cropTile, door),
                    "door unreachable after move");

                // The rows the building vacated must be walkable again
                Assert.IsTrue(coordinator.Pathfinder.IsPassable(new Point(x, 4)),
                    "stale wall left behind at the old position");
            }
            finally
            {
                coordinator.Detach();
            }
        }

        /// <summary>
        /// Every storage anchor whose approach tile is on the map must be deliverable from anywhere
        /// a worker can stand. (Anchors whose approach tile falls off the bottom edge are now
        /// rejected by placement validation.)
        /// </summary>
        [TestMethod]
        public void SingleStorage_AllLegalAnchors_DoorReachableFromField()
        {
            var failures = new List<string>();

            // Legal anchors: footprint (-1..1, -2..1) keeps y >= 2; approach tile (y+2) on the
            // 12-row map keeps y <= 9.
            for (int ay = 2; ay <= MapH - 3; ay++)
            {
                for (int ax = MinX + 1; ax <= MapW - 2; ax += 20)
                {
                    var pf = new FarmPathfinder(MapW, MapH);
                    pf.RebuildWalls(NewServiceWith((BuildingType.CropStorage, ax, ay)));
                    var door = BuildingConfig.GetDoorTile(BuildingType.CropStorage, new Point(ax, ay));

                    for (int sy = 0; sy < MapH; sy++)
                    {
                        for (int sx = MinX; sx < MapW; sx += 15)
                        {
                            var start = new Point(sx, sy);
                            if (!pf.IsPassable(start))
                                continue;
                            if (!DoorReachable(pf, start, door))
                                failures.Add($"anchor=({ax},{ay}) door=({door.X},{door.Y}) start=({sx},{sy})");
                        }
                    }
                }
            }

            Assert.AreEqual(0, failures.Count,
                "Unreachable storage doors:\n" + string.Join("\n", failures));
        }

        /// <summary>Topmost storage plus a monster house in any legal nearby spot stays deliverable.</summary>
        [TestMethod]
        public void StoragePlusMonsterHouse_AllRelativeOffsets_DoorReachable()
        {
            var failures = new List<string>();
            int storageX = 160, storageY = 2;

            for (int hy = 2; hy <= MapH - 3; hy++)
            {
                for (int hx = storageX - 12; hx <= storageX + 12; hx++)
                {
                    if (hx - 2 < MinX)
                        continue;
                    // Skip physically overlapping footprints (placement UI forbids those)
                    if (System.Math.Abs(hx - storageX) <= 3 && System.Math.Abs(hy - storageY) <= 4)
                        continue;

                    var pf = new FarmPathfinder(MapW, MapH);
                    pf.RebuildWalls(NewServiceWith(
                        (BuildingType.CropStorage, storageX, storageY),
                        (BuildingType.MonsterHouse, hx, hy)));
                    var door = BuildingConfig.GetDoorTile(BuildingType.CropStorage, new Point(storageX, storageY));

                    var start = new Point(storageX, 9);
                    if (!pf.IsPassable(start))
                        continue;
                    if (!DoorReachable(pf, start, door))
                        failures.Add($"storage=({storageX},{storageY}) house=({hx},{hy}) door=({door.X},{door.Y})");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "Unreachable storage doors with house layouts:\n" + string.Join("\n", failures));
        }
    }
}
