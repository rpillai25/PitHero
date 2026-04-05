using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Staves
{
    /// <summary>Factory for creating Shadowwood Staff gear.</summary>
    public static class ShadowwoodStaff
    {
        private const int PitLevel = 16;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_ShadowwoodStaff_Name,
                ItemKind.WeaponStaff,
                Rarity,
                InventoryTextKey.Inv_ShadowwoodStaff_Desc,
                525,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
