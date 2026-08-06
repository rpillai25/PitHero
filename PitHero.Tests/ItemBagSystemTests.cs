using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;

namespace PitHero.Tests
{
    [TestClass]
    public class ItemBagTests
    {
        [TestMethod]
        public void ItemBag_DefaultConstruction_HasStandardBagProperties()
        {
            var bag = new ItemBag();

            Assert.AreEqual("Inventory", bag.BagName);
            Assert.AreEqual(120, bag.Capacity);
            Assert.AreEqual(0, bag.Count);
            Assert.IsFalse(bag.IsFull);
        }

        [TestMethod]
        public void ItemBag_CustomConstruction_HasSpecifiedProperties()
        {
            var bag = new ItemBag("Test Bag", 16);

            Assert.AreEqual("Test Bag", bag.BagName);
            Assert.AreEqual(16, bag.Capacity);
            Assert.AreEqual(0, bag.Count);
            Assert.IsFalse(bag.IsFull);
        }

        [TestMethod]
        public void ItemBag_TryAdd_WorksUntilCapacityReached()
        {
            var bag = new ItemBag("Test Bag", 2);
            var item1 = new Gear("Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A test sword", 10, new StatBlock(1, 0, 0, 0));
            var item2 = new Gear("Shield", ItemKind.Shield, ItemRarity.Normal, "A test shield", 10, new StatBlock(0, 0, 1, 0));
            var item3 = new Gear("Armor", ItemKind.ArmorMail, ItemRarity.Normal, "A test armor", 10, new StatBlock(0, 0, 1, 0));

            // First two items should add successfully
            Assert.IsTrue(bag.TryAdd(item1));
            Assert.AreEqual(1, bag.Count);
            Assert.IsFalse(bag.IsFull);

            Assert.IsTrue(bag.TryAdd(item2));
            Assert.AreEqual(2, bag.Count);
            Assert.IsTrue(bag.IsFull);

            // Third item should fail to add
            Assert.IsFalse(bag.TryAdd(item3));
            Assert.AreEqual(2, bag.Count);
        }

        [TestMethod]
        public void ItemBag_Remove_WorksCorrectly()
        {
            var bag = new ItemBag();
            var item1 = new Gear("Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A test sword", 10, new StatBlock(1, 0, 0, 0));
            var item2 = new Gear("Shield", ItemKind.Shield, ItemRarity.Normal, "A test shield", 10, new StatBlock(0, 0, 1, 0));

            bag.TryAdd(item1);
            bag.TryAdd(item2);
            Assert.AreEqual(2, bag.Count);

            Assert.IsTrue(bag.Remove(item1));
            Assert.AreEqual(1, bag.Count);
            Assert.IsFalse(bag.Remove(item1)); // Already removed
            Assert.IsTrue(bag.Remove(item2));
            Assert.AreEqual(0, bag.Count);
        }

        [TestMethod]
        public void ItemBag_RemoveAt_WorksCorrectly()
        {
            var bag = new ItemBag();
            var item1 = new Gear("Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A test sword", 10, new StatBlock(1, 0, 0, 0));
            var item2 = new Gear("Shield", ItemKind.Shield, ItemRarity.Normal, "A test shield", 10, new StatBlock(0, 0, 1, 0));

            bag.TryAdd(item1);
            bag.TryAdd(item2);

            Assert.IsTrue(bag.RemoveAt(0));
            Assert.AreEqual(1, bag.Count);
            Assert.IsFalse(bag.RemoveAt(5)); // Invalid index
            Assert.IsTrue(bag.RemoveAt(0));
            Assert.AreEqual(0, bag.Count);
        }

    }

    /// <summary>
    /// Tests for the IBagSlotPreferenceProvider seam introduced in issue #362.
    /// Uses a lightweight fake provider to verify ItemBag honors/ignores the hint correctly.
    /// </summary>
    [TestClass]
    public class ItemBagSlotProviderSeamTests
    {
        // A simple provider that always returns a fixed preferred slot.
        private sealed class FixedSlotProvider : RolePlayingFramework.Inventory.IBagSlotPreferenceProvider
        {
            private readonly int _slot;
            public FixedSlotProvider(int slot) { _slot = slot; }
            public int GetPreferredEmptySlot(RolePlayingFramework.Inventory.ItemBag bag, RolePlayingFramework.Equipment.IItem item) => _slot;
        }

        [TestMethod]
        public void ItemBag_Provider_PreferredEmptySlotIsHonored()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag("Test", 10);
            bag.SlotPreferenceProvider = new FixedSlotProvider(7);

            var sword = GearItems.ShortSword();
            Assert.IsTrue(bag.TryAdd(sword));

            Assert.AreSame(sword, bag.GetSlotItem(7),
                "Provider preference of slot 7 should be honored when that slot is empty");
        }

        [TestMethod]
        public void ItemBag_Provider_OutOfRangeIndexFallsBackToFirstEmpty()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag("Test", 4);
            // Provider returns index 99 which is >= capacity (4)
            bag.SlotPreferenceProvider = new FixedSlotProvider(99);

            var sword = GearItems.ShortSword();
            Assert.IsTrue(bag.TryAdd(sword));

            // Should fall back to slot 0 (first empty)
            Assert.AreSame(sword, bag.GetSlotItem(0),
                "Out-of-range preferred index must fall back to first-empty slot");
        }

        [TestMethod]
        public void ItemBag_Provider_OccupiedPreferredIndexFallsBackToFirstEmpty()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag("Test", 10);
            // Pre-occupy slot 5
            var existing = GearItems.IronHelm();
            bag.SetSlotItem(5, existing);

            // Provider always returns 5, which is occupied
            bag.SlotPreferenceProvider = new FixedSlotProvider(5);

            var incoming = GearItems.ShortSword();
            Assert.IsTrue(bag.TryAdd(incoming));

            // Slot 5 is occupied; incoming must go to the first empty slot (slot 0)
            Assert.AreSame(existing, bag.GetSlotItem(5), "Pre-existing item at slot 5 should not move");
            Assert.AreSame(incoming, bag.GetSlotItem(0),
                "Occupied preferred slot must fall back to first-empty (slot 0)");
        }

        [TestMethod]
        public void ItemBag_Provider_ConsumableStackingWinsOverPreference()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag("Test", 10);
            // Pre-stack at slot 3
            var existingPotion = PotionItems.HPPotion();
            bag.SetSlotItem(3, existingPotion);

            // Provider would steer to slot 7, but stacking must take priority
            bag.SlotPreferenceProvider = new FixedSlotProvider(7);

            var incoming = PotionItems.HPPotion();
            Assert.IsTrue(bag.TryAdd(incoming));

            Assert.AreEqual(2, existingPotion.StackCount,
                "Consumable stacking must win over provider preference — stack count should be 2");
            Assert.IsNull(bag.GetSlotItem(7),
                "Provider's preferred slot (7) must remain empty when stacking absorbed the item");
        }

        [TestMethod]
        public void ItemBag_NullProvider_BehavesAsBeforeProviderSeam()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag("Test", 10);
            // SlotPreferenceProvider defaults to null — no change in behavior expected
            Assert.IsNull(bag.SlotPreferenceProvider);

            var sword = GearItems.ShortSword();
            bag.TryAdd(sword);

            Assert.AreSame(sword, bag.GetSlotItem(0),
                "Null provider: item must land at slot 0 (first-empty scan as before)");
        }
    }

    [TestClass]
    public class RarityAndHPMPBonusTests
    {
        [TestMethod]
        public void Gear_SupportsHPAndMPBonuses()
        {
            var gear = new Gear("Vitality Ring", ItemKind.Accessory, ItemRarity.Rare, "A test ring", 100,
                new StatBlock(0, 0, 2, 0), hp: 50, mp: 20);

            Assert.AreEqual(50, gear.HPBonus);
            Assert.AreEqual(20, gear.MPBonus);
        }

        [TestMethod]
        public void Hero_CalculatesEquipmentHPMPBonuses()
        {
            var hero = new Hero("Test", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var vitalityRing = new Gear("Vitality Ring", ItemKind.Accessory, ItemRarity.Rare, "A test ring", 100,
                new StatBlock(0, 0, 0, 0), hp: 50, mp: 20);

            var baseHP = hero.MaxHP;
            var baseMP = hero.MaxMP;

            Assert.IsTrue(hero.TryEquip(vitalityRing));

            Assert.AreEqual(baseHP + 50, hero.MaxHP);
            Assert.AreEqual(baseMP + 20, hero.MaxMP);
        }

        [TestMethod]
        public void Hero_SupportsShieldEquipment()
        {
            var hero = new Hero("Test", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var shield = new Gear("Iron Shield", ItemKind.Shield, ItemRarity.Normal, "A test shield", 10,
                new StatBlock(0, 0, 1, 0), def: 5);

            Assert.IsNull(hero.WeaponShield2);
            Assert.IsTrue(hero.TryEquip(shield));
            Assert.IsNotNull(hero.WeaponShield2);
            Assert.AreEqual("Iron Shield", hero.WeaponShield2.Name);

            // Test unequip
            Assert.IsTrue(hero.TryUnequip(EquipmentSlot.WeaponShield2));
            Assert.IsNull(hero.WeaponShield2);
        }

        [TestMethod]
        public void Hero_ShieldContributesToDefense()
        {
            var hero = new Hero("Test", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var shield = new Gear("Iron Shield", ItemKind.Shield, ItemRarity.Normal, "A test shield", 10,
                new StatBlock(0, 0, 0, 0), def: 10);

            var baseDefense = hero.GetEquipmentDefenseBonus();
            Assert.IsTrue(hero.TryEquip(shield));
            Assert.AreEqual(baseDefense + 10, hero.GetEquipmentDefenseBonus());
        }

        [TestMethod]
        public void Gear_ImplementsIGearInterface()
        {
            var gear = new Gear("Vitality Ring", ItemKind.Accessory, ItemRarity.Rare, "A test ring", 100,
                new StatBlock(0, 0, 2, 0), hp: 50, mp: 20);

            // Test that Gear implements IGear
            IGear gearInterface = gear;
            Assert.IsNotNull(gearInterface);

            // Test IGear properties through interface
            Assert.AreEqual(50, gearInterface.HPBonus);
            Assert.AreEqual(20, gearInterface.MPBonus);
            Assert.AreEqual(new StatBlock(0, 0, 2, 0), gearInterface.StatBonus);

            // Test IItem properties through interface
            IItem itemInterface = gear;
            Assert.AreEqual("Vitality Ring", itemInterface.Name);
            Assert.AreEqual(ItemKind.Accessory, itemInterface.Kind);
            Assert.AreEqual(ItemRarity.Rare, itemInterface.Rarity);
        }
    }
}