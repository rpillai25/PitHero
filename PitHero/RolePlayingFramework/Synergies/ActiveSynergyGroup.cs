using System.Collections.Generic;

namespace RolePlayingFramework.Synergies
{
    /// <summary>
    /// Groups multiple instances of the same SynergyPattern for stacking effects.
    /// Tracks all non-overlapping instances and provides aggregate multiplier calculations.
    /// Issue #133 - Core Synergy Stacking System
    /// </summary>
    public sealed class ActiveSynergyGroup
    {
        /// <summary>The synergy pattern this group represents.</summary>
        public SynergyPattern Pattern { get; }

        /// <summary>All active instances of this pattern (non-overlapping).</summary>
        private readonly List<ActiveSynergy> _instances;

        /// <summary>Read-only access to instances.</summary>
        public IReadOnlyList<ActiveSynergy> Instances => _instances;

        /// <summary>Number of active instances in this group.</summary>
        public int InstanceCount => _instances.Count;

        /// <summary>
        /// Total additive multiplier for effects based on instance count.
        /// Uses diminishing returns: 1 instance = 1.0, 2 = 1.5, 3 = 1.75
        /// </summary>
        public float TotalMultiplier => SynergyEffectAggregator.GetTotalMultiplier(InstanceCount);

        /// <summary>Creates a new synergy group for the given pattern.</summary>
        /// <param name="pattern">The synergy pattern this group tracks.</param>
        public ActiveSynergyGroup(SynergyPattern pattern)
        {
            Pattern = pattern;
            _instances = new List<ActiveSynergy>(SynergyEffectAggregator.MaxInstancesPerPattern);
        }

        /// <summary>
        /// Attempts to add an instance to this group.
        /// Rejects if max instances reached or if instance overlaps with existing ones.
        /// </summary>
        /// <param name="instance">The instance to add.</param>
        /// <returns>True if added, false if rejected due to cap or overlap.</returns>
        public bool TryAddInstance(ActiveSynergy instance)
        {
            // Check cap
            if (_instances.Count >= SynergyEffectAggregator.MaxInstancesPerPattern)
                return false;

            // Check overlap with existing instances
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i].SharesItems(instance))
                    return false;
            }

            _instances.Add(instance);
            return true;
        }

        /// <summary>Clears all instances from this group.</summary>
        public void Clear()
        {
            _instances.Clear();
        }
    }
}
