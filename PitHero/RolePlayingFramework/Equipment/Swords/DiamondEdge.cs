using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Diamond Edge gear.</summary>
    public static class DiamondEdge
    {
        private const int PitLevel = 23;
        private static readonly ItemRarity Rarity = RarityUtils.GetRarityForBiomeLevel(PitLevel);

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_DiamondEdge_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_DiamondEdge_Desc,
                700,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
