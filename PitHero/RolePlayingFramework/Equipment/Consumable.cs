using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Basic consumable that grants a one-time effect.</summary>
    public sealed class Consumable : IItem
    {
        public string Name { get; }
        public ItemKind Kind => ItemKind.Consumable; //one-time use items that go away upon consuming
        public StatBlock StatBonus { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }

        public Consumable(string name, in StatBlock stats, int atk = 0, int def = 0)
        {
            Name = name;
            StatBonus = stats;
            AttackBonus = atk;
            DefenseBonus = def;
        }
    }
}
