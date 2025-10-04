using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Wooden Shield gear.</summary>
    public static class WoodenShield
    {
        public static Gear Create() => new Gear(
            "WoodenShield",
            ItemKind.Shield,
            ItemRarity.Normal,
            "+2 Defense",
            80,
            new StatBlock(0, 0, 0, 0),
            def: 2);
    }
}
