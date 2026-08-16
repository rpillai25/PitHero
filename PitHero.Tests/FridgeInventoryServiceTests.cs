using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Farming;
using PitHero.Services;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for the slot-based refrigerator inventory (issue #386): flat 10-unit stacks,
    /// partial-stack top-off on deposit, partial-first drain on withdraw, capacity math, and
    /// save-restore.
    /// </summary>
    [TestClass]
    public class FridgeInventoryServiceTests
    {
        private const int StackSize = GameConfig.KitchenFridgeStackSize;
        private static readonly CropType CropA = CropType.Wheat;
        private static readonly CropType CropB = CropType.Tomato;

        private FridgeInventoryService _fridge;

        [TestInitialize]
        public void Setup()
        {
            _fridge = new FridgeInventoryService();
        }

        private int OccupiedSlots()
        {
            int occupied = 0;
            var slots = _fridge.GetSlots();
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty)
                    occupied++;
            return occupied;
        }

        [TestMethod]
        public void Deposit_TopsOffPartialStackBeforeOpeningANewSlot()
        {
            Assert.AreEqual(7, _fridge.Deposit(CropA, 7));
            Assert.AreEqual(1, OccupiedSlots());

            // 7 + 7: the first stack tops off to 10, the remaining 4 open a second slot
            Assert.AreEqual(7, _fridge.Deposit(CropA, 7));
            Assert.AreEqual(14, _fridge.Count(CropA));
            Assert.AreEqual(2, OccupiedSlots());

            var slots = _fridge.GetSlots();
            bool sawFull = false, sawPartial = false;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty) continue;
                if (slots[i].Count == StackSize) sawFull = true;
                if (slots[i].Count == 4) sawPartial = true;
                Assert.IsTrue(slots[i].Count <= StackSize, "fridge stack exceeded the flat 10-unit cap");
            }
            Assert.IsTrue(sawFull && sawPartial, "expected one full stack and one 4-unit stack");
        }

        [TestMethod]
        public void Deposit_ReturnsOnlyWhatFitWhenTheFridgeIsFull()
        {
            int stored = _fridge.Deposit(CropA, FridgeInventoryService.SlotCount * StackSize + 5);
            Assert.AreEqual(FridgeInventoryService.SlotCount * StackSize, stored);
            Assert.AreEqual(0, _fridge.CapacityFor(CropA));
            Assert.AreEqual(0, _fridge.Deposit(CropB, 1), "a full fridge must refuse further deposits");
        }

        [TestMethod]
        public void Withdraw_DrainsPartialStacksBeforeFullOnes()
        {
            _fridge.Deposit(CropA, 14); // one full stack + one 4-unit stack

            Assert.AreEqual(3, _fridge.Withdraw(CropA, 3));
            Assert.AreEqual(11, _fridge.Count(CropA));

            var slots = _fridge.GetSlots();
            bool fullStackIntact = false;
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty && slots[i].Count == StackSize)
                    fullStackIntact = true;
            Assert.IsTrue(fullStackIntact, "the partial stack should have been drained first");
        }

        [TestMethod]
        public void Withdraw_FreesTheSlotWhenAStackEmpties()
        {
            _fridge.Deposit(CropA, 4);
            Assert.AreEqual(4, _fridge.Withdraw(CropA, 9), "withdraw is best-effort");
            Assert.AreEqual(0, _fridge.Count(CropA));
            Assert.AreEqual(0, OccupiedSlots(), "an emptied stack must free its slot");
        }

        [TestMethod]
        public void CapacityFor_CountsPartialRoomAndEmptySlots()
        {
            Assert.AreEqual(FridgeInventoryService.SlotCount * StackSize, _fridge.CapacityFor(CropA));

            _fridge.Deposit(CropA, 7);
            Assert.AreEqual(3 + 31 * StackSize, _fridge.CapacityFor(CropA),
                "room in CropA's partial stack plus the empty slots");
            Assert.AreEqual(31 * StackSize, _fridge.CapacityFor(CropB),
                "another crop cannot use CropA's partial stack");
        }

        [TestMethod]
        public void ClearSlot_EmptiesOnlyThatSlotAndBumpsVersion()
        {
            _fridge.Deposit(CropA, 14);
            int before = _fridge.Version;

            var slots = _fridge.GetSlots();
            int firstOccupied = -1;
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty) { firstOccupied = i; break; }

            _fridge.ClearSlot(firstOccupied);
            Assert.AreEqual(4, _fridge.Count(CropA), "only the cleared stack's units vanish");
            Assert.IsTrue(_fridge.Version > before, "mutations must bump Version for the UI");
        }

        [TestMethod]
        public void RestoreSlots_RoundTripsContents()
        {
            _fridge.Deposit(CropA, 14);
            _fridge.Deposit(CropB, 3);

            var snapshot = new HarvestSlot[FridgeInventoryService.SlotCount];
            var slots = _fridge.GetSlots();
            for (int i = 0; i < slots.Count; i++)
                snapshot[i] = slots[i];

            var restored = new FridgeInventoryService();
            restored.RestoreSlots(snapshot);
            Assert.AreEqual(14, restored.Count(CropA));
            Assert.AreEqual(3, restored.Count(CropB));
        }

        [TestMethod]
        public void PreStockStackSize_ClampsToSliderRange()
        {
            _fridge.PreStockStackSize = 0;
            Assert.AreEqual(GameConfig.KitchenPreStockStackSizeMin, _fridge.PreStockStackSize);
            _fridge.PreStockStackSize = 99;
            Assert.AreEqual(GameConfig.KitchenPreStockStackSizeMax, _fridge.PreStockStackSize);
            _fridge.PreStockStackSize = 3;
            Assert.AreEqual(3, _fridge.PreStockStackSize);
        }
    }
}
