using System.Collections.Generic;
using System.Linq;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

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

        /// <summary>Total JP earned across all time.</summary>
        public int TotalJP { get; private set; }

        /// <summary>Current JP available to spend.</summary>
        public int CurrentJP { get; private set; }

        /// <summary>Job level based on skills purchased.</summary>
        public int JobLevel => CalculateJobLevel();

        public HeroCrystal(string name, IJob job, int level, in StatBlock baseStats)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            _learnedSkillIds = new HashSet<string>();
            TotalJP = 0;
            CurrentJP = 0;
        }

        private HeroCrystal(string name, IJob job, int level, in StatBlock baseStats, HashSet<string> learned, int totalJP, int currentJP)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            _learnedSkillIds = learned;
            TotalJP = totalJP;
            CurrentJP = currentJP;
        }

        /// <summary>Adds a learned skill id if not present.</summary>
        public void AddLearnedSkill(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId))
                _learnedSkillIds.Add(skillId);
        }

        /// <summary>Checks if the skill id has been learned.</summary>
        public bool HasSkill(string skillId) => _learnedSkillIds.Contains(skillId);

        /// <summary>Earns JP from battles, chests, events, or quests.</summary>
        public void EarnJP(int amount)
        {
            if (amount < 0) return;
            TotalJP += amount;
            CurrentJP += amount;
        }

        /// <summary>Attempts to purchase a skill with JP. Returns true if successful.</summary>
        public bool TryPurchaseSkill(ISkill skill)
        {
            if (skill == null) return false;
            if (_learnedSkillIds.Contains(skill.Id)) return false; // Already learned
            if (CurrentJP < skill.JPCost) return false; // Not enough JP

            CurrentJP -= skill.JPCost;
            _learnedSkillIds.Add(skill.Id);
            return true;
        }

        /// <summary>Calculates the job level based on number of skills purchased.</summary>
        private int CalculateJobLevel()
        {
            // Job level is simply the count of purchased skills for this job
            int count = 0;
            var jobSkills = Job.Skills;
            foreach (var skill in jobSkills)
            {
                if (_learnedSkillIds.Contains(skill.Id))
                    count++;
            }
            return count;
        }

        /// <summary>Checks if all skills have been mastered (max job level achieved).</summary>
        public bool IsJobMastered()
        {
            var jobSkills = Job.Skills;
            if (jobSkills.Count == 0) return true;

            // Check if all skills are learned
            foreach (var skill in jobSkills)
            {
                if (!_learnedSkillIds.Contains(skill.Id))
                    return false;
            }
            return true;
        }

        /// <summary>Calculates the sell value of this crystal based on level and job tier.</summary>
        /// <returns>The gold value for selling this crystal.</returns>
        public int CalculateSellValue()
        {
            // Base value per level (can be adjusted for game balance)
            const int BaseValuePerLevel = 50;
            
            // Tier multipliers: Primary = 1.0x, Secondary = 1.5x, Tertiary = 2.0x
            float tierMultiplier = Job.Tier switch
            {
                JobTier.Primary => 1.0f,
                JobTier.Secondary => 1.5f,
                JobTier.Tertiary => 2.0f,
                _ => 1.0f
            };
            
            return (int)(BaseValuePerLevel * Level * tierMultiplier);
        }

        /// <summary>Combines two crystals by averaging level, summing base stats, unioning skills and composing jobs.</summary>
        public static HeroCrystal Combine(string combinedName, HeroCrystal a, HeroCrystal b)
        {
            var level = (a.Level + b.Level + 1) / 2;
            var stats = a.BaseStats.Add(b.BaseStats.Scale(1f));
            var job = new CompositeJob(a.Job, b.Job);
            var union = new HashSet<string>(a._learnedSkillIds);
            foreach (var id in b._learnedSkillIds) union.Add(id);
            var totalJP = a.TotalJP + b.TotalJP;
            var currentJP = a.CurrentJP + b.CurrentJP;
            return new HeroCrystal(combinedName, job, level, stats, union, totalJP, currentJP);
        }
    }
}
