using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Axes
{
    /// <summary>Factory for creating Stone Hatchet gear.</summary>
    public static class StoneHatchet
    {
        private const int PitLevel = 5;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_StoneHatchet_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_StoneHatchet_Desc,
                150,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
