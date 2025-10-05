using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Iron Armor gear.</summary>
    public static class IronArmor
    {
        public static Gear Create() => new Gear(
            "IronArmor",
            ItemKind.ArmorMail,
            ItemRarity.Normal,
            "+4 Defense",
            180,
            new StatBlock(0, 0, 0, 0),
            def: 4);
    }
}
