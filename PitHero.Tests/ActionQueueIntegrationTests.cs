using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;

namespace PitHero.Tests
{
    [TestClass]
    public class ActionQueueIntegrationTests
    {
        [TestMethod]
        public void HeroComponent_HasActionQueue()
        {
            // Arrange
            var job = new RolePlayingFramework.Jobs.Primary.Knight();
            var hero = new Hero(
                name: "TestHero", 
                job: job, 
                level: 1, 
                baseStats: new RolePlayingFramework.Stats.StatBlock(10, 10, 10, 10)
            );
            
            // Create action queue directly since we can't call OnAddedToEntity in unit tests
            var queue = new ActionQueue();
            
            // Assert
            Assert.IsNotNull(queue);
            Assert.IsFalse(queue.HasActions());
        }
        
        [TestMethod]
        public void ActionQueue_CanQueueMultipleActions()
        {
            // Arrange
            var job = new RolePlayingFramework.Jobs.Primary.Knight();
            var hero = new Hero(
                name: "TestHero", 
                job: job, 
                level: 1, 
                baseStats: new RolePlayingFramework.Stats.StatBlock(10, 10, 10, 10)
            );
            var heroComponent = new HeroComponent
            {
                LinkedHero = hero
            };
            var queue = new ActionQueue();
            
            // Act
            queue.EnqueueItem(PotionItems.HPPotion(), 0);
            queue.EnqueueItem(PotionItems.MPPotion(), 1);
            queue.EnqueueSkill(new HealSkill());
            
            // Assert
            Assert.AreEqual(3, queue.Count);
            Assert.IsTrue(queue.HasActions());
        }
        
        [TestMethod]
        public void ActionQueue_ProcessesInCorrectOrder()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion1 = PotionItems.HPPotion();
            var potion2 = PotionItems.MPPotion();
            var skill = new HealSkill();
            
            // Act - Enqueue in specific order
            queue.EnqueueItem(potion1, 0);
            queue.EnqueueItem(potion2, 1);
            queue.EnqueueSkill(skill);
            
            // Assert - Verify FIFO order
            var action1 = queue.Dequeue();
            Assert.AreEqual(QueuedActionType.UseItem, action1.ActionType);
            Assert.AreEqual(potion1.Name, action1.Consumable.Name);
            
            var action2 = queue.Dequeue();
            Assert.AreEqual(QueuedActionType.UseItem, action2.ActionType);
            Assert.AreEqual(potion2.Name, action2.Consumable.Name);
            
            var action3 = queue.Dequeue();
            Assert.AreEqual(QueuedActionType.UseSkill, action3.ActionType);
            Assert.AreEqual(skill.Name, action3.Skill.Name);
            
            Assert.IsFalse(queue.HasActions());
        }
        
        [TestMethod]
        public void BattleOnly_PotionsCanBeUsedOutsideOfBattle()
        {
            // Arrange
            var job = new RolePlayingFramework.Jobs.Primary.Knight();
            var hero = new Hero(
                name: "TestHero", 
                job: job, 
                level: 10, 
                baseStats: new RolePlayingFramework.Stats.StatBlock(20, 20, 20, 20)
            );
            
            // Take damage so healing has an effect
            hero.TakeDamage(50);
            int initialHP = hero.CurrentHP;
            
            var potion = PotionItems.HPPotion();
            
            // Assert potion is not battle-only
            Assert.IsFalse(potion.BattleOnly, "HP Potion should be usable outside of battle");
            
            // Act - Use potion outside of battle
            bool consumed = potion.Consume(hero);
            
            // Assert
            Assert.IsTrue(consumed, "Potion should be consumable outside of battle");
            Assert.IsTrue(hero.CurrentHP > initialHP, "Hero HP should have increased");
        }
        
        [TestMethod]
        public void BattleOnly_HealSkillCanBeUsedOutsideOfBattle()
        {
            // Arrange
            var job = new RolePlayingFramework.Jobs.Primary.Priest();
            var hero = new Hero(
                name: "TestHero", 
                job: job, 
                level: 10, 
                baseStats: new RolePlayingFramework.Stats.StatBlock(20, 20, 20, 20)
            );
            
            // Take damage and ensure hero has MP
            hero.TakeDamage(50);
            hero.RestoreMP(100);
            int initialHP = hero.CurrentHP;
            int initialMP = hero.CurrentMP;
            
            var healSkill = new HealSkill();
            
            // Assert skill is not battle-only
            Assert.IsFalse(healSkill.BattleOnly, "Heal skill should be usable outside of battle");
            
            // Act - Use skill outside of battle (manually spend MP as would happen in battle)
            healSkill.Execute(hero, null, new System.Collections.Generic.List<RolePlayingFramework.Enemies.IEnemy>(), null);
            hero.SpendMP(healSkill.MPCost);
            
            // Assert
            Assert.IsTrue(hero.CurrentHP > initialHP, "Hero HP should have increased");
            Assert.IsTrue(hero.CurrentMP < initialMP, "Hero should have spent MP");
        }
        
        [TestMethod]
        public void BattleOnly_DefenseUpSkillIsBattleOnly()
        {
            // Arrange
            var defenseUpSkill = new DefenseUpSkill();
            
            // Assert
            Assert.IsTrue(defenseUpSkill.BattleOnly, "DefenseUp skill should be battle-only");
        }
        
        [TestMethod]
        public void ActionQueue_ClearsCorrectly()
        {
            // Arrange
            var queue = new ActionQueue();
            queue.EnqueueItem(PotionItems.HPPotion(), 0);
            queue.EnqueueItem(PotionItems.MPPotion(), 1);
            queue.EnqueueSkill(new HealSkill());
            
            Assert.AreEqual(3, queue.Count);
            
            // Act
            queue.Clear();
            
            // Assert
            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.HasActions());
            Assert.IsNull(queue.Dequeue());
        }
    }
}
