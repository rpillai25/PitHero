using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Protect Ring gear.</summary>
    public static class ProtectRing
    {
        public static Gear Create() => new Gear(
            "ProtectRing",
            ItemKind.Accessory,
            ItemRarity.Normal,
            "+2 Defense",
            120,
            new StatBlock(0, 0, 0, 0),
            def: 2);
    }
}
