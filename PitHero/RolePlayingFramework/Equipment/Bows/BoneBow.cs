using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Bows
{
    /// <summary>Factory for creating Bone Bow gear.</summary>
    public static class BoneBow
    {
        private const int PitLevel = 5;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_BoneBow_Name,
                ItemKind.WeaponBow,
                Rarity,
                InventoryTextKey.Inv_BoneBow_Desc,
                150,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
