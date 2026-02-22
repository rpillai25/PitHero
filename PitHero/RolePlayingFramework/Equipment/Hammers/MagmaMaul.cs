using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Hammers
{
    /// <summary>Factory for creating Magma Maul gear.</summary>
    public static class MagmaMaul
    {
        private const int PitLevel = 25;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(PitLevel, Rarity);
            return new Gear(
                "MagmaMaul",
                ItemKind.WeaponKnuckle,
                Rarity,
                "Molten-core war hammer.",
                750,
                new StatBlock(0, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
