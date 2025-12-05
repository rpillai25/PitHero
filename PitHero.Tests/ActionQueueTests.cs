using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;

namespace PitHero.Tests
{
    [TestClass]
    public class ActionQueueTests
    {
        [TestMethod]
        public void ActionQueue_StartsEmpty()
        {
            // Arrange
            var queue = new ActionQueue();
            
            // Assert
            Assert.IsFalse(queue.HasActions());
            Assert.AreEqual(0, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_CanEnqueueItem()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion = PotionItems.HPPotion();
            
            // Act
            queue.EnqueueItem(potion, 0);
            
            // Assert
            Assert.IsTrue(queue.HasActions());
            Assert.AreEqual(1, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_CanEnqueueSkill()
        {
            // Arrange
            var queue = new ActionQueue();
            var skill = new HealSkill();
            
            // Act
            queue.EnqueueSkill(skill);
            
            // Assert
            Assert.IsTrue(queue.HasActions());
            Assert.AreEqual(1, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_CanDequeueItem()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion = PotionItems.HPPotion();
            queue.EnqueueItem(potion, 5);
            
            // Act
            var action = queue.Dequeue();
            
            // Assert
            Assert.IsNotNull(action);
            Assert.AreEqual(QueuedActionType.UseItem, action.ActionType);
            Assert.AreEqual(potion, action.Consumable);
            Assert.AreEqual(5, action.BagIndex);
            Assert.IsFalse(queue.HasActions());
        }
        
        [TestMethod]
        public void ActionQueue_CanDequeueSkill()
        {
            // Arrange
            var queue = new ActionQueue();
            var skill = new HealSkill();
            queue.EnqueueSkill(skill);
            
            // Act
            var action = queue.Dequeue();
            
            // Assert
            Assert.IsNotNull(action);
            Assert.AreEqual(QueuedActionType.UseSkill, action.ActionType);
            Assert.AreEqual(skill, action.Skill);
            Assert.IsFalse(queue.HasActions());
        }
        
        [TestMethod]
        public void ActionQueue_FIFOOrder()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion1 = PotionItems.HPPotion();
            var potion2 = PotionItems.MPPotion();
            var skill = new HealSkill();
            
            // Act
            queue.EnqueueItem(potion1, 0);
            queue.EnqueueSkill(skill);
            queue.EnqueueItem(potion2, 1);
            
            // Assert
            Assert.AreEqual(3, queue.Count);
            
            var action1 = queue.Dequeue();
            Assert.AreEqual(QueuedActionType.UseItem, action1.ActionType);
            Assert.AreEqual(potion1, action1.Consumable);
            
            var action2 = queue.Dequeue();
            Assert.AreEqual(QueuedActionType.UseSkill, action2.ActionType);
            Assert.AreEqual(skill, action2.Skill);
            
            var action3 = queue.Dequeue();
            Assert.AreEqual(QueuedActionType.UseItem, action3.ActionType);
            Assert.AreEqual(potion2, action3.Consumable);
            
            Assert.IsFalse(queue.HasActions());
        }
        
        [TestMethod]
        public void ActionQueue_DequeueEmptyReturnsNull()
        {
            // Arrange
            var queue = new ActionQueue();
            
            // Act
            var action = queue.Dequeue();
            
            // Assert
            Assert.IsNull(action);
        }
        
        [TestMethod]
        public void ActionQueue_PeekDoesNotRemove()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion = PotionItems.HPPotion();
            queue.EnqueueItem(potion, 0);
            
            // Act
            var peeked = queue.Peek();
            
            // Assert
            Assert.IsNotNull(peeked);
            Assert.AreEqual(potion, peeked.Consumable);
            Assert.IsTrue(queue.HasActions());
            Assert.AreEqual(1, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_CanClear()
        {
            // Arrange
            var queue = new ActionQueue();
            queue.EnqueueItem(PotionItems.HPPotion(), 0);
            queue.EnqueueItem(PotionItems.MPPotion(), 1);
            queue.EnqueueSkill(new HealSkill());
            
            // Act
            queue.Clear();
            
            // Assert
            Assert.IsFalse(queue.HasActions());
            Assert.AreEqual(0, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_RespectMaxQueueSize()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion = PotionItems.HPPotion();
            
            // Act - Enqueue up to max size (5)
            for (int i = 0; i < ActionQueue.MaxQueueSize; i++)
            {
                bool result = queue.EnqueueItem(potion, i);
                Assert.IsTrue(result, $"Should be able to enqueue item {i + 1}");
            }
            
            // Assert - Queue should be full
            Assert.AreEqual(ActionQueue.MaxQueueSize, queue.Count);
            
            // Act - Try to enqueue one more item
            bool overflowResult = queue.EnqueueItem(potion, 99);
            
            // Assert - Should not be able to enqueue beyond max
            Assert.IsFalse(overflowResult, "Should not be able to enqueue beyond max size");
            Assert.AreEqual(ActionQueue.MaxQueueSize, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_RespectMaxQueueSizeForSkills()
        {
            // Arrange
            var queue = new ActionQueue();
            var skill = new HealSkill();
            
            // Act - Enqueue up to max size (5)
            for (int i = 0; i < ActionQueue.MaxQueueSize; i++)
            {
                bool result = queue.EnqueueSkill(skill);
                Assert.IsTrue(result, $"Should be able to enqueue skill {i + 1}");
            }
            
            // Assert - Queue should be full
            Assert.AreEqual(ActionQueue.MaxQueueSize, queue.Count);
            
            // Act - Try to enqueue one more skill
            bool overflowResult = queue.EnqueueSkill(skill);
            
            // Assert - Should not be able to enqueue beyond max
            Assert.IsFalse(overflowResult, "Should not be able to enqueue beyond max size");
            Assert.AreEqual(ActionQueue.MaxQueueSize, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_GetAllReturnsAllActions()
        {
            // Arrange
            var queue = new ActionQueue();
            var potion1 = PotionItems.HPPotion();
            var potion2 = PotionItems.MPPotion();
            var skill = new HealSkill();
            
            queue.EnqueueItem(potion1, 0);
            queue.EnqueueSkill(skill);
            queue.EnqueueItem(potion2, 1);
            
            // Act
            var actions = queue.GetAll();
            
            // Assert
            Assert.IsNotNull(actions);
            Assert.AreEqual(3, actions.Length);
            Assert.AreEqual(QueuedActionType.UseItem, actions[0].ActionType);
            Assert.AreEqual(potion1, actions[0].Consumable);
            Assert.AreEqual(QueuedActionType.UseSkill, actions[1].ActionType);
            Assert.AreEqual(skill, actions[1].Skill);
            Assert.AreEqual(QueuedActionType.UseItem, actions[2].ActionType);
            Assert.AreEqual(potion2, actions[2].Consumable);
            
            // GetAll should not modify the queue
            Assert.AreEqual(3, queue.Count);
        }
        
        [TestMethod]
        public void ActionQueue_GetAllReturnsEmptyArrayWhenEmpty()
        {
            // Arrange
            var queue = new ActionQueue();
            
            // Act
            var actions = queue.GetAll();
            
            // Assert
            Assert.IsNotNull(actions);
            Assert.AreEqual(0, actions.Length);
        }
    }
}
