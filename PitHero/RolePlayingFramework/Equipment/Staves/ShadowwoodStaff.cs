using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Staves
{
    /// <summary>Factory for creating Shadowwood Staff gear.</summary>
    public static class ShadowwoodStaff
    {
        private const int PitLevel = 16;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "ShadowwoodStaff",
                ItemKind.WeaponStaff,
                Rarity,
                "Staff carved from black petrified wood.",
                525,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Dark));
        }
    }
}
