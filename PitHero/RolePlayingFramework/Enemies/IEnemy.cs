using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Defines an enemy with stats and a basic attack kind.</summary>
    public interface IEnemy
    {
        /// <summary>Enemy display name.</summary>
        string Name { get; }

        /// <summary>Enemy level for scaling.</summary>
        int Level { get; }

        /// <summary>Base stats for the enemy.</summary>
        StatBlock Stats { get; }

        /// <summary>Type of damage the basic attack deals.</summary>
        DamageKind AttackKind { get; }

        /// <summary>Elemental type of the enemy.</summary>
        ElementType Element { get; }

        /// <summary>Elemental properties including resistances and weaknesses.</summary>
        ElementalProperties ElementalProps { get; }

        /// <summary>Current and maximum HP.</summary>
        int MaxHP { get; }
        int CurrentHP { get; }

        /// <summary>Experience awarded when defeated.</summary>
        int ExperienceYield { get; }

        /// <summary>Job Points awarded when defeated.</summary>
        int JPYield { get; }

        /// <summary>Synergy Points awarded when defeated.</summary>
        int SPYield { get; }

        /// <summary>Inflicts damage, returns true if died.</summary>
        bool TakeDamage(int amount);
    }
}
