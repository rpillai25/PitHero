using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero;
using PitHero.Config;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Headless verification of the kitchen/tavern dining walk routes against the REAL surface
    /// map. Parses PitHero.tmx's Collision layer as plain XML/CSV (no FNA content pipeline) and
    /// seeds a FarmPathfinder the same way MainGameScene seeds the live coordinators, then
    /// asserts every leg of the worker flow — house exit → posts → stove pickup → seat tables →
    /// sink → crop storage — is reachable without crossing a wall.
    /// </summary>
    [TestClass]
    public class KitchenFlowPathTests
    {
        private static int _mapWidth;
        private static int _mapHeight;
        private static readonly List<Point> _staticWalls = new List<Point>(512);

        // New-game farm layout (SetupNewGameFarmContent)
        private static readonly Point HouseAnchor = new Point(
            GameConfig.NewGameMonsterHouseAnchorTileX, GameConfig.NewGameMonsterHouseAnchorTileY);
        private static readonly Point StorageAnchor = new Point(
            GameConfig.NewGameCropStorageAnchorTileX, GameConfig.NewGameCropStorageAnchorTileY);
        // KitchenMonsterStateMachine: door = anchor+2, exit = anchor+3 (first walkable tile)
        private static Point HouseExitTile => new Point(HouseAnchor.X, HouseAnchor.Y + 3);

        // All 12 tavern seats (TavernSeatConfig registration; spec §2 of issue #319)
        private static readonly Point[] AllSeats =
        {
            new Point(93, 6), new Point(92, 7), new Point(94, 7),               // party table (93,7)
            new Point(97, 6), new Point(96, 7), new Point(98, 7),               // right lower table (97,7)
            new Point(93, 4), new Point(92, 3), new Point(94, 3),               // left upper table (93,3)
            new Point(97, 4), new Point(96, 3), new Point(98, 3),               // right upper table (97,3)
        };

        [ClassInitialize]
        public static void LoadCollisionLayer(TestContext _)
        {
            var tmxPath = FindTmxPath();
            var doc = XDocument.Load(tmxPath);
            var map = doc.Root;
            _mapWidth = int.Parse(map.Attribute("width").Value);
            _mapHeight = int.Parse(map.Attribute("height").Value);

            XElement collisionLayer = null;
            foreach (var layer in map.Elements("layer"))
            {
                if ((string)layer.Attribute("name") == "Collision")
                {
                    collisionLayer = layer;
                    break;
                }
            }
            Assert.IsNotNull(collisionLayer, "PitHero.tmx has no Collision layer");

            var csv = collisionLayer.Element("data").Value;
            var cells = csv.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(_mapWidth * _mapHeight, cells.Length, "Collision CSV cell count mismatch");

            _staticWalls.Clear();
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Trim() != "0")
                    _staticWalls.Add(new Point(i % _mapWidth, i / _mapWidth));
            }
            Assert.IsTrue(_staticWalls.Count > 0, "Collision layer parsed as empty — parsing bug");
        }

        private static string FindTmxPath()
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "PitHero", "Content", "Tilemaps", "PitHero.tmx");
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("Could not locate PitHero.tmx above " + AppContext.BaseDirectory);
        }

        /// <summary>Builds a pathfinder seeded exactly like the live kitchen coordinator's.</summary>
        private static FarmPathfinder CreateMapPathfinder(bool withNewGameBuildings)
        {
            var pathfinder = new FarmPathfinder(_mapWidth, _mapHeight);
            for (int i = 0; i < _staticWalls.Count; i++)
                pathfinder.AddStaticWall(_staticWalls[i]);

            if (withNewGameBuildings)
            {
                var buildings = new BuildingService();
                buildings.AddBuilding(new PlacedBuilding
                {
                    Type = BuildingType.MonsterHouse,
                    TileX = HouseAnchor.X,
                    TileY = HouseAnchor.Y,
                    UniqueId = 1
                });
                buildings.AddBuilding(new PlacedBuilding
                {
                    Type = BuildingType.CropStorage,
                    TileX = StorageAnchor.X,
                    TileY = StorageAnchor.Y,
                    UniqueId = 2
                });
                pathfinder.RebuildWalls(buildings);
            }
            return pathfinder;
        }

        private static void AssertRouteClear(FarmPathfinder pathfinder, Point start, Point goal, string leg)
        {
            Assert.IsTrue(pathfinder.IsPassable(start), $"{leg}: start {start} is a wall");
            Assert.IsTrue(pathfinder.IsPassable(goal), $"{leg}: goal {goal} is a wall");
            if (start == goal)
                return;
            var path = pathfinder.Search(start, goal);
            Assert.IsNotNull(path, $"{leg}: no path {start} → {goal}");
            for (int i = 0; i < path.Count; i++)
                Assert.IsTrue(pathfinder.IsPassable(path[i]), $"{leg}: path crosses wall at {path[i]}");

            var smoothed = pathfinder.SmoothPath(start, path);
            Assert.IsTrue(smoothed.Count > 0, $"{leg}: smoothed path empty");
            Assert.AreEqual(goal, smoothed[smoothed.Count - 1], $"{leg}: smoothed path does not end at goal");
        }

        [TestMethod]
        public void KitchenTiles_MatchExpectedMapTopology()
        {
            var pathfinder = CreateMapPathfinder(withNewGameBuildings: false);

            // Worker stand tiles are open floor
            for (int stove = 0; stove < GameConfig.MaxKitchenCooks; stove++)
                Assert.IsTrue(pathfinder.IsPassable(KitchenTaskCoordinator.GetStoveTile(stove)),
                    $"stove {stove} stand tile is a wall");
            Assert.IsTrue(pathfinder.IsPassable(KitchenTaskCoordinator.SinkTile), "sink tile is a wall");
            Assert.IsTrue(pathfinder.IsPassable(
                new Point(GameConfig.KitchenSinkTileX + 1, GameConfig.KitchenSinkTileY)),
                "runner post tile is a wall");

            // Plate tiles sit ON the counter (solid) — servers must resolve a passable neighbor
            for (int stove = 0; stove < GameConfig.MaxKitchenCooks; stove++)
            {
                var plateTile = KitchenTaskCoordinator.GetPlateTile(stove);
                Assert.IsFalse(pathfinder.IsPassable(plateTile),
                    $"plate tile {plateTile} unexpectedly walkable — counter topology changed");
                Assert.IsTrue(pathfinder.TryFindPassableNeighbor(plateTile, KitchenTaskCoordinator.SinkTile, out _),
                    $"plate tile {plateTile} has no passable neighbor");
            }

            // The kitchen room is actually enclosed (regression guard for the wall seeding itself)
            Assert.IsFalse(pathfinder.IsPassable(new Point(81, 2)), "kitchen west wall missing");
            Assert.IsFalse(pathfinder.IsPassable(new Point(88, 2)), "kitchen east wall missing");
        }

        [TestMethod]
        public void WorkerPosts_ReachableFromNewGameHouseExit()
        {
            var pathfinder = CreateMapPathfinder(withNewGameBuildings: true);

            for (int stove = 0; stove < GameConfig.MaxKitchenCooks; stove++)
                AssertRouteClear(pathfinder, HouseExitTile, KitchenTaskCoordinator.GetStoveTile(stove),
                    $"house exit → stove {stove}");
            AssertRouteClear(pathfinder, HouseExitTile, KitchenTaskCoordinator.SinkTile, "house exit → sink");
            AssertRouteClear(pathfinder, HouseExitTile,
                new Point(GameConfig.KitchenSinkTileX + 1, GameConfig.KitchenSinkTileY),
                "house exit → runner post");

            // And the walk home again
            AssertRouteClear(pathfinder, KitchenTaskCoordinator.SinkTile, HouseExitTile, "sink → house exit");
        }

        [TestMethod]
        public void Server_CanDeliverToEverySeatTable()
        {
            var pathfinder = CreateMapPathfinder(withNewGameBuildings: true);
            var sink = KitchenTaskCoordinator.SinkTile;

            for (int i = 0; i < AllSeats.Length; i++)
            {
                var seat = AllSeats[i];
                Assert.IsTrue(TavernSeatConfig.IsSeatTile(seat), $"seat {seat} not registered in TavernSeatConfig");
                Assert.IsTrue(pathfinder.IsPassable(seat), $"seat tile {seat} is a wall");

                Assert.IsTrue(TavernSeatConfig.TryGetPlateWorldPosition(seat, out var platePos),
                    $"no plate position for seat {seat}");
                var tableTile = new Point(
                    (int)(platePos.X / GameConfig.TileSize), (int)(platePos.Y / GameConfig.TileSize));
                Assert.AreEqual(TavernSeatConfig.GetTableTile(seat), tableTile,
                    $"plate world pos for seat {seat} does not land on its table tile");
                Assert.IsFalse(pathfinder.IsPassable(tableTile),
                    $"table tile {tableTile} unexpectedly walkable — table topology changed");

                // The server paths to the table's nearest passable neighbor (TrySetPathToTileOrNeighbor)
                Assert.IsTrue(pathfinder.TryFindPassableNeighbor(tableTile, sink, out var standTile),
                    $"table {tableTile} has no passable neighbor");
                AssertRouteClear(pathfinder, sink, standTile, $"sink → table {tableTile} (seat {seat})");
            }
        }

        [TestMethod]
        public void Server_CanReachEveryStovePickup()
        {
            var pathfinder = CreateMapPathfinder(withNewGameBuildings: true);
            var sink = KitchenTaskCoordinator.SinkTile;

            for (int stove = 0; stove < GameConfig.MaxKitchenCooks; stove++)
            {
                var plateTile = KitchenTaskCoordinator.GetPlateTile(stove);
                Assert.IsTrue(pathfinder.TryFindPassableNeighbor(plateTile, sink, out var standTile),
                    $"plate tile {plateTile} has no passable neighbor");
                AssertRouteClear(pathfinder, sink, standTile, $"sink → stove {stove} pickup");
            }
        }

        [TestMethod]
        public void Runner_CanReachStorageDoorAndReturn()
        {
            var pathfinder = CreateMapPathfinder(withNewGameBuildings: true);
            var sink = KitchenTaskCoordinator.SinkTile;
            var storageDoor = BuildingConfig.GetDoorTile(BuildingType.CropStorage, StorageAnchor);

            AssertRouteClear(pathfinder, sink, storageDoor, "sink → crop storage door");
            AssertRouteClear(pathfinder, storageDoor, sink, "crop storage door → sink");
        }

        [TestMethod]
        public void RebuildWalls_PreservesStaticMapWalls()
        {
            var pathfinder = CreateMapPathfinder(withNewGameBuildings: false);
            Assert.IsFalse(pathfinder.IsPassable(new Point(81, 2)));

            // Building changes must never erase static map collision
            var buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.MonsterHouse,
                TileX = HouseAnchor.X,
                TileY = HouseAnchor.Y,
                UniqueId = 1
            });
            pathfinder.RebuildWalls(buildings);
            pathfinder.RebuildWalls(buildings);

            Assert.IsFalse(pathfinder.IsPassable(new Point(81, 2)), "static wall lost after RebuildWalls");
            Assert.IsFalse(pathfinder.IsPassable(new Point(HouseAnchor.X, HouseAnchor.Y + 2)),
                "building wall missing after RebuildWalls");
        }
    }
}
