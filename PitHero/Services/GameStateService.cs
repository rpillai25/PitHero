using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>Non-UI snapshot of a stencil placed on the inventory grid.</summary>
    public struct PlacedStencilRecord
    {
        public string PatternId;
        public int AnchorX;
        public int AnchorY;
    }

    /// <summary>
    /// Represents Game State that exists independently of heroes.  This will be persisted independently of heroes.
    /// </summary>
    public class GameStateService
    {
        /// <summary>Gold currency that persists across all heroes.</summary>
        public int Funds { get; set; }

        /// <summary>Adds gold to Funds and records the gain with its source for balance analytics.</summary>
        public void AddFunds(int amount, string source)
        {
            Funds += amount;
            Analytics.AnalyticsService.LogGoldGained(amount, source, Funds);
        }

        private int _runnerCarryLevel = GameConfig.KitchenRunnerCarryLevelMin;

        /// <summary>
        /// Global kitchen-runner carry level (issue #386). Level 1 = 1 unit of up to 3 crop types
        /// per trip, level 2 = 5 units each, level 3 = 10 units each. Raised by one-of-a-kind
        /// items the hero finds (future feature). Persisted.
        /// </summary>
        public int RunnerCarryLevel
        {
            get => _runnerCarryLevel;
            set
            {
                int v = value;
                if (v < GameConfig.KitchenRunnerCarryLevelMin) v = GameConfig.KitchenRunnerCarryLevelMin;
                if (v > GameConfig.KitchenRunnerCarryLevelMax) v = GameConfig.KitchenRunnerCarryLevelMax;
                _runnerCarryLevel = v;
            }
        }

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

        /// <summary>Non-UI source of truth for stencils currently placed on the inventory grid.</summary>
        public List<PlacedStencilRecord> PlacedStencils { get; } = new List<PlacedStencilRecord>();

        /// <summary>Adds or replaces the record for the given pattern ID.</summary>
        public void SetPlacedStencil(string patternId, int anchorX, int anchorY)
        {
            for (int i = 0; i < PlacedStencils.Count; i++)
            {
                if (PlacedStencils[i].PatternId == patternId)
                {
                    PlacedStencils[i] = new PlacedStencilRecord { PatternId = patternId, AnchorX = anchorX, AnchorY = anchorY };
                    return;
                }
            }
            PlacedStencils.Add(new PlacedStencilRecord { PatternId = patternId, AnchorX = anchorX, AnchorY = anchorY });
        }

        /// <summary>Removes the record for the given pattern ID (no-op if absent).</summary>
        public void RemovePlacedStencil(string patternId)
        {
            for (int i = PlacedStencils.Count - 1; i >= 0; i--)
            {
                if (PlacedStencils[i].PatternId == patternId)
                {
                    PlacedStencils.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Removes all placed stencil records.</summary>
        public void ClearPlacedStencils()
        {
            PlacedStencils.Clear();
        }
    }
}
