using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Knuckles
{
    /// <summary>Factory for creating Stone Knuckles gear.</summary>
    public static class StoneKnuckles
    {
        private const int PitLevel = 5;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_StoneKnuckles_Name,
                ItemKind.WeaponKnuckle,
                Rarity,
                InventoryTextKey.Inv_StoneKnuckles_Desc,
                150,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
