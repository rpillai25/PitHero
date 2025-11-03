using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Wooden Shield gear.</summary>
    /// <remarks>
    /// Future Enhancement: Consider using BalanceConfig for procedural generation:
    /// - Defense: BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity)
    /// This would allow shield stats to scale with dungeon depth.
    /// </remarks>
    public static class WoodenShield
    {
        public static Gear Create() => new Gear(
            "WoodenShield",
            ItemKind.Shield,
            ItemRarity.Normal,
            "+2 Defense",
            80,
            new StatBlock(0, 0, 0, 0),
            def: 2,
            element: ElementType.Neutral);
    }
}
