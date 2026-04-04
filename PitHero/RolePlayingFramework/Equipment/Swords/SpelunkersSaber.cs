using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Swords
{
    /// <summary>Factory for creating Spelunker's Saber gear.</summary>
    public static class SpelunkersSaber
    {
        private const int PitLevel = 6;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_SpelunkersSaber_Name,
                ItemKind.WeaponSword,
                Rarity,
                InventoryTextKey.Inv_SpelunkersSaber_Desc,
                175,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
