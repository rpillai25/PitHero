using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Knuckles
{
    /// <summary>Factory for creating Shadow Claws gear.</summary>
    public static class ShadowClaws
    {
        private const int PitLevel = 18;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_ShadowClaws_Name,
                ItemKind.WeaponKnuckle,
                Rarity,
                InventoryTextKey.Inv_ShadowClaws_Desc,
                575,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
