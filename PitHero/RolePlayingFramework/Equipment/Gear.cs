using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Simple gear item with stat and flat bonuses.
    /// Supports elemental types and resistances for integration with the elemental combat system.
    /// </summary>
    /// <remarks>
    /// Elemental System Integration:
    /// - All gear has an Element property (Neutral, Fire, Water, Earth, Wind, Light, Dark)
    /// - Weapons typically deal elemental damage matching their element
    /// - Defensive gear (armor/shields/helms) can have ElementalProperties with resistances
    /// - Resistances are percentage-based (0.25 = 25% damage reduction, -0.15 = 15% extra damage)
    /// - Defensive gear typically resists its own element and is weak to opposing element
    /// 
    /// Current Equipment Elemental Assignments:
    /// Weapons:
    ///   - ShortSword: Neutral
    ///   - LongSword: Fire
    /// 
    /// Armor:
    ///   - LeatherArmor: Neutral
    ///   - IronArmor: Earth (25% Earth resist, -15% Wind weakness)
    /// 
    /// Shields:
    ///   - WoodenShield: Neutral
    ///   - IronShield: Water (30% Water resist, -15% Fire weakness)
    /// 
    /// Helms:
    ///   - SquireHelm: Neutral
    ///   - IronHelm: Earth (20% Earth resist, -10% Wind weakness)
    /// 
    /// Accessories:
    ///   - RingOfPower: Neutral
    ///   - NecklaceOfHealth: Light
    ///   - ProtectRing: Neutral
    ///   - MagicChain: Dark
    /// </remarks>
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
        public ElementType Element { get; }
        public ElementalProperties ElementalProps { get; }

        /// <summary>
        /// Creates a new Gear item with specified properties.
        /// </summary>
        /// <param name="name">Display name of the gear item</param>
        /// <param name="kind">Type of equipment (weapon, armor, shield, etc.)</param>
        /// <param name="rarity">Item rarity level</param>
        /// <param name="description">Item description text</param>
        /// <param name="price">Gold cost to purchase</param>
        /// <param name="stats">Stat bonuses (Strength, Agility, Vitality, Magic)</param>
        /// <param name="atk">Flat attack bonus (for weapons primarily)</param>
        /// <param name="def">Flat defense bonus (for armor/shields/helms primarily)</param>
        /// <param name="hp">Flat HP bonus</param>
        /// <param name="mp">Flat MP bonus</param>
        /// <param name="element">Elemental type (defaults to Neutral). Used for damage calculations.</param>
        /// <param name="elementalProps">Optional custom elemental properties with resistances. If null, creates default properties matching the element parameter.</param>
        public Gear(string name, ItemKind kind, ItemRarity rarity, string description, int price, in StatBlock stats, int atk = 0, int def = 0, int hp = 0, int mp = 0, ElementType element = ElementType.Neutral, ElementalProperties elementalProps = null)
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
            Element = element;
            ElementalProps = elementalProps ?? new ElementalProperties(element);
        }
    }
}
