using System.Collections.Generic;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Represents a stored hero template with job, level, base stats and learned skills.</summary>
    public sealed class HeroCrystal
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; }
        public StatBlock BaseStats { get; }

        /// <summary>Persistent learned skill ids (active + passive).</summary>
        private readonly HashSet<string> _learnedSkillIds;
        public IReadOnlyCollection<string> LearnedSkillIds => _learnedSkillIds;

        public HeroCrystal(string name, IJob job, int level, in StatBlock baseStats)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            _learnedSkillIds = new HashSet<string>();
        }

        private HeroCrystal(string name, IJob job, int level, in StatBlock baseStats, HashSet<string> learned)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            _learnedSkillIds = learned;
        }

        /// <summary>Adds a learned skill id if not present.</summary>
        public void AddLearnedSkill(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId))
                _learnedSkillIds.Add(skillId);
        }

        /// <summary>Checks if the skill id has been learned.</summary>
        public bool HasSkill(string skillId) => _learnedSkillIds.Contains(skillId);

        /// <summary>Combines two crystals by averaging level, summing base stats, unioning skills and composing jobs.</summary>
        public static HeroCrystal Combine(string combinedName, HeroCrystal a, HeroCrystal b)
        {
            var level = (a.Level + b.Level + 1) / 2;
            var stats = a.BaseStats.Add(b.BaseStats.Scale(1f));
            var job = new CompositeJob(a.Job, b.Job);
            var union = new HashSet<string>(a._learnedSkillIds);
            foreach (var id in b._learnedSkillIds) union.Add(id);
            return new HeroCrystal(combinedName, job, level, stats, union);
        }
    }
}
