using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Defines a job (vocation) that augments stats and grants skills.</summary>
    public interface IJob
    {
        /// <summary>Display name of the job (may require TextService; use NameKey for persistence).</summary>
        string Name { get; }

        /// <summary>Raw localization key used to identify and persist this job. Safe to call without TextService.</summary>
        string NameKey { get; }

        /// <summary>Short description of the job's identity and specialty.</summary>
        string Description { get; }

        /// <summary>Brief summary of the job's combat role and stat profile.</summary>
        string Role { get; }

        /// <summary>Base stat bonus applied at level 1.</summary>
        StatBlock BaseBonus { get; }

        /// <summary>Per-level stat growth added each time the hero levels up (from 2..N).</summary>
        StatBlock GrowthPerLevel { get; }

        /// <summary>All skill definitions (active + passive).</summary>
        IReadOnlyList<ISkill> Skills { get; }

        /// <summary>Tier of this job (Primary, Secondary, or Tertiary).</summary>
        JobTier Tier { get; }

        /// <summary>Bitflag identifying this job for equipment restriction checks.</summary>
        JobType JobFlag { get; }

        /// <summary>Computes total job stat contribution at a given level.</summary>
        StatBlock GetJobContributionAtLevel(int level);
    }
}
