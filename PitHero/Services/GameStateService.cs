using PitHero.Artifacts;
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

        /// <summary>
        /// Identity of this playthrough's hero: generated once when a new game starts, saved with the
        /// game and stamped on every replay. Time travel into a saved replay requires a match.
        /// </summary>
        public int HeroId { get; set; }

        /// <summary>Non-zero id for a newly created hero (wall-clock entropy; never called inside the simulation).</summary>
        public static int GenerateHeroId()
        {
            int id = System.Guid.NewGuid().GetHashCode() ^ System.Environment.TickCount;
            return id != 0 ? id : 1;
        }

        /// <summary>Adds gold to Funds and records the gain with its source for balance analytics.</summary>
        public void AddFunds(int amount, string source)
        {
            Funds += amount;
            Analytics.AnalyticsService.LogGoldGained(amount, source, Funds);
        }

        /// <summary>Removes gold from Funds and records the spend with its source for balance analytics. Callers check affordability first.</summary>
        public void SpendFunds(int amount, string source)
        {
            Funds -= amount;
            Analytics.AnalyticsService.LogGoldSpent(amount, source, Funds);
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

        // ── Local artifacts (issue #411) ─────────────────────────────────────────
        // Hero-specific artifacts live here (session save), not in the system save. The simulation
        // reads them every fixed step (crop growth, worker speed), so they are restored by
        // ApplyLoadedState — which replay start also runs — and cleared only for a new hero.

        private readonly bool[] _localArtifacts = new bool[ArtifactCatalog.Count];
        private readonly List<int> _localArtifactOrder = new List<int>(ArtifactCatalog.Count);

        /// <summary>Incremented on every local-artifact change (grant, load, clear) so version-cached UI refreshes.</summary>
        public int LocalArtifactVersion { get; private set; }

        /// <summary>Bit per owned local artifact ordinal; hashed into the replay divergence tripwire.</summary>
        public int LocalArtifactMask
        {
            get
            {
                int mask = 0;
                for (int i = 0; i < _localArtifacts.Length; i++)
                {
                    if (_localArtifacts[i])
                        mask |= 1 << i;
                }
                return mask;
            }
        }

        /// <summary>True when this hero owns the local artifact.</summary>
        public bool OwnsLocalArtifact(ArtifactType type)
        {
            int i = (int)type;
            return i >= 0 && i < _localArtifacts.Length && _localArtifacts[i];
        }

        /// <summary>Grants a local artifact to this hero. Idempotent: returns false when already owned or invalid.</summary>
        public bool GrantLocalArtifact(ArtifactType type)
        {
            int i = (int)type;
            if (i < 0 || i >= _localArtifacts.Length || _localArtifacts[i])
                return false;
            _localArtifacts[i] = true;
            if (!_localArtifactOrder.Contains(i))
                _localArtifactOrder.Add(i);
            LocalArtifactVersion++;
            return true;
        }

        /// <summary>Appends the owned local artifacts to <paramref name="result"/> in the order they were granted.</summary>
        public void GetLocalArtifactsInOrder(List<ArtifactType> result)
        {
            for (int i = 0; i < _localArtifactOrder.Count; i++)
            {
                int ordinal = _localArtifactOrder[i];
                if (ArtifactCatalog.IsValid(ordinal))
                    result.Add((ArtifactType)ordinal);
            }
        }

        /// <summary>Appends the owned local artifact ordinals (grant order, unknown ordinals included) for saving.</summary>
        public void CopyLocalArtifactOrdinals(List<int> result)
        {
            for (int i = 0; i < _localArtifactOrder.Count; i++)
                result.Add(_localArtifactOrder[i]);
        }

        /// <summary>
        /// Replaces the owned local artifacts from a save (slot, autosave or replay start state).
        /// Ordinals this build does not know are kept so a save/load cycle never drops a purchase.
        /// </summary>
        public void SetLocalArtifacts(List<int> ordinals)
        {
            for (int i = 0; i < _localArtifacts.Length; i++)
                _localArtifacts[i] = false;
            _localArtifactOrder.Clear();
            if (ordinals != null)
            {
                for (int i = 0; i < ordinals.Count; i++)
                {
                    int ordinal = ordinals[i];
                    if (_localArtifactOrder.Contains(ordinal))
                        continue;
                    _localArtifactOrder.Add(ordinal);
                    if (ArtifactCatalog.IsValid(ordinal))
                        _localArtifacts[ordinal] = true;
                }
            }
            LocalArtifactVersion++;
        }

        /// <summary>Forgets every local artifact (a new hero starts with none).</summary>
        public void ClearLocalArtifacts()
        {
            for (int i = 0; i < _localArtifacts.Length; i++)
                _localArtifacts[i] = false;
            _localArtifactOrder.Clear();
            LocalArtifactVersion++;
        }

        // ── Lifetime progression counters (issue #413) ───────────────────────────
        // Only ever increase (selling a crop never lowers its harvested total). Crop totals gate crop
        // unlocks in the shop and planting palette; both feed the Farm Stats window. Session state:
        // saved with the hero, restored by ApplyLoadedState (replay start included), reset for a new hero.

        /// <summary>Units harvested per crop over the hero's lifetime, indexed by (int)CropType.</summary>
        public int[] CropHarvestedTotals { get; } = new int[Farming.CropTypeInfo.Count];

        /// <summary>Dishes served per dish type over the hero's lifetime, indexed by (int)DishType.</summary>
        public int[] DishesServedTotals { get; } = new int[Dining.DishTypeInfo.Count];

        /// <summary>Adds harvested units to the crop's lifetime total.</summary>
        public void RecordHarvest(Farming.CropType crop, int units)
        {
            int i = (int)crop;
            if (units <= 0 || i < 0 || i >= CropHarvestedTotals.Length)
                return;
            CropHarvestedTotals[i] += units;
        }

        /// <summary>Counts one served dish of the given type.</summary>
        public void RecordDishServed(Dining.DishType dish)
        {
            int i = (int)dish;
            if (i < 0 || i >= DishesServedTotals.Length)
                return;
            DishesServedTotals[i]++;
        }

        /// <summary>Replaces both counter arrays from a save (missing entries read as zero).</summary>
        public void SetProgressCounters(int[] cropHarvested, int[] dishesServed)
        {
            CopyCounters(cropHarvested, CropHarvestedTotals);
            CopyCounters(dishesServed, DishesServedTotals);
        }

        /// <summary>Zeroes both counter arrays (a new hero starts from nothing).</summary>
        public void ClearProgressCounters()
        {
            CopyCounters(null, CropHarvestedTotals);
            CopyCounters(null, DishesServedTotals);
        }

        private static void CopyCounters(int[] source, int[] target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                int v = source != null && i < source.Length ? source[i] : 0;
                target[i] = v < 0 ? 0 : v;
            }
        }
    }
}
