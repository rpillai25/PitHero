using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Simple gear item with stat and flat bonuses.</summary>
    public sealed class Gear : IItem
    {
        public string Name { get; }
        public ItemKind Kind { get; }
        public StatBlock StatBonus { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public bool IsConsumable => false;

        public Gear(string name, ItemKind kind, in StatBlock stats, int atk = 0, int def = 0)
        {
            Name = name;
            Kind = kind;
            StatBonus = stats;
            AttackBonus = atk;
            DefenseBonus = def;
        }
    }
}
