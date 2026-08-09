using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services.Analytics;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>
    /// Service that stores all items (equipped + inventory) from fallen heroes.
    /// Items are stacked up to 999 per item type. Capacity is capped at 540 stacks (10 pages × 54 slots).
    /// When full, the lowest-priority stack is evicted to make room — or the incoming item is
    /// discarded if it is itself the weakest candidate (issue #373).
    /// </summary>
    public class SecondChanceMerchantVault
    {
        /// <summary>Represents a stacked item in the vault.</summary>
        public sealed class StackedItem
        {
            public IItem ItemTemplate { get; }
            public int Quantity { get; set; }

            public StackedItem(IItem itemTemplate, int quantity)
            {
                ItemTemplate = itemTemplate;
                Quantity = quantity;
            }
        }

        // ── Capacity constants ────────────────────────────────────────────────────

        /// <summary>Maximum number of pages the vault item grid can display.</summary>
        public const int MaxPages = 10;

        /// <summary>Number of item slots per page (9 columns × 6 rows).</summary>
        public const int SlotsPerPage = 54;

        /// <summary>Maximum number of distinct item stacks the vault will hold (MaxPages × SlotsPerPage).</summary>
        public const int MaxStacks = MaxPages * SlotsPerPage; // 540

        private const int MaxStackSize = 999;

        /// <summary>Sentinel index meaning "the incoming item" in eviction comparisons.</summary>
        private const int IncomingIndex = int.MaxValue;

        private readonly List<StackedItem> _stacks = new List<StackedItem>();
        private readonly List<HeroCrystal> _lostCrystals = new List<HeroCrystal>();

        /// <summary>Gets a read-only collection of all stacked items in the vault.</summary>
        public IReadOnlyList<StackedItem> Stacks => _stacks.AsReadOnly();

        /// <summary>Gets a read-only collection of all lost crystals in the vault.</summary>
        public IReadOnlyList<HeroCrystal> LostCrystals => _lostCrystals.AsReadOnly();

        /// <summary>Gets the total number of crystals in the vault.</summary>
        public int CrystalCount => _lostCrystals.Count;

        /// <summary>
        /// Adds a single item to the vault, stacking with existing items if applicable.
        /// When the vault is at its stack cap (<see cref="MaxStacks"/>), the weakest existing
        /// stack is evicted to make room using the same ranking as ExcessItemSellSelector
        /// (consumables-first; lower gear score/rarity/price is weaker).  If the incoming item
        /// is itself the weakest it is discarded rather than evicting a stronger item.
        /// Stacking into an existing same-item stack never triggers eviction.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <param name="logEvictions">
        /// Set to false to suppress console messages and analytics during save-restore, where
        /// a legacy >540-stack save is trimmed silently rather than spamming the event console.
        /// </param>
        public void AddItem(IItem item, bool logEvictions = true)
        {
            if (item == null) return;

            // For consumables, get the stack count from the item
            int quantityToAdd = 1;
            if (item is Consumable consumable)
                quantityToAdd = consumable.StackCount;

            // Keep adding until all quantity is placed
            while (quantityToAdd > 0)
            {
                // Try to find an existing stack of the same item that has space.
                // Stacking never triggers the cap — cap is measured in stack count, not quantity.
                StackedItem existingStack = null;
                for (int i = 0; i < _stacks.Count; i++)
                {
                    if (IsSameItem(_stacks[i].ItemTemplate, item) && _stacks[i].Quantity < MaxStackSize)
                    {
                        existingStack = _stacks[i];
                        break;
                    }
                }

                if (existingStack != null)
                {
                    // Add to existing stack (cap at MaxStackSize)
                    int availableSpace = MaxStackSize - existingStack.Quantity;
                    int amountToAdd = quantityToAdd < availableSpace ? quantityToAdd : availableSpace;
                    existingStack.Quantity += amountToAdd;
                    quantityToAdd -= amountToAdd;
                }
                else
                {
                    // A new stack is required — check capacity
                    int amountForNewStack = quantityToAdd < MaxStackSize ? quantityToAdd : MaxStackSize;

                    if (_stacks.Count >= MaxStacks)
                    {
                        // Vault is full: select the weakest candidate (existing or incoming)
                        int evictIndex = SelectEvictionIndex(item, amountForNewStack);

                        if (evictIndex == IncomingIndex)
                        {
                            // Incoming item is the weakest — discard this portion silently
                            EmitRejected(item, logEvictions);
                            quantityToAdd -= amountForNewStack;
                            continue;
                        }

                        // Evict the weakest existing stack
                        var evictedStack = _stacks[evictIndex];
                        EmitEvicted(evictedStack.ItemTemplate, evictedStack.Quantity, item, logEvictions);
                        _stacks.RemoveAt(evictIndex);
                    }

                    var newStack = new StackedItem(CloneItemTemplate(item), amountForNewStack);
                    _stacks.Add(newStack);
                    quantityToAdd -= amountForNewStack;
                }
            }
        }

        /// <summary>Adds multiple items to the vault.</summary>
        /// <param name="items">The items to add.</param>
        public void AddItems(IEnumerable<IItem> items)
        {
            if (items == null) return;

            foreach (var item in items)
                AddItem(item);
        }

        /// <summary>Removes a quantity of an item from the vault (e.g., when purchased).</summary>
        /// <param name="stack">The stack to remove from.</param>
        /// <param name="quantity">The quantity to remove.</param>
        /// <returns>True if the quantity was successfully removed.</returns>
        public bool RemoveQuantity(StackedItem stack, int quantity)
        {
            if (stack == null || quantity <= 0) return false;
            if (!_stacks.Contains(stack)) return false;
            if (stack.Quantity < quantity) return false;

            stack.Quantity -= quantity;

            // Remove the stack if it's empty
            if (stack.Quantity <= 0)
                _stacks.Remove(stack);

            return true;
        }

        /// <summary>Gets the total number of unique item stacks in the vault.</summary>
        public int StackCount => _stacks.Count;

        /// <summary>Gets the total quantity of all items in the vault.</summary>
        public int TotalItemCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _stacks.Count; i++)
                    total += _stacks[i].Quantity;
                return total;
            }
        }

        /// <summary>Clears all items from the vault.</summary>
        public void Clear()
        {
            _stacks.Clear();
            _lostCrystals.Clear();
        }

        // ── Eviction ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Two-pass, consumables-first eviction selection that mirrors ExcessItemSellSelector.Select.
        /// Pass 1 considers all Consumable stacks plus the incoming item at <see cref="IncomingIndex"/>
        /// if it is a consumable.  Pass 2 does the same for IGear items.  The first pass that finds
        /// any candidate wins.
        /// Returns the list index of the stack to evict, or <see cref="IncomingIndex"/> when the
        /// incoming item is itself the weakest (in which case the caller discards it).
        /// </summary>
        private int SelectEvictionIndex(IItem item, int incomingStackQty)
        {
            // Pass 1: consumables
            {
                int bestIndex = -1;
                long bestA = 0, bestB = 0, bestC = 0;
                bool hasCandidate = false;

                for (int i = 0; i < _stacks.Count; i++)
                {
                    if (_stacks[i].ItemTemplate is Consumable c)
                    {
                        ItemWeaknessRanking.ConsumableKey(c, _stacks[i].Quantity, out long a, out long b, out long kc);
                        if (!hasCandidate || ItemWeaknessRanking.IsWeaker(a, b, kc, i, bestA, bestB, bestC, bestIndex))
                        {
                            bestIndex = i; bestA = a; bestB = b; bestC = kc;
                        }
                        hasCandidate = true;
                    }
                }

                if (item is Consumable incomingC)
                {
                    ItemWeaknessRanking.ConsumableKey(incomingC, incomingStackQty, out long a, out long b, out long kc);
                    if (!hasCandidate || ItemWeaknessRanking.IsWeaker(a, b, kc, IncomingIndex, bestA, bestB, bestC, bestIndex))
                    {
                        bestIndex = IncomingIndex; bestA = a; bestB = b; bestC = kc;
                    }
                    hasCandidate = true;
                }

                if (hasCandidate)
                    return bestIndex;
            }

            // Pass 2: gear
            {
                int bestIndex = -1;
                long bestA = 0, bestB = 0, bestC = 0;
                bool hasCandidate = false;

                for (int i = 0; i < _stacks.Count; i++)
                {
                    if (_stacks[i].ItemTemplate is IGear g)
                    {
                        ItemWeaknessRanking.GearKey(g, out long a, out long b, out long c);
                        if (!hasCandidate || ItemWeaknessRanking.IsWeaker(a, b, c, i, bestA, bestB, bestC, bestIndex))
                        {
                            bestIndex = i; bestA = a; bestB = b; bestC = c;
                        }
                        hasCandidate = true;
                    }
                }

                if (item is IGear incomingG)
                {
                    ItemWeaknessRanking.GearKey(incomingG, out long a, out long b, out long c);
                    if (!hasCandidate || ItemWeaknessRanking.IsWeaker(a, b, c, IncomingIndex, bestA, bestB, bestC, bestIndex))
                    {
                        bestIndex = IncomingIndex; bestA = a; bestB = b; bestC = c;
                    }
                    hasCandidate = true;
                }

                if (hasCandidate)
                    return bestIndex;
            }

            // Fallback: no typed candidates found (vault contains unrecognized item types).
            // Discard the incoming item rather than evicting blindly.
            return IncomingIndex;
        }

        // ── Logging helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Emits a console event and debug analytics when an existing stack is evicted to make
        /// room for an incoming item.  All emits are guarded by <c>Core.Instance != null</c> so
        /// headless unit tests exercise the eviction path without triggering NREs.
        /// </summary>
        private static void EmitEvicted(IItem evicted, int evictedQty, IItem incoming, bool log)
        {
            if (!log) return;
            AnalyticsService.LogVaultEviction(evicted, evictedQty, incoming);
            if (Core.Instance == null) return;
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(
                UITextKey.ConsoleVaultItemEvicted,
                (evicted.Name, RarityUtils.GetRarityColor(evicted.Rarity)),
                (incoming.Name, RarityUtils.GetRarityColor(incoming.Rarity)));
        }

        /// <summary>
        /// Emits a console event and debug analytics when the incoming item itself is the weakest
        /// and is discarded without evicting anything.
        /// </summary>
        private static void EmitRejected(IItem rejected, bool log)
        {
            if (!log) return;
            AnalyticsService.LogVaultEviction(rejected, 0, null);
            if (Core.Instance == null) return;
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(
                UITextKey.ConsoleVaultItemRejected,
                (rejected.Name, RarityUtils.GetRarityColor(rejected.Rarity)));
        }

        // ── Identity / cloning ────────────────────────────────────────────────────

        /// <summary>Checks if two items are the same type (for stacking purposes).</summary>
        private bool IsSameItem(IItem item1, IItem item2)
        {
            if (item1 == null || item2 == null) return false;

            // Items are considered the same if they have the same name, kind, and rarity
            return item1.Name == item2.Name &&
                   item1.Kind == item2.Kind &&
                   item1.Rarity == item2.Rarity;
        }

        /// <summary>Creates a clean template copy of an item (for consumables, resets stack count to 0).</summary>
        private IItem CloneItemTemplate(IItem item)
        {
            // For now, just return the item as-is since IItem is typically immutable
            // For consumables, we'll use the item as a template and track quantity separately
            return item;
        }

        // ── Crystals ──────────────────────────────────────────────────────────────

        /// <summary>Adds a crystal to the vault.</summary>
        public void AddCrystal(HeroCrystal crystal)
        {
            if (crystal == null) return;
            _lostCrystals.Add(crystal);
        }

        /// <summary>Removes a crystal from the vault. Returns true if removed.</summary>
        public bool RemoveCrystal(HeroCrystal crystal)
        {
            if (crystal == null) return false;
            return _lostCrystals.Remove(crystal);
        }
    }
}
