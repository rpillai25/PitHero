using RolePlayingFramework.Stats;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Enemies;

namespace RolePlayingFramework.Combat
{
    /// <summary>Derived battle-specific stats for combat calculations</summary>
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
            
            // Evasion derived from Agility (0-255 range)
            // Formula: min(255, Agility * 2 + Level)
            int evasion = System.Math.Min(255, totalStats.Agility * 2 + hero.Level);
            
            return new BattleStats(attack, defense, evasion);
        }

        /// <summary>Calculate battle stats for a monster</summary>
        public static BattleStats CalculateForMonster(IEnemy enemy)
        {
            // Attack = Strength (monsters have no weapon bonus)
            int attack = enemy.Stats.Strength;
            
            // Defense = Agility/2 (monsters have no armor bonus)
            int defense = enemy.Stats.Agility / 2;
            
            // Evasion derived from Agility (0-255 range)
            // Formula: min(255, Agility * 2 + Level)
            int evasion = System.Math.Min(255, enemy.Stats.Agility * 2 + enemy.Level);
            
            return new BattleStats(attack, defense, evasion);
        }
    }
}