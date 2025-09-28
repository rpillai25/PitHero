using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Common interface for all inventory items.</summary>
    public interface IItem
    {
        /// <summary>Display name.</summary>
        string Name { get; }

        /// <summary>Item category.</summary>
        ItemKind Kind { get; }

        /// <summary>Item rarity level.</summary>
        ItemRarity Rarity { get; }

        /// <summary>Stat modifiers when equipped (for gear) or consumed (for consumables).</summary>
        StatBlock StatBonus { get; }

        /// <summary>Optional flat attack/defense modifiers (positive or negative).</summary>
        int AttackBonus { get; }
        int DefenseBonus { get; }

        /// <summary>Optional flat HP/AP modifiers (only for gear).</summary>
        int HPBonus { get; }
        int APBonus { get; }
    }
}
