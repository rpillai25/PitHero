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

        /// <summary>Weapon item for visualization (if ActionType is Attack). Can be null for unarmed attacks.</summary>
        public RolePlayingFramework.Equipment.IItem WeaponItem { get; }

        /// <summary>Constructor for item action.</summary>
        public QueuedAction(RolePlayingFramework.Equipment.Consumable consumable, int bagIndex)
        {
            ActionType = QueuedActionType.UseItem;
            Consumable = consumable;
            BagIndex = bagIndex;
            Skill = null;
            WeaponItem = null;
        }

        /// <summary>Constructor for skill action.</summary>
        public QueuedAction(RolePlayingFramework.Skills.ISkill skill)
        {
            ActionType = QueuedActionType.UseSkill;
            Consumable = null;
            BagIndex = -1;
            Skill = skill;
            WeaponItem = null;
        }

        /// <summary>Constructor for attack action.</summary>
        public QueuedAction(RolePlayingFramework.Equipment.IItem weaponItem)
        {
            ActionType = QueuedActionType.Attack;
            Consumable = null;
            BagIndex = -1;
            Skill = null;
            WeaponItem = weaponItem;
        }
    }

    /// <summary>Type of queued action.</summary>
    public enum QueuedActionType
    {
        UseItem,
        UseSkill,
        Attack
    }

    /// <summary>
    /// Queue of actions for the hero to perform during battle.
    /// Hero will take the next action from this queue when their turn comes during battle.
    /// Maximum of 5 actions can be queued at once.
    /// </summary>
    public class ActionQueue
    {
        /// <summary>Maximum number of actions that can be queued.</summary>
        public const int MaxQueueSize = 5;

        private readonly Queue<QueuedAction> _queue;

        public ActionQueue()
        {
            _queue = new Queue<QueuedAction>(MaxQueueSize);
        }

        /// <summary>Enqueue an item to be used. Returns false if queue is full.</summary>
        public bool EnqueueItem(RolePlayingFramework.Equipment.Consumable consumable, int bagIndex)
        {
            if (_queue.Count >= MaxQueueSize)
                return false;
            _queue.Enqueue(new QueuedAction(consumable, bagIndex));
            return true;
        }

        /// <summary>Enqueue a skill to be used. Returns false if queue is full.</summary>
        public bool EnqueueSkill(RolePlayingFramework.Skills.ISkill skill)
        {
            if (_queue.Count >= MaxQueueSize)
                return false;
            _queue.Enqueue(new QueuedAction(skill));
            return true;
        }

        /// <summary>Enqueue an attack action. Returns false if queue is full.</summary>
        public bool EnqueueAttack(RolePlayingFramework.Equipment.IItem weaponItem)
        {
            if (_queue.Count >= MaxQueueSize)
                return false;
            _queue.Enqueue(new QueuedAction(weaponItem));
            return true;
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

        /// <summary>Get all queued actions as an array for display purposes.</summary>
        public QueuedAction[] GetAll()
        {
            return _queue.ToArray();
        }
    }
}
