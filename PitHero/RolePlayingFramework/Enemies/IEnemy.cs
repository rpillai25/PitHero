using RolePlayingFramework.Combat;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Enemies
{
    /// <summary>Defines an enemy with stats and a basic attack kind.</summary>
    public interface IEnemy
    {
        /// <summary>Strongly-typed identifier for this enemy type.</summary>
        EnemyId EnemyId { get; }

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

        /// <summary>Gold awarded when defeated.</summary>
        int GoldYield { get; }

        /// <summary>
        /// Multiplier applied to the base join chance on defeat.
        /// Values above 1.0 increase chance, below 1.0 decrease it.
        /// </summary>
        float JoinPercentageModifier { get; }

        /// <summary>True if this enemy is a boss (stationary, one per floor, gates WizardOrb).</summary>
        bool IsBoss { get; }

        /// <summary>True if this enemy type can be recruited as an allied monster.</summary>
        bool IsRecruitable { get; }

        /// <summary>
        /// Vertical nudge (pixels, negative = raise) applied to a worn job prop (e.g. a kitchen hat)
        /// when this monster works a job. Defaults to 0 for sprites whose head sits at the top of the
        /// frame; types that seat the head lower override this so the prop doesn't cover the face.
        /// </summary>
        float HatYOffset => 0f;

        /// <summary>Inflicts damage, returns true if died.</summary>
        bool TakeDamage(int amount);
    }
}
