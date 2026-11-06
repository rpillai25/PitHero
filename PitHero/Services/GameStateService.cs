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

        // ── Watering priority (issue #450) ───────────────────────────────────────
        // Which crop a farm worker waters first: a permutation of every CropType ordinal, highest
        // priority first. Locked crops keep a place in it (the UI just hides them) so a crop's spot
        // never depends on what happens to be unlocked. Sim-read by FarmTaskCoordinator's water
        // claim, changed only by the SwapWateringPriority command and by crop unlocks, saved with
        // the hero (v41), restored by ApplyLoadedState (replay start included), reset for a new hero.

        private readonly int[] _wateringOrder = DefaultWateringPriorityOrder();
        private readonly int[] _wateringRank = BuildWateringRanks(DefaultWateringPriorityOrder());

        /// <summary>Incremented on every watering-priority change so version-cached UI refreshes.</summary>
        public int WateringPriorityVersion { get; private set; }

        /// <summary>
        /// The order a new hero (and every pre-v41 save) starts with: highest crop tier first, i.e.
        /// <see cref="Farming.CropUnlockConfig.ProgressionOrder"/> reversed, because later crops are
        /// worth the most.
        /// </summary>
        public static int[] DefaultWateringPriorityOrder()
        {
            var progression = Farming.CropUnlockConfig.ProgressionOrder;
            var order = new int[Farming.CropTypeInfo.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = (int)progression[progression.Length - 1 - i];
            return order;
        }

        /// <summary>True when values holds every CropType ordinal exactly once.</summary>
        public static bool IsWateringPriorityPermutation(int[] values)
        {
            if (values == null || values.Length != Farming.CropTypeInfo.Count)
                return false;
            int seen = 0;
            for (int i = 0; i < values.Length; i++)
            {
                int v = values[i];
                if (v < 0 || v >= Farming.CropTypeInfo.Count || (seen & (1 << v)) != 0)
                    return false;
                seen |= 1 << v;
            }
            return true;
        }

        private static int[] BuildWateringRanks(int[] order)
        {
            var ranks = new int[order.Length];
            for (int i = 0; i < order.Length; i++)
                ranks[order[i]] = i;
            return ranks;
        }

        private void RebuildWateringRanks()
        {
            for (int i = 0; i < _wateringOrder.Length; i++)
                _wateringRank[_wateringOrder[i]] = i;
            WateringPriorityVersion++;
        }

        /// <summary>The crop's place in the watering order, 0 = watered first (int.MaxValue for an unknown ordinal).</summary>
        public int GetWateringRank(Farming.CropType crop)
        {
            int i = (int)crop;
            return i >= 0 && i < _wateringRank.Length ? _wateringRank[i] : int.MaxValue;
        }

        /// <summary>Copies the watering order (crop ordinals, first = highest priority) into dest.</summary>
        public void CopyWateringPriorityOrder(int[] dest)
        {
            if (dest == null || dest.Length < _wateringOrder.Length)
                return;
            for (int i = 0; i < _wateringOrder.Length; i++)
                dest[i] = _wateringOrder[i];
        }

        /// <summary>The watering order as a fresh array, for the save gather.</summary>
        public int[] CopyWateringPriorityOrderArray()
        {
            var copy = new int[_wateringOrder.Length];
            CopyWateringPriorityOrder(copy);
            return copy;
        }

        /// <summary>Replaces the watering order from a save; anything that is not a full permutation restores the default.</summary>
        public void SetWateringPriorityOrder(int[] order)
        {
            var source = IsWateringPriorityPermutation(order) ? order : DefaultWateringPriorityOrder();
            for (int i = 0; i < _wateringOrder.Length; i++)
                _wateringOrder[i] = source[i];
            RebuildWateringRanks();
        }

        /// <summary>Restores the default watering order (a new hero).</summary>
        public void ResetWateringPriority() => SetWateringPriorityOrder(null);

        // ── Gem dig pity (issue #452) ────────────────────────────────────────────
        // Digs since each tier (Uncommon, Rare, Epic, Legendary) or a better one last came up at the
        // gem dig site. Sim-read by the DigGem handler (they raise that tier's odds, see
        // GemDigConfig), saved with the hero (v42), restored by ApplyLoadedState, reset for a new hero.

        private readonly int[] _gemPity = new int[Config.GemDigConfig.PityTierCount];

        /// <summary>
        /// Resolves one dig at the gem dig site from its roll (in [0, GemDigConfig.RollRange)) under
        /// the current pity counters, then advances the counters. Returns the gem found.
        /// </summary>
        public Config.GemType DigGem(long roll)
        {
            var gem = Config.GemDigConfig.Resolve(roll, _gemPity);
            Config.GemDigConfig.AdvancePity(_gemPity, gem);
            return gem;
        }

        /// <summary>Digs without the given pity tier (0..3 = Uncommon..Legendary) or better.</summary>
        public int GetGemPity(int pityIndex) => _gemPity[pityIndex];

        /// <summary>The pity counters as a fresh array, for the save gather.</summary>
        public int[] CopyGemPityArray() => (int[])_gemPity.Clone();

        /// <summary>Replaces the pity counters from a save; null or missing entries read as 0, negatives clamp to 0.</summary>
        public void SetGemPity(int[] counters)
        {
            for (int i = 0; i < _gemPity.Length; i++)
            {
                int v = counters != null && i < counters.Length ? counters[i] : 0;
                _gemPity[i] = v < 0 ? 0 : v;
            }
        }

        /// <summary>Clears the pity counters (a new hero).</summary>
        public void ResetGemPity() => SetGemPity(null);

        /// <summary>
        /// Swaps two crops' places in the watering order — the Up/Down buttons swap a crop with its
        /// visible neighbour. Returns false (no change) for an unknown or identical pair.
        /// </summary>
        public bool SwapWateringPriority(Farming.CropType a, Farming.CropType b)
        {
            int ia = (int)a, ib = (int)b;
            if (ia == ib || ia < 0 || ib < 0 || ia >= _wateringRank.Length || ib >= _wateringRank.Length)
                return false;
            int ra = _wateringRank[ia];
            int rb = _wateringRank[ib];
            _wateringOrder[ra] = ib;
            _wateringOrder[rb] = ia;
            RebuildWateringRanks();
            return true;
        }

        /// <summary>Moves a crop to the top of the watering order (a newly unlocked crop). Everything above it shifts down one.</summary>
        public void PromoteWateringPriority(Farming.CropType crop)
        {
            int c = (int)crop;
            if (c < 0 || c >= _wateringRank.Length)
                return;
            for (int i = _wateringRank[c]; i > 0; i--)
                _wateringOrder[i] = _wateringOrder[i - 1];
            _wateringOrder[0] = c;
            RebuildWateringRanks();
        }
    }
}
