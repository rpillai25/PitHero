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
        private readonly List<ISkill> _skills;

        protected BaseJob(string name, StatBlock baseBonus, StatBlock growthPerLevel)
        {
            Name = name;
            BaseBonus = baseBonus;
            GrowthPerLevel = growthPerLevel;
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
            return BaseBonus.Add(extra);
        }

        /// <summary>Adds any skills learnable at this level not already known.</summary>
        public void GetLearnableSkills(int level, HashSet<string> alreadyKnown, List<ISkill> buffer)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                var s = _skills[i];
                if (s.LearnLevel == level && !alreadyKnown.Contains(s.Id))
                {
                    buffer.Add(s);
                }
            }
        }
    }
}
