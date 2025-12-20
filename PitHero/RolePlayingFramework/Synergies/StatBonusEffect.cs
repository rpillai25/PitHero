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

        // Track applied values for proper removal with multipliers
        private float _lastAppliedMultiplier;

        public StatBonusEffect(string effectId, string description, in StatBlock statBonus, bool isPercentage = false, int hpBonus = 0, int mpBonus = 0)
        {
            EffectId = effectId;
            Description = description;
            StatBonus = statBonus;
            IsPercentage = isPercentage;
            HPBonus = hpBonus;
            MPBonus = mpBonus;
            _lastAppliedMultiplier = 0f;
        }

        /// <summary>Applies this effect with full multiplier (1.0).</summary>
        public void Apply(Hero hero)
        {
            Apply(hero, 1.0f);
        }

        /// <summary>
        /// Applies this effect to the hero with the given multiplier.
        /// Issue #133 - Synergy Stacking System
        /// </summary>
        public void Apply(Hero hero, float multiplier)
        {
            // Calculate scaled bonuses
            var scaledStats = new StatBlock(
                (int)(StatBonus.Strength * multiplier),
                (int)(StatBonus.Agility * multiplier),
                (int)(StatBonus.Vitality * multiplier),
                (int)(StatBonus.Magic * multiplier)
            );
            int scaledHP = (int)(HPBonus * multiplier);
            int scaledMP = (int)(MPBonus * multiplier);

            // Add stat bonuses to hero's synergy stat accumulator
            hero._synergyStatBonus = hero._synergyStatBonus.Add(scaledStats);
            hero._synergyHPBonus += scaledHP;
            hero._synergyMPBonus += scaledMP;

            _lastAppliedMultiplier = multiplier;
        }

        public void Remove(Hero hero)
        {
            // Use the last applied multiplier for removal
            float multiplier = _lastAppliedMultiplier > 0f ? _lastAppliedMultiplier : 1.0f;

            var scaledStats = new StatBlock(
                (int)(StatBonus.Strength * multiplier),
                (int)(StatBonus.Agility * multiplier),
                (int)(StatBonus.Vitality * multiplier),
                (int)(StatBonus.Magic * multiplier)
            );
            int scaledHP = (int)(HPBonus * multiplier);
            int scaledMP = (int)(MPBonus * multiplier);

            // Remove stat bonuses from hero's synergy stat accumulator
            hero._synergyStatBonus = new StatBlock(
                hero._synergyStatBonus.Strength - scaledStats.Strength,
                hero._synergyStatBonus.Agility - scaledStats.Agility,
                hero._synergyStatBonus.Vitality - scaledStats.Vitality,
                hero._synergyStatBonus.Magic - scaledStats.Magic
            );
            hero._synergyHPBonus -= scaledHP;
            hero._synergyMPBonus -= scaledMP;

            _lastAppliedMultiplier = 0f;
        }
    }
}
