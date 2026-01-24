using RolePlayingFramework.Jobs;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Combines two jobs; averages their stat contributions and unions skills.</summary>
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
        public StatBlock BaseBonus => _a.BaseBonus.Add(_b.BaseBonus).Scale(0.5f);
        public StatBlock GrowthPerLevel => _a.GrowthPerLevel.Add(_b.GrowthPerLevel).Scale(0.5f);
        public IReadOnlyList<ISkill> Skills => _skills;

        /// <summary>Tier is the maximum of the two component job tiers.</summary>
        public JobTier Tier => _a.Tier > _b.Tier ? _a.Tier : _b.Tier;

        /// <summary>Computes averaged job contribution at a level.</summary>
        public StatBlock GetJobContributionAtLevel(int level)
            => _a.GetJobContributionAtLevel(level).Add(_b.GetJobContributionAtLevel(level)).Scale(0.5f);
    }
}
