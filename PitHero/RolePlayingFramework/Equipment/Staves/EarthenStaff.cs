using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Staves
{
    /// <summary>Factory for creating Earthen Staff gear.</summary>
    public static class EarthenStaff
    {
        private const int PitLevel = 11;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "EarthenStaff",
                ItemKind.WeaponStaff,
                Rarity,
                "Staff embedded with earth crystals.",
                400,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
