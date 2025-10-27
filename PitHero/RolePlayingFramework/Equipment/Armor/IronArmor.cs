using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Iron Armor gear.</summary>
    /// <remarks>
    /// Future Enhancement: Consider using BalanceConfig for procedural generation:
    /// - Defense: BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity)
    /// - Stats: BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity)
    /// This would allow armor stats to scale with dungeon depth.
    /// </remarks>
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
