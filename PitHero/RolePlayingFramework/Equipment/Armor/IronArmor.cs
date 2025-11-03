using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

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
            "+4 Defense, Earth Resistant",
            180,
            new StatBlock(0, 0, 0, 0),
            def: 4,
            element: ElementType.Earth,
            elementalProps: new ElementalProperties(
                ElementType.Earth,
                new Dictionary<ElementType, float>
                {
                    { ElementType.Earth, 0.25f },   // 25% resistance to Earth
                    { ElementType.Wind, -0.15f }    // 15% weakness to Wind (opposing element)
                }));
    }
}
