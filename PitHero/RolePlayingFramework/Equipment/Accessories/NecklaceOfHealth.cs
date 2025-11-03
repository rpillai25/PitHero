using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Necklace of Health gear.</summary>
    public static class NecklaceOfHealth
    {
        public static Gear Create() => new Gear(
            "NecklaceOfHealth",
            ItemKind.Accessory,
            ItemRarity.Rare,
            "+10 HP",
            150,
            new StatBlock(0, 0, 2, 0),
            hp: 10,
            elementalProps: new ElementalProperties(ElementType.Light));
    }
}
