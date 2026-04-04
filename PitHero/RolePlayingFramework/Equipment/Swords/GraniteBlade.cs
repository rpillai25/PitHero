using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Granite Blade gear.</summary>
    public static class GraniteBlade
    {
        private const int PitLevel = 7;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_GraniteBlade_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_GraniteBlade_Desc,
                200,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
