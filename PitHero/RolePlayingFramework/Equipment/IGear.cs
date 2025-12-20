using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Interface for equippable gear items with stat bonuses.</summary>
    public interface IGear : IItem
    {
        /// <summary>Stat modifiers when equipped.</summary>
        StatBlock StatBonus { get; }

        /// <summary>Optional flat attack/defense modifiers (positive or negative).</summary>
        int AttackBonus { get; }
        int DefenseBonus { get; }

        /// <summary>Optional flat HP/MP modifiers.</summary>
        int HPBonus { get; }
        int MPBonus { get; }

        /// <summary>Elemental properties including type and resistances/weaknesses.</summary>
        ElementalProperties ElementalProps { get; }
    }
}