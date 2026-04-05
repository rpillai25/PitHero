using RolePlayingFramework.Enemies;
using System.Collections.Generic;

namespace PitHero.Config
{
    /// <summary>
    /// Configuration for enemy levels. Defines the fixed level that each enemy type should spawn at.
    /// </summary>
    public static class EnemyLevelConfig
    {
        /// <summary>
        /// Dictionary mapping enemy id to their preset spawn levels.
        /// These levels are fixed and do not scale with pit level.
        /// </summary>
        private static readonly Dictionary<EnemyId, int> EnemyLevels = new()
        {
            // Level 1 enemies (spawn in Pit Level 1-3)
            [EnemyId.Slime] = 1,
            [EnemyId.Bat] = 1,
            [EnemyId.Rat] = 1,

            // Level 2-4 enemies (early cave)
            [EnemyId.CaveMushroom] = 2,
            [EnemyId.StoneBeetle] = 4,

            // Level 3 enemies (spawn in Pit Level 4-6)
            [EnemyId.Goblin] = 3,
            [EnemyId.Spider] = 3,
            [EnemyId.Snake] = 3,

            // Level 6 enemies (spawn in Pit Level 7-8)
            [EnemyId.Skeleton] = 6,
            [EnemyId.Orc] = 6,
            [EnemyId.Wraith] = 6,

            // Level 7-9 enemies (mid cave)
            [EnemyId.ShadowImp] = 7,
            [EnemyId.TunnelWorm] = 8,
            [EnemyId.FireLizard] = 9,

            // Boss (spawn in Pit Level 9)
            [EnemyId.PitLord] = 10,

            // Level 11-14 enemies (deep cave)
            [EnemyId.MagmaOoze] = 11,
            [EnemyId.CrystalGolem] = 12,
            [EnemyId.CaveTroll] = 13,
            [EnemyId.GhostMiner] = 14,

            // Level 16-18 enemies (ancient cave)
            [EnemyId.ShadowBeast] = 16,
            [EnemyId.LavaDrake] = 17,
            [EnemyId.StoneWyrm] = 18,

            // Boss enemies (Cave Biome)
            [EnemyId.StoneGuardian] = 7,
            [EnemyId.EarthElemental] = 17,
            [EnemyId.MoltenTitan] = 22,
            [EnemyId.AncientWyrm] = 27,
        };

        /// <summary>
        /// Get the preset level for a given enemy type.
        /// </summary>
        /// <param name="enemyId">The id of the enemy type</param>
        /// <returns>The preset level for this enemy type, defaults to 1 if not found</returns>
        public static int GetPresetLevel(EnemyId enemyId)
        {
            return EnemyLevels.GetValueOrDefault(enemyId, 1);
        }

        /// <summary>
        /// Check if an enemy type has a specific preset level defined.
        /// </summary>
        /// <param name="enemyId">The id of the enemy type</param>
        /// <returns>True if the enemy has a preset level defined, false otherwise</returns>
        public static bool HasPresetLevel(EnemyId enemyId)
        {
            return EnemyLevels.ContainsKey(enemyId);
        }

        /// <summary>
        /// Get all defined enemy types and their preset levels.
        /// </summary>
        /// <returns>Read-only dictionary of enemy ids to their preset levels</returns>
        public static IReadOnlyDictionary<EnemyId, int> GetAllEnemyLevels()
        {
            return EnemyLevels;
        }
    }
}
