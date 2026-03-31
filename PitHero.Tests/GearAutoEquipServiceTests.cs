using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>Tests for the GearAutoEquipService.</summary>
    [TestClass]
    public class GearAutoEquipServiceTests
    {
        #region GetGearScore Tests

        [TestMethod]
        public void GetGearScore_WithAttackBonus_ReturnsCorrectScore()
        {
            var stats = new StatBlock(1, 2, 3, 4);
            var gear = new Gear("TestWeapon", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 100, stats, atk: 10, def: 5);

            var score = GearAutoEquipService.GetGearScore(gear);

            Assert.AreEqual(1 + 2 + 3 + 4 + 10 + 5, score);
        }

        [TestMethod]
        public void GetGearScore_WithHPBonus_NormalizesCorrectly()
        {
            var stats = new StatBlock(0, 0, 0, 0);
            var gear = new Gear("TestArmor", ItemKind.ArmorMail, ItemRarity.Normal, "Test", 100, stats, hp: 50);

            var score = GearAutoEquipService.GetGearScore(gear);

            Assert.AreEqual(50 / 5, score);
        }

        [TestMethod]
        public void GetGearScore_WithMPBonus_NormalizesCorrectly()
        {
            var stats = new StatBlock(0, 0, 0, 0);
            var gear = new Gear("TestAccessory", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats, mp: 30);

            var score = GearAutoEquipService.GetGearScore(gear);

            Assert.AreEqual(30 / 3, score);
        }

        [TestMethod]
        public void GetGearScore_NullGear_ReturnsZero()
        {
            var score = GearAutoEquipService.GetGearScore(null);
            Assert.AreEqual(0, score);
        }

        #endregion

        #region IsNewGearBetter Tests

        [TestMethod]
        public void IsNewGearBetter_HigherScore_ReturnsTrue()
        {
            var stats1 = new StatBlock(1, 1, 1, 1);
            var stats2 = new StatBlock(5, 5, 5, 5);
            var gear1 = new Gear("Weak", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 100, stats1);
            var gear2 = new Gear("Strong", ItemKind.WeaponSword, ItemRarity.Rare, "Test", 200, stats2);

            var result = GearAutoEquipService.IsNewGearBetter(gear2, gear1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsNewGearBetter_LowerScore_ReturnsFalse()
        {
            var stats1 = new StatBlock(5, 5, 5, 5);
            var stats2 = new StatBlock(1, 1, 1, 1);
            var gear1 = new Gear("Strong", ItemKind.WeaponSword, ItemRarity.Rare, "Test", 200, stats1);
            var gear2 = new Gear("Weak", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 100, stats2);

            var result = GearAutoEquipService.IsNewGearBetter(gear2, gear1);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsNewGearBetter_EqualScoreAndBetterElementalResistance_ReturnsTrue()
        {
            var stats = new StatBlock(2, 2, 2, 2);
            var resistances1 = new Dictionary<ElementType, float>();
            var resistances2 = new Dictionary<ElementType, float> { { ElementType.Fire, 0.25f } };
            var elementalProps1 = new ElementalProperties(ElementType.Neutral, resistances1);
            var elementalProps2 = new ElementalProperties(ElementType.Fire, resistances2);

            var gear1 = new Gear("Normal", ItemKind.Shield, ItemRarity.Normal, "Test", 100, stats, elementalProps: elementalProps1);
            var gear2 = new Gear("FireResist", ItemKind.Shield, ItemRarity.Normal, "Test", 100, stats, elementalProps: elementalProps2);

            var result = GearAutoEquipService.IsNewGearBetter(gear2, gear1);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsNewGearBetter_EqualScoreAndNoResistanceAdvantage_ReturnsFalse()
        {
            var stats = new StatBlock(2, 2, 2, 2);
            var gear1 = new Gear("Item1", ItemKind.Shield, ItemRarity.Normal, "Test", 100, stats);
            var gear2 = new Gear("Item2", ItemKind.Shield, ItemRarity.Normal, "Test", 100, stats);

            var result = GearAutoEquipService.IsNewGearBetter(gear2, gear1);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsNewGearBetter_NullExisting_ReturnsTrue()
        {
            var stats = new StatBlock(1, 1, 1, 1);
            var gear = new Gear("NewGear", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 100, stats);

            var result = GearAutoEquipService.IsNewGearBetter(gear, null);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsNewGearBetter_NullNew_ReturnsFalse()
        {
            var stats = new StatBlock(1, 1, 1, 1);
            var gear = new Gear("ExistingGear", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 100, stats);

            var result = GearAutoEquipService.IsNewGearBetter(null, gear);

            Assert.IsFalse(result);
        }

        #endregion

        #region TryAutoEquipOnHero Tests

        [TestMethod]
        public void TryAutoEquipOnHero_EmptySlot_EquipsAndRemovesFromBag()
        {
            var hero = CreateTestHero();
            var bag = new ItemBag("Test", 10);
            var stats = new StatBlock(2, 2, 2, 2);
            var weapon = new Gear("Sword", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 100, stats, atk: 5);
            bag.TryAdd(weapon);

            var result = GearAutoEquipService.TryAutoEquipOnHero(hero, bag, weapon);

            Assert.IsTrue(result);
            Assert.IsNotNull(hero.WeaponShield1);
            Assert.AreEqual("Sword", ((IGear)hero.WeaponShield1).Name);
            Assert.AreEqual(0, bag.Count);
        }

        [TestMethod]
        public void TryAutoEquipOnHero_OccupiedSlotAndBetterGear_SwapsCorrectly()
        {
            var hero = CreateTestHero();
            var bag = new ItemBag("Test", 10);

            var weakStats = new StatBlock(1, 1, 1, 1);
            var strongStats = new StatBlock(5, 5, 5, 5);
            var weakWeapon = new Gear("WeakSword", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 50, weakStats, atk: 2);
            var strongWeapon = new Gear("StrongSword", ItemKind.WeaponSword, ItemRarity.Rare, "Test", 200, strongStats, atk: 10);

            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, weakWeapon);
            bag.TryAdd(strongWeapon);

            var result = GearAutoEquipService.TryAutoEquipOnHero(hero, bag, strongWeapon);

            Assert.IsTrue(result);
            Assert.AreEqual("StrongSword", ((IGear)hero.WeaponShield1).Name);
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual("WeakSword", bag.Items[0].Name);
        }

        [TestMethod]
        public void TryAutoEquipOnHero_OccupiedSlotAndWorseGear_DoesNotSwap()
        {
            var hero = CreateTestHero();
            var bag = new ItemBag("Test", 10);

            var weakStats = new StatBlock(1, 1, 1, 1);
            var strongStats = new StatBlock(5, 5, 5, 5);
            var weakWeapon = new Gear("WeakSword", ItemKind.WeaponSword, ItemRarity.Normal, "Test", 50, weakStats, atk: 2);
            var strongWeapon = new Gear("StrongSword", ItemKind.WeaponSword, ItemRarity.Rare, "Test", 200, strongStats, atk: 10);

            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, strongWeapon);
            bag.TryAdd(weakWeapon);

            var result = GearAutoEquipService.TryAutoEquipOnHero(hero, bag, weakWeapon);

            Assert.IsFalse(result);
            Assert.AreEqual("StrongSword", ((IGear)hero.WeaponShield1).Name);
            Assert.AreEqual(1, bag.Count);
        }

        [TestMethod]
        public void TryAutoEquipOnHero_AccessoryAndFreeSlot_Equips()
        {
            var hero = CreateTestHero();
            var bag = new ItemBag("Test", 10);
            var stats = new StatBlock(1, 1, 1, 1);
            var accessory = new Gear("Ring", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats);
            bag.TryAdd(accessory);

            var result = GearAutoEquipService.TryAutoEquipOnHero(hero, bag, accessory);

            Assert.IsTrue(result);
            Assert.IsNotNull(hero.Accessory1);
            Assert.AreEqual("Ring", ((IGear)hero.Accessory1).Name);
            Assert.AreEqual(0, bag.Count);
        }

        [TestMethod]
        public void TryAutoEquipOnHero_AccessoryAndBothSlotsFull_ReturnsFalse()
        {
            var hero = CreateTestHero();
            var bag = new ItemBag("Test", 10);
            var stats = new StatBlock(1, 1, 1, 1);
            var accessory1 = new Gear("Ring1", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats);
            var accessory2 = new Gear("Ring2", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats);
            var accessory3 = new Gear("Ring3", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats);

            hero.SetEquipmentSlot(EquipmentSlot.Accessory1, accessory1);
            hero.SetEquipmentSlot(EquipmentSlot.Accessory2, accessory2);
            bag.TryAdd(accessory3);

            var result = GearAutoEquipService.TryAutoEquipOnHero(hero, bag, accessory3);

            Assert.IsFalse(result);
            Assert.AreEqual(1, bag.Count);
        }

        [TestMethod]
        public void TryAutoEquipOnHero_AccessoryAndSlot2Free_EquipsInSlot2()
        {
            var hero = CreateTestHero();
            var bag = new ItemBag("Test", 10);
            var stats = new StatBlock(1, 1, 1, 1);
            var accessory1 = new Gear("Ring1", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats);
            var accessory2 = new Gear("Ring2", ItemKind.Accessory, ItemRarity.Normal, "Test", 100, stats);

            hero.SetEquipmentSlot(EquipmentSlot.Accessory1, accessory1);
            bag.TryAdd(accessory2);

            var result = GearAutoEquipService.TryAutoEquipOnHero(hero, bag, accessory2);

            Assert.IsTrue(result);
            Assert.IsNotNull(hero.Accessory2);
            Assert.AreEqual("Ring2", ((IGear)hero.Accessory2).Name);
            Assert.AreEqual(0, bag.Count);
        }

        #endregion

        #region Helper Methods

        /// <summary>Creates a test hero with Knight job.</summary>
        private Hero CreateTestHero()
        {
            var baseStats = new StatBlock(10, 10, 10, 10);
            var knightJob = new Knight();
            return new Hero("TestHero", knightJob, 1, baseStats);
        }

        #endregion
    }
}
