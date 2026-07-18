using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class FarmTaskCoordinatorTests
    {
        private TileStateService _tileState;
        private BuildingService _buildingService;
        private FarmTaskCoordinator _coordinator;

        [TestInitialize]
        public void Setup()
        {
            _tileState = new TileStateService();
            _buildingService = new BuildingService();
            _coordinator = new FarmTaskCoordinator(_tileState, _buildingService, 240, 12);
        }

        [TestCleanup]
        public void Cleanup() => _coordinator.Detach();

        [TestMethod]
        public void MarkingTile_EnqueuesAction()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.ReadyToTill);

            Assert.AreEqual(1, _coordinator.PendingActionCount);
            Assert.IsTrue(_coordinator.TryClaimAction(out var action));
            Assert.AreEqual(FarmActionType.Till, action.Type);
            Assert.AreEqual(new Point(125, 5), action.TargetTile);
        }

        [TestMethod]
        public void MarkingTileTwice_EnqueuesOnce()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);

            Assert.AreEqual(1, _coordinator.PendingActionCount);
        }

        [TestMethod]
        public void UnmarkingQueuedTile_DropsActionOnClaim()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            _tileState.ClearFlag(tile, TileStateFlag.ReadyToTill);

            Assert.IsFalse(_coordinator.TryClaimAction(out _));
        }

        [TestMethod]
        public void UnmarkedThenRemarkedTile_EnqueuesAgain()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            _tileState.ClearFlag(tile, TileStateFlag.ReadyToTill);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);

            Assert.IsTrue(_coordinator.TryClaimAction(out var action));
            Assert.AreEqual(tile, action.TargetTile);
        }

        [TestMethod]
        public void ClaimedTile_IsNotReEnqueuedWhenMarkedAgain()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            Assert.IsTrue(_coordinator.TryClaimAction(out _));

            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);   // no transition — flag already set

            Assert.AreEqual(0, _coordinator.PendingActionCount);
        }

        [TestMethod]
        public void CompleteAction_BeforeFlagClear_KeepsTileOutOfQueue()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            Assert.IsTrue(_coordinator.TryClaimAction(out var action));

            // Mirrors the till sequence: CompleteAction first, then the flag flip fires events
            _coordinator.CompleteAction(in action);
            _tileState.ClearFlag(tile, TileStateFlag.ReadyToTill);
            _tileState.SetFlag(tile, TileStateFlag.Tilled);

            Assert.AreEqual(0, _coordinator.PendingActionCount);
            Assert.IsFalse(_coordinator.TryClaimAction(out _));
        }

        [TestMethod]
        public void ReleaseAction_ReturnsActionToFrontOfQueue()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(126, 5), TileStateFlag.ReadyToTill);
            Assert.IsTrue(_coordinator.TryClaimAction(out var first));

            _coordinator.ReleaseAction(in first);

            Assert.IsTrue(_coordinator.TryClaimAction(out var reclaimed));
            Assert.AreEqual(first.TargetTile, reclaimed.TargetTile);
        }

        [TestMethod]
        public void ClaimSkipsTile_OccupiedByBuilding()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            _buildingService.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.MonsterHouse,
                TileX = 125,
                TileY = 5,
                UniqueId = _buildingService.AllocateId()
            });

            Assert.IsFalse(_coordinator.TryClaimAction(out _));
        }

        [TestMethod]
        public void ReportBlocked_RetriesWhenBuildingsChange()
        {
            var tile = new Point(125, 5);
            _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
            Assert.IsTrue(_coordinator.TryClaimAction(out var action));

            _coordinator.ReportBlocked(in action);
            Assert.AreEqual(0, _coordinator.PendingActionCount);

            _buildingService.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = 200,
                TileY = 5,
                UniqueId = _buildingService.AllocateId()
            });

            Assert.AreEqual(1, _coordinator.PendingActionCount);
            Assert.IsTrue(_coordinator.TryClaimAction(out var retried));
            Assert.AreEqual(tile, retried.TargetTile);
        }

        [TestMethod]
        public void Rescan_PicksUpPreexistingFlags_WithoutDuplicates()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(126, 5), TileStateFlag.ReadyToTill);

            _coordinator.RescanReadyToTill();
            _coordinator.RescanReadyToTill();

            Assert.AreEqual(2, _coordinator.PendingActionCount);
        }

        [TestMethod]
        public void QueuePick_FrontAndBack_ClaimOppositeEnds()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(126, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(127, 5), TileStateFlag.ReadyToTill);

            Assert.IsTrue(_coordinator.TryClaimAction(0f, out var front));
            Assert.IsTrue(_coordinator.TryClaimAction(1f, out var back));

            Assert.AreEqual(new Point(125, 5), front.TargetTile);
            Assert.AreEqual(new Point(127, 5), back.TargetTile);
        }

        [TestMethod]
        public void QueuePick_Middle_ClaimsMiddleEntry()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(126, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(127, 5), TileStateFlag.ReadyToTill);

            Assert.IsTrue(_coordinator.TryClaimAction(0.5f, out var mid));

            Assert.AreEqual(new Point(126, 5), mid.TargetTile);
        }

        [TestMethod]
        public void QueuePick_SkipsInvalidatedEntries()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(126, 5), TileStateFlag.ReadyToTill);
            _tileState.ClearFlag(new Point(126, 5), TileStateFlag.ReadyToTill);

            Assert.IsTrue(_coordinator.TryClaimAction(1f, out var action));
            Assert.AreEqual(new Point(125, 5), action.TargetTile);
            Assert.IsFalse(_coordinator.TryClaimAction(1f, out _));
        }

        [TestMethod]
        public void TryGetNearestFieldTile_ReturnsClosestTilledOrPlannedTile()
        {
            _tileState.SetFlag(new Point(125, 5), TileStateFlag.Tilled);
            _tileState.SetFlag(new Point(200, 5), TileStateFlag.ReadyToTill);

            Assert.IsTrue(_coordinator.TryGetNearestFieldTile(new Point(130, 5), out var nearMark));
            Assert.AreEqual(new Point(125, 5), nearMark);

            Assert.IsTrue(_coordinator.TryGetNearestFieldTile(new Point(195, 5), out var nearPlan));
            Assert.AreEqual(new Point(200, 5), nearPlan);
        }

        [TestMethod]
        public void RightmostFarmObject_NoFarmObjects_IsMinusOne()
        {
            Assert.AreEqual(-1, _coordinator.RightmostFarmObjectTileX);
        }

        [TestMethod]
        public void RightmostFarmObject_BuildingPlacement_UpdatesCacheToFootprintEastEdge()
        {
            // MonsterHouse footprint spans anchor.X-2 .. anchor.X+2; AddBuilding fires
            // BuildingsChanged, which refreshes the cache — no explicit recalc needed
            _buildingService.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.MonsterHouse,
                TileX = 123,
                TileY = 2,
                UniqueId = _buildingService.AllocateId()
            });

            Assert.AreEqual(125, _coordinator.RightmostFarmObjectTileX);
        }

        [TestMethod]
        public void RightmostFarmObject_ReadyToTillDesignation_UpdatesCacheIncrementally()
        {
            _tileState.SetFlag(new Point(132, 6), TileStateFlag.ReadyToTill);
            Assert.AreEqual(132, _coordinator.RightmostFarmObjectTileX);

            // A designation further west must not lower the bound
            _tileState.SetFlag(new Point(125, 6), TileStateFlag.ReadyToTill);
            Assert.AreEqual(132, _coordinator.RightmostFarmObjectTileX);
        }

        [TestMethod]
        public void RightmostFarmObject_ClearingRightmostDesignation_ShrinksCache()
        {
            _tileState.SetFlag(new Point(125, 6), TileStateFlag.ReadyToTill);
            _tileState.SetFlag(new Point(132, 6), TileStateFlag.ReadyToTill);

            _tileState.ClearFlag(new Point(132, 6), TileStateFlag.ReadyToTill);

            Assert.AreEqual(125, _coordinator.RightmostFarmObjectTileX);
        }

        [TestMethod]
        public void RecalculateRightmostFarmObject_TakesMaxAcrossBuildingsAndTilledTiles()
        {
            // CropStorage footprint spans anchor.X-1 .. anchor.X+1 → east edge 128
            _buildingService.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = 127,
                TileY = 2,
                UniqueId = _buildingService.AllocateId()
            });
            // Directly-set Tilled flags raise no event (the load-restore case) — the explicit
            // recalc the load path performs must pick them up
            _tileState.SetFlag(new Point(135, 5), TileStateFlag.Tilled);

            _coordinator.RecalculateRightmostFarmObject();

            Assert.AreEqual(135, _coordinator.RightmostFarmObjectTileX);
        }

        [TestMethod]
        public void TryGetNearestFieldTile_FalseWhenNoFieldExists()
        {
            Assert.IsFalse(_coordinator.TryGetNearestFieldTile(new Point(130, 5), out _));
        }

        [TestMethod]
        public void QueuePick_MidQueueClaims_NeverLoseOrDuplicateTasks()
        {
            // Mirrors the in-game symptom: workers claiming mid-queue must drain every marked
            // tile exactly once — no tiles silently lost, none claimed twice.
            var marked = new System.Collections.Generic.HashSet<Point>();
            for (int i = 0; i < 30; i++)
            {
                var tile = new Point(125 + i, 5);
                _tileState.SetFlag(tile, TileStateFlag.ReadyToTill);
                marked.Add(tile);
            }

            var claimed = new System.Collections.Generic.HashSet<Point>();
            float[] picks = { 0f, 1f, 0.37f, 0.5f };
            int turn = 0;
            while (_coordinator.TryClaimAction(picks[turn++ % picks.Length], out var action))
            {
                Assert.IsTrue(claimed.Add(action.TargetTile), $"tile {action.TargetTile} claimed twice");
                _coordinator.CompleteAction(in action);
                _tileState.ClearFlag(action.TargetTile, TileStateFlag.ReadyToTill);
                _tileState.SetFlag(action.TargetTile, TileStateFlag.Tilled);
            }

            Assert.IsTrue(claimed.SetEquals(marked), "some marked tiles were never claimed");
        }

        [TestMethod]
        public void Constructor_ScansExistingFlags()
        {
            var tileState = new TileStateService();
            tileState.SetFlag(new Point(130, 4), TileStateFlag.ReadyToTill);

            var coordinator = new FarmTaskCoordinator(tileState, new BuildingService(), 240, 12);

            Assert.AreEqual(1, coordinator.PendingActionCount);
            coordinator.Detach();
        }
    }
}
