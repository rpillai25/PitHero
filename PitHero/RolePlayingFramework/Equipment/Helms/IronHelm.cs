using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Iron Helm gear.</summary>
    public static class IronHelm
    {
        public static Gear Create() => new Gear(
            "IronHelm",
            ItemKind.HatHelm,
            ItemRarity.Normal,
            "+3 Defense",
            135,
            new StatBlock(0, 0, 0, 0),
            def: 3);
    }
}
