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

        /// <summary>Stat modifiers when equipped (for gear) or consumed (for consumables).</summary>
        StatBlock StatBonus { get; }

        /// <summary>Optional flat attack/defense modifiers (positive or negative).</summary>
        int AttackBonus { get; }
        int DefenseBonus { get; }

        /// <summary>If true, item is consumable and removed on use.</summary>
        bool IsConsumable { get; }
    }
}
