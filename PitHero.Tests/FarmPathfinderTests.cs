using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class FarmPathfinderTests
    {
        [TestMethod]
        public void MonsterHouse_BottomTwoRowsAreSolid()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.MonsterHouse,
                TileX = 130,
                TileY = 5,
                UniqueId = 1
            });
            var pathfinder = new FarmPathfinder(240, 12);
            pathfinder.RebuildWalls(buildings);

            // 5x5 footprint anchored at (130,5): bottom two rows are dy=+1 and dy=+2
            int solidCount = 0;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (!pathfinder.IsPassable(new Point(130 + dx, 5 + dy)))
                        solidCount++;

            Assert.AreEqual(10, solidCount);
            Assert.IsTrue(pathfinder.IsPassable(new Point(130, 5)));        // anchor row walkable
            Assert.IsFalse(pathfinder.IsPassable(new Point(130, 6)));       // dy=+1 solid
            Assert.IsFalse(pathfinder.IsPassable(new Point(130, 7)));       // dy=+2 solid (door row)
            Assert.IsFalse(pathfinder.IsPassable(new Point(128, 7)));
            Assert.IsFalse(pathfinder.IsPassable(new Point(132, 6)));
        }

        [TestMethod]
        public void CropStorage_BottomTwoRowsAreSolid()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = 140,
                TileY = 5,
                UniqueId = 1
            });
            var pathfinder = new FarmPathfinder(240, 12);
            pathfinder.RebuildWalls(buildings);

            // 3x4 footprint (dx -1..1, dy -2..+1): bottom two rows are dy=0 and dy=+1
            int solidCount = 0;
            for (int dy = -2; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!pathfinder.IsPassable(new Point(140 + dx, 5 + dy)))
                        solidCount++;

            Assert.AreEqual(6, solidCount);
            Assert.IsTrue(pathfinder.IsPassable(new Point(140, 4)));    // dy=-1 walkable
            Assert.IsFalse(pathfinder.IsPassable(new Point(140, 5)));   // dy=0 solid
            Assert.IsFalse(pathfinder.IsPassable(new Point(140, 6)));   // dy=+1 solid
        }

        [TestMethod]
        public void OutOfBounds_IsNotPassable()
        {
            var pathfinder = new FarmPathfinder(240, 12);

            Assert.IsFalse(pathfinder.IsPassable(new Point(-1, 5)));
            Assert.IsFalse(pathfinder.IsPassable(new Point(240, 5)));
            Assert.IsFalse(pathfinder.IsPassable(new Point(5, 12)));
            Assert.IsTrue(pathfinder.IsPassable(new Point(0, 0)));
        }

        [TestMethod]
        public void Search_FindsPathAroundWalls()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.MonsterHouse,
                TileX = 130,
                TileY = 5,
                UniqueId = 1
            });
            var pathfinder = new FarmPathfinder(240, 12);
            pathfinder.RebuildWalls(buildings);

            // Walk from left of the house to right of the house, past the solid bottom rows
            var path = pathfinder.Search(new Point(126, 7), new Point(134, 7));

            Assert.IsNotNull(path);
            for (int i = 0; i < path.Count; i++)
                Assert.IsTrue(pathfinder.IsPassable(path[i]), $"path goes through wall at {path[i]}");
        }

        [TestMethod]
        public void SmoothPath_OpenField_CollapsesToSingleWaypoint()
        {
            var pathfinder = new FarmPathfinder(240, 12);
            var start = new Point(120, 2);
            var goal = new Point(130, 9);
            var path = pathfinder.Search(start, goal);
            Assert.IsNotNull(path);

            var smoothed = pathfinder.SmoothPath(start, path);

            Assert.AreEqual(1, smoothed.Count);
            Assert.AreEqual(goal, smoothed[0]);
        }

        [TestMethod]
        public void SmoothPath_AroundBuilding_KeepsIntermediateWaypoint()
        {
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.MonsterHouse,
                TileX = 130,
                TileY = 5,
                UniqueId = 1
            });
            var pathfinder = new FarmPathfinder(240, 12);
            pathfinder.RebuildWalls(buildings);

            var start = new Point(126, 6);
            var goal = new Point(134, 6);
            var path = pathfinder.Search(start, goal);
            Assert.IsNotNull(path);

            var smoothed = pathfinder.SmoothPath(start, path);

            // Direct line crosses the solid bottom rows, so smoothing must keep at least one
            // intermediate waypoint and end at the goal.
            Assert.IsTrue(smoothed.Count >= 2);
            Assert.AreEqual(goal, smoothed[smoothed.Count - 1]);
        }
    }
}
