using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Hammers
{
    /// <summary>Factory for creating Stone Crusher gear.</summary>
    public static class StoneCrusher
    {
        private const int PitLevel = 7;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "StoneCrusher",
                ItemKind.WeaponHammer,
                Rarity,
                "Heavy stone-headed hammer.",
                200,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
