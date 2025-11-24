using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Iron Armor gear.</summary>
    public static class IronArmor
    {
        private const int PitLevel = 15;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "IronArmor",
                ItemKind.ArmorMail,
                Rarity,
                $"Regular armor for soldiers.",
                180,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(
                    ElementType.Earth,
                    new Dictionary<ElementType, float>
                    {
                        { ElementType.Earth, 0.25f },   // 25% resistance to Earth
                        { ElementType.Wind, -0.15f }    // 15% weakness to Wind (opposing element)
                    }));
        }
    }
}
