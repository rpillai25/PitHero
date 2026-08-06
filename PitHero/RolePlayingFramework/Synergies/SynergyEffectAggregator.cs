using System;

namespace RolePlayingFramework.Synergies
{
    /// <summary>
    /// Static utility for calculating diminishing returns multipliers for stacked synergies.
    /// Issue #133 - Core Synergy Stacking System
    /// </summary>
    public static class SynergyEffectAggregator
    {
        /// <summary>Number of leading instances that use the hand-tuned normal-return values.</summary>
        public const int NormalReturnInstances = 3;

        /// <summary>Normal-return multipliers for the first instances [1st, 2nd, 3rd].</summary>
        private static readonly float[] Multipliers = { 1.0f, 0.5f, 0.25f };

        /// <summary>Decay ratio applied per instance beyond the normal returns; keeps the total asymptoting below 2.0.</summary>
        public const float ExtraInstanceDecay = 0.5f;

        /// <summary>Acceleration bonus per extra instance (before skill learned).</summary>
        public const float AccelerationBonusPerInstance = 0.35f;

        /// <summary>Maximum acceleration cap (70% bonus).</summary>
        public const float MaxAccelerationCap = 1.70f;

        /// <summary>
        /// Calculates the total additive multiplier for a given instance count.
        /// 1 instance = 1.0, 2 = 1.5, 3 = 1.75, then ever-smaller returns (4 = 1.875, ...) approaching 2.0.
        /// </summary>
        /// <param name="instanceCount">Number of active instances of the same pattern.</param>
        /// <returns>Total multiplier (additive sum of diminishing values).</returns>
        public static float GetTotalMultiplier(int instanceCount)
        {
            if (instanceCount <= 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < instanceCount; i++)
            {
                total += GetInstanceMultiplier(i);
            }
            return total;
        }

        /// <summary>
        /// Gets the individual multiplier for a specific instance index (0-based).
        /// Indices past the normal returns keep decaying geometrically instead of dropping to zero.
        /// </summary>
        /// <param name="instanceIndex">0-based index of the instance.</param>
        /// <returns>The multiplier for that instance.</returns>
        public static float GetInstanceMultiplier(int instanceIndex)
        {
            if (instanceIndex < 0)
                return 0f;
            if (instanceIndex < Multipliers.Length)
                return Multipliers[instanceIndex];

            float value = Multipliers[Multipliers.Length - 1];
            for (int i = Multipliers.Length; i <= instanceIndex; i++)
            {
                value *= ExtraInstanceDecay;
            }
            return value;
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
