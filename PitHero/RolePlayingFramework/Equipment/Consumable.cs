using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Basic consumable that grants a one-time effect.</summary>
    public sealed class Consumable : IItem
    {
        public string Name { get; }
        public ItemKind Kind => ItemKind.Accessory; // non-gear bucket
        public StatBlock StatBonus { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public bool IsConsumable => true;

        public Consumable(string name, in StatBlock stats, int atk = 0, int def = 0)
        {
            Name = name;
            StatBonus = stats;
            AttackBonus = atk;
            DefenseBonus = def;
        }
    }
}
