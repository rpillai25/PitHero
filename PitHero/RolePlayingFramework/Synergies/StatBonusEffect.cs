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
            // Add stat bonuses to hero's synergy stat accumulator
            hero._synergyStatBonus = hero._synergyStatBonus.Add(StatBonus);
            hero._synergyHPBonus += HPBonus;
            hero._synergyMPBonus += MPBonus;
        }
        
        public void Remove(Hero hero)
        {
            // Remove stat bonuses from hero's synergy stat accumulator
            hero._synergyStatBonus = new StatBlock(
                hero._synergyStatBonus.Strength - StatBonus.Strength,
                hero._synergyStatBonus.Agility - StatBonus.Agility,
                hero._synergyStatBonus.Vitality - StatBonus.Vitality,
                hero._synergyStatBonus.Magic - StatBonus.Magic
            );
            hero._synergyHPBonus -= HPBonus;
            hero._synergyMPBonus -= MPBonus;
        }
    }
}
