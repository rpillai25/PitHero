using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Basic consumable that grants a one-time effect.</summary>
    public sealed class Consumable : IItem
    {
        public string Name { get; }
        public ItemKind Kind => ItemKind.Consumable; //one-time use items that go away upon consuming
        public ItemRarity Rarity { get; }
        public int HPRestoreAmount { get; }
        public int APRestoreAmount { get; }

        public Consumable(string name, ItemRarity rarity, int hpRestoreAmount = 0, int apRestoreAmount = 0)
        {
            Name = name;
            Rarity = rarity;
            HPRestoreAmount = hpRestoreAmount;
            APRestoreAmount = apRestoreAmount;
        }

        /// <summary>Consume this item and apply its effect.</summary>
        /// <param name="context">Context object that can be used to apply effects (e.g., Hero, ItemBag, etc.)</param>
        /// <returns>True if the item was consumed successfully.</returns>
        public bool Consume(object context)
        {
            // Base implementation does nothing - specific consumables
            // can use a delegate pattern for custom effects
            return true;
        }
    }
}
