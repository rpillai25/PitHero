using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Base job implementation providing common behavior.</summary>
    public abstract class BaseJob : IJob
    {
        public string Name { get; }
        public StatBlock BaseBonus { get; }
        public StatBlock GrowthPerLevel { get; }
        public JobAbility[] Abilities { get; }

        protected BaseJob(string name, StatBlock baseBonus, StatBlock growthPerLevel, JobAbility[] abilities)
        {
            Name = name;
            BaseBonus = baseBonus;
            GrowthPerLevel = growthPerLevel;
            Abilities = abilities ?? System.Array.Empty<JobAbility>();
        }

        /// <summary>Computes total job stat contribution at a given level.</summary>
        public StatBlock GetJobContributionAtLevel(int level)
        {
            if (level <= 1)
                return BaseBonus;
            var extra = GrowthPerLevel.Scale(level - 1);
            return BaseBonus.Add(extra);
        }
    }
}
