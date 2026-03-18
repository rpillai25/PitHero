using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class RolePlayingFrameworkInventoryAndBattleTests
    {
        /// <summary>Hero gains experience and levels up, increasing base stats and derived HP/AP.</summary>
        [TestMethod]
        public void Hero_LevelUp_IncreasesStats()
        {
            var hero = new Hero("Apprentice", new Mage(), level: 1, baseStats: new StatBlock(2, 2, 2, 4));
            var maxHPBefore = hero.MaxHP;
            var maxMPBefore = hero.MaxMP;

            // Add enough experience to level twice (100 for lvl1->2, 200 for lvl2->3 total 300)
            hero.AddExperience(300);

            Assert.AreEqual(3, hero.Level, "Hero should be level 3 after gaining 300 XP.");
            Assert.IsTrue(hero.MaxHP > maxHPBefore, "MaxHP should increase after leveling.");
            Assert.IsTrue(hero.MaxMP > maxMPBefore, "MaxMP should increase after leveling.");
        }

        /// <summary>All jobs can equip any weapon or armor type in the appropriate slot.</summary>
        [TestMethod]
        public void Equip_NoJobRestrictions_AnyJobCanEquipAnyGear()
        {
            var mage = new Hero("Mage", new Mage(), level: 2, baseStats: new StatBlock(1, 2, 2, 6));
            var knight = new Hero("Knight", new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A bronze sword", 10, new StatBlock(1, 0, 0, 0));
            var rod = new Gear("Oak Rod", ItemKind.WeaponRod, ItemRarity.Normal, "An oak rod", 10, new StatBlock(0, 0, 0, 1));
            var staff = new Gear("Walking Stick", ItemKind.WeaponStaff, ItemRarity.Normal, "A walking stick", 10, new StatBlock(0, 0, 0, 1));
            var robe = new Gear("Tattered Cloth", ItemKind.ArmorRobe, ItemRarity.Normal, "Worn cloth garments", 10, new StatBlock(0, 0, 1, 0));

            Assert.IsTrue(mage.TryEquip(sword), "Mage should be able to equip swords.");
            Assert.IsTrue(mage.TryEquip(rod), "Mage should be able to equip rods.");

            Assert.IsTrue(knight.TryEquip(sword), "Knight should be able to equip swords.");
            Assert.IsTrue(knight.TryEquip(rod), "Knight should be able to equip rods.");
            Assert.IsTrue(knight.TryEquip(staff), "Knight should be able to equip staves.");
            Assert.IsTrue(knight.TryEquip(robe), "Knight should be able to equip robes.");
        }

        /// <summary>SetEquipmentSlot accepts any weapon/armor/hat type regardless of job.</summary>
        [TestMethod]
        public void SetEquipmentSlot_NoJobRestrictions_AcceptsAllTypes()
        {
            var knight = new Hero("Knight", new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var staff = new Gear("Walking Stick", ItemKind.WeaponStaff, ItemRarity.Normal, "A walking stick", 10, new StatBlock(0, 0, 0, 1));
            var robe = new Gear("Tattered Cloth", ItemKind.ArmorRobe, ItemRarity.Normal, "Worn cloth garments", 10, new StatBlock(0, 0, 1, 0));
            var wizardHat = new Gear("Pointy Hat", ItemKind.HatWizard, ItemRarity.Normal, "A wizard hat", 10, new StatBlock(0, 0, 0, 1));

            Assert.IsTrue(knight.SetEquipmentSlot(EquipmentSlot.WeaponShield1, staff), "Knight should equip staff via SetEquipmentSlot.");
            Assert.IsTrue(knight.SetEquipmentSlot(EquipmentSlot.Armor, robe), "Knight should equip robe via SetEquipmentSlot.");
            Assert.IsTrue(knight.SetEquipmentSlot(EquipmentSlot.Hat, wizardHat), "Knight should equip wizard hat via SetEquipmentSlot.");
        }

        /// <summary>SetEquipmentSlot still rejects wrong item types for the slot.</summary>
        [TestMethod]
        public void SetEquipmentSlot_RejectsWrongSlotType()
        {
            var knight = new Hero("Knight", new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A bronze sword", 10, new StatBlock(1, 0, 0, 0));
            var robe = new Gear("Tattered Cloth", ItemKind.ArmorRobe, ItemRarity.Normal, "Worn cloth garments", 10, new StatBlock(0, 0, 1, 0));

            Assert.IsFalse(knight.SetEquipmentSlot(EquipmentSlot.Armor, sword), "Weapon should not go in armor slot.");
            Assert.IsFalse(knight.SetEquipmentSlot(EquipmentSlot.WeaponShield1, robe), "Armor should not go in weapon slot.");
        }
    }
}
