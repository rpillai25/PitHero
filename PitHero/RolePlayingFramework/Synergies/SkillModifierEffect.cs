using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Synergy effect that modifies skill properties (AP cost, range, power, etc.).</summary>
    public sealed class SkillModifierEffect : ISynergyEffect
    {
        public string EffectId { get; }
        public string Description { get; }
        
        /// <summary>Target skill ID to modify (null means all skills).</summary>
        public string? TargetSkillId { get; }
        
        /// <summary>MP cost reduction (flat amount).</summary>
        public int MPCostReduction { get; }
        
        /// <summary>MP cost reduction (percentage, e.g., 20 = -20%).</summary>
        public float MPCostReductionPercent { get; }
        
        /// <summary>Skill range increase.</summary>
        public int RangeIncrease { get; }
        
        /// <summary>Skill power multiplier (e.g., 1.2 = +20% power).</summary>
        public float PowerMultiplier { get; }
        
        public SkillModifierEffect(
            string effectId, 
            string description, 
            string? targetSkillId = null,
            int mpCostReduction = 0,
            float mpCostReductionPercent = 0f,
            int rangeIncrease = 0,
            float powerMultiplier = 1.0f)
        {
            EffectId = effectId;
            Description = description;
            TargetSkillId = targetSkillId;
            MPCostReduction = mpCostReduction;
            MPCostReductionPercent = mpCostReductionPercent;
            RangeIncrease = rangeIncrease;
            PowerMultiplier = powerMultiplier;
        }
        
        public void Apply(Hero hero)
        {
            // Skill modifiers are applied through hero's passive skill modifier system
            // This integrates with existing MPCostReduction property
            if (MPCostReductionPercent > 0)
            {
                hero.MPCostReduction += MPCostReductionPercent / 100f;
            }
        }
        
        public void Remove(Hero hero)
        {
            // Skill modifiers are removed through hero's passive skill modifier system
            if (MPCostReductionPercent > 0)
            {
                hero.MPCostReduction -= MPCostReductionPercent / 100f;
            }
        }
    }
}
