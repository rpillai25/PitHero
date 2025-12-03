using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Represents an action that can be queued for the hero to perform during battle.
    /// </summary>
    public class QueuedAction
    {
        /// <summary>Type of action to perform.</summary>
        public QueuedActionType ActionType { get; }
        
        /// <summary>Consumable item to use (if ActionType is UseItem).</summary>
        public RolePlayingFramework.Equipment.Consumable Consumable { get; }
        
        /// <summary>Bag index of the consumable (if ActionType is UseItem).</summary>
        public int BagIndex { get; }
        
        /// <summary>Skill to use (if ActionType is UseSkill).</summary>
        public RolePlayingFramework.Skills.ISkill Skill { get; }
        
        /// <summary>Constructor for item action.</summary>
        public QueuedAction(RolePlayingFramework.Equipment.Consumable consumable, int bagIndex)
        {
            ActionType = QueuedActionType.UseItem;
            Consumable = consumable;
            BagIndex = bagIndex;
            Skill = null;
        }
        
        /// <summary>Constructor for skill action.</summary>
        public QueuedAction(RolePlayingFramework.Skills.ISkill skill)
        {
            ActionType = QueuedActionType.UseSkill;
            Consumable = null;
            BagIndex = -1;
            Skill = skill;
        }
    }
    
    /// <summary>Type of queued action.</summary>
    public enum QueuedActionType
    {
        UseItem,
        UseSkill
    }
    
    /// <summary>
    /// Queue of actions for the hero to perform during battle.
    /// Hero will take the next action from this queue when their turn comes during battle.
    /// </summary>
    public class ActionQueue
    {
        private readonly Queue<QueuedAction> _queue;
        
        public ActionQueue()
        {
            _queue = new Queue<QueuedAction>(4); // Pre-allocate small capacity
        }
        
        /// <summary>Enqueue an item to be used.</summary>
        public void EnqueueItem(RolePlayingFramework.Equipment.Consumable consumable, int bagIndex)
        {
            _queue.Enqueue(new QueuedAction(consumable, bagIndex));
        }
        
        /// <summary>Enqueue a skill to be used.</summary>
        public void EnqueueSkill(RolePlayingFramework.Skills.ISkill skill)
        {
            _queue.Enqueue(new QueuedAction(skill));
        }
        
        /// <summary>Dequeue the next action.</summary>
        public QueuedAction Dequeue()
        {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }
        
        /// <summary>Peek at the next action without removing it.</summary>
        public QueuedAction Peek()
        {
            return _queue.Count > 0 ? _queue.Peek() : null;
        }
        
        /// <summary>Check if the queue has any actions.</summary>
        public bool HasActions()
        {
            return _queue.Count > 0;
        }
        
        /// <summary>Get the number of actions in the queue.</summary>
        public int Count => _queue.Count;
        
        /// <summary>Clear all actions from the queue.</summary>
        public void Clear()
        {
            _queue.Clear();
        }
    }
}
