using System.Collections.Generic;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Jobs.Secondary;
using RolePlayingFramework.Jobs.Tertiary;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Combines two jobs; sums their stat contributions and unions skills.</summary>
    public sealed class CompositeJob : IJob
    {
        private readonly IJob _a;
        private readonly IJob _b;
        private readonly List<ISkill> _skills;

        public CompositeJob(IJob a, IJob b)
        {
            _a = a;
            _b = b;
            _skills = new List<ISkill>(a.Skills.Count + b.Skills.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < a.Skills.Count; i++)
            {
                var s = a.Skills[i];
                if (seen.Add(s.Id)) _skills.Add(s);
            }
            for (int i = 0; i < b.Skills.Count; i++)
            {
                var s = b.Skills[i];
                if (seen.Add(s.Id)) _skills.Add(s);
            }
        }

        public string Name => $"{_a.Name}-{_b.Name}";
        public StatBlock BaseBonus => _a.BaseBonus.Add(_b.BaseBonus);
        public StatBlock GrowthPerLevel => _a.GrowthPerLevel.Add(_b.GrowthPerLevel);
        public IReadOnlyList<ISkill> Skills => _skills;
        
        /// <summary>Tier is the maximum of the two component job tiers.</summary>
        public JobTier Tier => _a.Tier > _b.Tier ? _a.Tier : _b.Tier;

        /// <summary>Computes combined job contribution at a level.</summary>
        public StatBlock GetJobContributionAtLevel(int level)
            => _a.GetJobContributionAtLevel(level).Add(_b.GetJobContributionAtLevel(level));
    }
}
