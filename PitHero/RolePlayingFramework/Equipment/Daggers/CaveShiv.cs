using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Daggers
{
    /// <summary>Factory for creating Cave Shiv gear.</summary>
    public static class CaveShiv
    {
        private const int PitLevel = 4;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_CaveShiv_Name,
                ItemKind.WeaponKnife,
                Rarity,
                InventoryTextKey.Inv_CaveShiv_Desc,
                125,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
