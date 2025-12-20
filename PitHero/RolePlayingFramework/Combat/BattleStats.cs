using RolePlayingFramework.Balance;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Combat
{
    /// <summary>Derived battle-specific stats for combat calculations</summary>
    /// <remarks>
    /// Uses BalanceConfig.CalculateEvasion for consistent evasion calculation across all entities.
    /// </remarks>
    public readonly struct BattleStats
    {
        public readonly int Attack;
        public readonly int Defense;
        public readonly int Evasion;

        public BattleStats(int attack, int defense, int evasion)
        {
            Attack = attack;
            Defense = defense;
            Evasion = evasion;
        }

        /// <summary>Calculate battle stats for a hero</summary>
        public static BattleStats CalculateForHero(Hero hero)
        {
            var totalStats = hero.GetTotalStats();

            // Attack = Strength + weapon bonuses
            int attack = totalStats.Strength + hero.GetEquipmentAttackBonus();

            // Defense = Agility/2 + armor bonuses
            int defense = totalStats.Agility / 2 + hero.GetEquipmentDefenseBonus();

            // Evasion calculated using BalanceConfig formula
            int evasion = BalanceConfig.CalculateEvasion(totalStats.Agility, hero.Level);

            return new BattleStats(attack, defense, evasion);
        }

        /// <summary>Calculate battle stats for a monster</summary>
        public static BattleStats CalculateForMonster(IEnemy enemy)
        {
            // Attack = Strength (monsters have no weapon bonus)
            int attack = enemy.Stats.Strength;

            // Defense = Agility/2 (monsters have no armor bonus)
            int defense = enemy.Stats.Agility / 2;

            // Evasion calculated using BalanceConfig formula
            int evasion = BalanceConfig.CalculateEvasion(enemy.Stats.Agility, enemy.Level);

            return new BattleStats(attack, defense, evasion);
        }
    }
}