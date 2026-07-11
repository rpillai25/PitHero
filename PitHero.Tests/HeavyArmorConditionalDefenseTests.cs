using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Verifies that the knight.heavy_armor passive grants its +2 defense bonus
    /// only when ArmorMail is equipped, for both Hero and Mercenary (Phase 5).
    /// </summary>
    [TestClass]
    public class HeavyArmorConditionalDefenseTests
    {
        // ── Helper: Knight Hero with HeavyArmor skill ─────────────────────────────────

        private static Hero MakeKnightHeroWithHeavyArmor()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 2, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("Arthur", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            hero.EarnJP(200);
            var knight = new Knight();
            hero.TryPurchaseSkill(knight.Skills[1]); // Heavy Armor (100 JP)
            return hero;
        }

        // ── Hero: no armor equipped → bonus not applied ───────────────────────────────

        [TestMethod]
        [TestCategory("HeavyArmor")]
        public void Hero_HeavyArmor_WithoutAnyArmor_HeavyArmorDefenseBonusIsTwo_ButNotInBattleStats()
        {
            var hero = MakeKnightHeroWithHeavyArmor();

            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus,
                "HeavyArmorPassive must set HeavyArmorDefenseBonus = 2");
            Assert.AreEqual(0, hero.PassiveDefenseBonus,
                "PassiveDefenseBonus must stay 0; the +2 is gated on ArmorMail");

            // With no armor in slot, bonus must NOT appear in GetBattleStats().Defense
            int def = hero.GetBattleStats().Defense;

            // Now equip mail armor — defense must rise by exactly 2
            var mail = new Gear("TestMail", ItemKind.ArmorMail, ItemRarity.Normal, "Mail", 100,
                new StatBlock(0, 0, 0, 0), def: 0);
            hero.TryEquip(mail);
            int defWithMail = hero.GetBattleStats().Defense;

            Assert.AreEqual(def + 2, defWithMail,
                "Equipping ArmorMail should add exactly +2 HeavyArmorDefenseBonus to battle defense");
        }

        // ── Hero: ArmorMail equipped → bonus applied ─────────────────────────────────

        [TestMethod]
        [TestCategory("HeavyArmor")]
        public void Hero_HeavyArmor_WithMailArmor_AddsDefenseBonusToGetBattleStats()
        {
            var hero = MakeKnightHeroWithHeavyArmor();
            int defNoArmor = hero.GetBattleStats().Defense;

            var mail = new Gear("TestMail", ItemKind.ArmorMail, ItemRarity.Normal, "Mail", 100,
                new StatBlock(0, 0, 0, 0), def: 0);
            bool equipped = hero.TryEquip(mail);
            Assert.IsTrue(equipped, "Knight hero must be able to equip ArmorMail");

            Assert.AreEqual(defNoArmor + 2, hero.GetBattleStats().Defense,
                "HeavyArmorDefenseBonus of +2 must be added to defense when ArmorMail is equipped");
        }

        // ── Mercenary: no armor equipped → bonus not applied ─────────────────────────

        [TestMethod]
        [TestCategory("HeavyArmor")]
        public void Merc_HeavyArmor_WithoutMailArmor_DoesNotAddDefenseBonus()
        {
            var merc = new Mercenary("Test", new Knight(), level: 5, baseStats: new StatBlock(5, 5, 5, 5));
            merc.LearnSkill(new HeavyArmorPassive());

            Assert.AreEqual(2, merc.HeavyArmorDefenseBonus,
                "HeavyArmorPassive must set HeavyArmorDefenseBonus = 2");
            Assert.AreEqual(0, merc.PassiveDefenseBonus,
                "PassiveDefenseBonus must stay 0; the +2 is gated on ArmorMail");

            // No armor — bonus must not contribute to defense
            var stats = merc.GetTotalStats();
            int expectedDef = stats.Vitality + merc.PassiveDefenseBonus;
            Assert.AreEqual(expectedDef, merc.GetBattleStats().Defense,
                "HeavyArmorDefenseBonus must not affect defense when no ArmorMail is equipped");
        }

        // ── Mercenary: ArmorMail equipped → bonus applied ────────────────────────────

        [TestMethod]
        [TestCategory("HeavyArmor")]
        public void Merc_HeavyArmor_WithMailArmor_AddsDefenseBonusToGetBattleStats()
        {
            var merc = new Mercenary("Test", new Knight(), level: 5, baseStats: new StatBlock(5, 5, 5, 5));
            merc.LearnSkill(new HeavyArmorPassive());

            int defWithoutArmor = merc.GetBattleStats().Defense;

            var mail = new Gear("TestMail", ItemKind.ArmorMail, ItemRarity.Normal, "Mail", 100,
                new StatBlock(0, 0, 0, 0), def: 0);
            bool equipped = merc.Equip(mail);
            Assert.IsTrue(equipped, "Knight merc must be able to equip ArmorMail");

            Assert.AreEqual(defWithoutArmor + 2, merc.GetBattleStats().Defense,
                "HeavyArmorDefenseBonus of +2 must be included in defense when ArmorMail is equipped");
        }
    }
}
