using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for AutoCropSellService. All tests call TrySellPass() directly to bypass the
    /// 1-second Time.DeltaTime throttle, which never elapses in a headless test context.
    /// The Enabled=false path is verified via Update() (which returns before touching the timer).
    /// Services are constructed directly (not via Core.Services) for headless safety.
    /// </summary>
    [TestClass]
    public class AutoCropSellServiceTests
    {
        private const int BuildingA = 1;
        private const int BuildingB = 2;

        private CropStorageInventoryService _storage   = null!;
        private GameStateService            _gameState = null!;
        private AutoCropSellService         _service   = null!;

        [TestInitialize]
        public void Setup()
        {
            _storage   = new CropStorageInventoryService(buildingService: null);
            _gameState = new GameStateService();
            _service   = new AutoCropSellService(_storage, _gameState);
        }

        // ── Default state ────────────────────────────────────────────────────

        [TestMethod]
        public void Service_DisabledByDefault()
        {
            Assert.IsFalse(_service.Enabled, "AutoCropSellService should be disabled by default");
        }

        [TestMethod]
        public void Designations_AllTrueByDefault()
        {
            for (int i = 0; i < CropTypeInfo.Count; i++)
                Assert.IsTrue(_service.Designations[i],
                    $"All crop types should be designated for auto-sell by default (crop {i})");
        }

        // ── Update() when disabled ───────────────────────────────────────────

        [TestMethod]
        public void Update_WhenDisabled_SellsNothing()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max);
            _gameState.Funds = 100;

            // Enabled is false (default) — Update should exit immediately
            _service.Update();

            Assert.AreEqual(100, _gameState.Funds, "Funds should be unchanged when Enabled=false");
            Assert.AreEqual(max, _storage.GetSlots(BuildingA)[0].Count,
                "Full stack should remain when Enabled=false");
        }

        // ── TrySellPass() ────────────────────────────────────────────────────

        [TestMethod]
        public void TrySellPass_SellsStackAtMaxSize()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max);
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(CropConfig.GetHarvestStackSellPrice(CropType.Wheat, max), _gameState.Funds,
                "Funds should increase by the stack sell price");
            Assert.IsTrue(_storage.GetSlots(BuildingA)[0].IsEmpty, "Sold slot should be cleared");
        }

        [TestMethod]
        public void TrySellPass_IgnoresStackBelowMaxSize()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max - 1);
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(0, _gameState.Funds, "Nothing should be sold below max stack size");
            Assert.AreEqual(max - 1, _storage.GetSlots(BuildingA)[0].Count,
                "Partial stack should remain untouched");
        }

        [TestMethod]
        public void TrySellPass_RespectsDesignationOff()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max);
            _service.Designations[(int)CropType.Wheat] = false;
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(0, _gameState.Funds, "Undesignated crops should not be sold");
            Assert.AreEqual(max, _storage.GetSlots(BuildingA)[0].Count,
                "Undesignated full stack should remain untouched");
        }

        [TestMethod]
        public void TrySellPass_SellsMultipleFullStacksAcrossBuildingsInOnePass()
        {
            int wheatMax  = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            int turnipMax = CropConfig.GetMaxHarvestStack(CropType.Turnip);
            int tomatoMax = CropConfig.GetMaxHarvestStack(CropType.Tomato);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, wheatMax);
            _storage.DepositReturningStored(BuildingB, CropType.Turnip, turnipMax);
            // Full but designated off — must survive the pass
            _storage.DepositReturningStored(BuildingB, CropType.Tomato, tomatoMax);
            _service.Designations[(int)CropType.Tomato] = false;
            _gameState.Funds = 0;

            _service.TrySellPass();

            int expected = CropConfig.GetHarvestStackSellPrice(CropType.Wheat, wheatMax)
                         + CropConfig.GetHarvestStackSellPrice(CropType.Turnip, turnipMax);
            Assert.AreEqual(expected, _gameState.Funds,
                "Both designated full stacks should be sold in one pass");
            Assert.IsTrue(_storage.GetSlots(BuildingA)[0].IsEmpty, "Building A stack should be sold");
            Assert.IsTrue(_storage.GetSlots(BuildingB)[0].IsEmpty, "Building B Turnip stack should be sold");
            Assert.AreEqual(tomatoMax, _storage.GetSlots(BuildingB)[1].Count,
                "Designated-off Tomato stack should remain");
        }

        [TestMethod]
        public void TrySellPass_MixedSlots_OnlyFullDesignatedSold()
        {
            int wheatMax  = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            int turnipMax = CropConfig.GetMaxHarvestStack(CropType.Turnip);
            int tomatoMax = CropConfig.GetMaxHarvestStack(CropType.Tomato);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, wheatMax);       // full, designated
            _storage.DepositReturningStored(BuildingA, CropType.Turnip, turnipMax - 5); // partial, designated
            _storage.DepositReturningStored(BuildingA, CropType.Tomato, tomatoMax);     // full, undesignated
            _service.Designations[(int)CropType.Tomato] = false;
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(CropConfig.GetHarvestStackSellPrice(CropType.Wheat, wheatMax), _gameState.Funds,
                "Only the full designated stack should be sold");
            var slots = _storage.GetSlots(BuildingA);
            Assert.IsTrue(slots[0].IsEmpty, "Full designated Wheat stack should be sold");
            Assert.AreEqual(turnipMax - 5, slots[1].Count, "Partial Turnip stack should remain");
            Assert.AreEqual(tomatoMax, slots[2].Count, "Undesignated Tomato stack should remain");
        }

        [TestMethod]
        public void TrySellPass_EmptyStorage_DoesNothing()
        {
            _gameState.Funds = 50;

            _service.TrySellPass();

            Assert.AreEqual(50, _gameState.Funds, "Funds should be unchanged with no stored crops");
        }

        // ── KeepStacks ───────────────────────────────────────────────────────

        [TestMethod]
        public void KeepStacks_ZeroByDefault()
        {
            Assert.AreEqual(0, _service.KeepStacks, "KeepStacks should default to 0 (sell every full stack)");
        }

        [TestMethod]
        public void KeepStacks_ClampsToValidRange()
        {
            _service.KeepStacks = -3;
            Assert.AreEqual(0, _service.KeepStacks, "KeepStacks should clamp below at 0");

            _service.KeepStacks = 99;
            Assert.AreEqual(AutoCropSellService.MaxKeepStacks, _service.KeepStacks,
                "KeepStacks should clamp above at MaxKeepStacks");
        }

        [TestMethod]
        public void TrySellPass_KeepStacks_SellsOnlyStacksBeyondKeepCount()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            // Three full Wheat stacks in one building
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max * 3);
            _service.KeepStacks = 2;
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(CropConfig.GetHarvestStackSellPrice(CropType.Wheat, max), _gameState.Funds,
                "Only the third full stack should be sold when KeepStacks=2");
            var slots = _storage.GetSlots(BuildingA);
            int fullStacksRemaining = 0;
            for (int s = 0; s < slots.Count; s++)
                if (!slots[s].IsEmpty && slots[s].Type == CropType.Wheat && slots[s].Count == max)
                    fullStacksRemaining++;
            Assert.AreEqual(2, fullStacksRemaining, "Two full Wheat stacks should be kept");
        }

        [TestMethod]
        public void TrySellPass_KeepStacks_AtOrBelowKeepCount_SellsNothing()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            // Two full Wheat stacks, keep 2 — nothing to sell yet
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max * 2);
            _service.KeepStacks = 2;
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(0, _gameState.Funds, "No stacks should be sold at or below the keep count");
        }

        [TestMethod]
        public void TrySellPass_KeepStacks_CountsPerCropAcrossBuildings()
        {
            int wheatMax  = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            int turnipMax = CropConfig.GetMaxHarvestStack(CropType.Turnip);
            // Two full Wheat stacks split across buildings, one full Turnip stack
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, wheatMax);
            _storage.DepositReturningStored(BuildingB, CropType.Wheat, wheatMax);
            _storage.DepositReturningStored(BuildingB, CropType.Turnip, turnipMax);
            _service.KeepStacks = 1;
            _gameState.Funds = 0;

            _service.TrySellPass();

            Assert.AreEqual(CropConfig.GetHarvestStackSellPrice(CropType.Wheat, wheatMax), _gameState.Funds,
                "One Wheat stack (the second, counted across buildings) should be sold; the Turnip stack is its crop's first and is kept");
            Assert.AreEqual(turnipMax, _storage.GetSlots(BuildingB)[1].Count,
                "The single full Turnip stack should be kept when KeepStacks=1");
        }

        [TestMethod]
        public void TrySellPass_KeepStacks_KeepCountResetsBetweenPasses()
        {
            int max = CropConfig.GetMaxHarvestStack(CropType.Wheat);
            _storage.DepositReturningStored(BuildingA, CropType.Wheat, max);
            _service.KeepStacks = 1;
            _gameState.Funds = 0;

            // Two passes: the single kept stack must not be sold by the second pass's counter state
            _service.TrySellPass();
            _service.TrySellPass();

            Assert.AreEqual(0, _gameState.Funds, "The kept stack should survive repeated passes");
            Assert.AreEqual(max, _storage.GetSlots(BuildingA)[0].Count, "Kept stack should remain untouched");
        }
    }
}
