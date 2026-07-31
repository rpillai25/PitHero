using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Services.AutoJob;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for AutoJobAssignmentService and the demand evaluators. Services are constructed
    /// directly (not via Core.Services) for headless safety; ReassessNow() is called directly to
    /// bypass the in-game-time cadence gate, which never elapses in a headless context.
    /// </summary>
    [TestClass]
    public class AutoJobAssignmentServiceTests
    {
        private static AlliedMonster Monster(string name, int fishing, int cooking, int farming,
            MonsterJob job = MonsterJob.None, string typeName = "Monster_Slime")
        {
            var m = new AlliedMonster(name, typeName, fishing, cooking, farming);
            m.Job = job;
            return m;
        }

        private static AutoJobAssignmentService CreateHeadlessService(AlliedMonsterManager roster)
        {
            // Null service dependencies: kitchen sees zero backlog, farming sees zero workload.
            return new AutoJobAssignmentService(roster,
                new KitchenJobDemandEvaluator(null, null, null),
                new FarmingJobDemandEvaluator(null, null, null));
        }

        // ── Service basics ───────────────────────────────────────────────────

        [TestMethod]
        public void Service_DisabledByDefault()
        {
            var service = CreateHeadlessService(new AlliedMonsterManager());
            Assert.IsFalse(service.Enabled, "AutoJobAssignmentService should be disabled by default");
        }

        [TestMethod]
        public void Update_WhenDisabled_IsANoOp()
        {
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("Bob", 1, 9, 1, MonsterJob.Farming));
            var service = CreateHeadlessService(roster);

            service.Update();

            Assert.AreEqual(MonsterJob.Farming, roster.AlliedMonsters[0].Job,
                "Update must not touch assignments while disabled");
        }

        [TestMethod]
        public void ReassessNow_AppliesSolverOutputToRoster()
        {
            // Zero backlog/workload: kitchen still wants its base crew of 3, farming wants none.
            // The 3 best cooks staff the kitchen; the rest go home.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("A", 1, 2, 9, MonsterJob.Farming));
            roster.AddAlliedMonster(Monster("B", 1, 8, 1));
            roster.AddAlliedMonster(Monster("C", 1, 6, 1));
            roster.AddAlliedMonster(Monster("D", 1, 4, 1));
            var service = CreateHeadlessService(roster);

            service.ReassessNow();

            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[1].Job, "Best cook staffs the kitchen");
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[2].Job, "Second cook staffs the kitchen");
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[3].Job, "Third cook staffs the kitchen");
            Assert.AreEqual(MonsterJob.None, roster.AlliedMonsters[0].Job,
                "Former farmer with no farm workload goes home (worst cook of the four)");
        }

        [TestMethod]
        public void ReassessNow_EmptyRoster_DoesNotThrow()
        {
            var service = CreateHeadlessService(new AlliedMonsterManager());
            service.ReassessNow();
        }

        [TestMethod]
        public void ReassessNow_WithExtraEvaluator_StaffsTheNewJob()
        {
            // Extensibility guarantee: registering a fishing evaluator routes workers to Fishing
            // with zero solver changes.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("A", 9, 1, 1));
            var service = new AutoJobAssignmentService(roster, null, null);
            service.AddEvaluator(new FixedDemandEvaluator(MonsterJob.Fishing, min: 1, desired: 1));

            service.ReassessNow();

            Assert.AreEqual(MonsterJob.Fishing, roster.AlliedMonsters[0].Job);
        }

        // ── Cadence and shift-boundary triggers ──────────────────────────────
        // A lone monster with zero workload gets Cooking (kitchen base crew) when a
        // reassessment fires, so a job change is the observable "reassess happened" signal.

        [TestMethod]
        public void TickCadence_FirstTick_InitializesWithoutReassessing()
        {
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("Bob", 1, 5, 1, MonsterJob.Farming));
            var service = CreateHeadlessService(roster);

            service.TickCadence(100f, isNighttime: false);

            Assert.AreEqual(MonsterJob.Farming, roster.AlliedMonsters[0].Job,
                "First tick only initializes the interval; no reassessment fires");
        }

        [TestMethod]
        public void TickCadence_ReassessesAfterInterval()
        {
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("Bob", 1, 5, 1, MonsterJob.Farming));
            var service = CreateHeadlessService(roster);

            service.TickCadence(0f, isNighttime: false);
            service.TickCadence(GameConfig.AutoJobReassessIntervalSeconds - 1f, isNighttime: false);
            Assert.AreEqual(MonsterJob.Farming, roster.AlliedMonsters[0].Job, "Interval not yet elapsed");

            service.TickCadence(GameConfig.AutoJobReassessIntervalSeconds, isNighttime: false);
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[0].Job, "Interval elapsed — reassess fires");
        }

        [TestMethod]
        public void TickCadence_ShiftChange_ReassessesImmediately()
        {
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("Bob", 1, 5, 1, MonsterJob.Farming));
            var service = CreateHeadlessService(roster);

            service.TickCadence(0f, isNighttime: false);
            service.TickCadence(30f, isNighttime: true);

            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[0].Job,
                "Day→night boundary reassesses immediately, well before the 60-minute cadence");
        }

        [TestMethod]
        public void TickCadence_ShiftChange_RestartsTheInterval()
        {
            var roster = new AlliedMonsterManager();
            var service = CreateHeadlessService(roster);

            service.TickCadence(0f, isNighttime: false);
            service.TickCadence(50f, isNighttime: true);   // boundary reassess at t=50

            // Now give the monster a job and verify the next cadence fire is 60s after the
            // boundary (t=110), not 60s after the original init (t=60).
            roster.AddAlliedMonster(Monster("Bob", 1, 5, 1, MonsterJob.Farming));
            service.TickCadence(65f, isNighttime: true);
            Assert.AreEqual(MonsterJob.Farming, roster.AlliedMonsters[0].Job,
                "Boundary reassess restarted the interval — t=65 is too early");

            service.TickCadence(50f + GameConfig.AutoJobReassessIntervalSeconds, isNighttime: true);
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[0].Job,
                "Cadence fires one full interval after the boundary reassess");
        }

        // ── Day/night shift segregation ──────────────────────────────────────

        [TestMethod]
        public void ReassessNow_StaffsDayAndNightShiftsIndependently()
        {
            // Day monsters (Slime) and nocturnal monsters (Orc) never work at the same time, so
            // each shift must field its own kitchen crew — even when the day shift has better cooks.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("DayA", 1, 9, 1));
            roster.AddAlliedMonster(Monster("DayB", 1, 8, 1));
            roster.AddAlliedMonster(Monster("DayC", 1, 7, 1));
            roster.AddAlliedMonster(Monster("DayD", 1, 6, 1));
            roster.AddAlliedMonster(Monster("NightA", 1, 5, 1, typeName: "Monster_Orc"));
            roster.AddAlliedMonster(Monster("NightB", 1, 4, 1, typeName: "Monster_Skeleton"));
            roster.AddAlliedMonster(Monster("NightC", 1, 3, 1, typeName: "Monster_Orc"));
            roster.AddAlliedMonster(Monster("NightD", 1, 2, 1, typeName: "Monster_Orc"));
            var service = CreateHeadlessService(roster);

            service.ReassessNow();

            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[0].Job, "Best day cook staffs the day kitchen");
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[1].Job);
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[2].Job);
            Assert.AreEqual(MonsterJob.None, roster.AlliedMonsters[3].Job, "Fourth day monster exceeds the base crew");
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[4].Job,
                "Night shift fields its own kitchen crew despite lower cooking skill than the day shift");
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[5].Job);
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[6].Job);
            Assert.AreEqual(MonsterJob.None, roster.AlliedMonsters[7].Job, "Fourth night monster exceeds the base crew");
        }

        [TestMethod]
        public void ReassessNow_DemandClampsApplyPerShift()
        {
            // Kitchen base crew clamps to each shift's own size, not the whole roster's.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("DayA", 1, 5, 1));
            roster.AddAlliedMonster(Monster("DayB", 1, 5, 1));
            roster.AddAlliedMonster(Monster("NightA", 1, 5, 1, typeName: "Monster_Orc"));
            roster.AddAlliedMonster(Monster("NightB", 1, 5, 1, typeName: "Monster_Skeleton"));
            var service = CreateHeadlessService(roster);

            service.ReassessNow();

            for (int i = 0; i < 4; i++)
                Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[i].Job,
                    $"Monster {i}: with 2 monsters per shift, the whole shift staffs the kitchen");
        }

        [TestMethod]
        public void ReassessNow_StickyKitchenProtectionIsPerShift()
        {
            // A nocturnal monster already on Cooking must not shield the day shift from staffing
            // its own kitchen, and vice versa — stickiness applies within each shift group only.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("Day", 1, 4, 1));
            roster.AddAlliedMonster(Monster("Night", 1, 4, 1, MonsterJob.Cooking, typeName: "Monster_Orc"));
            var service = CreateHeadlessService(roster);

            service.ReassessNow();

            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[0].Job,
                "Day shift staffs its kitchen even though a night monster already holds a kitchen job");
            Assert.AreEqual(MonsterJob.Cooking, roster.AlliedMonsters[1].Job,
                "Sticky night kitchen worker keeps the job");
        }

        // ── Farming priority over kitchen (issue: dead-end kitchen with unworked farm) ──

        /// <summary>Service whose farming evaluator sees a real workload of planCount placed plans.</summary>
        private static AutoJobAssignmentService CreateServiceWithFarmWorkload(
            AlliedMonsterManager roster, int planCount)
        {
            var planting = new CropPlantingService();
            var growth = new CropGrowthService(planting);
            for (int i = 0; i < planCount; i++)
                planting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = i, TileY = 0 });
            return new AutoJobAssignmentService(roster,
                new KitchenJobDemandEvaluator(null, null, null),
                new FarmingJobDemandEvaluator(null, growth, planting));
        }

        [TestMethod]
        public void ReassessNow_TwoMonstersWithFarmWorkload_BothFarm()
        {
            // The reported bug: with only 2 workers and farm work outstanding, both used to be
            // sunk into a dead-end kitchen. Farming now takes priority and the kitchen (which
            // can't field its 3-monster crew anyway) stands down.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("A", 1, 5, 5));
            roster.AddAlliedMonster(Monster("B", 1, 5, 5));
            var service = CreateServiceWithFarmWorkload(roster,
                planCount: GameConfig.AutoJobFarmCropsPerWorkerBaseline + 1);   // farming desired = 2

            service.ReassessNow();

            Assert.AreEqual(MonsterJob.Farming, roster.AlliedMonsters[0].Job, "Both workers farm");
            Assert.AreEqual(MonsterJob.Farming, roster.AlliedMonsters[1].Job, "Both workers farm");
        }

        [TestMethod]
        public void ReassessNow_FourMonsters_OneFarmerThreeKitchen()
        {
            // With 4 monsters the kitchen's full crew fits alongside the guaranteed farmer:
            // 1 farms (even though farming wants 2), 3 staff the kitchen.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("A", 1, 5, 5));
            roster.AddAlliedMonster(Monster("B", 1, 5, 5));
            roster.AddAlliedMonster(Monster("C", 1, 5, 5));
            roster.AddAlliedMonster(Monster("D", 1, 5, 5));
            var service = CreateServiceWithFarmWorkload(roster,
                planCount: GameConfig.AutoJobFarmCropsPerWorkerBaseline + 1);   // farming desired = 2

            service.ReassessNow();

            int farmers = 0, cooks = 0;
            for (int i = 0; i < roster.AlliedMonsters.Count; i++)
            {
                if (roster.AlliedMonsters[i].Job == MonsterJob.Farming) farmers++;
                if (roster.AlliedMonsters[i].Job == MonsterJob.Cooking) cooks++;
            }
            Assert.AreEqual(1, farmers, "One guaranteed farmer");
            Assert.AreEqual(3, cooks, "Kitchen keeps its full base crew");
        }

        [TestMethod]
        public void ReassessNow_StickyKitchenCrew_ReleasedWhenFarmTasksAppearWithZeroFarmers()
        {
            // The one exception to all-day kitchen stickiness: farm work exists but nobody at all
            // is farming, so kitchen work is pointless and the crew is released to the farm.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("A", 1, 5, 5, MonsterJob.Cooking));
            roster.AddAlliedMonster(Monster("B", 1, 5, 5, MonsterJob.Cooking));
            var service = CreateServiceWithFarmWorkload(roster, planCount: 1);

            service.ReassessNow();

            int farmers = 0;
            for (int i = 0; i < roster.AlliedMonsters.Count; i++)
            {
                if (roster.AlliedMonsters[i].Job == MonsterJob.Farming) farmers++;
            }
            Assert.IsTrue(farmers >= 1, "At least one sticky kitchen worker must be released to the farm");
        }

        [TestMethod]
        public void ReassessNow_FarmerGuaranteeHoldsPerShift()
        {
            // Each shift is a disjoint workforce, so the ≥1-farmer guarantee applies to the day
            // crew and the night crew independently.
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("DayA", 1, 5, 5, MonsterJob.Cooking));
            roster.AddAlliedMonster(Monster("DayB", 1, 5, 5, MonsterJob.Cooking));
            roster.AddAlliedMonster(Monster("NightA", 1, 5, 5, MonsterJob.Cooking, typeName: "Monster_Orc"));
            roster.AddAlliedMonster(Monster("NightB", 1, 5, 5, MonsterJob.Cooking, typeName: "Monster_Orc"));
            var service = CreateServiceWithFarmWorkload(roster, planCount: 1);

            service.ReassessNow();

            int dayFarmers = 0, nightFarmers = 0;
            for (int i = 0; i < roster.AlliedMonsters.Count; i++)
            {
                if (roster.AlliedMonsters[i].Job != MonsterJob.Farming)
                    continue;
                if (roster.AlliedMonsters[i].MonsterTypeName == "Monster_Orc") nightFarmers++;
                else dayFarmers++;
            }
            Assert.IsTrue(dayFarmers >= 1, "Day shift must field its own farmer");
            Assert.IsTrue(nightFarmers >= 1, "Night shift must field its own farmer");
        }

        [TestMethod]
        public void ReassessNow_LargeRoster_KitchenStillFieldsBaseCrew()
        {
            // Regression guard: farming priority must not starve the kitchen when workers are
            // plentiful — 6 monsters with a small farm workload still yield a full kitchen crew.
            var roster = new AlliedMonsterManager();
            for (int i = 0; i < 6; i++)
                roster.AddAlliedMonster(Monster($"M{i}", 1, 5, 5));
            var service = CreateServiceWithFarmWorkload(roster, planCount: 1);   // farming desired = 1

            service.ReassessNow();

            int farmers = 0, cooks = 0;
            for (int i = 0; i < roster.AlliedMonsters.Count; i++)
            {
                if (roster.AlliedMonsters[i].Job == MonsterJob.Farming) farmers++;
                if (roster.AlliedMonsters[i].Job == MonsterJob.Cooking) cooks++;
            }
            Assert.AreEqual(1, farmers, "Small farm workload wants exactly one farmer");
            Assert.AreEqual(3, cooks, "Kitchen fields its full base crew from the surplus");
        }

        private sealed class FixedDemandEvaluator : IJobDemandEvaluator
        {
            private readonly JobDemandEntry _entry;
            public FixedDemandEvaluator(MonsterJob job, int min, int desired)
                => _entry = new JobDemandEntry { Job = job, MinWorkers = min, DesiredWorkers = desired, Sticky = false };
            public MonsterJob Job => _entry.Job;
            public JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers) => _entry;
        }

        // ── Farming demand math ──────────────────────────────────────────────

        [TestMethod]
        public void FarmingDemand_ZeroWorkload_WantsNoWorkers()
        {
            var d = FarmingJobDemandEvaluator.ComputeDemand(0, 0, availableWorkers: 10);
            Assert.AreEqual(0, d.DesiredWorkers);
            Assert.AreEqual(0, d.MinWorkers);
            Assert.IsFalse(d.Sticky, "Farming is not sticky — farmers are released during lulls");
        }

        [TestMethod]
        public void FarmingDemand_BaselineScalesByCeilingDivision()
        {
            int per = GameConfig.AutoJobFarmCropsPerWorkerBaseline;
            Assert.AreEqual(1, FarmingJobDemandEvaluator.ComputeDemand(0, 1, 10).DesiredWorkers);
            Assert.AreEqual(1, FarmingJobDemandEvaluator.ComputeDemand(0, per, 10).DesiredWorkers);
            Assert.AreEqual(2, FarmingJobDemandEvaluator.ComputeDemand(0, per + 1, 10).DesiredWorkers);
        }

        [TestMethod]
        public void FarmingDemand_BurstOutranksBaseline()
        {
            // A watering/harvest wave (many outstanding tasks) demands more workers than the
            // baseline care load alone would.
            int tasksPer = GameConfig.AutoJobFarmTasksPerWorker;
            var d = FarmingJobDemandEvaluator.ComputeDemand(tasksPer * 3, careLoad: 1, availableWorkers: 10);
            Assert.AreEqual(3, d.DesiredWorkers, "Burst signal should win when larger than baseline");
            Assert.AreEqual(1, d.MinWorkers, "At least one farmer whenever any workload exists");
        }

        [TestMethod]
        public void FarmingDemand_ClampsToAvailableWorkers()
        {
            var d = FarmingJobDemandEvaluator.ComputeDemand(1000, 1000, availableWorkers: 4);
            Assert.AreEqual(4, d.DesiredWorkers, "Demand can never exceed the available workers");
        }

        [TestMethod]
        public void FarmingEvaluator_CountsPlansFromCropPlantingService()
        {
            var planting = new CropPlantingService();
            var growth = new CropGrowthService(planting);
            int per = GameConfig.AutoJobFarmCropsPerWorkerBaseline;
            for (int i = 0; i < per + 1; i++)
                planting.AddPlan(new PlacedCropPlan { Type = CropType.Wheat, TileX = i, TileY = 0 });
            var evaluator = new FarmingJobDemandEvaluator(null, growth, planting);

            var d = evaluator.EvaluateDemand(rosterSize: 10, availableWorkers: 10);

            Assert.AreEqual(2, d.DesiredWorkers, "Placed plans count toward the baseline care load");
        }

        // ── Kitchen demand math ──────────────────────────────────────────────

        [TestMethod]
        public void KitchenDemand_ZeroBacklog_StillWantsBaseCrew()
        {
            var d = KitchenJobDemandEvaluator.ComputeDemand(0, rosterSize: 10, availableWorkers: 10);
            Assert.AreEqual(GameConfig.AutoJobKitchenBaseStaff, d.DesiredWorkers,
                "Kitchen keeps a cook + server + runner crew even with no orders");
            Assert.AreEqual(GameConfig.AutoJobKitchenBaseStaff, d.MinWorkers,
                "Base crew is a firm minimum so it fills before farming's desired extras");
            Assert.IsTrue(d.Sticky, "Kitchen workers must never be pulled away");
        }

        [TestMethod]
        public void KitchenDemand_BaseCrewClampsToTinyRoster()
        {
            // Available equals the full roster (no farming reservation), so a partial crew is
            // still fielded on tiny rosters, exactly as before the farming-priority change.
            var d = KitchenJobDemandEvaluator.ComputeDemand(0, rosterSize: 2, availableWorkers: 2);
            Assert.AreEqual(2, d.DesiredWorkers);
        }

        [TestMethod]
        public void KitchenDemand_ZeroedWhenFarmingReservationLeavesUnderBaseCrew()
        {
            // Farming reserved a worker and fewer than cook+server+runner remain: a partial
            // kitchen has no runner and is pointless, so the kitchen cedes the shortage entirely.
            var d = KitchenJobDemandEvaluator.ComputeDemand(0, rosterSize: 3, availableWorkers: 2);
            Assert.AreEqual(0, d.DesiredWorkers, "All-or-nothing when competing with farming");
            Assert.AreEqual(0, d.MinWorkers);
        }

        [TestMethod]
        public void KitchenDemand_BacklogAddsExtraWorkers()
        {
            int per = GameConfig.AutoJobKitchenBacklogPerExtraWorker;
            var d = KitchenJobDemandEvaluator.ComputeDemand(per * 2, rosterSize: 10, availableWorkers: 10);
            Assert.AreEqual(GameConfig.AutoJobKitchenBaseStaff + 2, d.DesiredWorkers);
            Assert.AreEqual(GameConfig.AutoJobKitchenBaseStaff, d.MinWorkers);
        }

        [TestMethod]
        public void KitchenDemand_CapsAtMaxWorkers()
        {
            var d = KitchenJobDemandEvaluator.ComputeDemand(1000, rosterSize: 20, availableWorkers: 20);
            Assert.AreEqual(GameConfig.AutoJobKitchenMaxWorkers, d.DesiredWorkers,
                "Kitchen demand is capped at the coordinator's worker limit");
        }

        [TestMethod]
        public void KitchenDemand_NothingOrderable_FieldsNoOne()
        {
            // No coverable dish means servers can never take an order, so no ticket can ever
            // exist — staffing the kitchen would be dead weight.
            var d = KitchenJobDemandEvaluator.ComputeDemand(0, rosterSize: 10, availableWorkers: 10,
                anyDishOrderable: false);
            Assert.AreEqual(0, d.DesiredWorkers);
            Assert.AreEqual(0, d.MinWorkers);
            Assert.IsTrue(d.Sticky,
                "Workers already in the kitchen stay put when the larder runs dry mid-day");
        }

        // ── Kitchen evaluator: empty-larder gate ─────────────────────────────

        private static KitchenTaskCoordinator CreateHeadlessKitchen(
            out BuildingService buildings, out CropStorageInventoryService storage)
        {
            buildings = new BuildingService();
            buildings.AddBuilding(new PlacedBuilding
            {
                Type = BuildingType.CropStorage,
                TileX = GameConfig.NewGameCropStorageAnchorTileX,
                TileY = GameConfig.NewGameCropStorageAnchorTileY,
                UniqueId = 1,
            });
            storage = new CropStorageInventoryService(buildings);
            var coordinator = new KitchenTaskCoordinator(null, buildings, 240, 12);
            coordinator.SetHeadlessServices(storage, new GameStateService());
            return coordinator;
        }

        [TestMethod]
        public void KitchenEvaluator_EmptyLarder_ReportsZeroDemand()
        {
            var coordinator = CreateHeadlessKitchen(out _, out _);
            var evaluator = new KitchenJobDemandEvaluator(coordinator, null, null);

            var d = evaluator.EvaluateDemand(rosterSize: 3, availableWorkers: 3);

            Assert.AreEqual(0, d.DesiredWorkers, "Empty fridge + storage: no dish is coverable");
            Assert.AreEqual(0, d.MinWorkers);
        }

        [TestMethod]
        public void KitchenEvaluator_StockedLarder_WantsBaseCrew()
        {
            var coordinator = CreateHeadlessKitchen(out _, out var storage);
            var def = DishConfig.GetDefinition((DishType)0);
            for (int i = 0; i < def.Recipe.Length; i++)
                Assert.IsTrue(storage.TryDeposit(1, def.Recipe[i].Crop, def.Recipe[i].Qty));
            var evaluator = new KitchenJobDemandEvaluator(coordinator, null, null);

            var d = evaluator.EvaluateDemand(rosterSize: 3, availableWorkers: 3);

            Assert.AreEqual(GameConfig.AutoJobKitchenBaseStaff, d.DesiredWorkers,
                "One coverable dish is enough to staff the kitchen");
        }

        [TestMethod]
        public void ReassessNow_LoneMonsterNewGame_StaysHomeInsteadOfKitchen()
        {
            // The reported scenario: fresh game, one monster, no farm workload, nothing in
            // storage to cook with. The monster must stay home, not take the chef hat.
            var coordinator = CreateHeadlessKitchen(out _, out _);
            var roster = new AlliedMonsterManager();
            roster.AddAlliedMonster(Monster("Solo", 1, 9, 1));
            var service = new AutoJobAssignmentService(roster,
                new KitchenJobDemandEvaluator(coordinator, null, null),
                new FarmingJobDemandEvaluator(null, null, null));

            service.ReassessNow();

            Assert.AreEqual(MonsterJob.None, roster.AlliedMonsters[0].Job);
        }
    }
}
