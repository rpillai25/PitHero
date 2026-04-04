using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Rods
{
    /// <summary>Factory for creating Ember Rod gear.</summary>
    public static class EmberRod
    {
        private const int PitLevel = 21;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_EmberRod_Name,
                ItemKind.WeaponRod,
                Rarity,
                "Staff topped with ever-burning ember.",
                650,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
