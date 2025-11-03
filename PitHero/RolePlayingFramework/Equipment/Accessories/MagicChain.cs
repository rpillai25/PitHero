using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Magic Chain gear.</summary>
    public static class MagicChain
    {
        public static Gear Create() => new Gear(
            "MagicChain",
            ItemKind.Accessory,
            ItemRarity.Uncommon,
            "+2 Magic",
            200,
            new StatBlock(0, 0, 0, 2),
            mp: 5,
            elementalProps: new ElementalProperties(ElementType.Dark));
    }
}
