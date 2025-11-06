using RolePlayingFramework.Heroes;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Synergy effect that applies stat bonuses to the hero.</summary>
    public sealed class StatBonusEffect : ISynergyEffect
    {
        public string EffectId { get; }
        public string Description { get; }
        
        /// <summary>The stat bonuses to apply (can be flat or percentage-based).</summary>
        public StatBlock StatBonus { get; }
        
        /// <summary>If true, bonuses are applied as percentages (e.g., 10 = +10%).</summary>
        public bool IsPercentage { get; }
        
        /// <summary>HP bonus (flat amount).</summary>
        public int HPBonus { get; }
        
        /// <summary>MP bonus (flat amount).</summary>
        public int MPBonus { get; }
        
        public StatBonusEffect(string effectId, string description, in StatBlock statBonus, bool isPercentage = false, int hpBonus = 0, int mpBonus = 0)
        {
            EffectId = effectId;
            Description = description;
            StatBonus = statBonus;
            IsPercentage = isPercentage;
            HPBonus = hpBonus;
            MPBonus = mpBonus;
        }
        
        public void Apply(Hero hero)
        {
            // Stat bonuses are applied through hero's synergy stat tracking
            // This will be integrated with Hero class in next step
        }
        
        public void Remove(Hero hero)
        {
            // Stat bonuses are removed through hero's synergy stat tracking
            // This will be integrated with Hero class in next step
        }
    }
}
