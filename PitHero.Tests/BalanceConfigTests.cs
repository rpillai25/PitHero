using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the centralized balance configuration system.
    /// Validates formulas, progression curves, and stat calculations.
    /// </summary>
    [TestClass]
    public class BalanceConfigTests
    {
        #region Level Progression Tests

        [TestMethod]
        public void CalculateExperienceForLevel_Level1_ReturnsZero()
        {
            // Level 1 should require 0 experience
            var exp = BalanceConfig.CalculateExperienceForLevel(1);
            Assert.AreEqual(0, exp);
        }

        [TestMethod]
        public void CalculateExperienceForLevel_Level2_ReturnsBaseExperience()
        {
            // Level 2 should require exactly BaseExperienceForLevel2
            var exp = BalanceConfig.CalculateExperienceForLevel(2);
            Assert.AreEqual(BalanceConfig.BaseExperienceForLevel2, exp);
        }

        [TestMethod]
        public void CalculateExperienceForLevel_GrowsExponentially()
        {
            // Experience curve should grow exponentially
            var exp5 = BalanceConfig.CalculateExperienceForLevel(5);
            var exp10 = BalanceConfig.CalculateExperienceForLevel(10);
            var exp20 = BalanceConfig.CalculateExperienceForLevel(20);
            
            // Each milestone should be significantly higher than the previous
            Assert.IsTrue(exp10 > exp5 * 4, "Level 10 should require much more than 4x Level 5");
            Assert.IsTrue(exp20 > exp10 * 4, "Level 20 should require much more than 4x Level 10");
        }

        [TestMethod]
        public void CalculateExperienceForLevel_Level99_IsReasonable()
        {
            // Level 99 should be achievable but require significant grinding
            var exp99 = BalanceConfig.CalculateExperienceForLevel(99);
            
            // Should be in a reasonable range (not too easy, not impossible)
            // Expected around 1-2 million XP
            Assert.IsTrue(exp99 > 1_000_000, "Level 99 should require over 1 million XP");
            Assert.IsTrue(exp99 < 10_000_000, "Level 99 should not require more than 10 million XP");
        }

        [TestMethod]
        public void CalculateExperienceForLevel_InvalidLevel_Handled()
        {
            // Test boundary conditions
            var expNegative = BalanceConfig.CalculateExperienceForLevel(-5);
            var expZero = BalanceConfig.CalculateExperienceForLevel(0);
            var expOver99 = BalanceConfig.CalculateExperienceForLevel(150);
            
            Assert.AreEqual(0, expNegative);
            Assert.AreEqual(0, expZero);
            // Over 99 should cap at level 99
            Assert.AreEqual(BalanceConfig.CalculateExperienceForLevel(99), expOver99);
        }

        [TestMethod]
        public void EstimatePlayerLevelForPitLevel_EarlyGame_CorrectProgression()
        {
            // Pit 1-10 should map to levels 1-15 (roughly)
            var level1 = BalanceConfig.EstimatePlayerLevelForPitLevel(1);
            var level10 = BalanceConfig.EstimatePlayerLevelForPitLevel(10);
            
            Assert.IsTrue(level1 >= 1 && level1 <= 3, "Pit 1 should be levels 1-3");
            Assert.IsTrue(level10 >= 13 && level10 <= 17, "Pit 10 should be around level 15");
        }

        [TestMethod]
        public void EstimatePlayerLevelForPitLevel_MidGame_CorrectProgression()
        {
            // Pit 11-20 should map to levels 16-35
            var level15 = BalanceConfig.EstimatePlayerLevelForPitLevel(15);
            var level20 = BalanceConfig.EstimatePlayerLevelForPitLevel(20);
            
            Assert.IsTrue(level15 >= 23 && level15 <= 28, "Pit 15 should be around level 25");
            Assert.IsTrue(level20 >= 33 && level20 <= 38, "Pit 20 should be around level 35");
        }

        [TestMethod]
        public void EstimatePlayerLevelForPitLevel_LateGame_CapsAt99()
        {
            // Very high pit levels should cap at 99
            var level50 = BalanceConfig.EstimatePlayerLevelForPitLevel(50);
            var level100 = BalanceConfig.EstimatePlayerLevelForPitLevel(100);
            var level200 = BalanceConfig.EstimatePlayerLevelForPitLevel(200);
            
            Assert.IsTrue(level50 <= 99, "Level 50 should not exceed max level");
            Assert.AreEqual(99, level100, "High pit levels should cap at 99");
            Assert.AreEqual(99, level200, "Very high pit levels should cap at 99");
        }

        [TestMethod]
        public void EstimatePlayerLevelForPitLevel_InvalidPit_Handled()
        {
            var levelNegative = BalanceConfig.EstimatePlayerLevelForPitLevel(-5);
            var levelZero = BalanceConfig.EstimatePlayerLevelForPitLevel(0);
            
            Assert.AreEqual(1, levelNegative, "Negative pit should return level 1");
            Assert.AreEqual(1, levelZero, "Pit 0 should return level 1");
        }

        #endregion

        #region Monster Stat Calculation Tests

        [TestMethod]
        public void CalculateMonsterHP_BalancedArchetype_ScalesWithLevel()
        {
            var hp1 = BalanceConfig.CalculateMonsterHP(1, BalanceConfig.MonsterArchetype.Balanced);
            var hp10 = BalanceConfig.CalculateMonsterHP(10, BalanceConfig.MonsterArchetype.Balanced);
            var hp50 = BalanceConfig.CalculateMonsterHP(50, BalanceConfig.MonsterArchetype.Balanced);
            
            Assert.IsTrue(hp1 > 0, "Level 1 monster should have positive HP");
            Assert.IsTrue(hp10 > hp1, "Level 10 monster should have more HP than level 1");
            Assert.IsTrue(hp50 > hp10 * 3, "Level 50 monster should have significantly more HP than level 10");
        }

        [TestMethod]
        public void CalculateMonsterHP_TankArchetype_HasMoreHP()
        {
            // Tank should have more HP than balanced at same level
            var hpBalanced = BalanceConfig.CalculateMonsterHP(25, BalanceConfig.MonsterArchetype.Balanced);
            var hpTank = BalanceConfig.CalculateMonsterHP(25, BalanceConfig.MonsterArchetype.Tank);
            
            Assert.IsTrue(hpTank > hpBalanced, "Tank should have more HP than Balanced");
            Assert.IsTrue(hpTank >= hpBalanced * 1.4f, "Tank should have at least 40% more HP");
        }

        [TestMethod]
        public void CalculateMonsterHP_FastFragileArchetype_HasLessHP()
        {
            // FastFragile should have less HP than balanced
            var hpBalanced = BalanceConfig.CalculateMonsterHP(25, BalanceConfig.MonsterArchetype.Balanced);
            var hpFast = BalanceConfig.CalculateMonsterHP(25, BalanceConfig.MonsterArchetype.FastFragile);
            
            Assert.IsTrue(hpFast < hpBalanced, "FastFragile should have less HP than Balanced");
        }

        [TestMethod]
        public void CalculateMonsterHP_Level99_DoesNotBreakGame()
        {
            // Max level monsters should have reasonable HP (not exceeding game limits)
            var hp99Balanced = BalanceConfig.CalculateMonsterHP(99, BalanceConfig.MonsterArchetype.Balanced);
            var hp99Tank = BalanceConfig.CalculateMonsterHP(99, BalanceConfig.MonsterArchetype.Tank);
            
            Assert.IsTrue(hp99Balanced < 10000, "Level 99 balanced HP should be under 10k");
            Assert.IsTrue(hp99Tank < 10000, "Level 99 tank HP should be under 10k");
        }

        [TestMethod]
        public void CalculateMonsterStat_BalancedArchetype_ScalesWithLevel()
        {
            var str1 = BalanceConfig.CalculateMonsterStat(1, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Strength);
            var str50 = BalanceConfig.CalculateMonsterStat(50, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Strength);
            var str99 = BalanceConfig.CalculateMonsterStat(99, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Strength);
            
            Assert.IsTrue(str1 >= 1, "Level 1 should have at least 1 stat");
            Assert.IsTrue(str50 > str1 * 10, "Level 50 should have much higher stats");
            Assert.IsTrue(str99 <= 99, "Stats should not exceed max stat cap");
        }

        [TestMethod]
        public void CalculateMonsterStat_ArchetypeModifiers_ApplyCorrectly()
        {
            // Tank should have high Vitality, low Agility
            var tankVit = BalanceConfig.CalculateMonsterStat(30, BalanceConfig.MonsterArchetype.Tank, BalanceConfig.StatType.Vitality);
            var tankAgi = BalanceConfig.CalculateMonsterStat(30, BalanceConfig.MonsterArchetype.Tank, BalanceConfig.StatType.Agility);
            
            // FastFragile should have high Agility, low Vitality
            var fastAgi = BalanceConfig.CalculateMonsterStat(30, BalanceConfig.MonsterArchetype.FastFragile, BalanceConfig.StatType.Agility);
            var fastVit = BalanceConfig.CalculateMonsterStat(30, BalanceConfig.MonsterArchetype.FastFragile, BalanceConfig.StatType.Vitality);
            
            // MagicUser should have high Magic
            var magicMag = BalanceConfig.CalculateMonsterStat(30, BalanceConfig.MonsterArchetype.MagicUser, BalanceConfig.StatType.Magic);
            var balancedMag = BalanceConfig.CalculateMonsterStat(30, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Magic);
            
            Assert.IsTrue(tankVit > tankAgi, "Tank Vitality should exceed Agility");
            Assert.IsTrue(fastAgi > fastVit, "FastFragile Agility should exceed Vitality");
            Assert.IsTrue(magicMag > balancedMag, "MagicUser Magic should exceed Balanced");
        }

        [TestMethod]
        public void CalculateMonsterStat_AllStats_AtLeastOne()
        {
            // Even with low multipliers, stats should be at least 1
            var tankMag = BalanceConfig.CalculateMonsterStat(1, BalanceConfig.MonsterArchetype.Tank, BalanceConfig.StatType.Magic);
            var fastVit = BalanceConfig.CalculateMonsterStat(1, BalanceConfig.MonsterArchetype.FastFragile, BalanceConfig.StatType.Vitality);
            
            Assert.IsTrue(tankMag >= 1, "Tank Magic should be at least 1");
            Assert.IsTrue(fastVit >= 1, "FastFragile Vitality should be at least 1");
        }

        [TestMethod]
        public void CalculateMonsterExperience_ScalesWithLevel()
        {
            var exp1 = BalanceConfig.CalculateMonsterExperience(1);
            var exp10 = BalanceConfig.CalculateMonsterExperience(10);
            var exp50 = BalanceConfig.CalculateMonsterExperience(50);
            var exp99 = BalanceConfig.CalculateMonsterExperience(99);
            
            Assert.IsTrue(exp1 > 0, "Level 1 should give XP");
            Assert.IsTrue(exp10 > exp1 * 4, "Level 10 should give significantly more XP");
            Assert.IsTrue(exp50 > exp10 * 3, "Level 50 should give much more XP");
            Assert.IsTrue(exp99 < 10000, "Level 99 XP should be reasonable (under 10k)");
        }

        #endregion

        #region Equipment Stat Calculation Tests

        [TestMethod]
        public void CalculateEquipmentAttackBonus_ScalesWithPitLevel()
        {
            var attack1 = BalanceConfig.CalculateEquipmentAttackBonus(1, ItemRarity.Normal);
            var attack20 = BalanceConfig.CalculateEquipmentAttackBonus(20, ItemRarity.Normal);
            var attack50 = BalanceConfig.CalculateEquipmentAttackBonus(50, ItemRarity.Normal);
            
            Assert.IsTrue(attack1 >= 1, "Pit 1 equipment should have at least 1 attack");
            Assert.IsTrue(attack20 > attack1, "Pit 20 equipment should have more attack");
            Assert.IsTrue(attack50 > attack20, "Pit 50 equipment should have even more attack");
        }

        [TestMethod]
        public void CalculateEquipmentAttackBonus_RarityMultiplier_AppliesCorrectly()
        {
            var attackNormal = BalanceConfig.CalculateEquipmentAttackBonus(20, ItemRarity.Normal);
            var attackRare = BalanceConfig.CalculateEquipmentAttackBonus(20, ItemRarity.Rare);
            var attackLegendary = BalanceConfig.CalculateEquipmentAttackBonus(20, ItemRarity.Legendary);
            
            Assert.IsTrue(attackRare > attackNormal, "Rare should have more attack than Normal");
            Assert.IsTrue(attackLegendary > attackRare * 1.5f, "Legendary should have significantly more attack");
        }

        [TestMethod]
        public void CalculateEquipmentDefenseBonus_ScalesWithPitLevel()
        {
            var def1 = BalanceConfig.CalculateEquipmentDefenseBonus(1, ItemRarity.Normal);
            var def30 = BalanceConfig.CalculateEquipmentDefenseBonus(30, ItemRarity.Normal);
            
            Assert.IsTrue(def1 >= 1, "Pit 1 equipment should have at least 1 defense");
            Assert.IsTrue(def30 > def1, "Pit 30 equipment should have more defense");
        }

        [TestMethod]
        public void CalculateEquipmentDefenseBonus_LowerThanAttack()
        {
            // Defense scaling should be slower than attack (divisor 3 vs 2)
            var attack50 = BalanceConfig.CalculateEquipmentAttackBonus(50, ItemRarity.Normal);
            var defense50 = BalanceConfig.CalculateEquipmentDefenseBonus(50, ItemRarity.Normal);
            
            Assert.IsTrue(defense50 < attack50, "Defense should scale slower than attack");
        }

        [TestMethod]
        public void CalculateEquipmentStatBonus_ScalesWithPitAndRarity()
        {
            var stat1 = BalanceConfig.CalculateEquipmentStatBonus(1, ItemRarity.Normal);
            var stat25 = BalanceConfig.CalculateEquipmentStatBonus(25, ItemRarity.Normal);
            var stat25Legendary = BalanceConfig.CalculateEquipmentStatBonus(25, ItemRarity.Legendary);
            
            // Stat bonuses start at 0 for low pit levels
            Assert.IsTrue(stat1 >= 0, "Low pit should have minimal stat bonus");
            Assert.IsTrue(stat25 > 0, "Mid pit should have positive stat bonus");
            Assert.IsTrue(stat25Legendary > stat25 * 2, "Legendary should have much higher stat bonus");
        }

        [TestMethod]
        public void CalculateEquipmentStatBonus_HighLevel_DoesNotBreakGame()
        {
            // Even legendary equipment at high pit levels should not exceed stat caps
            var statLegendary99 = BalanceConfig.CalculateEquipmentStatBonus(99, ItemRarity.Legendary);
            
            Assert.IsTrue(statLegendary99 < 99, "Equipment stat bonus should not reach max stat alone");
        }

        #endregion

        #region Elemental Damage Multiplier Tests

        [TestMethod]
        public void GetElementalDamageMultiplier_NeutralAttack_ReturnsOne()
        {
            var props = new ElementalProperties(ElementType.Fire);
            var multiplier = BalanceConfig.GetElementalDamageMultiplier(ElementType.Neutral, props);
            
            Assert.AreEqual(1.0f, multiplier, 0.01f, "Neutral attack should deal normal damage");
        }

        [TestMethod]
        public void GetElementalDamageMultiplier_SameElement_ReturnsResistance()
        {
            var props = new ElementalProperties(ElementType.Fire);
            var multiplier = BalanceConfig.GetElementalDamageMultiplier(ElementType.Fire, props);
            
            Assert.AreEqual(0.5f, multiplier, 0.01f, "Same element should be resisted (0.5x)");
        }

        [TestMethod]
        public void GetElementalDamageMultiplier_OpposingElement_ReturnsWeakness()
        {
            // Fire opposes Water, so Fire attack on Water target should be 2x
            var waterProps = new ElementalProperties(ElementType.Water);
            var multiplier = BalanceConfig.GetElementalDamageMultiplier(ElementType.Fire, waterProps);
            
            Assert.AreEqual(2.0f, multiplier, 0.01f, "Opposing element should deal double damage");
        }

        [TestMethod]
        public void GetElementalDamageMultiplier_AllOpposingPairs_CorrectWeakness()
        {
            // Test all opposing pairs
            var fireVsWater = BalanceConfig.GetElementalDamageMultiplier(
                ElementType.Fire, new ElementalProperties(ElementType.Water));
            var waterVsFire = BalanceConfig.GetElementalDamageMultiplier(
                ElementType.Water, new ElementalProperties(ElementType.Fire));
            var earthVsWind = BalanceConfig.GetElementalDamageMultiplier(
                ElementType.Earth, new ElementalProperties(ElementType.Wind));
            var lightVsDark = BalanceConfig.GetElementalDamageMultiplier(
                ElementType.Light, new ElementalProperties(ElementType.Dark));
            
            Assert.AreEqual(2.0f, fireVsWater, 0.01f);
            Assert.AreEqual(2.0f, waterVsFire, 0.01f);
            Assert.AreEqual(2.0f, earthVsWind, 0.01f);
            Assert.AreEqual(2.0f, lightVsDark, 0.01f);
        }

        [TestMethod]
        public void GetElementalDamageMultiplier_CustomResistance_AppliesCorrectly()
        {
            // Create properties with custom resistance to Fire
            var resistances = new Dictionary<ElementType, float> { { ElementType.Fire, 0.5f } };
            var props = new ElementalProperties(ElementType.Earth, resistances);
            
            var multiplier = BalanceConfig.GetElementalDamageMultiplier(ElementType.Fire, props);
            
            // Base multiplier is 1.0 (no relationship), minus 50% resistance = 0.5
            Assert.IsTrue(multiplier < 1.0f, "Custom resistance should reduce damage");
            Assert.IsTrue(multiplier >= 0.0f, "Multiplier should not be negative");
        }

        [TestMethod]
        public void GetElementalDamageMultiplier_NoRelationship_ReturnsOne()
        {
            // Fire vs Earth has no relationship
            var earthProps = new ElementalProperties(ElementType.Earth);
            var multiplier = BalanceConfig.GetElementalDamageMultiplier(ElementType.Fire, earthProps);
            
            Assert.AreEqual(1.0f, multiplier, 0.01f, "Unrelated elements should deal normal damage");
        }

        #endregion

        #region Battle Stat Formula Tests

        [TestMethod]
        public void CalculateAttackDamage_AttackGreaterThanDefense_LinearScaling()
        {
            // When attack >= defense, use formula: (attack * 2) - defense
            var damage = BalanceConfig.CalculateAttackDamage(50, 30);
            var expected = 50 * 2 - 30; // 70
            
            Assert.AreEqual(expected, damage, "High attack vs low defense should use linear formula");
        }

        [TestMethod]
        public void CalculateAttackDamage_AttackLessThanDefense_QuadraticPenalty()
        {
            // When attack < defense, use formula: (attack * attack) / defense
            var damage = BalanceConfig.CalculateAttackDamage(20, 50);
            var expected = (20 * 20) / 50; // 8
            
            Assert.AreEqual(expected, damage, "Low attack vs high defense should use quadratic formula");
        }

        [TestMethod]
        public void CalculateAttackDamage_EqualStats_ModerateResult()
        {
            var damage = BalanceConfig.CalculateAttackDamage(40, 40);
            var expected = 40 * 2 - 40; // 40
            
            Assert.AreEqual(expected, damage, "Equal attack/defense should deal moderate damage");
        }

        [TestMethod]
        public void CalculateAttackDamage_MinimumDamageIsOne()
        {
            // Very low attack vs very high defense should still deal 1 damage
            var damage = BalanceConfig.CalculateAttackDamage(1, 100);
            
            Assert.AreEqual(1, damage, "Minimum damage should be 1");
        }

        [TestMethod]
        public void CalculateAttackDamage_TypicalCases_ReasonableResults()
        {
            // Test various realistic combat scenarios
            var lowVsLow = BalanceConfig.CalculateAttackDamage(10, 8);
            var midVsMid = BalanceConfig.CalculateAttackDamage(50, 40);
            var highVsHigh = BalanceConfig.CalculateAttackDamage(100, 80);
            
            Assert.IsTrue(lowVsLow > 0, "Low stats should deal positive damage");
            Assert.IsTrue(midVsMid > lowVsLow * 2, "Mid stats should scale up");
            Assert.IsTrue(highVsHigh > midVsMid, "High stats should deal more damage");
        }

        [TestMethod]
        public void CalculateEvasion_ScalesWithAgilityAndLevel()
        {
            var evasion1 = BalanceConfig.CalculateEvasion(10, 5);
            var evasion2 = BalanceConfig.CalculateEvasion(25, 10);
            var evasion3 = BalanceConfig.CalculateEvasion(50, 30);
            
            Assert.IsTrue(evasion1 < evasion2, "Higher agility should increase evasion");
            Assert.IsTrue(evasion2 < evasion3, "Evasion should scale with both agility and level");
        }

        [TestMethod]
        public void CalculateEvasion_Formula_MatchesExpected()
        {
            // Formula: min(255, agility * 2 + level)
            var evasion = BalanceConfig.CalculateEvasion(50, 30);
            var expected = System.Math.Min(255, 50 * 2 + 30); // 130
            
            Assert.AreEqual(expected, evasion, "Evasion formula should match specification");
        }

        [TestMethod]
        public void CalculateEvasion_CapsAt255()
        {
            // Very high agility/level should cap at 255
            var evasion = BalanceConfig.CalculateEvasion(99, 99);
            
            Assert.AreEqual(255, evasion, "Evasion should cap at 255");
        }

        [TestMethod]
        public void CalculateEvasion_LowStats_ReasonableChance()
        {
            var evasion = BalanceConfig.CalculateEvasion(5, 1);
            // 5 * 2 + 1 = 11, which is ~4% evasion chance
            
            Assert.IsTrue(evasion >= 10 && evasion <= 20, "Low agility should have low evasion");
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_MonsterStatsAtLevel50_Reasonable()
        {
            // Test that a level 50 balanced monster has reasonable stats
            var hp = BalanceConfig.CalculateMonsterHP(50, BalanceConfig.MonsterArchetype.Balanced);
            var str = BalanceConfig.CalculateMonsterStat(50, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Strength);
            var agi = BalanceConfig.CalculateMonsterStat(50, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Agility);
            var vit = BalanceConfig.CalculateMonsterStat(50, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Vitality);
            var mag = BalanceConfig.CalculateMonsterStat(50, BalanceConfig.MonsterArchetype.Balanced, BalanceConfig.StatType.Magic);
            var exp = BalanceConfig.CalculateMonsterExperience(50);
            
            Assert.IsTrue(hp > 100 && hp < 1000, "Level 50 monster HP should be 100-1000");
            Assert.IsTrue(str > 20 && str < 70, "Level 50 monster stats should be 20-70");
            Assert.IsTrue(agi > 20 && agi < 70, "Level 50 monster stats should be 20-70");
            Assert.IsTrue(vit > 20 && vit < 70, "Level 50 monster stats should be 20-70");
            Assert.IsTrue(mag > 20 && mag < 70, "Level 50 monster stats should be 20-70");
            Assert.IsTrue(exp > 100 && exp < 1000, "Level 50 monster XP should be 100-1000");
        }

        [TestMethod]
        public void Integration_EquipmentAtPit50_Reasonable()
        {
            // Test that Pit 50 equipment has reasonable bonuses
            var attackNormal = BalanceConfig.CalculateEquipmentAttackBonus(50, ItemRarity.Normal);
            var attackLegendary = BalanceConfig.CalculateEquipmentAttackBonus(50, ItemRarity.Legendary);
            var defenseNormal = BalanceConfig.CalculateEquipmentDefenseBonus(50, ItemRarity.Normal);
            var statNormal = BalanceConfig.CalculateEquipmentStatBonus(50, ItemRarity.Normal);
            
            Assert.IsTrue(attackNormal > 10 && attackNormal < 50, "Pit 50 normal attack should be 10-50");
            Assert.IsTrue(attackLegendary > 50 && attackLegendary < 150, "Pit 50 legendary attack should be 50-150");
            Assert.IsTrue(defenseNormal > 5 && defenseNormal < 30, "Pit 50 defense should be 5-30");
            Assert.IsTrue(statNormal > 5 && statNormal < 20, "Pit 50 stat bonus should be 5-20");
        }

        [TestMethod]
        public void Integration_ExperienceProgression_FormulasWorkTogether()
        {
            // Verify that all experience-related formulas work together coherently
            
            // Test 1: Monster XP increases with level
            var xp1 = BalanceConfig.CalculateMonsterExperience(1);
            var xp10 = BalanceConfig.CalculateMonsterExperience(10);
            var xp50 = BalanceConfig.CalculateMonsterExperience(50);
            Assert.IsTrue(xp10 > xp1 * 3, "Level 10 monster should give significantly more XP");
            Assert.IsTrue(xp50 > xp10 * 3, "Level 50 monster should give much more XP");
            
            // Test 2: Level requirements increase faster than XP gains (creates grinding)
            var reqLevel10 = BalanceConfig.CalculateExperienceForLevel(10);
            var reqLevel20 = BalanceConfig.CalculateExperienceForLevel(20);
            var xpGainRatio = (float)xp10 / xp1; // How much more XP high level monsters give
            var levelReqRatio = (float)reqLevel20 / reqLevel10; // How much more XP higher levels need
            
            Assert.IsTrue(levelReqRatio > xpGainRatio, 
                "Level requirements should grow faster than monster XP to encourage exploration");
            
            // Test 3: Pit level estimation produces ascending levels
            var pitLevel1 = BalanceConfig.EstimatePlayerLevelForPitLevel(1);
            var pitLevel20 = BalanceConfig.EstimatePlayerLevelForPitLevel(20);
            var pitLevel50 = BalanceConfig.EstimatePlayerLevelForPitLevel(50);
            
            Assert.IsTrue(pitLevel20 > pitLevel1, "Deeper pits should expect higher player levels");
            Assert.IsTrue(pitLevel50 > pitLevel20, "Much deeper pits should expect much higher levels");
            Assert.IsTrue(pitLevel50 <= 99, "Pit level estimates should not exceed max level");
        }

        #endregion
    }
}
