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

        [TestMethod]
        public void BagItems_CreateCorrectRarities()
        {
            Assert.AreEqual(ItemRarity.Normal, BagItems.StandardBag().Rarity);
            Assert.AreEqual(ItemRarity.Uncommon, BagItems.ForagersBag().Rarity);
            Assert.AreEqual(ItemRarity.Rare, BagItems.TravellersBag().Rarity);
            Assert.AreEqual(ItemRarity.Epic, BagItems.AdventurersBag().Rarity);
            Assert.AreEqual(ItemRarity.Legendary, BagItems.MerchantsBag().Rarity);
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