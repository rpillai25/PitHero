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

        /// <summary>Current and maximum HP.</summary>
        int MaxHP { get; }
        int CurrentHP { get; }

        /// <summary>Inflicts damage, returns true if died.</summary>
        bool TakeDamage(int amount);
    }
}
