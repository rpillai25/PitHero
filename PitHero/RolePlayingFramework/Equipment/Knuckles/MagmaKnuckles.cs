using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Knuckles
{
    /// <summary>Factory for creating Magma Knuckles gear.</summary>
    public static class MagmaKnuckles
    {
        private const int PitLevel = 23;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_MagmaKnuckles_Name,
                ItemKind.WeaponKnuckle,
                Rarity,
                InventoryTextKey.Inv_MagmaKnuckles_Desc,
                700,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
