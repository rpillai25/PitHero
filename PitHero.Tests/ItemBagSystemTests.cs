using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Jobs.Secondary;
using RolePlayingFramework.Jobs.Tertiary;

namespace PitHero.Tests
{
    [TestClass]
    public class ItemBagTests
    {
        [TestMethod]
        public void ItemBag_DefaultConstruction_HasStandardBagProperties()
        {
            var bag = new ItemBag();

            Assert.AreEqual("Standard Bag", bag.BagName);
            Assert.AreEqual(12, bag.Capacity);
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

        [TestMethod]
        public void ItemBag_TryUpgrade_WorksWithValidBags()
        {
            var bag = new ItemBag(); // Standard bag (8 capacity)
            var foragersBag = BagItems.ForagersBag();

            Assert.IsTrue(bag.TryUpgrade(foragersBag));
            Assert.AreEqual("Forager's Bag", bag.BagName);
            Assert.AreEqual(16, bag.Capacity);

            var merchantsBag = BagItems.MerchantsBag();
            Assert.IsTrue(bag.TryUpgrade(merchantsBag));
            Assert.AreEqual("Merchant's Bag", bag.BagName);
            Assert.AreEqual(32, bag.Capacity);
        }

        [TestMethod]
        public void ItemBag_TryUpgrade_FailsForDowngrades()
        {
            var bag = new ItemBag("Merchant's Bag", 32);
            var standardBag = BagItems.StandardBag();

            Assert.IsFalse(bag.TryUpgrade(standardBag)); // Can't downgrade
            Assert.AreEqual("Merchant's Bag", bag.BagName);
            Assert.AreEqual(32, bag.Capacity);
        }

        [TestMethod]
        public void BagItems_CreateCorrectRarities()
        {
            Assert.AreEqual(ItemRarity.Normal, BagItems.StandardBag().Rarity);
            Assert.AreEqual(ItemRarity.Uncommon, BagItems.ForagersBag().Rarity);
            Assert.AreEqual(ItemRarity.Rare, BagItems.TravellersBag().Rarity);
            Assert.AreEqual(ItemRarity.Epic, BagItems.AdventurersBag().Rarity);
            Assert.AreEqual(ItemRarity.Legendary, BagItems.MerchantsBag().Rarity);
        }

        [TestMethod]
        public void ItemBag_GetBagStats_ReturnsCorrectValues()
        {
            var (capacity1, name1) = ItemBag.GetBagStats(BagItems.StandardBag());
            Assert.AreEqual(12, capacity1);
            Assert.AreEqual("Standard Bag", name1);

            var (capacity2, name2) = ItemBag.GetBagStats(BagItems.ForagersBag());
            Assert.AreEqual(16, capacity2);
            Assert.AreEqual("Forager's Bag", name2);

            var (capacity3, name3) = ItemBag.GetBagStats(BagItems.TravellersBag());
            Assert.AreEqual(20, capacity3);
            Assert.AreEqual("Traveller's Bag", name3);

            var (capacity4, name4) = ItemBag.GetBagStats(BagItems.AdventurersBag());
            Assert.AreEqual(24, capacity4);
            Assert.AreEqual("Adventurer's Bag", name4);

            var (capacity5, name5) = ItemBag.GetBagStats(BagItems.MerchantsBag());
            Assert.AreEqual(32, capacity5);
            Assert.AreEqual("Merchant's Bag", name5);
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