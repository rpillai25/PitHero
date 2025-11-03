using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Long Sword gear.</summary>
    /// <remarks>
    /// Future Enhancement: Consider using BalanceConfig for procedural generation:
    /// - Attack: BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity)
    /// - Stats: BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity)
    /// This would allow weapon stats to scale with dungeon depth and rarity.
    /// </remarks>
    public static class LongSword
    {
        public static Gear Create() => new Gear(
            "LongSword",
            ItemKind.WeaponSword,
            ItemRarity.Normal,
            "+4 Attack",
            150,
            new StatBlock(0, 0, 0, 0),
            atk: 4,
            elementalProps: new ElementalProperties(ElementType.Fire));
    }
}
