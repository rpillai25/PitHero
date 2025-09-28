using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Simple gear item with stat and flat bonuses.</summary>
    public sealed class Gear : IItem
    {
        public string Name { get; }
        public ItemKind Kind { get; }
        public ItemRarity Rarity { get; }
        public StatBlock StatBonus { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int HPBonus { get; }
        public int APBonus { get; }

        public Gear(string name, ItemKind kind, ItemRarity rarity, in StatBlock stats, int atk = 0, int def = 0, int hp = 0, int ap = 0)
        {
            Name = name;
            Kind = kind;
            Rarity = rarity;
            StatBonus = stats;
            AttackBonus = atk;
            DefenseBonus = def;
            HPBonus = hp;
            APBonus = ap;
        }
    }
}
