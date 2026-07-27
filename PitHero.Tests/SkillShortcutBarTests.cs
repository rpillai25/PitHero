using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.UI;
using PitHero.UI;
using RolePlayingFramework.Equipment;
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
        public void ShortcutSlotData_CreateSkillReference_DefaultsToHeroOwner()
        {
            // Arrange
            var skill = new KnightSkills.SpinSlashSkill();

            // Act
            var shortcutData = ShortcutSlotData.CreateSkillReference(skill);

            // Assert
            Assert.IsNull(shortcutData.OwnerMercenary);
        }

        [TestMethod]
        public void ShortcutSlotData_CreateSkillReference_WithMercenary_SetsOwner()
        {
            // Arrange
            var skill = new KnightSkills.SpinSlashSkill();
            var merc = new RolePlayingFramework.Mercenaries.Mercenary(
                "Fynn Swift", new RolePlayingFramework.Jobs.Primary.Knight(), 5,
                new RolePlayingFramework.Stats.StatBlock(10, 10, 10, 5));

            // Act
            var shortcutData = ShortcutSlotData.CreateSkillReference(skill, merc);

            // Assert
            Assert.AreEqual(ShortcutSlotType.Skill, shortcutData.SlotType);
            Assert.AreEqual(skill, shortcutData.ReferencedSkill);
            Assert.AreEqual(merc, shortcutData.OwnerMercenary);
        }

        [TestMethod]
        public void ShortcutSlotData_Clear_ResetsOwnerMercenary()
        {
            // Arrange
            var skill = new KnightSkills.SpinSlashSkill();
            var merc = new RolePlayingFramework.Mercenaries.Mercenary(
                "Fynn Swift", new RolePlayingFramework.Jobs.Primary.Knight(), 5,
                new RolePlayingFramework.Stats.StatBlock(10, 10, 10, 5));
            var shortcutData = ShortcutSlotData.CreateSkillReference(skill, merc);

            // Act
            shortcutData.Clear();

            // Assert
            Assert.AreEqual(ShortcutSlotType.Empty, shortcutData.SlotType);
            Assert.IsNull(shortcutData.OwnerMercenary);
        }
        
        [TestMethod]
        public void ShortcutBar_ConnectToDragManager_IsIdempotent()
        {
            // ReconnectUIToHero (crystal ceremony after hero death) calls ConnectToDragManager a
            // second time on the same bar. A duplicate subscription runs the skill-drop handler twice:
            // the second run, after EndDrag cleared DragSkillOwner, overwrote merc-owned slots as hero-owned.
            var previousDrop = InventoryDragManager.OnDropRequested;
            var previousSkillDrop = InventoryDragManager.OnSkillDropRequested;
            try
            {
                InventoryDragManager.OnDropRequested = null;
                InventoryDragManager.OnSkillDropRequested = null;

                var bar = new ShortcutBar();
                bar.ConnectToDragManager();
                bar.ConnectToDragManager();

                Assert.AreEqual(1, InventoryDragManager.OnDropRequested.GetInvocationList().Length,
                    "Repeated ConnectToDragManager must not stack item-drop handlers");
                Assert.AreEqual(1, InventoryDragManager.OnSkillDropRequested.GetInvocationList().Length,
                    "Repeated ConnectToDragManager must not stack skill-drop handlers");
            }
            finally
            {
                InventoryDragManager.OnDropRequested = previousDrop;
                InventoryDragManager.OnSkillDropRequested = previousSkillDrop;
            }
        }

        [TestMethod]
        public void ShortcutBar_SkillDrop_LandsInSlot()
        {
            var prevDrop = InventoryDragManager.OnDropRequested;
            var prevSkillDrop = InventoryDragManager.OnSkillDropRequested;
            try
            {
                InventoryDragManager.OnDropRequested = null;
                InventoryDragManager.OnSkillDropRequested = null;

                var stage = new Stage();
                var bar = new ShortcutBar();
                stage.AddElement(bar);
                bar.SetBasePosition(100f, 200f);
                bar.ConnectToDragManager();

                var skill = new KnightSkills.SpinSlashSkill();
                InventoryDragManager.BeginSkillDrag(skill, stage);

                // slot 0 occupies 32x32 at (100,200); drop on its center
                InventoryDragManager.NotifySkillDropRequested(skill, new Vector2(116f, 216f));

                var data = bar.GetShortcutSlotData(0);
                Assert.AreEqual(ShortcutSlotType.Skill, data.SlotType, "skill should land in slot 0");
                Assert.AreEqual(skill, data.ReferencedSkill);
                Assert.IsFalse(InventoryDragManager.IsDragging, "drag should end after a successful drop");
            }
            finally
            {
                if (InventoryDragManager.IsDragging)
                    InventoryDragManager.CancelDrag();
                InventoryDragManager.OnDropRequested = prevDrop;
                InventoryDragManager.OnSkillDropRequested = prevSkillDrop;
            }
        }

        [TestMethod]
        public void ShortcutBar_StaleBarFromUnloadedScene_DoesNotSwallowSkillDrop()
        {
            // Regression: after returning to the title and loading another game, skill drops
            // stopped working. The old scene's bar stayed subscribed to the static events,
            // its handler ran first (subscription order), missed the slot lookup on its dead
            // stage, and cancelled the drag before the live bar could handle it.
            var prevDrop = InventoryDragManager.OnDropRequested;
            var prevSkillDrop = InventoryDragManager.OnSkillDropRequested;
            try
            {
                InventoryDragManager.OnDropRequested = null;
                InventoryDragManager.OnSkillDropRequested = null;

                // First game session: bar on stage A (positioned so the drop point would miss it)
                var staleStage = new Stage();
                var staleBar = new ShortcutBar();
                staleStage.AddElement(staleBar);
                staleBar.SetBasePosition(500f, 500f);
                staleBar.ConnectToDragManager();

                // Second game session: new bar on stage B, subscribed after the stale bar
                var liveStage = new Stage();
                var liveBar = new ShortcutBar();
                liveStage.AddElement(liveBar);
                liveBar.SetBasePosition(100f, 200f);
                liveBar.ConnectToDragManager();

                var skill = new KnightSkills.SpinSlashSkill();
                InventoryDragManager.BeginSkillDrag(skill, liveStage);
                InventoryDragManager.NotifySkillDropRequested(skill, new Vector2(116f, 216f));

                Assert.AreEqual(ShortcutSlotType.Skill, liveBar.GetShortcutSlotData(0).SlotType,
                    "live bar must receive the skill even with a stale bar subscribed first");
                Assert.AreEqual(ShortcutSlotType.Empty, staleBar.GetShortcutSlotData(0).SlotType,
                    "stale bar must not consume the drop");

                // And once the old scene unloads properly, its bar must fully unsubscribe
                staleBar.DisconnectFromStaticEvents();
                Assert.AreEqual(1, InventoryDragManager.OnSkillDropRequested.GetInvocationList().Length,
                    "DisconnectFromStaticEvents must remove the stale bar's skill-drop handler");
                Assert.AreEqual(1, InventoryDragManager.OnDropRequested.GetInvocationList().Length,
                    "DisconnectFromStaticEvents must remove the stale bar's item-drop handler");
            }
            finally
            {
                if (InventoryDragManager.IsDragging)
                    InventoryDragManager.CancelDrag();
                InventoryDragManager.OnDropRequested = prevDrop;
                InventoryDragManager.OnSkillDropRequested = prevSkillDrop;
            }
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
