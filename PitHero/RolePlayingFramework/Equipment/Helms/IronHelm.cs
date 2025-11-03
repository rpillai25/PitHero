using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Iron Helm gear.</summary>
    public static class IronHelm
    {
        private const int PitLevel = 15;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "IronHelm",
                ItemKind.HatHelm,
                Rarity,
                $"+{defenseBonus} Defense, Earth Resistant",
                135,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(
                    ElementType.Earth,
                    new Dictionary<ElementType, float>
                    {
                        { ElementType.Earth, 0.20f },   // 20% resistance to Earth
                        { ElementType.Wind, -0.10f }    // 10% weakness to Wind (opposing element)
                    }));
        }
    }
}
