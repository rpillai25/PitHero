using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Squire Helm gear.</summary>
    public static class SquireHelm
    {
        public static Gear Create() => new Gear(
            "SquireHelm",
            ItemKind.HatHelm,
            ItemRarity.Normal,
            "+2 Defense",
            90,
            new StatBlock(0, 0, 0, 0),
            def: 2);
    }
}
