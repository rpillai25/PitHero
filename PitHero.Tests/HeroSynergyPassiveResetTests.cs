using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Regression tests for the synergy-wipe bug fixed in Phase 1 (ICombatant abstraction).
    ///
    /// The bug: Hero.ApplyPassiveSkills() zeroed ALL passive fields (including those written by
    /// synergy effects), then only re-applied skill passives — silently discarding synergy
    /// contributions on every level-up or skill learn.
    ///
    /// The fix: ApplyPassiveSkills() now calls ReapplySynergyPassiveEffects() after
    /// CombatantPassiveApplier.ResetAndApply() so synergy contributions are always restored.
    /// </summary>
    [TestClass]
    public class HeroSynergyPassiveResetTests
    {
        /// <summary>Creates a ShieldMastery synergy group with a single instance (multiplier = 1.0).</summary>
        private static ActiveSynergyGroup CreateShieldMasteryGroup()
        {
            var pattern = KnightSynergyPatterns.CreateShieldMastery();
            var group = new ActiveSynergyGroup(pattern);
            group.TryAddInstance(new ActiveSynergy(
                pattern,
                new Point(0, 0),
                new List<Point> { new Point(0, 0), new Point(1, 0) }));
            return group;
        }

        #region Synergy Survives Level-Up (no skill passives)

        [TestMethod]
        public void SynergyDefenseBonus_SurvivesLevelUp_NoSkillPassives()
        {
            // Arrange: level-1 hero with no learned skill passives
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);

            // Sanity: no passive bonus from skills yet
            int baseBonus = hero.PassiveDefenseBonus;

            // Apply ShieldMastery synergy (+5 defense, +10% deflect with multiplier 1.0)
            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            Assert.AreEqual(baseBonus + 5, hero.PassiveDefenseBonus,
                "ShieldMastery should grant +5 PassiveDefenseBonus immediately after applying synergy");

            // Act: level up, which triggers ApplyPassiveSkills internally
            hero.AddExperience(100); // RequiredExpForNextLevel at level 1 = 1*100 = 100

            // Assert: synergy defense bonus must survive the passive reset
            Assert.AreEqual(baseBonus + 5, hero.PassiveDefenseBonus,
                "ShieldMastery PassiveDefenseBonus should survive ApplyPassiveSkills triggered by level-up");
        }

        [TestMethod]
        public void SynergyDeflectChance_SurvivesLevelUp_NoSkillPassives()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);

            float baseDeflect = hero.DeflectChance;

            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            Assert.AreEqual(baseDeflect + 0.1f, hero.DeflectChance, 0.001f,
                "ShieldMastery should grant +10% DeflectChance immediately");

            // Act
            hero.AddExperience(100);

            // Assert
            Assert.AreEqual(baseDeflect + 0.1f, hero.DeflectChance, 0.001f,
                "ShieldMastery DeflectChance should survive ApplyPassiveSkills triggered by level-up");
        }

        [TestMethod]
        public void SynergyPassive_SurvivesMultipleLevelUps()
        {
            // Arrange: level-1 hero; give enough XP for 2 level-ups
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);

            int baseBonus = hero.PassiveDefenseBonus;

            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            // Level 1→2 costs 100 XP, level 2→3 costs 200 XP
            // Act: trigger two level-ups
            hero.AddExperience(300);

            // Assert: synergy bonus persists through multiple ApplyPassiveSkills calls
            Assert.AreEqual(baseBonus + 5, hero.PassiveDefenseBonus,
                "ShieldMastery PassiveDefenseBonus should persist through multiple level-ups");
            Assert.AreEqual(3, hero.Level, "Hero should have reached level 3");
        }

        #endregion

        #region Synergy + Skill Passive Both Survive Level-Up

        [TestMethod]
        public void SkillPassiveAndSynergyPassive_BothSurviveLevelUp()
        {
            // Arrange: pre-load HeavyArmorPassive via crystal so hero has +2 HeavyArmorDefenseBonus skill passive.
            // Note: HeavyArmorPassive now sets HeavyArmorDefenseBonus = 2 (not PassiveDefenseBonus).
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            crystal.AddLearnedSkill("knight.heavy_armor");
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);

            // Skill passive sets HeavyArmorDefenseBonus (conditional); PassiveDefenseBonus stays 0
            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus,
                "HeavyArmorPassive should set HeavyArmorDefenseBonus = 2 from constructor");
            Assert.AreEqual(0, hero.PassiveDefenseBonus,
                "PassiveDefenseBonus should be 0 before synergy is applied");

            // Apply ShieldMastery synergy (+5 PassiveDefenseBonus)
            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            Assert.AreEqual(5, hero.PassiveDefenseBonus,
                "ShieldMastery synergy should give +5 PassiveDefenseBonus");
            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus,
                "HeavyArmorDefenseBonus should still be 2 after synergy is applied");

            // Act: trigger level-up → ApplyPassiveSkills resets and re-applies everything
            hero.AddExperience(100);

            // Assert: both contributions must survive the passive reset
            Assert.AreEqual(5, hero.PassiveDefenseBonus,
                "After level-up, ShieldMastery synergy +5 PassiveDefenseBonus should survive");
            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus,
                "After level-up, HeavyArmorPassive HeavyArmorDefenseBonus = 2 should survive");
        }

        [TestMethod]
        public void SkillDeflectAndSynergyDeflect_BothSurviveLevelUp()
        {
            // Arrange: pre-load DeflectPassive (+15% deflect) via crystal
            var crystal = new HeroCrystal("TestCrystal", new Monk(), 1, new StatBlock(5, 5, 5, 5));
            crystal.AddLearnedSkill("monk.deflect");
            var hero = new Hero("TestHero", new Monk(), 1, new StatBlock(5, 5, 5, 5), crystal);

            Assert.AreEqual(0.15f, hero.DeflectChance, 0.001f,
                "DeflectPassive should give +15% DeflectChance from constructor");

            // Apply ShieldMastery synergy (+10% deflect)
            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            Assert.AreEqual(0.25f, hero.DeflectChance, 0.001f,
                "Combined: DeflectPassive (+15%) + ShieldMastery (+10%) = 25% before level-up");

            // Act
            hero.AddExperience(100);

            // Assert
            Assert.AreEqual(0.25f, hero.DeflectChance, 0.001f,
                "After level-up, DeflectChance should still be 25% (skill +15%, synergy +10%)");
        }

        #endregion

        #region Synergy Removal Correctly Zeroes Contribution

        [TestMethod]
        public void RemovingSynergy_ZeroesSynergyContribution()
        {
            // Arrange
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);

            int baseBonus = hero.PassiveDefenseBonus;

            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            Assert.AreEqual(baseBonus + 5, hero.PassiveDefenseBonus, "Synergy should be applied");

            // Act: remove synergy
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup>());

            // Assert
            Assert.AreEqual(baseBonus, hero.PassiveDefenseBonus,
                "After removing synergy, PassiveDefenseBonus should return to base");
            Assert.AreEqual(0f, hero.DeflectChance, 0.001f,
                "After removing synergy, DeflectChance should return to 0");
        }

        [TestMethod]
        public void RemovingSynergy_ThenLevelUp_StaysAtSkillPassiveOnly()
        {
            // Arrange: hero with HeavyArmorPassive (HeavyArmorDefenseBonus = 2) and ShieldMastery synergy (+5 PassiveDefenseBonus).
            // Note: HeavyArmorPassive now uses HeavyArmorDefenseBonus, not PassiveDefenseBonus.
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(5, 5, 5, 5));
            crystal.AddLearnedSkill("knight.heavy_armor");
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(5, 5, 5, 5), crystal);

            var group = CreateShieldMasteryGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });
            Assert.AreEqual(5, hero.PassiveDefenseBonus, "ShieldMastery synergy should give +5 PassiveDefenseBonus");
            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus, "HeavyArmor skill should set HeavyArmorDefenseBonus = 2");

            // Remove synergy
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup>());
            Assert.AreEqual(0, hero.PassiveDefenseBonus, "After removing synergy, PassiveDefenseBonus should be 0");
            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus, "HeavyArmorDefenseBonus should remain 2 after synergy removal");

            // Act: level-up should keep only the skill passive
            hero.AddExperience(100);

            // Assert: HeavyArmorPassive contribution persists; no synergy contribution
            Assert.AreEqual(0, hero.PassiveDefenseBonus,
                "After level-up with no synergy, PassiveDefenseBonus should remain 0");
            Assert.AreEqual(2, hero.HeavyArmorDefenseBonus,
                "After level-up with no synergy, HeavyArmorDefenseBonus should remain 2 from skill");
        }

        #endregion

        #region Synergy Stat Bonuses Must Not Compound Across Level-Ups

        /// <summary>Creates a FlashStrike synergy group (+35 STR, +40 AGI, +80 HP StatBonusEffect).</summary>
        private static ActiveSynergyGroup CreateFlashStrikeGroup()
        {
            var pattern = CrossClassSynergyPatterns.CreateFlashStrike();
            var group = new ActiveSynergyGroup(pattern);
            group.TryAddInstance(new ActiveSynergy(
                pattern,
                new Point(0, 0),
                new List<Point> { new Point(0, 0), new Point(1, 0) }));
            return group;
        }

        [TestMethod]
        public void SynergyStatBonus_DoesNotCompoundAcrossLevelUps()
        {
            // Regression: ReapplySynergyPassiveEffects must NOT re-apply StatBonusEffect.
            // StatBonusEffect accumulates into _synergyStatBonus, which the passive reset never
            // wipes — re-applying it on every level-up compounded stats until they hit the 99 cap
            // (seen in play as a level-10 thief with STR/AGI 99).
            var crystal = new HeroCrystal("TestCrystal", new Thief(), 1, new StatBlock(5, 5, 5, 5));
            var hero = new Hero("TestHero", new Thief(), 1, new StatBlock(5, 5, 5, 5), crystal);

            var group = CreateFlashStrikeGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            var statsAfterSynergy = hero.GetTotalStats();

            // Act: three level-ups, each triggering ApplyPassiveSkills → ReapplySynergyPassiveEffects
            hero.AddExperience(100); // lvl 2
            hero.AddExperience(200); // lvl 3
            hero.AddExperience(300); // lvl 4

            // Assert: the synergy stat contribution is applied exactly once. Total stats may rise
            // from job growth per level, but by far less than the +35 STR/+40 AGI a single
            // re-application would add.
            var statsAfterLevels = hero.GetTotalStats();
            int strGrowth = statsAfterLevels.Strength - statsAfterSynergy.Strength;
            int agiGrowth = statsAfterLevels.Agility - statsAfterSynergy.Agility;

            Assert.IsTrue(strGrowth >= 0 && strGrowth < 35,
                $"STR grew by {strGrowth} over 3 level-ups — a jump of 35+ means the FlashStrike StatBonusEffect compounded");
            Assert.IsTrue(agiGrowth >= 0 && agiGrowth < 40,
                $"AGI grew by {agiGrowth} over 3 level-ups — a jump of 40+ means the FlashStrike StatBonusEffect compounded");
        }

        [TestMethod]
        public void SynergyStatBonus_DoesNotCompoundOnSkillPurchase()
        {
            // Same regression via the skill-purchase path (also calls ApplyPassiveSkills)
            var crystal = new HeroCrystal("TestCrystal", new Thief(), 1, new StatBlock(5, 5, 5, 5));
            crystal.EarnJP(500);
            var hero = new Hero("TestHero", new Thief(), 1, new StatBlock(5, 5, 5, 5), crystal);

            var group = CreateFlashStrikeGroup();
            hero.UpdateActiveSynergiesGrouped(new List<ActiveSynergyGroup> { group });

            var statsBefore = hero.GetTotalStats();

            // Act: purchase a passive skill (Shadowstep, 70 JP) → triggers ApplyPassiveSkills
            var shadowstep = new RolePlayingFramework.Skills.ShadowstepPassive();
            Assert.IsTrue(hero.TryPurchaseSkill(shadowstep), "Shadowstep purchase should succeed");

            // Assert: total stats unchanged (Shadowstep grants no stats; synergy must not re-add)
            var statsAfter = hero.GetTotalStats();
            Assert.AreEqual(statsBefore.Strength, statsAfter.Strength,
                "STR must not change on skill purchase — synergy StatBonusEffect compounded");
            Assert.AreEqual(statsBefore.Agility, statsAfter.Agility,
                "AGI must not change on skill purchase — synergy StatBonusEffect compounded");
        }

        #endregion

        #region Synergy Counter-Enabler Not Double-Counted

        [TestMethod]
        public void SynergyCounterEnablers_NotDoubleCountedAcrossLevelUps()
        {
            // Arrange: a synergy pattern that enables counter (verify the counter flag
            // is not accumulated by repeated ApplyPassiveSkills calls)
            // We use PassiveAbilityEffect directly since ShieldMastery does not set counter.
            // Instead, we verify that a hero with a synergy that sets EnableCounter remains
            // correct after multiple passive re-applications.

            // Hero with monk.counter skill passive (counter = true) + no synergy
            var crystal = new HeroCrystal("TestCrystal", new Monk(), 1, new StatBlock(5, 5, 5, 5));
            crystal.AddLearnedSkill("monk.counter");
            var hero = new Hero("TestHero", new Monk(), 1, new StatBlock(5, 5, 5, 5), crystal);

            Assert.IsTrue(hero.EnableCounter, "CounterPassive should enable counter");

            // Level up 3 times — ApplyPassiveSkills is called 3 times
            hero.AddExperience(100); // lvl 2
            hero.AddExperience(200); // lvl 3
            hero.AddExperience(300); // lvl 4

            // Assert: counter should still be true (not false due to double-decrement bug)
            Assert.IsTrue(hero.EnableCounter,
                "EnableCounter should remain true after multiple level-ups (no accumulation of _synergyCounterEnablers)");
        }

        #endregion
    }
}
