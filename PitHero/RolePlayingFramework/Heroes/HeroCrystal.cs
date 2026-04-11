using Microsoft.Xna.Framework;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Represents a stored hero template with job, level, base stats and learned skills.</summary>
    public sealed class HeroCrystal
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; }
        public StatBlock BaseStats { get; }
        public Color Color { get; private set; }

        /// <summary>True if this crystal is a combo of two jobs.</summary>
        public bool IsCombo => Job is CompositeJob;

        /// <summary>True if all skills for this crystal's job have been learned.</summary>
        public bool Mastered => _mastered;
        private bool _mastered;

        /// <summary>Persistent learned skill ids (active + passive).</summary>
        private readonly HashSet<string> _learnedSkillIds;
        public IReadOnlyCollection<string> LearnedSkillIds => _learnedSkillIds;

        /// <summary>Total JP earned across all time.</summary>
        public int TotalJP { get; private set; }

        /// <summary>Current JP available to spend.</summary>
        public int CurrentJP { get; private set; }

        /// <summary>Job level based on skills purchased.</summary>
        public int JobLevel => CalculateJobLevel();

        // Synergy progression tracking
        /// <summary>Synergy points earned per synergy pattern ID.</summary>
        private readonly Dictionary<string, int> _synergyPoints;
        public IReadOnlyDictionary<string, int> SynergyPoints => _synergyPoints;

        /// <summary>Learned synergy skill IDs.</summary>
        private readonly HashSet<string> _learnedSynergySkillIds;
        public IReadOnlyCollection<string> LearnedSynergySkillIds => _learnedSynergySkillIds;

        /// <summary>Discovered synergy pattern IDs (for UI display).</summary>
        private readonly HashSet<string> _discoveredSynergyIds;
        public IReadOnlyCollection<string> DiscoveredSynergyIds => _discoveredSynergyIds;

        public HeroCrystal(string name, IJob job, int level, in StatBlock baseStats)
            : this(name, job, level, baseStats, CrystalColorUtil.GetJobColor(job))
        {
        }

        public HeroCrystal(string name, IJob job, int level, in StatBlock baseStats, Color color)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            Color = color;
            _learnedSkillIds = new HashSet<string>();
            TotalJP = 0;
            CurrentJP = 0;
            _synergyPoints = new Dictionary<string, int>();
            _learnedSynergySkillIds = new HashSet<string>();
            _discoveredSynergyIds = new HashSet<string>();
        }

        private HeroCrystal(string name, IJob job, int level, in StatBlock baseStats, HashSet<string> learned, int totalJP, int currentJP,
            Dictionary<string, int>? synergyPoints = null, HashSet<string>? learnedSynergySkillIds = null, HashSet<string>? discoveredSynergyIds = null,
            Color? color = null)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            Color = color ?? CrystalColorUtil.GetJobColor(job);
            _learnedSkillIds = learned;
            TotalJP = totalJP;
            CurrentJP = currentJP;
            _synergyPoints = synergyPoints ?? new Dictionary<string, int>();
            _learnedSynergySkillIds = learnedSynergySkillIds ?? new HashSet<string>();
            _discoveredSynergyIds = discoveredSynergyIds ?? new HashSet<string>();
        }

        /// <summary>Adds a learned skill id if not present.</summary>
        public void AddLearnedSkill(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId))
            {
                _learnedSkillIds.Add(skillId);
                _mastered = IsJobMastered();
            }
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
            _mastered = IsJobMastered();
            return true;
        }

        /// <summary>Calculates the job level based on number of skills purchased.</summary>
        private int CalculateJobLevel()
        {
            int count = 0;
            var jobSkills = Job.Skills;
            for (int i = 0; i < jobSkills.Count; i++)
            {
                if (_learnedSkillIds.Contains(jobSkills[i].Id))
                    count++;
            }
            return count;
        }

        /// <summary>Checks if all skills have been mastered (max job level achieved).</summary>
        public bool IsJobMastered()
        {
            var jobSkills = Job.Skills;
            if (jobSkills.Count == 0) return true;

            for (int i = 0; i < jobSkills.Count; i++)
            {
                if (!_learnedSkillIds.Contains(jobSkills[i].Id))
                    return false;
            }
            return true;
        }

        /// <summary>Sets the mastered state directly (used during save/load and combine).</summary>
        public void SetMastered(bool mastered) => _mastered = mastered;

        // Synergy system methods

        /// <summary>Earns synergy points for a specific synergy pattern.</summary>
        public void EarnSynergyPoints(string synergyId, int amount)
        {
            if (string.IsNullOrEmpty(synergyId) || amount < 0) return;

            if (!_synergyPoints.ContainsKey(synergyId))
                _synergyPoints[synergyId] = 0;

            _synergyPoints[synergyId] += amount;
        }

        /// <summary>Gets the total synergy points earned for a specific synergy pattern.</summary>
        public int GetSynergyPoints(string synergyId)
        {
            return _synergyPoints.TryGetValue(synergyId, out var points) ? points : 0;
        }

        /// <summary>Marks a synergy as discovered (for UI display).</summary>
        public void DiscoverSynergy(string synergyId)
        {
            if (!string.IsNullOrEmpty(synergyId))
                _discoveredSynergyIds.Add(synergyId);
        }

        /// <summary>Checks if a synergy has been discovered.</summary>
        public bool HasDiscoveredSynergy(string synergyId)
        {
            return _discoveredSynergyIds.Contains(synergyId);
        }

        /// <summary>Learns a synergy skill.</summary>
        public void LearnSynergySkill(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId))
                _learnedSynergySkillIds.Add(skillId);
        }

        /// <summary>Checks if a synergy skill has been learned.</summary>
        public bool HasSynergySkill(string skillId)
        {
            return _learnedSynergySkillIds.Contains(skillId);
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
            var bSkillEnum = b._learnedSkillIds.GetEnumerator();
            while (bSkillEnum.MoveNext()) union.Add(bSkillEnum.Current);
            bSkillEnum.Dispose();
            var totalJP = a.TotalJP + b.TotalJP;
            var currentJP = a.CurrentJP + b.CurrentJP;

            // Combine synergy data
            var combinedSynergyPoints = new Dictionary<string, int>(a._synergyPoints);
            var bSynEnum = b._synergyPoints.GetEnumerator();
            while (bSynEnum.MoveNext())
            {
                if (combinedSynergyPoints.ContainsKey(bSynEnum.Current.Key))
                    combinedSynergyPoints[bSynEnum.Current.Key] += bSynEnum.Current.Value;
                else
                    combinedSynergyPoints[bSynEnum.Current.Key] = bSynEnum.Current.Value;
            }
            bSynEnum.Dispose();

            var combinedSynergySkills = new HashSet<string>(a._learnedSynergySkillIds);
            var bSynSkillEnum = b._learnedSynergySkillIds.GetEnumerator();
            while (bSynSkillEnum.MoveNext()) combinedSynergySkills.Add(bSynSkillEnum.Current);
            bSynSkillEnum.Dispose();

            var combinedDiscoveredSynergies = new HashSet<string>(a._discoveredSynergyIds);
            var bDiscEnum = b._discoveredSynergyIds.GetEnumerator();
            while (bDiscEnum.MoveNext()) combinedDiscoveredSynergies.Add(bDiscEnum.Current);
            bDiscEnum.Dispose();

            // Blend colors using HSV
            var blendedColor = CrystalColorUtil.CombineColors(a.Color, b.Color);

            var combined = new HeroCrystal(combinedName, job, level, stats, union, totalJP, currentJP,
                combinedSynergyPoints, combinedSynergySkills, combinedDiscoveredSynergies, blendedColor);
            combined.SetMastered(combined.IsJobMastered());
            return combined;
        }
    }
}
