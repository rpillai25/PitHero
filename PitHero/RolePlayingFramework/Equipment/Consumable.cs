using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Basic consumable that grants a one-time effect.</summary>
    public sealed class Consumable : IItem
    {
        public string Name { get; }
        public ItemKind Kind => ItemKind.Consumable; //one-time use items that go away upon consuming
        public ItemRarity Rarity { get; }
        public StatBlock StatBonus { get; }
        public int AttackBonus => 0; // Consumables don't have these bonuses
        public int DefenseBonus => 0; // Consumables don't have these bonuses
        public int HPBonus => 0; // Consumables don't have these bonuses
        public int APBonus => 0; // Consumables don't have these bonuses

        public Consumable(string name, ItemRarity rarity, in StatBlock stats)
        {
            Name = name;
            Rarity = rarity;
            StatBonus = stats;
        }
    }
}
