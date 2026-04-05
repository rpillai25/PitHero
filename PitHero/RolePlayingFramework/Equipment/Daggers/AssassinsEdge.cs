using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Daggers
{
    /// <summary>Factory for creating Assassin's Edge gear.</summary>
    public static class AssassinsEdge
    {
        private const int PitLevel = 22;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_AssassinsEdge_Name,
                ItemKind.WeaponKnife,
                Rarity,
                InventoryTextKey.Inv_AssassinsEdge_Desc,
                675,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
