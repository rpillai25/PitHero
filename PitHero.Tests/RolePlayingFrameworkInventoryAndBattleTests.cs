using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class RolePlayingFrameworkInventoryAndBattleTests
    {
        /// <summary>Equipping job-appropriate gear modifies hero stats and helps in battle.</summary>
        [TestMethod]
        public void Knight_EquipSwordAndMail_AffectsBattle()
        {
            var hero = new Hero("Knight", new Knight(), level: 3, baseStats: new StatBlock(5, 4, 5, 1));
            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, new StatBlock(1, 0, 0, 0), atk: 2);
            var mail = new Gear("Leather Mail", ItemKind.ArmorMail, new StatBlock(0, 0, 1, 0), def: 1);

            Assert.IsTrue(hero.TryEquip(sword));
            Assert.IsTrue(hero.TryEquip(mail));

            var slime = new Slime(level: 1);
            var orchestrator = new BattleOrchestrator(new SimpleAttackResolver());
            var won = orchestrator.Run(hero, slime);

            Assert.IsTrue(won, "Knight should be able to defeat a level 1 slime with basic gear.");
            Assert.IsTrue(hero.Experience > 0, "Hero should gain experience on win.");
        }

        /// <summary>Hero gains experience and levels up, increasing base stats and derived HP/MP.</summary>
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

        /// <summary>Job-based equip restrictions: Mage cannot equip swords, Knight cannot equip rods.</summary>
        [TestMethod]
        public void Equip_Restrictions_ByJob()
        {
            var mage = new Hero("Mage", new Mage(), level: 2, baseStats: new StatBlock(1, 2, 2, 6));
            var knight = new Hero("Knight", new Knight(), level: 2, baseStats: new StatBlock(6, 2, 4, 1));

            var sword = new Gear("Bronze Sword", ItemKind.WeaponSword, new StatBlock(1, 0, 0, 0));
            var rod = new Gear("Oak Rod", ItemKind.WeaponRod, new StatBlock(0, 0, 0, 1));

            Assert.IsFalse(mage.TryEquip(sword), "Mage should not be able to equip swords.");
            Assert.IsTrue(mage.TryEquip(rod), "Mage should be able to equip rods.");

            Assert.IsTrue(knight.TryEquip(sword), "Knight should be able to equip swords.");
            Assert.IsFalse(knight.TryEquip(rod), "Knight should not be able to equip rods.");
        }
    }
}
