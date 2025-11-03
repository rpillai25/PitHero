using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Ring of Power gear.</summary>
    public static class RingOfPower
    {
        public static Gear Create() => new Gear(
            "RingOfPower",
            ItemKind.Accessory,
            ItemRarity.Uncommon,
            "+1 Strength",
            150,
            new StatBlock(1, 0, 0, 0),
            elementalProps: new ElementalProperties(ElementType.Neutral));
    }
}
