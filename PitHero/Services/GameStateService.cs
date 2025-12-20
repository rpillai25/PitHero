using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>
    /// Represents Game State that exists independently of heroes.  This will be persisted independently of heroes.
    /// </summary>
    public class GameStateService
    {
        /// <summary>Discovered stencils mapped by pattern ID to discovery source.</summary>
        public Dictionary<string, StencilDiscoverySource> DiscoveredStencils { get; } = new();

        /// <summary>Discovers a stencil if not already discovered.</summary>
        public void DiscoverStencil(string patternId, StencilDiscoverySource source)
        {
            if (!DiscoveredStencils.ContainsKey(patternId))
            {
                DiscoveredStencils[patternId] = source;
            }
        }

        /// <summary>Checks if a stencil has been discovered.</summary>
        public bool IsStencilDiscovered(string patternId)
        {
            return DiscoveredStencils.ContainsKey(patternId);
        }
    }
}
