using Nez;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Combat
{
    /// <summary>Enhanced attack resolver with new evasion and damage formulas</summary>
    public sealed class EnhancedAttackResolver : IAttackResolver
    {
        /// <summary>Calculate if an attack is evaded based on target's evasion</summary>
        public bool CalculateEvasion(int targetEvasion)
        {
            int roll = Random.Range(0, 256); // 0-255 inclusive
            return roll < targetEvasion; // True = evaded/missed
        }

        /// <summary>Calculate damage using new formula</summary>
        public int CalculateDamage(int attack, int defense)
        {
            int damage;
            if (attack >= defense)
            {
                damage = attack * 2 - defense;
            }
            else
            {
                damage = attack * attack / defense;
            }

            // Ensure minimum damage of 1
            return System.Math.Max(1, damage);
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
            // This is a simplified conversion for legacy calls
            var attackerBattle = new BattleStats(
                attackerStats.Strength,
                attackerStats.Agility / 2,
                System.Math.Min(255, attackerStats.Agility * 2 + attackerLevel)
            );
            
            var defenderBattle = new BattleStats(
                defenderStats.Strength,
                defenderStats.Agility / 2,
                System.Math.Min(255, defenderStats.Agility * 2 + defenderLevel)
            );

            return Resolve(attackerBattle, defenderBattle, kind);
        }
    }
}