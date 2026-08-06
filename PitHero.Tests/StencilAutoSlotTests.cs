using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for StencilBagSlotPreferenceProvider and the ItemBag slot-preference seam
    /// (issue #362 — stencil auto-slotting).
    /// </summary>
    [TestClass]
    public class StencilAutoSlotTests
    {
        // ShieldMastery: [Sword](0,0) [Shield](1,0) — a horizontal pair easy to reason about.
        private const string ShieldMasteryId = "knight.shield_mastery";

        // HeavyFortification: [Mail](0,0) [Mail+Helm](0,1) [Shield](1,1) — uses rows 0-1 relative
        private const string HeavyFortificationId = "knight.heavy_fortification";

        // ---- Helpers ----------------------------------------------------------------

        private static GameStateService MakeServiceWithStencil(string patternId, int anchorX, int anchorY)
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil(patternId, anchorX, anchorY);
            return svc;
        }

        private static ItemBag MakeBagWithProvider(int capacity, GameStateService svc)
        {
            var bag = new ItemBag("Test", capacity);
            bag.SlotPreferenceProvider = new StencilBagSlotPreferenceProvider(svc);
            return bag;
        }

        // ---- SynergyPatternRegistry -------------------------------------------------

        [TestMethod]
        public void SynergyPatternRegistry_All_Has63Patterns()
        {
            Assert.AreEqual(63, SynergyPatternRegistry.All.Count,
                "Registry should contain exactly 63 patterns");
        }

        [TestMethod]
        public void SynergyPatternRegistry_GetById_ReturnsKnownPattern()
        {
            var pattern = SynergyPatternRegistry.GetById(ShieldMasteryId);
            Assert.IsNotNull(pattern, "ShieldMastery pattern should be in the registry");
            Assert.AreEqual(ShieldMasteryId, pattern.Id);
        }

        [TestMethod]
        public void SynergyPatternRegistry_GetById_ReturnsNullForUnknownId()
        {
            Assert.IsNull(SynergyPatternRegistry.GetById("not.a.real.pattern"));
        }

        [TestMethod]
        public void SynergyPatternRegistry_GetById_NullReturnsNull()
        {
            Assert.IsNull(SynergyPatternRegistry.GetById(null));
        }

        // ---- GameStateService placed-stencil snapshot -------------------------------

        [TestMethod]
        public void GameStateService_SetPlacedStencil_AddsRecord()
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil("pat_a", 3, 4);
            Assert.AreEqual(1, svc.PlacedStencils.Count);
            Assert.AreEqual("pat_a", svc.PlacedStencils[0].PatternId);
            Assert.AreEqual(3, svc.PlacedStencils[0].AnchorX);
            Assert.AreEqual(4, svc.PlacedStencils[0].AnchorY);
        }

        [TestMethod]
        public void GameStateService_SetPlacedStencil_UpsertsSamePattern()
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil("pat_a", 0, 3);
            svc.SetPlacedStencil("pat_a", 5, 7); // update anchor
            Assert.AreEqual(1, svc.PlacedStencils.Count, "Upsert must not duplicate");
            Assert.AreEqual(5, svc.PlacedStencils[0].AnchorX);
            Assert.AreEqual(7, svc.PlacedStencils[0].AnchorY);
        }

        [TestMethod]
        public void GameStateService_RemovePlacedStencil_RemovesRecord()
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil("pat_a", 0, 3);
            svc.SetPlacedStencil("pat_b", 5, 3);
            svc.RemovePlacedStencil("pat_a");
            Assert.AreEqual(1, svc.PlacedStencils.Count);
            Assert.AreEqual("pat_b", svc.PlacedStencils[0].PatternId);
        }

        [TestMethod]
        public void GameStateService_RemovePlacedStencil_NoopWhenAbsent()
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil("pat_a", 0, 3);
            svc.RemovePlacedStencil("not_there"); // should not throw or corrupt
            Assert.AreEqual(1, svc.PlacedStencils.Count);
        }

        [TestMethod]
        public void GameStateService_ClearPlacedStencils_RemovesAll()
        {
            var svc = new GameStateService();
            svc.SetPlacedStencil("pat_a", 0, 3);
            svc.SetPlacedStencil("pat_b", 5, 3);
            svc.ClearPlacedStencils();
            Assert.AreEqual(0, svc.PlacedStencils.Count);
        }

        // ---- StencilBagSlotPreferenceProvider: anchor+offset → bagIndex ---------------
        //
        // Grid layout (20 wide, rows 3-8 are bag-backed):
        //   bagIndex = (gridY - 3) * 20 + gridX
        //
        // ShieldMastery offsets: (0,0)=Sword at anchor; (1,0)=Shield at anchor+(1,0)
        // If anchor = (2, 3):
        //   Sword cell: gridX=2, gridY=3 → bagIndex = (3-3)*20+2 = 2
        //   Shield cell: gridX=3, gridY=3 → bagIndex = (3-3)*20+3 = 3

        [TestMethod]
        public void Provider_MatchingKind_ReturnsCorrectBagIndex()
        {
            // ShieldMastery anchor (2,3): Sword cell → bagIndex 2, Shield cell → bagIndex 3
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 2, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            var sword = GearItems.ShortSword();  // ItemKind.WeaponSword
            bag.TryAdd(sword);

            // Sword should land at bagIndex 2 (the first WeaponSword cell of ShieldMastery)
            Assert.IsNotNull(bag.GetSlotItem(2), "Sword should be placed at the stencil's sword cell (bagIndex 2)");
            Assert.AreSame(sword, bag.GetSlotItem(2));
        }

        [TestMethod]
        public void Provider_TryAdd_PlacesBothCellsOfShieldMastery()
        {
            // anchor (0,3): Sword→bagIndex 0, Shield→bagIndex 1
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 0, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            var sword = GearItems.ShortSword();
            var shield = GearItems.IronShield();

            bag.TryAdd(sword);
            bag.TryAdd(shield);

            Assert.AreSame(sword, bag.GetSlotItem(0), "Sword at cell 0");
            Assert.AreSame(shield, bag.GetSlotItem(1), "Shield at cell 1");
        }

        [TestMethod]
        public void Provider_NonMatchingKind_ReturnsNegativeOne_FallsBackToFirstEmpty()
        {
            // ShieldMastery anchor (0,3): cells are Sword and Shield.
            // Adding armor should get -1 from the provider and fall to slot 0.
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 0, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            var armor = GearItems.LeatherArmor(); // ItemKind.ArmorGi — not in ShieldMastery
            bag.TryAdd(armor);

            // No matching stencil cell → first-empty = slot 0
            Assert.AreSame(armor, bag.GetSlotItem(0), "Armor falls back to first empty slot (0)");
        }

        [TestMethod]
        public void Provider_OutOfRangeRow_CellSkipped()
        {
            // Place ShieldMastery at anchor (0, 0): cells at gridY=0 (not a bag row) → provider returns -1
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 0, anchorY: 0);
            var bag = MakeBagWithProvider(120, svc);

            var sword = GearItems.ShortSword();
            bag.TryAdd(sword);

            // Cell at row 0 is outside rows 3-8, so provider returns -1; item goes to first-empty = slot 0
            Assert.AreSame(sword, bag.GetSlotItem(0), "Out-of-range row must be skipped; sword falls to slot 0");
        }

        [TestMethod]
        public void Provider_OutOfRangeColumn_CellSkipped()
        {
            // Place ShieldMastery at anchor (19, 3): Sword cell gridX=19 (ok), Shield cell gridX=20 (out of range col 0-19)
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 19, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            // Sword at (19,3) → bagIndex = (3-3)*20+19 = 19 (valid)
            var sword = GearItems.ShortSword();
            bag.TryAdd(sword);
            Assert.AreSame(sword, bag.GetSlotItem(19), "Sword at valid col 19 should land at bagIndex 19");

            // Shield at (20,3) → col 20 is out of range; falls to first-empty non-19 slot = slot 0
            var shield = GearItems.IronShield();
            bag.TryAdd(shield);
            Assert.AreSame(shield, bag.GetSlotItem(0), "Shield cell col 20 is out of range; falls to first empty");
        }

        [TestMethod]
        public void Provider_OccupiedCell_SkippedAndFallsToNextMatchOrFirstEmpty()
        {
            // anchor (0,3): Sword→slot 0, Shield→slot 1
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 0, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            // Pre-occupy slot 0 (the sword cell)
            var existingSword = GearItems.RustyBlade();
            bag.SetSlotItem(0, existingSword);

            // Adding a new sword: slot 0 is occupied → provider skips it → no more sword cells → returns -1 → first-empty = slot 1
            var newSword = GearItems.ShortSword();
            bag.TryAdd(newSword);

            Assert.AreSame(existingSword, bag.GetSlotItem(0), "Pre-placed sword stays at slot 0");
            Assert.AreSame(newSword, bag.GetSlotItem(1), "New sword falls back to first-empty (slot 1)");
        }

        [TestMethod]
        public void Provider_TwoStencils_EarlierRecordWins()
        {
            // First stencil: ShieldMastery at (0,3) → Sword at bagIndex 0
            // Second stencil: ShieldMastery at (2,3) → Sword at bagIndex 2
            var svc = new GameStateService();
            svc.SetPlacedStencil(ShieldMasteryId, anchorX: 0, anchorY: 3);
            // A different pattern whose first cell is also a WeaponSword would be ideal, but
            // to keep the test simple we can add a second record with a different anchor.
            // The provider must pick the FIRST matching empty cell in list order.
            // We can reuse the same pattern id — SetPlacedStencil upserts, so we need to
            // use a second pattern. Use knight.heavy_fortification which starts with ArmorMail
            // and doesn't clash. For the sword case, let's just add a *different* pattern
            // that also requires WeaponSword as its first offset: CreateHolyStrike starts
            // at (0,0)=WeaponSword.  Its id is "knight.holy_strike".
            //
            // Actually a cleaner approach: use two records for *different* patterns, both
            // having a WeaponSword cell in a valid row, and verify the one placed first wins.
            //
            // CreateSwordProficiency: three WeaponSword in a row — first cell at (0,0).
            svc.SetPlacedStencil("knight.sword_proficiency", anchorX: 5, anchorY: 3);

            var bag = MakeBagWithProvider(120, svc);
            var sword = GearItems.ShortSword();
            bag.TryAdd(sword);

            // ShieldMastery was added first; its sword cell is at (0,3) → bagIndex 0.
            Assert.AreSame(sword, bag.GetSlotItem(0),
                "Sword must land in the earlier-recorded stencil's cell (bagIndex 0), not the later one (bagIndex 5)");
            Assert.IsNull(bag.GetSlotItem(5), "Later stencil's slot must remain empty");
        }

        [TestMethod]
        public void Provider_NullProvider_BehavesAsFirstEmpty()
        {
            var bag = new ItemBag("Test", 10);
            // SlotPreferenceProvider left as null (default)
            var sword = GearItems.ShortSword();
            bag.TryAdd(sword);
            Assert.AreSame(sword, bag.GetSlotItem(0), "Null provider → item lands at slot 0 as before");
        }

        // ---- Consumable stacking wins over provider ----------------------------------

        [TestMethod]
        public void Provider_ConsumableStacking_WinsOverPreference()
        {
            // anchor (0,3) matches WeaponSword; but adding a potion to a bag that already
            // has a potion stack should absorb into the existing stack, not call the provider.
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 0, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            // Pre-place a potion at slot 5 (not a stencil cell)
            var existingPotion = PotionItems.HPPotion();
            bag.SetSlotItem(5, existingPotion);
            Assert.AreEqual(1, existingPotion.StackCount);

            // Add another potion — must stack into slot 5
            var incomingPotion = PotionItems.HPPotion();
            bag.TryAdd(incomingPotion);

            Assert.AreEqual(2, existingPotion.StackCount,
                "Consumable stacking must absorb the incoming potion into the existing stack at slot 5");
            Assert.AreEqual(1, bag.Count,
                "Bag count must still be 1 (the stack at slot 5; stacking doesn't add a slot)");
        }

        [TestMethod]
        public void Provider_ConsumableStacking_DoesNotMoveStackToPreferredSlot()
        {
            // If stacking wins the item never gets re-slotted to the stencil cell.
            var svc = MakeServiceWithStencil(ShieldMasteryId, anchorX: 0, anchorY: 3);
            var bag = MakeBagWithProvider(120, svc);

            var potion = PotionItems.HPPotion();
            bag.SetSlotItem(10, potion);

            var incoming = PotionItems.HPPotion();
            bag.TryAdd(incoming);

            // Slot 10 absorbed; stencil cells 0 and 1 must remain empty.
            Assert.IsNull(bag.GetSlotItem(0), "Stencil sword cell must remain empty after stacking");
            Assert.IsNull(bag.GetSlotItem(1), "Stencil shield cell must remain empty after stacking");
        }
    }
}
