using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.VirtualGame;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Virtual Game Layer tests for the Mercenary Dismissal feature.
    ///
    /// Covers two flows:
    ///   1. Party Dismissal  – dismiss a hired mercenary; gear returns to hero inventory
    ///      (or overflows to the Second Chance Vault when the inventory is full).
    ///   2. Tavern Dismissal – dismiss a tavern mercenary; the entry is removed and a
    ///      walk-out animation would be triggered in the live ECS layer.
    ///
    /// Also verifies context-menu availability rules:
    ///   - The context menu is always shown for tavern mercenaries.
    ///   - The "Hire" option is hidden when 2 mercenaries are already hired.
    /// </summary>
    [TestClass]
    public class MercenaryDismissalVGLTests
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Helpers

        private static Mercenary MakeKnight(string name = "Kay", int level = 5)
        {
            return new Mercenary(name, new Knight(), level, new StatBlock(8, 5, 7, 2));
        }

        private static Mercenary MakeMage(string name = "Merlin", int level = 5)
        {
            return new Mercenary(name, new Mage(), level, new StatBlock(2, 4, 3, 9));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Party dismissal – basic flow

        /// <summary>
        /// Dismissing a hired mercenary that carries no gear should remove them from the
        /// hired list and return a result with zero items transferred.
        /// </summary>
        [TestMethod]
        public void PartyDismissal_NoGear_RemovesMercenaryFromHiredList()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Kay");
            party.AddHired(merc);

            var inventory = new Inventory(capacity: 10);
            var vault = new SecondChanceMerchantVault();

            DismissalResult result = party.DismissHiredMercenary(0, inventory, vault);

            Assert.AreEqual(0, party.HiredCount, "Hired count should be 0 after dismissal.");
            Assert.AreEqual("Kay", result.MercenaryName, "Result should carry dismissed mercenary name.");
            Assert.AreEqual(0, result.ItemsReturnedToInventory, "No items should go to inventory.");
            Assert.AreEqual(0, result.ItemsOverflowedToVault, "No items should go to vault.");
        }

        /// <summary>
        /// Gear equipped by a dismissed hired mercenary should be placed in the hero inventory
        /// when capacity is available.
        /// </summary>
        [TestMethod]
        public void PartyDismissal_GearEquipped_GearReturnedToInventory()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Lancelot");
            merc.Equip(GearItems.LongSword());
            party.AddHired(merc);

            var inventory = new Inventory(capacity: 10);
            var vault = new SecondChanceMerchantVault();

            DismissalResult result = party.DismissHiredMercenary(0, inventory, vault);

            Assert.AreEqual(0, party.HiredCount, "Hired count should be 0 after dismissal.");
            Assert.AreEqual(1, result.ItemsReturnedToInventory, "Sword should go to inventory.");
            Assert.AreEqual(0, result.ItemsOverflowedToVault, "Nothing should overflow to vault.");
            Assert.AreEqual(1, inventory.Items.Count, "Inventory should contain the sword.");
        }

        /// <summary>
        /// All equipped gear slots (weapon, armor, hat, second weapon, two accessories)
        /// should be collected and returned to inventory on dismissal.
        /// </summary>
        [TestMethod]
        public void PartyDismissal_AllGearSlotsFilled_AllGearReturnedToInventory()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Galahad");
            merc.Equip(GearItems.LongSword());
            merc.Equip(GearItems.IronArmor());
            merc.Equip(GearItems.IronHelm());
            // Knights cannot equip WeaponShield2 slot with a second sword; skip that slot.
            merc.Equip(GearItems.ProtectRing());  // Accessory1
            merc.Equip(GearItems.NecklaceOfHealth()); // Accessory2
            party.AddHired(merc);

            var inventory = new Inventory(capacity: 10);
            var vault = new SecondChanceMerchantVault();

            DismissalResult result = party.DismissHiredMercenary(0, inventory, vault);

            int total = result.ItemsReturnedToInventory + result.ItemsOverflowedToVault;
            Assert.IsTrue(total >= 4, $"At least 4 gear pieces should be returned; got {total}.");
            Assert.AreEqual(0, result.ItemsOverflowedToVault, "Inventory had capacity for all items.");
            Assert.AreEqual(0, party.HiredCount, "Mercenary should be removed from hired list.");
        }

        /// <summary>
        /// When the hero inventory is full, gear that does not fit should overflow into the
        /// Second Chance Vault so that no items are lost.
        /// Only one gear item is equipped here to avoid triggering vault stacking logic
        /// (IsSameItem calls Gear.Name which requires Nez services; stacking only runs when
        /// the vault already contains at least one item of the same type).
        /// </summary>
        [TestMethod]
        public void PartyDismissal_InventoryFull_GearOverflowsToVault()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Percival");
            merc.Equip(GearItems.LongSword()); // one piece of gear
            party.AddHired(merc);

            // Fill inventory to capacity so nothing fits
            var inventory = new Inventory(capacity: 0);
            var vault = new SecondChanceMerchantVault();

            DismissalResult result = party.DismissHiredMercenary(0, inventory, vault);

            Assert.AreEqual(0, result.ItemsReturnedToInventory, "Inventory is full; no items should go there.");
            Assert.AreEqual(1, result.ItemsOverflowedToVault, "The sword should overflow to vault.");
            Assert.AreEqual(1, vault.Stacks.Count, "Vault should hold the overflow item.");
        }

        /// <summary>
        /// When only part of the inventory capacity remains, items that fit go to inventory
        /// and the rest overflow to the vault.
        /// </summary>
        [TestMethod]
        public void PartyDismissal_PartialInventoryCapacity_SplitBetweenInventoryAndVault()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Tristan");
            merc.Equip(GearItems.LongSword());
            merc.Equip(GearItems.IronArmor());
            party.AddHired(merc);

            // Inventory can hold exactly one item
            var inventory = new Inventory(capacity: 1);
            var vault = new SecondChanceMerchantVault();

            DismissalResult result = party.DismissHiredMercenary(0, inventory, vault);

            Assert.AreEqual(1, result.ItemsReturnedToInventory, "One item should fit in inventory.");
            Assert.AreEqual(1, result.ItemsOverflowedToVault, "One item should overflow to vault.");
        }

        /// <summary>
        /// After dismissal the mercenary's equipment slots must all be null, ensuring
        /// no item references remain on the dismissed mercenary object.
        /// </summary>
        [TestMethod]
        public void PartyDismissal_AfterDismissal_MercenaryHasNoGear()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Bors");
            merc.Equip(GearItems.LongSword());
            merc.Equip(GearItems.IronArmor());
            party.AddHired(merc);

            var inventory = new Inventory(capacity: 10);
            var vault = new SecondChanceMerchantVault();

            party.DismissHiredMercenary(0, inventory, vault);

            Assert.IsNull(merc.WeaponShield1, "WeaponShield1 should be cleared after dismissal.");
            Assert.IsNull(merc.Armor, "Armor should be cleared after dismissal.");
            Assert.IsNull(merc.Hat, "Hat should be cleared after dismissal.");
            Assert.IsNull(merc.WeaponShield2, "WeaponShield2 should be cleared after dismissal.");
            Assert.IsNull(merc.Accessory1, "Accessory1 should be cleared after dismissal.");
            Assert.IsNull(merc.Accessory2, "Accessory2 should be cleared after dismissal.");
        }

        /// <summary>
        /// Dismissing one of two hired mercenaries should leave the remaining mercenary in place.
        /// </summary>
        [TestMethod]
        public void PartyDismissal_OneOfTwoHired_RemainingMercenaryUnaffected()
        {
            var party = new VirtualMercenaryParty();
            var merc1 = MakeKnight("Kay");
            var merc2 = MakeMage("Merlin");
            party.AddHired(merc1);
            party.AddHired(merc2);

            var inventory = new Inventory(capacity: 10);
            var vault = new SecondChanceMerchantVault();

            // Dismiss index 0 (Kay)
            DismissalResult result = party.DismissHiredMercenary(0, inventory, vault);

            Assert.AreEqual(1, party.HiredCount, "One mercenary should remain hired.");
            Assert.AreEqual("Kay", result.MercenaryName, "Kay should have been dismissed.");
            Assert.AreSame(merc2, party.HiredMercenaries[0], "Merlin should remain at index 0.");
        }

        /// <summary>
        /// After dismissing a hired mercenary the party can accept a new hire (back below max).
        /// </summary>
        [TestMethod]
        public void PartyDismissal_AfterDismissal_CanHireAgain()
        {
            var party = new VirtualMercenaryParty();
            party.AddHired(MakeKnight("Kay"));
            party.AddHired(MakeMage("Merlin"));

            Assert.IsFalse(party.CanHire(), "Party is full; hire should be blocked.");

            party.DismissHiredMercenary(0, new Inventory(capacity: 10), new SecondChanceMerchantVault());

            Assert.IsTrue(party.CanHire(), "After dismissal there should be room to hire again.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Tavern dismissal

        /// <summary>
        /// Dismissing a tavern mercenary should remove them from the tavern list.
        /// </summary>
        [TestMethod]
        public void TavernDismissal_RemovesMercenaryFromTavernList()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Agravain");
            party.AddToTavern(merc);

            party.DismissTavernMercenary(0);

            Assert.AreEqual(0, party.TavernCount, "Tavern should be empty after dismissal.");
        }

        /// <summary>
        /// Tavern dismissal should not affect the hired mercenaries list.
        /// </summary>
        [TestMethod]
        public void TavernDismissal_DoesNotAffectHiredMercenaries()
        {
            var party = new VirtualMercenaryParty();
            var hired = MakeKnight("Gawain");
            party.AddHired(hired);

            var tavern = MakeMage("Nimue");
            party.AddToTavern(tavern);

            party.DismissTavernMercenary(0);

            Assert.AreEqual(1, party.HiredCount, "Hired mercenary should be unaffected.");
            Assert.AreEqual(0, party.TavernCount, "Tavern should be empty.");
        }

        /// <summary>
        /// Dismissing the correct mercenary by index when multiple are in the tavern.
        /// </summary>
        [TestMethod]
        public void TavernDismissal_CorrectMercenaryRemovedByIndex()
        {
            var party = new VirtualMercenaryParty();
            var merc0 = MakeKnight("First");
            var merc1 = MakeMage("Second");
            var merc2 = MakeKnight("Third");
            party.AddToTavern(merc0);
            party.AddToTavern(merc1);
            party.AddToTavern(merc2);

            // Dismiss middle entry
            party.DismissTavernMercenary(1);

            Assert.AreEqual(2, party.TavernCount, "Two mercenaries should remain.");
            Assert.AreSame(merc0, party.TavernMercenaries[0], "merc0 should still be at index 0.");
            Assert.AreSame(merc2, party.TavernMercenaries[1], "merc2 should shift to index 1.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Context menu availability rules

        /// <summary>
        /// The tavern context menu should always be available regardless of how many
        /// mercenaries are hired (even when at the cap of 2).
        /// </summary>
        [TestMethod]
        public void ContextMenu_AlwaysAvailable_EvenWhenHiredCapReached()
        {
            var party = new VirtualMercenaryParty();
            party.AddHired(MakeKnight("Kay"));
            party.AddHired(MakeMage("Merlin"));

            Assert.IsTrue(party.ShouldShowTavernContextMenu(),
                "Context menu must be shown even when 2 mercenaries are hired.");
        }

        /// <summary>
        /// The "Hire" option inside the context menu should only be visible when fewer than
        /// 2 mercenaries are hired.
        /// </summary>
        [TestMethod]
        public void ContextMenu_HireOption_HiddenWhenAtMax()
        {
            var party = new VirtualMercenaryParty();
            party.AddHired(MakeKnight("Kay"));
            party.AddHired(MakeMage("Merlin"));

            Assert.IsFalse(party.ShouldShowHireOption(),
                "Hire option should be hidden when the party is full.");
        }

        /// <summary>
        /// The "Hire" option should be visible when the party is not yet full.
        /// </summary>
        [TestMethod]
        public void ContextMenu_HireOption_VisibleWhenBelowMax()
        {
            var party = new VirtualMercenaryParty();
            party.AddHired(MakeKnight("Kay")); // Only one hired

            Assert.IsTrue(party.ShouldShowHireOption(),
                "Hire option should be visible when fewer than 2 are hired.");
        }

        /// <summary>
        /// The "Hire" option should be visible when no mercenaries are hired at all.
        /// </summary>
        [TestMethod]
        public void ContextMenu_HireOption_VisibleWhenNoneHired()
        {
            var party = new VirtualMercenaryParty();

            Assert.IsTrue(party.ShouldShowHireOption(),
                "Hire option should be visible when no one is hired.");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Hire-from-tavern

        /// <summary>
        /// HireFromTavern moves the mercenary from the tavern list to the hired list.
        /// </summary>
        [TestMethod]
        public void HireFromTavern_MovesFromTavernToHired()
        {
            var party = new VirtualMercenaryParty();
            var merc = MakeKnight("Lamorak");
            party.AddToTavern(merc);

            bool hired = party.HireFromTavern(merc);

            Assert.IsTrue(hired, "Hire should succeed.");
            Assert.AreEqual(0, party.TavernCount, "Tavern should be empty after hiring.");
            Assert.AreEqual(1, party.HiredCount, "Hired count should be 1.");
            Assert.AreSame(merc, party.HiredMercenaries[0], "Hired list should reference the same mercenary.");
        }

        /// <summary>
        /// HireFromTavern should fail when the hired party is already at the maximum of 2.
        /// </summary>
        [TestMethod]
        public void HireFromTavern_FailsWhenHiredCapReached()
        {
            var party = new VirtualMercenaryParty();
            party.AddHired(MakeKnight("Kay"));
            party.AddHired(MakeMage("Merlin"));

            var extra = MakeKnight("Lamorak");
            party.AddToTavern(extra);

            bool hired = party.HireFromTavern(extra);

            Assert.IsFalse(hired, "Hire should fail when party is full.");
            Assert.AreEqual(1, party.TavernCount, "Extra mercenary should still be in tavern.");
            Assert.AreEqual(2, party.HiredCount, "Hired count should remain 2.");
        }
    }
}
