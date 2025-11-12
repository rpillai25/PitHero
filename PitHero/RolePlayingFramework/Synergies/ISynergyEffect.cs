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
        
        /// <summary>Applies this effect to the hero.</summary>
        void Apply(Hero hero);
        
        /// <summary>Removes this effect from the hero.</summary>
        void Remove(Hero hero);
    }
}
