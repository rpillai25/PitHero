using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Spears
{
    /// <summary>Factory for creating Stone Lance gear.</summary>
    public static class StoneLance
    {
        private const int PitLevel = 6;
        private const ItemRarity Rarity = ItemRarity.Normal;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "StoneLance",
                ItemKind.WeaponSword,
                Rarity,
                "Stone-tipped thrusting weapon.",
                175,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Earth));
        }
    }
}
