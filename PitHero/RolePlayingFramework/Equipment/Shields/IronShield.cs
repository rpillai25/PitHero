using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Shields
{
    /// <summary>Factory for creating Iron Shield gear.</summary>
    public static class IronShield
    {
        private const int PitLevel = 15;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "IronShield",
                ItemKind.Shield,
                Rarity,
                $"Regular shield of soldiers.",
                120,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(
                    ElementType.Water,
                    new Dictionary<ElementType, float>
                    {
                        { ElementType.Water, 0.30f },   // 30% resistance to Water
                        { ElementType.Fire, -0.15f }    // 15% weakness to Fire (opposing element)
                    }));
        }
    }
}
