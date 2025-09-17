using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Defines a job (vocation) that augments stats and grants abilities.</summary>
    public interface IJob
    {
        /// <summary>Display name of the job.</summary>
        string Name { get; }

        /// <summary>Base stat bonus applied at level 1.</summary>
        StatBlock BaseBonus { get; }

        /// <summary>Per-level stat growth added each time the hero levels up (from 2..N).</summary>
        StatBlock GrowthPerLevel { get; }

        /// <summary>Abilities granted passively by the job.</summary>
        JobAbility[] Abilities { get; }

        /// <summary>Computes total job stat contribution at a given level.</summary>
        StatBlock GetJobContributionAtLevel(int level);
    }
}
