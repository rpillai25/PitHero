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
            int bossBonus = IsBossFloor(pitLevel) ? 2 : 0;
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
        /// </summary>
        public static int DetermineCaveTreasureLevel(int pitLevel, float roll)
        {
            if (pitLevel <= 10)
            {
                return 1;
            }

            if (IsBossFloor(pitLevel))
            {
                return roll < 0.6f ? 2 : 1;
            }

            return roll < 0.35f ? 2 : 1;
        }

        /// <summary>
        /// Creates explicit cave enemy pool mapping for levels 1-25.
        /// Each pool contains 10 monsters following the sliding window system.
        /// </summary>
        private static string[][] CreateCaveEnemyPools()
        {
            var levels = new string[CaveEndLevel + 1][];

            // Pool 1 (Pit 1-5): Early Cave
            levels[1] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle" };
            levels[2] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle" };
            levels[3] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle" };
            levels[4] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle" };
            levels[5] = System.Array.Empty<string>(); // Stone Guardian boss

            // Pool 2 (Pit 6-10): Mid Cave
            levels[6] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle", "Goblin", "Spider", "Snake", "Shadow Imp", "Tunnel Worm" };
            levels[7] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle", "Goblin", "Spider", "Snake", "Shadow Imp", "Tunnel Worm" };
            levels[8] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle", "Goblin", "Spider", "Snake", "Shadow Imp", "Tunnel Worm" };
            levels[9] = new[] { "Slime", "Bat", "Rat", "Cave Mushroom", "Stone Beetle", "Goblin", "Spider", "Snake", "Shadow Imp", "Fire Lizard" };
            levels[10] = System.Array.Empty<string>(); // Pit Lord boss

            // Pool 3 (Pit 11-15): Deep Cave
            levels[11] = new[] { "Goblin", "Spider", "Snake", "Tunnel Worm", "Fire Lizard", "Skeleton", "Orc", "Wraith", "Magma Ooze", "Crystal Golem" };
            levels[12] = new[] { "Goblin", "Spider", "Snake", "Tunnel Worm", "Fire Lizard", "Skeleton", "Orc", "Wraith", "Magma Ooze", "Crystal Golem" };
            levels[13] = new[] { "Goblin", "Spider", "Snake", "Tunnel Worm", "Fire Lizard", "Skeleton", "Orc", "Wraith", "Cave Troll", "Ghost Miner" };
            levels[14] = new[] { "Goblin", "Spider", "Snake", "Tunnel Worm", "Fire Lizard", "Skeleton", "Orc", "Wraith", "Cave Troll", "Ghost Miner" };
            levels[15] = System.Array.Empty<string>(); // Earth Elemental boss

            // Pool 4 (Pit 16-20): Ancient Cave
            levels[16] = new[] { "Skeleton", "Orc", "Wraith", "Magma Ooze", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm" };
            levels[17] = new[] { "Skeleton", "Orc", "Wraith", "Magma Ooze", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm" };
            levels[18] = new[] { "Skeleton", "Orc", "Wraith", "Magma Ooze", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm" };
            levels[19] = new[] { "Skeleton", "Orc", "Wraith", "Magma Ooze", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm" };
            levels[20] = System.Array.Empty<string>(); // Molten Titan boss

            // Pool 5 (Pit 21-25): Abyssal Cave
            levels[21] = new[] { "Skeleton", "Orc", "Wraith", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm", "Magma Ooze" };
            levels[22] = new[] { "Skeleton", "Orc", "Wraith", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm", "Magma Ooze" };
            levels[23] = new[] { "Skeleton", "Orc", "Wraith", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm", "Magma Ooze" };
            levels[24] = new[] { "Skeleton", "Orc", "Wraith", "Crystal Golem", "Cave Troll", "Ghost Miner", "Shadow Beast", "Lava Drake", "Stone Wyrm", "Magma Ooze" };
            levels[25] = System.Array.Empty<string>(); // Ancient Wyrm big boss

            return levels;
        }
    }
}