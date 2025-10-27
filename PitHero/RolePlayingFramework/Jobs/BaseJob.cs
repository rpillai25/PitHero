using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Base job implementation providing common behavior.</summary>
    public abstract class BaseJob : IJob
    {
        public string Name { get; }
        public StatBlock BaseBonus { get; }
        public StatBlock GrowthPerLevel { get; }
        public IReadOnlyList<ISkill> Skills => _skills;
        public JobTier Tier { get; }
        private readonly List<ISkill> _skills;

        protected BaseJob(string name, StatBlock baseBonus, StatBlock growthPerLevel, JobTier tier)
        {
            Name = name;
            BaseBonus = baseBonus;
            GrowthPerLevel = growthPerLevel;
            Tier = tier;
            _skills = new List<ISkill>(8);
            DefineSkills(_skills);
        }

        /// <summary>Override to populate job skill list.</summary>
        protected virtual void DefineSkills(List<ISkill> list) { }

        /// <summary>Computes total job stat contribution at a given level.</summary>
        public StatBlock GetJobContributionAtLevel(int level)
        {
            if (level <= 1)
                return BaseBonus;
            var extra = GrowthPerLevel.Scale(level - 1);
            var contribution = BaseBonus.Add(extra);
            // Clamp job contribution to ensure stats don't exceed caps
            return StatConstants.ClampStatBlock(contribution);
        }
    }
}
