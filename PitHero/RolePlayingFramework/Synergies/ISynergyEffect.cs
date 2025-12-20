using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Interface for synergy effects that can be applied to heroes.</summary>
    public interface ISynergyEffect
    {
        /// <summary>Unique identifier for this effect.</summary>
        string EffectId { get; }

        /// <summary>Human-readable description of the effect.</summary>
        string Description { get; }

        /// <summary>
        /// Applies this effect to the hero with full effect (multiplier = 1.0).
        /// Backward-compatible method for single-instance synergies.
        /// </summary>
        void Apply(Hero hero);

        /// <summary>
        /// Applies this effect to the hero with the given multiplier for stacked synergies.
        /// Issue #133 - Synergy Stacking System
        /// </summary>
        /// <param name="hero">The hero to apply the effect to.</param>
        /// <param name="multiplier">The effect multiplier (e.g., 1.0, 1.5, 1.75 for 1-3 stacks).</param>
        void Apply(Hero hero, float multiplier);

        /// <summary>Removes this effect from the hero.</summary>
        void Remove(Hero hero);
    }
}
