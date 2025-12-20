using Microsoft.Xna.Framework;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Represents a discovered stencil for a synergy pattern.</summary>
    public sealed class SynergyStencil
    {
        /// <summary>Unique identifier for this stencil.</summary>
        public string Id { get; }

        /// <summary>ID of the synergy pattern this stencil represents.</summary>
        public string SynergyPatternId { get; }

        /// <summary>Whether this stencil can be placed on the inventory grid.</summary>
        public bool IsPlaceable { get; set; }

        /// <summary>Anchor position of the stencil overlay on the inventory grid (null if not placed).</summary>
        public Point? OverlayAnchor { get; set; }

        /// <summary>Source of how this stencil was discovered.</summary>
        public StencilDiscoverySource DiscoverySource { get; private set; }

        public SynergyStencil(string id, string synergyPatternId)
        {
            Id = id;
            SynergyPatternId = synergyPatternId;
            IsPlaceable = true;
            OverlayAnchor = null;
            DiscoverySource = StencilDiscoverySource.Unknown;
        }

        /// <summary>Marks this stencil as discovered from a specific source.</summary>
        public void MarkDiscovered(StencilDiscoverySource source)
        {
            if (DiscoverySource == StencilDiscoverySource.Unknown)
                DiscoverySource = source;
        }
    }
}
