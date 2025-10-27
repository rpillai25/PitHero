namespace RolePlayingFramework.Stats
{
    /// <summary>
    /// Defines game-wide stat caps and progression constants.
    /// These values ensure consistent balance across all heroes and jobs.
    /// </summary>
    public static class StatConstants
    {
        /// <summary>Maximum HP a hero can have.</summary>
        public const int MaxHP = 9999;

        /// <summary>Maximum MP a hero can have.</summary>
        public const int MaxMP = 999;

        /// <summary>Maximum value for each individual stat (Strength, Agility, Vitality, Magic).</summary>
        public const int MaxStat = 99;

        /// <summary>Maximum character level.</summary>
        public const int MaxLevel = 99;

        /// <summary>Validates that HP is within the valid range [0, MaxHP].</summary>
        /// <param name="hp">HP value to validate</param>
        /// <returns>HP value clamped to [0, MaxHP]</returns>
        public static int ClampHP(int hp)
        {
            if (hp < 0) return 0;
            if (hp > MaxHP) return MaxHP;
            return hp;
        }

        /// <summary>Validates that MP is within the valid range [0, MaxMP].</summary>
        /// <param name="mp">MP value to validate</param>
        /// <returns>MP value clamped to [0, MaxMP]</returns>
        public static int ClampMP(int mp)
        {
            if (mp < 0) return 0;
            if (mp > MaxMP) return MaxMP;
            return mp;
        }

        /// <summary>Validates that a stat is within the valid range [0, MaxStat].</summary>
        /// <param name="stat">Stat value to validate</param>
        /// <returns>Stat value clamped to [0, MaxStat]</returns>
        public static int ClampStat(int stat)
        {
            if (stat < 0) return 0;
            if (stat > MaxStat) return MaxStat;
            return stat;
        }

        /// <summary>Validates that a level is within the valid range [1, MaxLevel].</summary>
        /// <param name="level">Level value to validate</param>
        /// <returns>Level value clamped to [1, MaxLevel]</returns>
        public static int ClampLevel(int level)
        {
            if (level < 1) return 1;
            if (level > MaxLevel) return MaxLevel;
            return level;
        }

        /// <summary>Clamps all stats in a StatBlock to their maximum values.</summary>
        /// <param name="stats">StatBlock to clamp</param>
        /// <returns>New StatBlock with all stats clamped to MaxStat</returns>
        public static StatBlock ClampStatBlock(in StatBlock stats)
        {
            return new StatBlock(
                ClampStat(stats.Strength),
                ClampStat(stats.Agility),
                ClampStat(stats.Vitality),
                ClampStat(stats.Magic)
            );
        }
    }
}
