using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Helms
{
    /// <summary>Factory for creating Diamond Circlet gear.</summary>
    public static class DiamondCirclet
    {
        private const int PitLevel = 23;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "DiamondCirclet",
                ItemKind.HatHeadband,
                Rarity,
                "Headpiece with diamond reinforcement.",
                1000,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
