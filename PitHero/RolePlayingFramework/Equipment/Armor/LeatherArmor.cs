using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Leather Armor gear.</summary>
    public static class LeatherArmor
    {
        private const int PitLevel = 5;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_LeatherArmor_Name,
                ItemKind.ArmorMail,
                Rarity,
                InventoryTextKey.Inv_LeatherArmor_Desc,
                120,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral));
        }
    }
}
