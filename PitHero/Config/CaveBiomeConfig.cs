using PitHero;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Config
{
    /// <summary>
    /// Cave biome progression rules for pit levels 1 through 25.
    /// </summary>
    public static class CaveBiomeConfig
    {
        /// <summary>First pit level in the Cave biome.</summary>
        public const int CaveStartLevel = 1;

        /// <summary>Last pit level in the Cave biome.</summary>
        public const int CaveEndLevel = 25;

        private static readonly string[][] CaveEnemyPoolsByLevel = CreateCaveEnemyPools();

        /// <summary>
        /// Returns true if the provided pit level is in the Cave biome.
        /// </summary>
        public static bool IsCaveLevel(int pitLevel)
        {
            return pitLevel >= CaveStartLevel && pitLevel <= CaveEndLevel;
        }

        /// <summary>
        /// Returns true if the pit level is a Cave boss floor.
        /// </summary>
        public static bool IsBossFloor(int pitLevel)
        {
            if (!IsCaveLevel(pitLevel))
            {
                return false;
            }

            return pitLevel == 5 || pitLevel == 10 || pitLevel == 15 || pitLevel == 20 || pitLevel == 25;
        }

        /// <summary>
        /// Gets explicit non-boss enemy pool mapping for Cave levels 1-25.
        /// Boss floors return an empty pool because those floors use boss logic.
        /// </summary>
        public static string[] GetEnemyPoolForLevel(int pitLevel)
        {
            if (!IsCaveLevel(pitLevel))
            {
                return System.Array.Empty<string>();
            }

            return CaveEnemyPoolsByLevel[pitLevel];
        }

        /// <summary>
        /// Gets enemy spawn level for Cave progression, with a boss bonus on boss floors.
        /// </summary>
        public static int GetScaledEnemyLevelForPitLevel(int pitLevel)
        {
            int baseLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
            // Boss floors get a small +2 spike to preserve milestone difficulty without changing the base curve.
            int bossBonus = IsBossFloor(pitLevel) ? 2 : 0;
            // Clamp keeps levels in the global 1-99 bounds for safety at all call sites.
            return StatConstants.ClampLevel(baseLevel + bossBonus);
        }

        /// <summary>
        /// Gets cave rarity band by pit level.
        /// </summary>
        public static ItemRarity GetCaveRarityBand(int pitLevel)
        {
            if (pitLevel <= 10)
            {
                return ItemRarity.Normal;
            }

            return ItemRarity.Uncommon;
        }

        /// <summary>
        /// Determines cave treasure level using cave-specific rarity bands.
        /// Pit levels 1-10 always yield level 1 (Normal).
        /// Pit levels 11-14 (non-boss): 35% level 2 (Uncommon), 65% level 1.
        /// Pit level 15 (boss): 60% level 2 (Uncommon), 40% level 1. No level 3.
        /// Boss floors 20 and 25: 20% level 3 (Rare), 50% level 2, 30% level 1.
        /// Non-boss pit levels 16-25: 10% level 3 (Rare), 35% level 2, 55% level 1.
        /// </summary>
        public static int DetermineCaveTreasureLevel(int pitLevel, float roll)
        {
            if (pitLevel <= 10)
            {
                return 1;
            }

            // Boss floor 15 does not grant level 3 — only levels 1 and 2.
            if (pitLevel == 15)
            {
                return roll < 0.6f ? 2 : 1;
            }

            // Non-boss floors 11-14.
            if (pitLevel < 16)
            {
                return roll < 0.35f ? 2 : 1;
            }

            // Boss floors 20 and 25: 20% level 3, 50% level 2, 30% level 1.
            if (IsBossFloor(pitLevel))
            {
                if (roll < 0.2f) return 3;
                if (roll < 0.7f) return 2;
                return 1;
            }

            // Non-boss pit levels 16-25: 10% level 3, 35% level 2, 55% level 1.
            if (roll < 0.1f) return 3;
            if (roll < 0.45f) return 2;
            return 1;
        }

        /// <summary>
        /// Creates explicit cave enemy pool mapping for levels 1-25.
        /// Pool 1 uses a 5-entry starter roster for levels 1-4.
        /// Pools 2-5 use 10-entry sliding windows for levels 6-9, 11-14, 16-19, and 21-24.
        /// Boss floors (5, 10, 15, 20, 25) intentionally use empty regular pools because boss logic handles those floors.
        /// </summary>
        private static string[][] CreateCaveEnemyPools()
        {
            var levels = new string[CaveEndLevel + 1][];

            // Pool 1 (Pit 1-5): Early Cave
            levels[1] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle };
            levels[2] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle };
            levels[3] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle };
            levels[4] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle };
            levels[5] = System.Array.Empty<string>(); // Stone Guardian boss

            // Pool 2 (Pit 6-10): Mid Cave
            levels[6] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle, MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_ShadowImp, MonsterTextKey.Monster_TunnelWorm };
            levels[7] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle, MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_ShadowImp, MonsterTextKey.Monster_TunnelWorm };
            levels[8] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle, MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_ShadowImp, MonsterTextKey.Monster_TunnelWorm };
            levels[9] = new[] { MonsterTextKey.Monster_Slime, MonsterTextKey.Monster_Bat, MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_CaveMushroom, MonsterTextKey.Monster_StoneBeetle, MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_ShadowImp, MonsterTextKey.Monster_FireLizard };
            levels[10] = System.Array.Empty<string>(); // Pit Lord boss

            // Pool 3 (Pit 11-15): Deep Cave
            levels[11] = new[] { MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_TunnelWorm, MonsterTextKey.Monster_FireLizard, MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_MagmaOoze, MonsterTextKey.Monster_CrystalGolem };
            levels[12] = new[] { MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_TunnelWorm, MonsterTextKey.Monster_FireLizard, MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_MagmaOoze, MonsterTextKey.Monster_CrystalGolem };
            levels[13] = new[] { MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_TunnelWorm, MonsterTextKey.Monster_FireLizard, MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner };
            levels[14] = new[] { MonsterTextKey.Monster_Goblin, MonsterTextKey.Monster_Spider, MonsterTextKey.Monster_Snake, MonsterTextKey.Monster_TunnelWorm, MonsterTextKey.Monster_FireLizard, MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner };
            levels[15] = System.Array.Empty<string>(); // Earth Elemental boss

            // Pool 4 (Pit 16-20): Ancient Cave
            levels[16] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_MagmaOoze, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm };
            levels[17] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_MagmaOoze, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm };
            levels[18] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_MagmaOoze, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm };
            levels[19] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_MagmaOoze, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm };
            levels[20] = System.Array.Empty<string>(); // Molten Titan boss

            // Pool 5 (Pit 21-25): Abyssal Cave
            levels[21] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm, MonsterTextKey.Monster_MagmaOoze };
            levels[22] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm, MonsterTextKey.Monster_MagmaOoze };
            levels[23] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm, MonsterTextKey.Monster_MagmaOoze };
            levels[24] = new[] { MonsterTextKey.Monster_Skeleton, MonsterTextKey.Monster_Orc, MonsterTextKey.Monster_Wraith, MonsterTextKey.Monster_CrystalGolem, MonsterTextKey.Monster_CaveTroll, MonsterTextKey.Monster_GhostMiner, MonsterTextKey.Monster_ShadowBeast, MonsterTextKey.Monster_LavaDrake, MonsterTextKey.Monster_StoneWyrm, MonsterTextKey.Monster_MagmaOoze };
            levels[25] = System.Array.Empty<string>(); // Ancient Wyrm big boss

            return levels;
        }
    }
}