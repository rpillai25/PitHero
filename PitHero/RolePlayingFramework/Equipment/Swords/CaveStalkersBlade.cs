using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Cave Stalker's Blade gear.</summary>
    public static class CaveStalkersBlade
    {
        private const int PitLevel = 3;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_CaveStalkersBlade_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_CaveStalkersBlade_Desc,
                100,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
