using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Combines two jobs; sums their stat contributions and unions abilities.</summary>
    public sealed class CompositeJob : IJob
    {
        private readonly IJob _a;
        private readonly IJob _b;

        public CompositeJob(IJob a, IJob b)
        {
            _a = a;
            _b = b;
        }

        public string Name => $"{_a.Name}-{_b.Name}";
        public StatBlock BaseBonus => _a.BaseBonus.Add(_b.BaseBonus);
        public StatBlock GrowthPerLevel => _a.GrowthPerLevel.Add(_b.GrowthPerLevel);
        public JobAbility[] Abilities
        {
            get
            {
                // Simple union without allocations per call
                var list = new System.Collections.Generic.List<JobAbility>(_a.Abilities.Length + _b.Abilities.Length);
                for (int i = 0; i < _a.Abilities.Length; i++) if (!list.Contains(_a.Abilities[i])) list.Add(_a.Abilities[i]);
                for (int i = 0; i < _b.Abilities.Length; i++) if (!list.Contains(_b.Abilities[i])) list.Add(_b.Abilities[i]);
                return list.ToArray();
            }
        }

        /// <summary>Computes combined job contribution at a level.</summary>
        public StatBlock GetJobContributionAtLevel(int level)
            => _a.GetJobContributionAtLevel(level).Add(_b.GetJobContributionAtLevel(level));
    }
}
