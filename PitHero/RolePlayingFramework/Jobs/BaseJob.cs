using Nez;
using PitHero;
using PitHero.Services;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Base job implementation providing common behavior.</summary>
    public abstract class BaseJob : IJob
    {
        private readonly string _nameKey;
        private readonly string _descKey;
        private readonly string _roleKey;
        private TextService _textService;

        private TextService GetTextService()
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService;
        }

        public string Name => GetTextService()?.DisplayText(TextType.Job, _nameKey) ?? _nameKey;
        public string Description => GetTextService()?.DisplayText(TextType.Job, _descKey) ?? _descKey;
        public string Role => GetTextService()?.DisplayText(TextType.Job, _roleKey) ?? _roleKey;
        public StatBlock BaseBonus { get; }
        public StatBlock GrowthPerLevel { get; }
        public IReadOnlyList<ISkill> Skills => _skills;
        public JobTier Tier { get; }
        public JobType JobFlag { get; }
        private readonly List<ISkill> _skills;

        protected BaseJob(string name, StatBlock baseBonus, StatBlock growthPerLevel, JobTier tier, JobType jobFlag = JobType.None, string description = "", string role = "")
        {
            _nameKey = name;
            _descKey = description;
            _roleKey = role;
            BaseBonus = baseBonus;
            GrowthPerLevel = growthPerLevel;
            Tier = tier;
            JobFlag = jobFlag;
            _skills = new List<ISkill>(8);
            DefineSkills(_skills);
        }

        /// <summary>Override to populate job skill list.</summary>
        protected virtual void DefineSkills(List<ISkill> list) { }

        /// <summary>Computes total job stat contribution at a given level.</summary>
        public StatBlock GetJobContributionAtLevel(int level)
        {
            // Use GrowthCurveCalculator to calculate job contribution
            // Pass StatBlock.Zero as baseStats since we only want job contribution
            return GrowthCurveCalculator.CalculateTotalStatsAtLevel(
                StatBlock.Zero,
                BaseBonus,
                GrowthPerLevel,
                level
            );
        }
    }
}
