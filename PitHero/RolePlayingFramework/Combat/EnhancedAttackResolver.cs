using Nez;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Combat
{
    /// <summary>Enhanced attack resolver with new evasion and damage formulas</summary>
    /// <remarks>
    /// Uses BalanceConfig for damage and evasion calculations to ensure consistency
    /// across all combat systems. See BalanceConfig for formula details and tuning.
    /// </remarks>
    public sealed class EnhancedAttackResolver : IAttackResolver
    {
        /// <summary>
        /// Rolls the dodge check for the given effective dodge chance (0-255 space).
        /// The roll is ALWAYS consumed — even when dodgeChance is 0 — to keep the
        /// Nez.Random call sequence identical regardless of damage kind (RNG contract).
        /// </summary>
        public bool RollDodge(int dodgeChance)
        {
            int roll = Random.Range(0, 256); // 0-255 inclusive
            return roll < dodgeChance; // True = evaded/missed
        }

        /// <summary>Calculate damage using BalanceConfig formula</summary>
        /// <remarks>
        /// Delegates to BalanceConfig.CalculateAttackDamage for consistent damage calculation.
        /// </remarks>
        public int CalculateDamage(int attack, int defense)
        {
            return BalanceConfig.CalculateAttackDamage(attack, defense);
        }

        /// <summary>Computes an attack using enhanced battle stats</summary>
        public AttackResult Resolve(BattleStats attackerBattleStats, BattleStats defenderBattleStats, DamageKind kind)
        {
            // Default to Neutral element for backwards compatibility
            return Resolve(attackerBattleStats, defenderBattleStats, kind, ElementType.Neutral, new ElementalProperties(ElementType.Neutral));
        }

        /// <summary>Computes an attack with elemental damage multipliers</summary>
        public AttackResult Resolve(BattleStats attackerBattleStats, BattleStats defenderBattleStats,
            DamageKind kind, ElementType attackElement, ElementalProperties defenderProps)
        {
            // Dodge check first: relative to the attacker's accuracy, clamped so nothing
            // is unhittable. Magical attacks bypass physical evasion entirely.
            int dodgeChance = kind == DamageKind.Magical
                ? 0
                : BalanceConfig.CalculateDodgeChance(defenderBattleStats.Evasion, attackerBattleStats.Accuracy);
            if (RollDodge(dodgeChance))
            {
                return new AttackResult(false, 0);
            }

            // Calculate base damage
            int baseDamage = CalculateDamage(attackerBattleStats.Attack, defenderBattleStats.Defense);

            // Apply elemental multiplier using BalanceConfig
            float elementalMultiplier = BalanceConfig.GetElementalDamageMultiplier(attackElement, defenderProps);
            int damage = (int)(baseDamage * elementalMultiplier);

            // Add variance +/-10%
            var variance = (damage * 10) / 100;
            var finalDamage = damage + Random.Range(-variance, variance + 1);
            finalDamage = System.Math.Max(1, finalDamage);

            return new AttackResult(true, finalDamage);
        }

        /// <summary>Legacy method for compatibility</summary>
        public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats, DamageKind kind, int attackerLevel, int defenderLevel)
        {
            // Default to Neutral element for backwards compatibility
            return Resolve(attackerStats, defenderStats, kind, attackerLevel, defenderLevel,
                ElementType.Neutral, new ElementalProperties(ElementType.Neutral));
        }

        /// <summary>Legacy method with elemental support</summary>
        public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats,
            DamageKind kind, int attackerLevel, int defenderLevel,
            ElementType attackElement, ElementalProperties defenderProps)
        {
            // Convert to battle stats (evasion and accuracy share the base formula)
            int attackerRating = BalanceConfig.CalculateEvasion(attackerStats.Agility, attackerLevel);
            var attackerBattle = new BattleStats(
                attackerStats.Strength,
                attackerStats.Agility / 2,
                attackerRating,
                attackerRating
            );

            int defenderRating = BalanceConfig.CalculateEvasion(defenderStats.Agility, defenderLevel);
            var defenderBattle = new BattleStats(
                defenderStats.Strength,
                defenderStats.Agility / 2,
                defenderRating,
                defenderRating
            );

            return Resolve(attackerBattle, defenderBattle, kind, attackElement, defenderProps);
        }
    }
}