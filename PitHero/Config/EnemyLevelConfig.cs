using System.Collections.Generic;

namespace PitHero.Config
{
    /// <summary>
    /// Configuration for enemy levels. Defines the fixed level that each enemy type should spawn at.
    /// </summary>
    public static class EnemyLevelConfig
    {
        /// <summary>
        /// Dictionary mapping enemy names to their preset spawn levels.
        /// These levels are fixed and do not scale with pit level.
        /// </summary>
        private static readonly Dictionary<string, int> EnemyLevels = new()
        {
            // Level 1 enemies (spawn in Pit Level 1-3)
            ["Slime"] = 1,
            ["Bat"] = 1,
            ["Rat"] = 1,
            
            // Level 3 enemies (spawn in Pit Level 4-6)
            ["Goblin"] = 3,
            ["Spider"] = 3,
            ["Snake"] = 3,
            
            // Level 6 enemies (spawn in Pit Level 7-8)
            ["Skeleton"] = 6,
            ["Orc"] = 6,
            ["Wraith"] = 6,
            
            // Boss (spawn in Pit Level 9)
            ["Pit Lord"] = 10,
        };

        /// <summary>
        /// Get the preset level for a given enemy type.
        /// </summary>
        /// <param name="enemyName">The name of the enemy type</param>
        /// <returns>The preset level for this enemy type, defaults to 1 if not found</returns>
        public static int GetPresetLevel(string enemyName)
        {
            return EnemyLevels.GetValueOrDefault(enemyName, 1);
        }

        /// <summary>
        /// Check if an enemy type has a specific preset level defined.
        /// </summary>
        /// <param name="enemyName">The name of the enemy type</param>
        /// <returns>True if the enemy has a preset level defined, false otherwise</returns>
        public static bool HasPresetLevel(string enemyName)
        {
            return EnemyLevels.ContainsKey(enemyName);
        }

        /// <summary>
        /// Get all defined enemy types and their preset levels.
        /// </summary>
        /// <returns>Read-only dictionary of enemy names to their preset levels</returns>
        public static IReadOnlyDictionary<string, int> GetAllEnemyLevels()
        {
            return EnemyLevels;
        }
    }
}