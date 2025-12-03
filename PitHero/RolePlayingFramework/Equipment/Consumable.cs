using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Abstract base consumable with one-time effect.</summary>
    public abstract class Consumable : IItem
    {
        public string Name { get; }
        public ItemKind Kind => ItemKind.Consumable;
        public ItemRarity Rarity { get; }
        public string Description { get; protected set; }
        public int Price { get; protected set; }
        public int HPRestoreAmount { get; protected set; }
        public int MPRestoreAmount { get; protected set; }
        public int StackSize { get; protected set; }
        public int StackCount { get; set; }
        
        /// <summary>True if this consumable can only be used during battle.</summary>
        public bool BattleOnly { get; protected set; }

        protected Consumable(string name, ItemRarity rarity, string description, int price, int hpRestoreAmount = 0, int mpRestoreAmount = 0, bool battleOnly = false)
        {
            Name = name;
            Rarity = rarity;
            Description = description;
            Price = price;
            HPRestoreAmount = hpRestoreAmount;
            MPRestoreAmount = mpRestoreAmount;
            StackSize = 16;
            StackCount = 1;
            BattleOnly = battleOnly;
        }

        /// <summary>Consume this item and apply its effect.</summary>
        public virtual bool Consume(object context)
        {
            return true; // base does nothing
        }
    }
}
