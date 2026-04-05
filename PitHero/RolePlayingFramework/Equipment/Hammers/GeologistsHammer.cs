using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Hammers
{
    /// <summary>Factory for creating Geologist's Hammer gear.</summary>
    public static class GeologistsHammer
    {
        private const int PitLevel = 12;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_GeologistsHammer_Name,
                ItemKind.WeaponHammer,
                Rarity,
                InventoryTextKey.Inv_GeologistsHammer_Desc,
                425,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
