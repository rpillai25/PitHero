using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Knuckles
{
    /// <summary>Factory for creating Quartz Knuckles gear.</summary>
    public static class QuartzKnuckles
    {
        private const int PitLevel = 13;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_QuartzKnuckles_Name,
                ItemKind.WeaponKnuckle,
                Rarity,
                InventoryTextKey.Inv_QuartzKnuckles_Desc,
                450,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
