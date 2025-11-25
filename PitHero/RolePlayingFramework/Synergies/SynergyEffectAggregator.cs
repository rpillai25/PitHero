using System;

namespace RolePlayingFramework.Synergies
{
    /// <summary>
    /// Static utility for calculating diminishing returns multipliers for stacked synergies.
    /// Issue #133 - Core Synergy Stacking System
    /// </summary>
    public static class SynergyEffectAggregator
    {
        /// <summary>Maximum number of instances allowed per pattern.</summary>
        public const int MaxInstancesPerPattern = 3;
        
        /// <summary>Diminishing returns multipliers for each instance [1st, 2nd, 3rd].</summary>
        private static readonly float[] Multipliers = { 1.0f, 0.5f, 0.25f };
        
        /// <summary>Acceleration bonus per extra instance (before skill learned).</summary>
        public const float AccelerationBonusPerInstance = 0.35f;
        
        /// <summary>Maximum acceleration cap (70% bonus).</summary>
        public const float MaxAccelerationCap = 1.70f;
        
        /// <summary>
        /// Calculates the total additive multiplier for a given instance count.
        /// 1 instance = 1.0, 2 instances = 1.5, 3 instances = 1.75
        /// </summary>
        /// <param name="instanceCount">Number of active instances of the same pattern.</param>
        /// <returns>Total multiplier (additive sum of diminishing values).</returns>
        public static float GetTotalMultiplier(int instanceCount)
        {
            if (instanceCount <= 0)
                return 0f;
            
            float total = 0f;
            int count = Math.Min(instanceCount, Multipliers.Length);
            for (int i = 0; i < count; i++)
            {
                total += Multipliers[i];
            }
            return total;
        }
        
        /// <summary>
        /// Gets the individual multiplier for a specific instance index (0-based).
        /// </summary>
        /// <param name="instanceIndex">0-based index of the instance.</param>
        /// <returns>The multiplier for that instance, or 0 if beyond cap.</returns>
        public static float GetInstanceMultiplier(int instanceIndex)
        {
            if (instanceIndex < 0 || instanceIndex >= Multipliers.Length)
                return 0f;
            return Multipliers[instanceIndex];
        }
        
        /// <summary>
        /// Calculates the synergy point acceleration multiplier for earning points.
        /// Before skill is learned: BasePoints * (1 + 0.35 * (InstanceCount - 1)), capped at +70%
        /// After skill is learned: 1.0 (no acceleration)
        /// </summary>
        /// <param name="instanceCount">Number of active instances.</param>
        /// <param name="skillLearned">True if the synergy skill has already been learned.</param>
        /// <returns>Acceleration multiplier for synergy points.</returns>
        public static float GetPointsAccelerationMultiplier(int instanceCount, bool skillLearned)
        {
            if (skillLearned)
                return 1.0f;
            
            if (instanceCount <= 1)
                return 1.0f;
            
            float acceleration = 1.0f + AccelerationBonusPerInstance * (instanceCount - 1);
            return Math.Min(acceleration, MaxAccelerationCap);
        }
    }
}
