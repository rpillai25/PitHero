using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Bows
{
    /// <summary>Factory for creating Inferno Bow gear.</summary>
    public static class InfernoBow
    {
        private const int PitLevel = 23;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_InfernoBow_Name,
                ItemKind.WeaponBow,
                Rarity,
                InventoryTextKey.Inv_InfernoBow_Desc,
                700,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
