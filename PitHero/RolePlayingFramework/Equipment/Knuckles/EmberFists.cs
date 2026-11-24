using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;
using PitHero;

namespace RolePlayingFramework.Equipment.Knuckles
{
    /// <summary>Factory for creating Ember Fists gear.</summary>
    public static class EmberFists
    {
        private const int PitLevel = 9;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                InventoryTextKey.Inv_EmberFists_Name,
                ItemKind.WeaponKnuckle,
                Rarity,
                InventoryTextKey.Inv_EmberFists_Desc,
                250,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
