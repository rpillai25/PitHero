using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Bows
{
    /// <summary>Factory for creating Twig Bow gear.</summary>
    public static class TwigBow
    {
        private const int PitLevel = 1;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_TwigBow_Name,
                ItemKind.WeaponBow,
                Rarity,
                InventoryTextKey.Inv_TwigBow_Desc,
                40,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}
