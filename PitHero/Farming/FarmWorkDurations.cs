namespace PitHero.Farming
{
    /// <summary>
    /// Farm work durations as a function of the worker's farming level (1-9). Tilling and harvesting
    /// interpolate between explicit level-1 and level-9 endpoints (issue #422) so levelling a farmer
    /// is plainly visible in the field; watering and planting keep the older fixed-step scale.
    /// Every value is derived from the level alone, so a re-simulation reproduces it exactly.
    /// </summary>
    public static class FarmWorkDurations
    {
        /// <summary>Seconds a worker of the given farming level spends hoeing one tile.</summary>
        public static float GetTillDuration(int farmingLevel)
        {
            return Lerp(GameConfig.TillDurationAtLevel1Seconds, GameConfig.TillDurationAtLevel9Seconds, farmingLevel);
        }

        /// <summary>Seconds a worker of the given farming level spends picking one grown crop.</summary>
        public static float GetHarvestDuration(int farmingLevel)
        {
            return Lerp(GameConfig.HarvestDurationAtLevel1Seconds, GameConfig.HarvestDurationAtLevel9Seconds, farmingLevel);
        }

        /// <summary>
        /// Seconds an apple-tree harvester waits under the tree before jumping. Scales with the same
        /// fraction as GetHarvestDuration so a high-level worker is quick on trees too.
        /// </summary>
        public static float GetAppleHarvestWaitDuration(int farmingLevel)
        {
            var scale = GetHarvestDuration(farmingLevel) / GameConfig.HarvestDurationAtLevel1Seconds;
            return GameConfig.AppleHarvestWaitSeconds * scale;
        }

        /// <summary>
        /// Duration multiplier for the farm actions that still use the fixed-step scale
        /// (watering, planting): 1.0 at level 1, falling by FarmProficiencySpeedStep per level.
        /// </summary>
        public static float GetSpeedScale(int farmingLevel)
        {
            return 1f - GameConfig.FarmProficiencySpeedStep * (ClampLevel(farmingLevel) - 1);
        }

        /// <summary>Linear interpolation from the level-min value to the level-max value.</summary>
        private static float Lerp(float atMinLevel, float atMaxLevel, int level)
        {
            int span = GameConfig.MonsterJobLevelMax - GameConfig.MonsterJobLevelMin;
            if (span <= 0)
                return atMinLevel;
            float t = (ClampLevel(level) - GameConfig.MonsterJobLevelMin) / (float)span;
            return atMinLevel + (atMaxLevel - atMinLevel) * t;
        }

        private static int ClampLevel(int level)
        {
            if (level < GameConfig.MonsterJobLevelMin) return GameConfig.MonsterJobLevelMin;
            if (level > GameConfig.MonsterJobLevelMax) return GameConfig.MonsterJobLevelMax;
            return level;
        }
    }
}
