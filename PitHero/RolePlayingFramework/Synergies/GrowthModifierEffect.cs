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
        
        public GrowthModifierEffect(string effectId, string description, in StatBlock growthMultipliers)
        {
            EffectId = effectId;
            Description = description;
            GrowthMultipliers = growthMultipliers;
        }
        
        public void Apply(Hero hero)
        {
            // Growth modifiers affect level-up calculations
            // This would need to be integrated into Hero's level-up system
            // For now, this is a placeholder for future implementation
        }
        
        public void Remove(Hero hero)
        {
            // Growth modifiers are removed when synergy is broken
            // This would need to be integrated into Hero's level-up system
        }
    }
}
