using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Iron Shield gear.</summary>
    public static class IronShield
    {
        public static Gear Create() => new Gear(
            "IronShield",
            ItemKind.Shield,
            ItemRarity.Normal,
            "+3 Defense",
            120,
            new StatBlock(0, 0, 0, 0),
            def: 3);
    }
}
