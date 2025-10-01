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
        public int APRestoreAmount { get; protected set; }

        protected Consumable(string name, ItemRarity rarity, string description, int price, int hpRestoreAmount = 0, int apRestoreAmount = 0)
        {
            Name = name;
            Rarity = rarity;
            Description = description;
            Price = price;
            HPRestoreAmount = hpRestoreAmount;
            APRestoreAmount = apRestoreAmount;
        }

        /// <summary>Consume this item and apply its effect.</summary>
        public virtual bool Consume(object context)
        {
            return true; // base does nothing
        }
    }
}
