using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Hammers
{
    /// <summary>Factory for creating Mallet gear.</summary>
    public static class Mallet
    {
        private const int PitLevel = 3;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_Mallet_Name,
                ItemKind.WeaponHammer,
                Rarity,
                InventoryTextKey.Inv_Mallet_Desc,
                100,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}
