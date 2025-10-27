namespace RolePlayingFramework.Stats
{
    /// <summary>
    /// Provides utility methods for calculating stat progression and growth curves.
    /// Used to ensure consistent stat progression across all jobs and heroes.
    /// </summary>
    public static class GrowthCurveCalculator
    {
        /// <summary>
        /// Calculates stat value at a given level using linear progression.
        /// Formula: baseValue + (growthPerLevel * (level - 1))
        /// </summary>
        /// <param name="baseValue">Starting stat value at level 1</param>
        /// <param name="growthPerLevel">Amount the stat increases per level</param>
        /// <param name="level">Current level (1-based)</param>
        /// <returns>Stat value at the given level</returns>
        public static int CalculateLinearGrowth(int baseValue, int growthPerLevel, int level)
        {
            // Ensure level is at least 1
            if (level < 1) level = 1;
            
            // At level 1, return base value
            if (level == 1) return baseValue;
            
            // Calculate linear growth: base + (growth * levels gained)
            return baseValue + (growthPerLevel * (level - 1));
        }

        /// <summary>
        /// Calculates stat value at a given level using exponential progression.
        /// Formula: baseValue * (growthRate ^ (level - 1))
        /// </summary>
        /// <param name="baseValue">Starting stat value at level 1</param>
        /// <param name="growthRate">Multiplier applied each level (e.g., 1.05 for 5% growth)</param>
        /// <param name="level">Current level (1-based)</param>
        /// <returns>Stat value at the given level, rounded to nearest integer</returns>
        public static int CalculateExponentialGrowth(int baseValue, float growthRate, int level)
        {
            // Ensure level is at least 1
            if (level < 1) level = 1;
            
            // At level 1, return base value
            if (level == 1) return baseValue;
            
            // Calculate exponential growth: base * (rate ^ levels)
            // Use level - 1 since we start at level 1 with baseValue
            double result = baseValue * System.Math.Pow(growthRate, level - 1);
            
            // Round to nearest integer
            return (int)(result + 0.5);
        }

        /// <summary>
        /// Calculates HP at a given level based on vitality and multipliers.
        /// Formula: baseHP + (totalVitality * vitalityMultiplier)
        /// </summary>
        /// <param name="totalVitality">Total vitality stat (base + job + equipment)</param>
        /// <param name="baseHP">Base HP value (default 25)</param>
        /// <param name="vitalityMultiplier">HP gained per point of vitality (default 5)</param>
        /// <returns>Calculated HP value, capped at MaxHP</returns>
        public static int CalculateHP(int totalVitality, int baseHP = 25, int vitalityMultiplier = 5)
        {
            // Calculate raw HP
            int hp = baseHP + (totalVitality * vitalityMultiplier);
            
            // Clamp to valid range
            return StatConstants.ClampHP(hp);
        }

        /// <summary>
        /// Calculates MP at a given level based on magic and multipliers.
        /// Formula: baseMP + (totalMagic * magicMultiplier)
        /// </summary>
        /// <param name="totalMagic">Total magic stat (base + job + equipment)</param>
        /// <param name="baseMP">Base MP value (default 10)</param>
        /// <param name="magicMultiplier">MP gained per point of magic (default 3)</param>
        /// <returns>Calculated MP value, capped at MaxMP</returns>
        public static int CalculateMP(int totalMagic, int baseMP = 10, int magicMultiplier = 3)
        {
            // Calculate raw MP
            int mp = baseMP + (totalMagic * magicMultiplier);
            
            // Clamp to valid range
            return StatConstants.ClampMP(mp);
        }

        /// <summary>
        /// Calculates the required growth per level to reach a target value at max level.
        /// Formula: (targetValue - baseValue) / (maxLevel - 1)
        /// </summary>
        /// <param name="baseValue">Starting stat value at level 1</param>
        /// <param name="targetValue">Desired stat value at max level</param>
        /// <param name="maxLevel">Maximum level (default 99)</param>
        /// <returns>Required growth per level to reach target</returns>
        public static int CalculateRequiredGrowth(int baseValue, int targetValue, int maxLevel = 99)
        {
            // Ensure maxLevel is at least 2 to avoid division by zero
            if (maxLevel < 2) return 0;
            
            // Calculate total growth needed
            int totalGrowth = targetValue - baseValue;
            
            // Distribute growth evenly across levels
            // Use integer division with rounding
            int levelsToGrow = maxLevel - 1;
            return (totalGrowth + levelsToGrow / 2) / levelsToGrow;
        }

        /// <summary>
        /// Validates if the growth curve hits the target value and respects caps.
        /// Checks that the stat reaches the target value (within tolerance) at max level
        /// and doesn't exceed the cap at any level.
        /// </summary>
        /// <param name="baseValue">Starting stat value at level 1</param>
        /// <param name="growthPerLevel">Amount the stat increases per level</param>
        /// <param name="targetValue">Desired stat value at max level</param>
        /// <param name="cap">Maximum allowed value for the stat</param>
        /// <param name="tolerance">Acceptable difference from target (default 5)</param>
        /// <returns>True if the growth curve is valid, false otherwise</returns>
        public static bool ValidateGrowthCurve(int baseValue, int growthPerLevel, int targetValue, int cap, int tolerance = 5)
        {
            // Check if base value exceeds cap
            if (baseValue > cap)
                return false;
            
            // Calculate stat at max level
            int valueAtMaxLevel = CalculateLinearGrowth(baseValue, growthPerLevel, StatConstants.MaxLevel);
            
            // Check if max level value exceeds cap
            if (valueAtMaxLevel > cap)
                return false;
            
            // Check if max level value is within tolerance of target
            int difference = System.Math.Abs(valueAtMaxLevel - targetValue);
            if (difference > tolerance)
                return false;
            
            return true;
        }

        /// <summary>
        /// Calculates total stats at a given level by combining base stats, job base bonus, and job growth.
        /// Formula: baseStats + jobBaseBonus + (jobGrowthPerLevel * (level - 1))
        /// </summary>
        /// <param name="baseStats">Hero's base stats (racial/innate stats)</param>
        /// <param name="jobBaseBonus">Job's base stat bonus applied at level 1</param>
        /// <param name="jobGrowthPerLevel">Job's per-level stat growth</param>
        /// <param name="level">Current level (1-based)</param>
        /// <returns>Total stats at the given level, with each stat clamped to MaxStat</returns>
        public static StatBlock CalculateTotalStatsAtLevel(
            in StatBlock baseStats,
            in StatBlock jobBaseBonus,
            in StatBlock jobGrowthPerLevel,
            int level)
        {
            // Ensure level is at least 1
            if (level < 1) level = 1;
            
            // Calculate job contribution at this level
            // At level 1: jobBaseBonus
            // At level 2+: jobBaseBonus + (jobGrowthPerLevel * (level - 1))
            StatBlock jobContribution;
            if (level == 1)
            {
                jobContribution = jobBaseBonus;
            }
            else
            {
                // Scale growth by levels gained (level - 1)
                var scaledGrowth = jobGrowthPerLevel.Scale(level - 1);
                jobContribution = jobBaseBonus.Add(scaledGrowth);
            }
            
            // Combine base stats with job contribution
            var totalStats = baseStats.Add(jobContribution);
            
            // Clamp all stats to their maximum values
            return StatConstants.ClampStatBlock(totalStats);
        }
    }
}
