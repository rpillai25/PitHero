using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Necklace of Health gear.</summary>
    public static class NecklaceOfHealth
    {
        public static Gear Create() => new Gear(
            "NecklaceOfHealth",
            ItemKind.Accessory,
            ItemRarity.Normal,
            "+10 HP",
            150,
            new StatBlock(0, 0, 0, 0),
            hp: 10);
    }
}
