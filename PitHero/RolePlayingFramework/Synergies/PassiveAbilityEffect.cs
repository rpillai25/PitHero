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
        }
        
        public void Apply(Hero hero)
        {
            // Apply passive ability modifiers
            hero.PassiveDefenseBonus += DefenseBonus;
            hero.DeflectChance += DeflectChanceIncrease;
            hero.EnableCounter = hero.EnableCounter || EnableCounter;
            hero.MPTickRegen += MPTickRegen;
            hero.HealPowerBonus += HealPowerBonus;
            hero.FireDamageBonus += FireDamageBonus;
        }
        
        public void Remove(Hero hero)
        {
            // Remove passive ability modifiers
            hero.PassiveDefenseBonus -= DefenseBonus;
            hero.DeflectChance -= DeflectChanceIncrease;
            // Note: EnableCounter cannot be cleanly removed without tracking
            hero.MPTickRegen -= MPTickRegen;
            hero.HealPowerBonus -= HealPowerBonus;
            hero.FireDamageBonus -= FireDamageBonus;
        }
    }
}
