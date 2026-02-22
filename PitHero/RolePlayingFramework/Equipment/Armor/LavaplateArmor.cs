using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment.Armor
{
    /// <summary>Factory for creating Lavaplate Armor gear.</summary>
    public static class LavaplateArmor
    {
        private const int PitLevel = 17;
        private const ItemRarity Rarity = ItemRarity.Uncommon;

        public static Gear Create()
        {
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(PitLevel, Rarity);
            return new Gear(
                "LavaplateArmor",
                ItemKind.ArmorMail,
                Rarity,
                "Magma-forged heavy armor.",
                700,
                new StatBlock(0, 0, 0, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Fire));
        }
    }
}
