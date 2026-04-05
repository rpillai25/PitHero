using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Accessories
{
    /// <summary>Factory for creating Magic Chain gear.</summary>
    public static class MagicChain
    {
        private const int PitLevel = 18;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int statBonus = BalanceConfig.CalculateEquipmentStatBonus(PitLevel, Rarity);
            // MP bonus scales with stat bonus for magic-focused items
            int mpBonus = statBonus * 3;
            return new Gear(
                InventoryTextKey.Inv_MagicChain_Name,
                ItemKind.Accessory,
                Rarity,
                InventoryTextKey.Inv_MagicChain_Desc,
                200,
                new StatBlock(0, 0, 0, statBonus),
                mp: mpBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
