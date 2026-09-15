using System;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Synergies;

namespace PitHero.Services
{
    /// <summary>
    /// Simulation-side owner of the hero's active synergies. Every simulation tick the hero component
    /// hands it the bag; when any bag slot's item reference changed since the last pass it rebuilds the
    /// Party grid's bag rows (PartyGridLayout), runs the pattern detector and applies the result to the
    /// hero. The Party window only reads the outcome for its glow effects. Synergy passives (deflect,
    /// defense, stat bonuses, granted skills) therefore never depend on which windows the player has
    /// opened — a replay recomputes them identically. Equipment cells never take part.
    /// Allocation-free after warm-up; the change check is a reference compare per bag slot.
    /// </summary>
    public sealed class HeroSynergyResolver
    {
        private readonly SynergyDetector _detector = new SynergyDetector();
        private readonly IItem[,] _grid = new IItem[PartyGridLayout.Width, PartyGridLayout.Height];
        private IItem[] _signature;
        private IItem[] _next;
        private GameStateService _gameState;

        /// <summary>Raised on the simulation thread right after the hero's synergies were recomputed.</summary>
        public event Action Changed;

        /// <summary>How many times the detector has run (diagnostics and tests).</summary>
        public int RecomputeCount { get; private set; }

        public HeroSynergyResolver()
        {
            var all = SynergyPatternRegistry.All;
            for (int i = 0; i < all.Count; i++)
                _detector.RegisterPattern(all[i]);
        }

        /// <summary>Forces the next <see cref="Sync"/> to recompute even if nothing appears to have changed.</summary>
        public void Invalidate()
        {
            _signature = null;
        }

        /// <summary>Recomputes and applies the hero's synergies if any bag slot's item changed.</summary>
        public void Sync(Hero hero, ItemBag bag, GameStateService gameState)
        {
            if (hero == null || bag == null)
                return;
            _gameState = gameState;

            int cells = bag.Capacity;
            if (_next == null || _next.Length != cells)
            {
                _next = new IItem[cells];
                _signature = null;
            }

            for (int i = 0; i < cells; i++)
                _next[i] = bag.GetSlotItem(i);

            bool changed = _signature == null;
            if (!changed)
            {
                for (int i = 0; i < cells; i++)
                {
                    if (!ReferenceEquals(_signature[i], _next[i])) { changed = true; break; }
                }
            }
            if (!changed)
                return;

            // Swap the buffers so the just-written snapshot becomes the signature
            var old = _signature ?? new IItem[cells];
            _signature = _next;
            _next = old;

            PartyGridLayout.FillSynergyGrid(_grid, bag);
            var groups = _detector.DetectSynergiesGrouped(_grid, PartyGridLayout.Width, PartyGridLayout.Height);
            hero.UpdateActiveSynergiesGrouped(groups, gameState);
            RecomputeCount++;
            Changed?.Invoke();
        }

        /// <summary>True when the bag slot is a cell of one of the hero's active synergies.</summary>
        public static bool IsBagIndexInSynergy(Hero hero, int bagIndex)
        {
            if (hero == null || bagIndex < 0)
                return false;
            var cell = PartyGridLayout.BagIndexToCell(bagIndex);
            var synergies = hero.ActiveSynergies;
            for (int i = 0; i < synergies.Count; i++)
            {
                var slots = synergies[i].AffectedSlots;
                for (int j = 0; j < slots.Count; j++)
                    if (slots[j].X == cell.X && slots[j].Y == cell.Y)
                        return true;
            }
            return false;
        }

        /// <summary>True when the bag slot is covered by a placed stencil (from the persisted records).</summary>
        public static bool IsBagIndexUnderStencil(GameStateService gameState, int bagIndex)
        {
            if (gameState == null || bagIndex < 0)
                return false;
            var cell = PartyGridLayout.BagIndexToCell(bagIndex);
            var records = gameState.PlacedStencils;
            for (int r = 0; r < records.Count; r++)
            {
                var pattern = SynergyPatternRegistry.GetById(records[r].PatternId);
                if (pattern == null) continue;
                var offsets = pattern.GridOffsets;
                for (int o = 0; o < offsets.Count; o++)
                    if (records[r].AnchorX + offsets[o].X == cell.X && records[r].AnchorY + offsets[o].Y == cell.Y)
                        return true;
            }
            return false;
        }

        /// <summary>Auto-sell protection: items in an active synergy or under a placed stencil are never sold.</summary>
        public bool IsBagIndexProtected(Hero hero, int bagIndex)
        {
            return IsBagIndexInSynergy(hero, bagIndex) || IsBagIndexUnderStencil(_gameState, bagIndex);
        }
    }
}
