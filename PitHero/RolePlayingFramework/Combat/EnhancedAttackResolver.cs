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
        /// <summary>Calculate if an attack is evaded based on target's evasion</summary>
        public bool CalculateEvasion(int targetEvasion)
        {
            int roll = Random.Range(0, 256); // 0-255 inclusive
            return roll < targetEvasion; // True = evaded/missed
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
            // Check for evasion first
            if (CalculateEvasion(defenderBattleStats.Evasion))
            {
                return new AttackResult(false, 0); // Miss due to evasion
            }

            // Calculate damage using new formula
            int damage = CalculateDamage(attackerBattleStats.Attack, defenderBattleStats.Defense);

            // Add small variance +/-10%
            var variance = (damage * 10) / 100;
            var finalDamage = damage + Random.Range(-variance, variance + 1);
            finalDamage = System.Math.Max(1, finalDamage);

            return new AttackResult(true, finalDamage);
        }

        /// <summary>Legacy method for compatibility</summary>
        public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats, DamageKind kind, int attackerLevel, int defenderLevel)
        {
            // For backwards compatibility, convert to battle stats
            // Uses BalanceConfig.CalculateEvasion for consistent evasion calculation
            var attackerBattle = new BattleStats(
                attackerStats.Strength,
                attackerStats.Agility / 2,
                BalanceConfig.CalculateEvasion(attackerStats.Agility, attackerLevel)
            );
            
            var defenderBattle = new BattleStats(
                defenderStats.Strength,
                defenderStats.Agility / 2,
                BalanceConfig.CalculateEvasion(defenderStats.Agility, defenderLevel)
            );

            return Resolve(attackerBattle, defenderBattle, kind);
        }
    }
}