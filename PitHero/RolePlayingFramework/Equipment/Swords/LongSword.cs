using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Long Sword gear.</summary>
    public static class LongSword
    {
        public static Gear Create() => new Gear(
            "LongSword",
            ItemKind.WeaponSword,
            ItemRarity.Normal,
            "+4 Attack",
            150,
            new StatBlock(0, 0, 0, 0),
            atk: 4);
    }
}
