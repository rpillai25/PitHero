using PitHero;
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
            [MonsterTextKey.Monster_Slime] = 1,
            [MonsterTextKey.Monster_Bat] = 1,
            [MonsterTextKey.Monster_Rat] = 1,

            // Level 2-4 enemies (early cave)
            [MonsterTextKey.Monster_CaveMushroom] = 2,
            [MonsterTextKey.Monster_StoneBeetle] = 4,

            // Level 3 enemies (spawn in Pit Level 4-6)
            [MonsterTextKey.Monster_Goblin] = 3,
            [MonsterTextKey.Monster_Spider] = 3,
            [MonsterTextKey.Monster_Snake] = 3,

            // Level 6 enemies (spawn in Pit Level 7-8)
            [MonsterTextKey.Monster_Skeleton] = 6,
            [MonsterTextKey.Monster_Orc] = 6,
            [MonsterTextKey.Monster_Wraith] = 6,

            // Level 7-9 enemies (mid cave)
            [MonsterTextKey.Monster_ShadowImp] = 7,
            [MonsterTextKey.Monster_TunnelWorm] = 8,
            [MonsterTextKey.Monster_FireLizard] = 9,

            // Boss (spawn in Pit Level 9)
            [MonsterTextKey.Monster_PitLord] = 10,

            // Level 11-14 enemies (deep cave)
            [MonsterTextKey.Monster_MagmaOoze] = 11,
            [MonsterTextKey.Monster_CrystalGolem] = 12,
            [MonsterTextKey.Monster_CaveTroll] = 13,
            [MonsterTextKey.Monster_GhostMiner] = 14,

            // Level 16-18 enemies (ancient cave)
            [MonsterTextKey.Monster_ShadowBeast] = 16,
            [MonsterTextKey.Monster_LavaDrake] = 17,
            [MonsterTextKey.Monster_StoneWyrm] = 18,

            // Boss enemies (Cave Biome)
            [MonsterTextKey.Monster_StoneGuardian] = 7,
            [MonsterTextKey.Monster_EarthElemental] = 17,
            [MonsterTextKey.Monster_MoltenTitan] = 22,
            [MonsterTextKey.Monster_AncientWyrm] = 27,
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