using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class MercenarySkillTests
    {
        #region LearnAllJobSkills Tests

        [TestMethod]
        public void LearnAllJobSkills_Knight_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Knight should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Thief_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Thief(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Thief should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Mage should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Priest_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Priest should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Monk_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Monk should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_Archer_LearnsAllFourSkills()
        {
            var merc = new Mercenary("Test", new Archer(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Archer should have 4 skills");
        }

        [TestMethod]
        public void LearnAllJobSkills_CalledTwice_DoesNotDuplicate()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            merc.LearnAllJobSkills();
            Assert.AreEqual(4, merc.LearnedSkills.Count, "Calling LearnAllJobSkills twice should not duplicate skills");
        }

        #endregion

        #region Active Skill Tests

        [TestMethod]
        public void LearnAllJobSkills_Knight_HasActiveAttackSkills()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            bool hasActive = false;
            var skills = merc.Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].Kind == SkillKind.Active && merc.LearnedSkills.ContainsKey(skills[i].Id))
                {
                    hasActive = true;
                    break;
                }
            }
            Assert.IsTrue(hasActive, "Knight should have at least one active skill for battle");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_HasActiveAttackSkills()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            bool hasActive = false;
            var skills = merc.Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].Kind == SkillKind.Active && merc.LearnedSkills.ContainsKey(skills[i].Id))
                {
                    hasActive = true;
                    break;
                }
            }
            Assert.IsTrue(hasActive, "Mage should have at least one active skill for battle");
        }

        #endregion

        #region Passive Skill Application Tests

        [TestMethod]
        public void LearnAllJobSkills_Knight_AppliesHeavyArmorDefenseBonus()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(2, merc.HeavyArmorDefenseBonus, "Knight heavy_armor passive should set HeavyArmorDefenseBonus to 2");
            Assert.AreEqual(0, merc.PassiveDefenseBonus, "PassiveDefenseBonus should remain 0; bonus is gated on ArmorMail");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_AppliesFireDamageBonus()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.25f, merc.FireDamageBonus, 0.001f, "Mage passive should give +25% fire damage");
        }

        [TestMethod]
        public void LearnAllJobSkills_Mage_AppliesMPCostReduction()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.15f, merc.MPCostReduction, 0.001f, "Mage passive should give -15% MP cost");
        }

        [TestMethod]
        public void LearnAllJobSkills_Priest_AppliesMPTickRegen()
        {
            var merc = new Mercenary("Test", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(1, merc.MPTickRegen, "Priest passive should give +1 MP tick regen");
        }

        [TestMethod]
        public void LearnAllJobSkills_Priest_AppliesHealPowerBonus()
        {
            var merc = new Mercenary("Test", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.25f, merc.HealPowerBonus, 0.001f, "Priest passive should give +25% heal power");
        }

        [TestMethod]
        public void LearnAllJobSkills_Monk_AppliesEnableCounter()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.IsTrue(merc.EnableCounter, "Monk passive should enable counter");
        }

        [TestMethod]
        public void LearnAllJobSkills_Monk_AppliesDeflectChance()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();
            Assert.AreEqual(0.15f, merc.DeflectChance, 0.001f, "Monk passive should give 15% deflect chance");
        }

        [TestMethod]
        public void LearnAllJobSkills_NoSkills_PassivesAreZero()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            Assert.AreEqual(0, merc.PassiveDefenseBonus, "Before learning skills, passive defense should be 0");
            Assert.AreEqual(0f, merc.DeflectChance, "Before learning skills, deflect chance should be 0");
            Assert.IsFalse(merc.EnableCounter, "Before learning skills, counter should be disabled");
        }

        #endregion

        #region Mercenary Out-of-Battle Healing Tests

        [TestMethod]
        public void PriestMercenary_HasHealingSkillUsableOutsideOfBattle()
        {
            var merc = new Mercenary("Healer", new Priest(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            bool hasOutOfBattleHeal = false;
            var skills = merc.Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].Kind == SkillKind.Active && skills[i].HPRestoreAmount > 0 && !skills[i].BattleOnly)
                {
                    hasOutOfBattleHeal = true;
                    break;
                }
            }
            Assert.IsTrue(hasOutOfBattleHeal, "Priest mercenary should have a healing skill usable outside of battle");
        }

        [TestMethod]
        public void PriestMercenary_CanSpendMPForHealingSkill()
        {
            var merc = new Mercenary("Healer", new Priest(), 5, new StatBlock(5, 5, 10, 5));
            merc.LearnAllJobSkills();

            int initialMP = merc.CurrentMP;
            Assert.IsTrue(initialMP > 0, "Priest mercenary should have MP available");

            var healSkill = merc.LearnedSkills["priest.heal"];
            Assert.IsNotNull(healSkill, "Priest mercenary should have learned Heal skill");
            Assert.IsTrue(merc.CurrentMP >= healSkill.MPCost, "Priest mercenary should have enough MP for Heal");

            bool mpSpent = merc.UseMP(healSkill.MPCost);
            Assert.IsTrue(mpSpent, "Mercenary should be able to spend MP for healing skill");
            Assert.AreEqual(initialMP - healSkill.MPCost, merc.CurrentMP, "MP should decrease by skill cost");
        }

        [TestMethod]
        public void PriestMercenary_CanHealOtherMercenary()
        {
            var healer = new Mercenary("Healer", new Priest(), 5, new StatBlock(5, 5, 5, 10));
            healer.LearnAllJobSkills();

            var target = new Mercenary("Injured", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            target.TakeDamage(target.MaxHP / 2);

            int targetHPBefore = target.CurrentHP;
            Assert.IsTrue(targetHPBefore < target.MaxHP, "Target should have lost HP");

            var healSkill = healer.LearnedSkills["priest.heal"];
            int healerMPBefore = healer.CurrentMP;

            bool mpSpent = healer.UseMP(healSkill.MPCost);
            Assert.IsTrue(mpSpent, "Healer should be able to spend MP");

            bool healed = target.RestoreHP(healSkill.HPRestoreAmount);
            Assert.IsTrue(healed, "Target should be healed");
            Assert.IsTrue(target.CurrentHP > targetHPBefore, "Target HP should increase after healing");
            Assert.AreEqual(healerMPBefore - healSkill.MPCost, healer.CurrentMP, "Healer MP should decrease");
        }

        [TestMethod]
        public void PriestMercenary_CanHealHero()
        {
            var healer = new Mercenary("Healer", new Priest(), 5, new StatBlock(5, 5, 5, 10));
            healer.LearnAllJobSkills();

            var hero = new Hero("Hero", new Knight(), 5, new StatBlock(10, 10, 10, 10));
            hero.TakeDamage(hero.MaxHP / 2);

            int heroHPBefore = hero.CurrentHP;
            Assert.IsTrue(heroHPBefore < hero.MaxHP, "Hero should have lost HP");

            var healSkill = healer.LearnedSkills["priest.heal"];
            int healerMPBefore = healer.CurrentMP;

            bool mpSpent = healer.UseMP(healSkill.MPCost);
            Assert.IsTrue(mpSpent, "Healer mercenary should be able to spend MP");

            bool healed = hero.RestoreHP(healSkill.HPRestoreAmount);
            Assert.IsTrue(healed, "Hero should be healed by mercenary's healing skill");
            Assert.IsTrue(hero.CurrentHP > heroHPBefore, "Hero HP should increase after mercenary healing");
            Assert.AreEqual(healerMPBefore - healSkill.MPCost, healer.CurrentMP, "Healer mercenary MP should decrease");
        }

        [TestMethod]
        public void NonPriestMercenary_HasNoHealingSkill()
        {
            var merc = new Mercenary("Fighter", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            bool hasHealingSkill = false;
            var skills = merc.Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].HPRestoreAmount > 0 && !skills[i].BattleOnly)
                {
                    hasHealingSkill = true;
                    break;
                }
            }
            Assert.IsFalse(hasHealingSkill, "Non-priest mercenary should not have out-of-battle healing skills");
        }

        [TestMethod]
        public void PriestMercenary_CannotHealWithoutEnoughMP()
        {
            var merc = new Mercenary("Healer", new Priest(), 1, new StatBlock(1, 1, 1, 1));
            merc.LearnAllJobSkills();

            var healSkill = merc.LearnedSkills["priest.heal"];

            // Drain MP below skill cost
            while (merc.CurrentMP >= healSkill.MPCost)
            {
                merc.UseMP(1);
            }

            bool mpSpent = merc.UseMP(healSkill.MPCost);
            Assert.IsFalse(mpSpent, "Mercenary should not be able to spend MP when insufficient");
        }

        #endregion

        #region LightArmor Extra Equip Permission Tests

        [TestMethod]
        public void KnightMerc_WithLightArmorSkill_CanEquipRobe()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            var robe = new Gear("TestRobe", ItemKind.ArmorRobe, ItemRarity.Normal, "Test", 100, new StatBlock(0, 0, 0, 0));

            // Without skill: ArmorRobe defaults to Mage|Priest only, Knight cannot equip
            Assert.IsFalse(merc.CanEquipItem(robe), "Knight without light_armor should not be able to equip a robe");

            merc.LearnSkill(new LightArmorPassive());

            Assert.IsTrue(merc.CanEquipItem(robe), "Knight with light_armor should be able to equip a robe via extra permission");
        }

        [TestMethod]
        public void KnightMerc_ForgetLightArmorSkill_CanNoLongerEquipRobe()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnSkill(new LightArmorPassive());
            var robe = new Gear("TestRobe", ItemKind.ArmorRobe, ItemRarity.Normal, "Test", 100, new StatBlock(0, 0, 0, 0));

            Assert.IsTrue(merc.CanEquipItem(robe), "Knight with light_armor should be able to equip a robe");

            merc.ForgetSkill("knight.light_armor");

            Assert.IsFalse(merc.CanEquipItem(robe), "After forgetting light_armor, knight should no longer be able to equip a robe");
        }

        #endregion

        #region SpendMP Economist Reduction Tests

        [TestMethod]
        public void MageMerc_WithEconomistSkill_SpendMP_AppliesReduction()
        {
            var merc = new Mercenary("Test", new Mage(), 10, new StatBlock(5, 5, 5, 10));
            merc.LearnAllJobSkills();

            // MPCostReduction should be 0.15f from EconomistPassive
            Assert.AreEqual(0.15f, merc.MPCostReduction, 0.001f, "Economist passive should set 15% MP cost reduction");

            int mpBefore = merc.CurrentMP;
            // SpendMP(10) with 15% reduction: reduced = (int)(10 * 0.85f) = 8
            bool spent = merc.SpendMP(10);

            Assert.IsTrue(spent, "SpendMP should succeed when enough MP is available");
            Assert.AreEqual(mpBefore - 8, merc.CurrentMP, "SpendMP(10) with 15% reduction should cost 8 MP");
        }

        [TestMethod]
        public void MageMerc_WithEconomistSkill_UseMP_AlsoAppliesReduction()
        {
            var merc = new Mercenary("Test", new Mage(), 10, new StatBlock(5, 5, 5, 10));
            merc.LearnAllJobSkills();

            int mpBefore = merc.CurrentMP;
            merc.UseMP(10);

            // UseMP is an alias for SpendMP, so same reduction applies
            Assert.AreEqual(mpBefore - 8, merc.CurrentMP, "UseMP(10) with 15% reduction should cost 8 MP (UseMP aliases SpendMP)");
        }

        #endregion

        #region ForgetSkill Passive Removal Tests

        [TestMethod]
        public void ForgetSkill_RemovesHeavyArmorDefenseBonus()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnSkill(new HeavyArmorPassive());

            Assert.AreEqual(2, merc.HeavyArmorDefenseBonus, "Heavy armor passive should set HeavyArmorDefenseBonus to 2");

            merc.ForgetSkill("knight.heavy_armor");

            Assert.AreEqual(0, merc.HeavyArmorDefenseBonus, "After forgetting heavy_armor, HeavyArmorDefenseBonus should return to 0");
        }

        [TestMethod]
        public void ForgetSkill_RemovesFireDamageBonus()
        {
            var merc = new Mercenary("Test", new Mage(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            Assert.IsTrue(merc.FireDamageBonus > 0f, "Mage should have fire damage bonus after learning all skills");

            merc.ForgetSkill("mage.heart_fire");

            Assert.AreEqual(0f, merc.FireDamageBonus, 0.001f, "After forgetting heart_fire passive, fire damage bonus should return to 0");
        }

        [TestMethod]
        public void ForgetSkill_NonExistentSkill_ReturnsFalseAndDoesNotThrow()
        {
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            bool result = merc.ForgetSkill("nonexistent.skill");
            Assert.IsFalse(result, "ForgetSkill should return false for a skill that was never learned");
        }

        [TestMethod]
        public void ForgetSkill_AfterLearnAndForget_PassivesMatchFreshMerc()
        {
            var merc = new Mercenary("Test", new Monk(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnAllJobSkills();

            Assert.IsTrue(merc.EnableCounter, "Monk with counter passive should have counter enabled");
            Assert.AreEqual(0.15f, merc.DeflectChance, 0.001f, "Monk with deflect passive should have deflect chance");

            merc.ForgetSkill("monk.counter");
            merc.ForgetSkill("monk.deflect");

            Assert.IsFalse(merc.EnableCounter, "After forgetting counter, EnableCounter should be false");
            Assert.AreEqual(0f, merc.DeflectChance, 0.001f, "After forgetting deflect, DeflectChance should be 0");
        }

        #endregion

        #region HeavyArmor Conditional Defense Tests

        [TestMethod]
        public void Merc_HeavyArmor_WithRobeArmor_DoesNotAddDefenseBonus()
        {
            // Knight with light_armor can equip robes; heavy armor bonus should NOT apply to robes
            var merc = new Mercenary("Test", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            merc.LearnSkill(new HeavyArmorPassive());
            merc.LearnSkill(new LightArmorPassive());

            var robe = new Gear("TestRobe", ItemKind.ArmorRobe, ItemRarity.Normal, "Test", 100, new StatBlock(0, 0, 0, 0), def: 0);
            bool equipped = merc.Equip(robe);
            Assert.IsTrue(equipped, "Knight with light_armor should equip a robe");

            int def = merc.GetBattleStats().Defense;
            var stats = merc.GetTotalStats();
            int expected = stats.Vitality + merc.PassiveDefenseBonus; // HeavyArmorDefenseBonus NOT added
            Assert.AreEqual(expected, def,
                "HeavyArmorDefenseBonus should not apply when ArmorRobe is equipped instead of ArmorMail");
        }

        #endregion
    }
}
