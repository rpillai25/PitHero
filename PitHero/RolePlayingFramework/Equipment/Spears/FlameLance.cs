using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Flame Lance gear.</summary>
    public static class FlameLance
    {
        private const int PitLevel = 15;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_FlameLance_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_FlameLance_Desc,
                500,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
