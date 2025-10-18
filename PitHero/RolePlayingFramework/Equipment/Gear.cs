using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Simple gear item with stat and flat bonuses.</summary>
    public sealed class Gear : IGear
    {
        public string Name { get; }
        public ItemKind Kind { get; }
        public ItemRarity Rarity { get; }
        public string Description { get; }
        public int Price { get; }
        public StatBlock StatBonus { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int HPBonus { get; }
        public int MPBonus { get; }

        public Gear(string name, ItemKind kind, ItemRarity rarity, string description, int price, in StatBlock stats, int atk = 0, int def = 0, int hp = 0, int mp = 0)
        {
            Name = name;
            Kind = kind;
            Rarity = rarity;
            Description = description;
            Price = price;
            StatBonus = stats;
            AttackBonus = atk;
            DefenseBonus = def;
            HPBonus = hp;
            MPBonus = mp;
        }
    }
}
