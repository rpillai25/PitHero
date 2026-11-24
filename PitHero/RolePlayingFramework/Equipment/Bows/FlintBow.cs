using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Bows
{
    /// <summary>Factory for creating Flint Bow gear.</summary>
    public static class FlintBow
    {
        private const int PitLevel = 9;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_FlintBow_Name,
                ItemKind.WeaponBow,
                Rarity,
                InventoryTextKey.Inv_FlintBow_Desc,
                250,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
