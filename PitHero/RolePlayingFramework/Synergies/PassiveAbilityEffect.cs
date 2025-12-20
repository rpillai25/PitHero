using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Synergy effect that grants passive abilities (counter, deflect, regen, etc.).</summary>
    public sealed class PassiveAbilityEffect : ISynergyEffect
    {
        public string EffectId { get; }
        public string Description { get; }

        /// <summary>Defense bonus amount.</summary>
        public int DefenseBonus { get; }

        /// <summary>Deflect chance increase (0-1 range).</summary>
        public float DeflectChanceIncrease { get; }

        /// <summary>Enables counter-attack ability.</summary>
        public bool EnableCounter { get; }

        /// <summary>MP regeneration per tick.</summary>
        public int MPTickRegen { get; }

        /// <summary>Healing power bonus multiplier.</summary>
        public float HealPowerBonus { get; }

        /// <summary>Fire damage bonus multiplier.</summary>
        public float FireDamageBonus { get; }

        // Track applied values for proper removal with multipliers
        private float _lastAppliedMultiplier;

        public PassiveAbilityEffect(
            string effectId,
            string description,
            int defenseBonus = 0,
            float deflectChanceIncrease = 0f,
            bool enableCounter = false,
            int mpTickRegen = 0,
            float healPowerBonus = 0f,
            float fireDamageBonus = 0f)
        {
            EffectId = effectId;
            Description = description;
            DefenseBonus = defenseBonus;
            DeflectChanceIncrease = deflectChanceIncrease;
            EnableCounter = enableCounter;
            MPTickRegen = mpTickRegen;
            HealPowerBonus = healPowerBonus;
            FireDamageBonus = fireDamageBonus;
            _lastAppliedMultiplier = 0f;
        }

        /// <summary>Applies this effect with full multiplier (1.0).</summary>
        public void Apply(Hero hero)
        {
            Apply(hero, 1.0f);
        }

        /// <summary>
        /// Applies this effect to the hero with the given multiplier.
        /// Note: EnableCounter is binary and not affected by multiplier.
        /// Issue #133 - Synergy Stacking System
        /// </summary>
        public void Apply(Hero hero, float multiplier)
        {
            // Apply scaled passive ability modifiers
            hero.PassiveDefenseBonus += (int)(DefenseBonus * multiplier);
            hero.DeflectChance += DeflectChanceIncrease * multiplier;

            // Counter is binary - not scaled by multiplier
            if (EnableCounter)
            {
                hero._synergyCounterEnablers++;
                hero.EnableCounter = true;
            }

            hero.MPTickRegen += (int)(MPTickRegen * multiplier);
            hero.HealPowerBonus += HealPowerBonus * multiplier;
            hero.FireDamageBonus += FireDamageBonus * multiplier;

            _lastAppliedMultiplier = multiplier;
        }

        public void Remove(Hero hero)
        {
            // Use the last applied multiplier for removal
            float multiplier = _lastAppliedMultiplier > 0f ? _lastAppliedMultiplier : 1.0f;

            // Remove scaled passive ability modifiers
            hero.PassiveDefenseBonus -= (int)(DefenseBonus * multiplier);
            hero.DeflectChance -= DeflectChanceIncrease * multiplier;

            if (EnableCounter)
            {
                hero._synergyCounterEnablers--;
                if (hero._synergyCounterEnablers <= 0)
                {
                    hero._synergyCounterEnablers = 0;
                    hero.EnableCounter = false;
                }
            }

            hero.MPTickRegen -= (int)(MPTickRegen * multiplier);
            hero.HealPowerBonus -= HealPowerBonus * multiplier;
            hero.FireDamageBonus -= FireDamageBonus * multiplier;

            _lastAppliedMultiplier = 0f;
        }
    }
}
