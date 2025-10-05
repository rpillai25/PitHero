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
            "+2 Magic, +5 MP",
            200,
            new StatBlock(0, 0, 2, 0),
            ap: 5);
    }
}
