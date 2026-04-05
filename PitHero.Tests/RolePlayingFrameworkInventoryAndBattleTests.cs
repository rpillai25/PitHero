using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using PitHero;

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

        /// <summary>Job-based equip restrictions: Mage cannot equip swords, Knight cannot equip rods (via AllowedJobs on gear).</summary>
        [TestMethod]
        public void Equip_Restrictions_ByJob_ViaAllowedJobs()
        {
            var mage = new Hero(JobTextKey.Job_Mage_Name, new Mage(), level: 2, baseStats: new StatBlock(1, 2, 2, 6));
            var knight = new Hero(JobTextKey.Job_Knight_Name, new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A bronze sword", 10, new StatBlock(1, 0, 0, 0));
            var rod = new Gear("Oak Rod", ItemKind.WeaponRod, ItemRarity.Normal, "An oak rod", 10, new StatBlock(0, 0, 0, 1));

            Assert.IsFalse(mage.TryEquip(sword), "Mage should not be able to equip swords.");
            Assert.IsTrue(mage.TryEquip(rod), "Mage should be able to equip rods.");

            Assert.IsTrue(knight.TryEquip(sword), "Knight should be able to equip swords.");
            Assert.IsFalse(knight.TryEquip(rod), "Knight should not be able to equip rods.");
        }

        /// <summary>AllowedJobs defaults from ItemKind: Knight can equip ArmorMail but not ArmorRobe.</summary>
        [TestMethod]
        public void Equip_AllowedJobs_DefaultsFromItemKind()
        {
            var knight = new Hero(JobTextKey.Job_Knight_Name, new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));
            var monk = new Hero(JobTextKey.Job_Monk_Name, new Monk(), level: 2, baseStats: new StatBlock(6, 4, 4, 2));

            var mail = new Gear("Iron Mail", ItemKind.ArmorMail, ItemRarity.Normal, "Heavy armor", 10, new StatBlock(0, 0, 1, 0));
            var robe = new Gear("Tattered Cloth", ItemKind.ArmorRobe, ItemRarity.Normal, "Cloth garments", 10, new StatBlock(0, 0, 0, 1));
            var gi = new Gear("Fighting Gi", ItemKind.ArmorGi, ItemRarity.Normal, "Light armor", 10, new StatBlock(0, 1, 0, 0));

            Assert.IsTrue(knight.TryEquip(mail), "Knight should equip ArmorMail.");
            Assert.IsFalse(knight.TryEquip(robe), "Knight should not equip ArmorRobe.");
            Assert.IsFalse(knight.TryEquip(gi), "Knight should not equip ArmorGi.");

            Assert.IsFalse(monk.TryEquip(mail), "Monk should not equip ArmorMail.");
            Assert.IsFalse(monk.TryEquip(robe), "Monk should not equip ArmorRobe.");
            Assert.IsTrue(monk.TryEquip(gi), "Monk should equip ArmorGi.");
        }

        /// <summary>Gear with custom AllowedJobs override permits cross-class equipping.</summary>
        [TestMethod]
        public void Equip_CustomAllowedJobs_OverridesDefault()
        {
            var knight = new Hero(JobTextKey.Job_Knight_Name, new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));
            var mage = new Hero(JobTextKey.Job_Mage_Name, new Mage(), level: 2, baseStats: new StatBlock(1, 2, 2, 6));

            // Staff normally only for Priest, but this one allows Knight and Priest
            var holyStaff = new Gear("Holy Staff", ItemKind.WeaponStaff, ItemRarity.Rare, "A blessed staff", 200,
                new StatBlock(2, 0, 0, 2), allowedJobs: JobType.Knight | JobType.Priest);

            Assert.IsTrue(knight.TryEquip(holyStaff), "Knight should equip staff with custom AllowedJobs.");
            Assert.IsFalse(mage.TryEquip(holyStaff), "Mage should not equip staff limited to Knight|Priest.");
        }

        /// <summary>SetEquipmentSlot enforces AllowedJobs restrictions on gear.</summary>
        [TestMethod]
        public void SetEquipmentSlot_EnforcesAllowedJobs()
        {
            var knight = new Hero(JobTextKey.Job_Knight_Name, new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var staff = new Gear("Walking Stick", ItemKind.WeaponStaff, ItemRarity.Normal, "A walking stick", 10, new StatBlock(0, 0, 0, 1));
            var robe = new Gear("Tattered Cloth", ItemKind.ArmorRobe, ItemRarity.Normal, "Worn cloth garments", 10, new StatBlock(0, 0, 1, 0));
            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A bronze sword", 10, new StatBlock(1, 0, 0, 0));
            var helm = new Gear("Iron Helm", ItemKind.HatHelm, ItemRarity.Normal, "An iron helm", 10, new StatBlock(0, 0, 1, 0));

            Assert.IsFalse(knight.SetEquipmentSlot(EquipmentSlot.WeaponShield1, staff), "Knight should not equip staff.");
            Assert.IsFalse(knight.SetEquipmentSlot(EquipmentSlot.Armor, robe), "Knight should not equip robe.");
            Assert.IsTrue(knight.SetEquipmentSlot(EquipmentSlot.WeaponShield1, sword), "Knight should equip sword.");
            Assert.IsTrue(knight.SetEquipmentSlot(EquipmentSlot.Hat, helm), "Knight should equip helm.");
        }

        /// <summary>SetEquipmentSlot still rejects wrong item types for the slot regardless of AllowedJobs.</summary>
        [TestMethod]
        public void SetEquipmentSlot_RejectsWrongSlotType()
        {
            var knight = new Hero(JobTextKey.Job_Knight_Name, new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, ItemRarity.Normal, "A bronze sword", 10, new StatBlock(1, 0, 0, 0));
            var mail = new Gear("Iron Mail", ItemKind.ArmorMail, ItemRarity.Normal, "Heavy armor", 10, new StatBlock(0, 0, 1, 0));

            Assert.IsFalse(knight.SetEquipmentSlot(EquipmentSlot.Armor, sword), "Weapon should not go in armor slot.");
            Assert.IsFalse(knight.SetEquipmentSlot(EquipmentSlot.WeaponShield1, mail), "Armor should not go in weapon slot.");
        }

        /// <summary>Gear.GetDefaultAllowedJobs returns correct defaults for all ItemKinds.</summary>
        [TestMethod]
        public void Gear_DefaultAllowedJobs_MatchesItemKind()
        {
            Assert.AreEqual(JobType.Knight, Gear.GetDefaultAllowedJobs(ItemKind.WeaponSword));
            Assert.AreEqual(JobType.Monk, Gear.GetDefaultAllowedJobs(ItemKind.WeaponKnuckle));
            Assert.AreEqual(JobType.Priest, Gear.GetDefaultAllowedJobs(ItemKind.WeaponStaff));
            Assert.AreEqual(JobType.Mage, Gear.GetDefaultAllowedJobs(ItemKind.WeaponRod));
            Assert.AreEqual(JobType.Thief | JobType.Mage, Gear.GetDefaultAllowedJobs(ItemKind.WeaponKnife));
            Assert.AreEqual(JobType.Archer, Gear.GetDefaultAllowedJobs(ItemKind.WeaponBow));
            Assert.AreEqual(JobType.Knight | JobType.Priest, Gear.GetDefaultAllowedJobs(ItemKind.WeaponHammer));
            Assert.AreEqual(JobType.Knight, Gear.GetDefaultAllowedJobs(ItemKind.ArmorMail));
            Assert.AreEqual(JobType.Monk, Gear.GetDefaultAllowedJobs(ItemKind.ArmorGi));
            Assert.AreEqual(JobType.Mage | JobType.Priest, Gear.GetDefaultAllowedJobs(ItemKind.ArmorRobe));
            Assert.AreEqual(JobType.All, Gear.GetDefaultAllowedJobs(ItemKind.Shield));
            Assert.AreEqual(JobType.All, Gear.GetDefaultAllowedJobs(ItemKind.Accessory));
        }

        /// <summary>CanEquipItem uses AllowedJobs and respects extra permissions.</summary>
        [TestMethod]
        public void CanEquipItem_RespectsExtraPermissions()
        {
            var knight = new Hero(JobTextKey.Job_Knight_Name, new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));
            var robe = new Gear("Tattered Cloth", ItemKind.ArmorRobe, ItemRarity.Normal, "Worn cloth garments", 10, new StatBlock(0, 0, 1, 0));

            Assert.IsFalse(knight.CanEquipItem(robe), "Knight should not equip robe by default.");

            knight.AddExtraEquipPermission(ItemKind.ArmorRobe);
            Assert.IsTrue(knight.CanEquipItem(robe), "Knight with ArmorRobe permission should equip robe.");
        }
    }
}
