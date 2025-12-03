using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;
using RolePlayingFramework.Skills;

namespace PitHero.Tests
{
    [TestClass]
    public class SkillShortcutBarTests
    {
        [TestMethod]
        public void ShortcutSlotData_CreateItemReference_SetsCorrectType()
        {
            // Arrange
            var slot = new InventorySlot(new InventorySlotData(0, 0, InventorySlotType.Inventory));
            
            // Act
            var shortcutData = ShortcutSlotData.CreateItemReference(slot);
            
            // Assert
            Assert.AreEqual(ShortcutSlotType.Item, shortcutData.SlotType);
            Assert.AreEqual(slot, shortcutData.ReferencedSlot);
            Assert.IsNull(shortcutData.ReferencedSkill);
        }
        
        [TestMethod]
        public void ShortcutSlotData_CreateSkillReference_SetsCorrectType()
        {
            // Arrange
            var skill = new KnightSkills.SpinSlashSkill();
            
            // Act
            var shortcutData = ShortcutSlotData.CreateSkillReference(skill);
            
            // Assert
            Assert.AreEqual(ShortcutSlotType.Skill, shortcutData.SlotType);
            Assert.IsNull(shortcutData.ReferencedSlot);
            Assert.AreEqual(skill, shortcutData.ReferencedSkill);
        }
        
        [TestMethod]
        public void ShortcutSlotData_Clear_ResetsToEmpty()
        {
            // Arrange
            var skill = new KnightSkills.SpinSlashSkill();
            var shortcutData = ShortcutSlotData.CreateSkillReference(skill);
            
            // Act
            shortcutData.Clear();
            
            // Assert
            Assert.AreEqual(ShortcutSlotType.Empty, shortcutData.SlotType);
            Assert.IsNull(shortcutData.ReferencedSlot);
            Assert.IsNull(shortcutData.ReferencedSkill);
        }
        
        [TestMethod]
        public void InventorySelectionManager_SetSelectedFromHeroCrystalTab_SetsSkillSelection()
        {
            // Arrange
            var skill = new KnightSkills.HeavyStrikeSkill();
            
            // Act
            InventorySelectionManager.SetSelectedFromHeroCrystalTab(skill, null);
            
            // Assert
            Assert.IsTrue(InventorySelectionManager.HasSelection());
            Assert.IsTrue(InventorySelectionManager.IsSelectionFromHeroCrystalTab());
            Assert.AreEqual(skill, InventorySelectionManager.GetSelectedSkill());
            
            // Cleanup
            InventorySelectionManager.ClearSelection();
        }
        
        [TestMethod]
        public void InventorySelectionManager_ClearSelection_ClearsSkillSelection()
        {
            // Arrange
            var skill = new KnightSkills.SpinSlashSkill();
            InventorySelectionManager.SetSelectedFromHeroCrystalTab(skill, null);
            
            // Act
            InventorySelectionManager.ClearSelection();
            
            // Assert
            Assert.IsFalse(InventorySelectionManager.HasSelection());
            Assert.IsFalse(InventorySelectionManager.IsSelectionFromHeroCrystalTab());
            Assert.IsNull(InventorySelectionManager.GetSelectedSkill());
        }
        
        [TestMethod]
        public void ActiveSkill_HasCorrectKind()
        {
            // Arrange & Act
            var spinSlash = new KnightSkills.SpinSlashSkill();
            var heavyStrike = new KnightSkills.HeavyStrikeSkill();
            
            // Assert
            Assert.AreEqual(SkillKind.Active, spinSlash.Kind);
            Assert.AreEqual(SkillKind.Active, heavyStrike.Kind);
        }
        
        [TestMethod]
        public void PassiveSkill_HasCorrectKind()
        {
            // Arrange & Act
            var lightArmor = new KnightSkills.LightArmorPassive();
            var heavyArmor = new KnightSkills.HeavyArmorPassive();
            
            // Assert
            Assert.AreEqual(SkillKind.Passive, lightArmor.Kind);
            Assert.AreEqual(SkillKind.Passive, heavyArmor.Kind);
        }
    }
    
    // Helper class to access Knight skills
    public static class KnightSkills
    {
        public class SpinSlashSkill : BaseSkill
        {
            public SpinSlashSkill() : base("knight.spin_slash", "Spin Slash", SkillKind.Active, SkillTargetType.SurroundingEnemies, 4, 120) { }
        }
        
        public class HeavyStrikeSkill : BaseSkill
        {
            public HeavyStrikeSkill() : base("knight.heavy_strike", "Heavy Strike", SkillKind.Active, SkillTargetType.SingleEnemy, 5, 180) { }
        }
        
        public class LightArmorPassive : BaseSkill
        {
            public LightArmorPassive() : base("knight.light_armor", "Light Armor", SkillKind.Passive, SkillTargetType.Self, 0, 50) { }
        }
        
        public class HeavyArmorPassive : BaseSkill
        {
            public HeavyArmorPassive() : base("knight.heavy_armor", "Heavy Armor", SkillKind.Passive, SkillTargetType.Self, 0, 100) { }
        }
    }
}
