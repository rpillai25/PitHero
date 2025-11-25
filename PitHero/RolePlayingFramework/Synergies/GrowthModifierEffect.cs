using RolePlayingFramework.Heroes;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Synergy effect that modifies stat growth at level-up.</summary>
    public sealed class GrowthModifierEffect : ISynergyEffect
    {
        public string EffectId { get; }
        public string Description { get; }
        
        /// <summary>Growth rate modifiers for each stat (e.g., 1.1 = +10% growth).</summary>
        public StatBlock GrowthMultipliers { get; }
        
        // Track applied values for proper removal with multipliers
        private float _lastAppliedMultiplier;
        
        public GrowthModifierEffect(string effectId, string description, in StatBlock growthMultipliers)
        {
            EffectId = effectId;
            Description = description;
            GrowthMultipliers = growthMultipliers;
            _lastAppliedMultiplier = 0f;
        }
        
        /// <summary>Applies this effect with full multiplier (1.0).</summary>
        public void Apply(Hero hero)
        {
            Apply(hero, 1.0f);
        }
        
        /// <summary>
        /// Applies this effect to the hero with the given multiplier.
        /// Growth modifiers affect level-up calculations.
        /// TODO: Integrate into Hero's level-up system for full implementation.
        /// Issue #133 - Synergy Stacking System
        /// </summary>
        public void Apply(Hero hero, float multiplier)
        {
            // Growth modifiers affect level-up calculations
            // This would need to be integrated into Hero's level-up system
            // For now, this is a placeholder for future implementation
            _lastAppliedMultiplier = multiplier;
        }
        
        public void Remove(Hero hero)
        {
            // Growth modifiers are removed when synergy is broken
            // This would need to be integrated into Hero's level-up system
            _lastAppliedMultiplier = 0f;
        }
    }
}
