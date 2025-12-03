using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;

namespace PitHero.Tests
{
    [TestClass]
    public class BattleOnlyPropertyTests
    {
        [TestMethod]
        public void Consumables_DefaultBattleOnlyIsFalse()
        {
            // Arrange & Act
            var hpPotion = PotionItems.HPPotion();
            var mpPotion = PotionItems.MPPotion();
            var mixPotion = PotionItems.MixPotion();
            
            // Assert
            Assert.IsFalse(hpPotion.BattleOnly, "HP Potion should not be battle-only");
            Assert.IsFalse(mpPotion.BattleOnly, "MP Potion should not be battle-only");
            Assert.IsFalse(mixPotion.BattleOnly, "Mix Potion should not be battle-only");
        }
        
        [TestMethod]
        public void Skills_DefaultBattleOnlyIsTrue()
        {
            // Most skills should be battle-only by default
            // Arrange & Act
            var defenseUp = new DefenseUpSkill();
            var calmSpirit = new CalmSpiritPassive();
            var mender = new MenderPassive();
            
            // Assert
            Assert.IsTrue(defenseUp.BattleOnly, "DefenseUp should be battle-only");
            Assert.IsTrue(calmSpirit.BattleOnly, "Passive skills should be battle-only");
            Assert.IsTrue(mender.BattleOnly, "Passive skills should be battle-only");
        }
        
        [TestMethod]
        public void HealSkill_BattleOnlyIsFalse()
        {
            // Arrange & Act
            var healSkill = new HealSkill();
            
            // Assert
            Assert.IsFalse(healSkill.BattleOnly, "Heal skill should be usable outside of battle");
        }
        
        [TestMethod]
        public void SkillInterface_HasBattleOnlyProperty()
        {
            // Arrange
            var skill = new HealSkill() as ISkill;
            
            // Assert
            Assert.IsNotNull(skill);
            // Verify that BattleOnly is accessible through the interface
            bool battleOnly = skill.BattleOnly;
            Assert.IsFalse(battleOnly);
        }
    }
}
