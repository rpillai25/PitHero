using Nez;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Synergies;

namespace PitHero.Services
{
    /// <summary>
    /// Steers incoming items into the first empty stencil cell whose RequiredKind matches the item's kind.
    /// Implements <see cref="IBagSlotPreferenceProvider"/> for the hero inventory bag.
    /// </summary>
    public sealed class StencilBagSlotPreferenceProvider : IBagSlotPreferenceProvider
    {
        // Grid-layout constants — must stay in sync with InventoryGrid.UpdateBagSlots.
        // Rows 3-8 (inclusive) of the 20-wide grid back the 120 bag slots.
        // bagIndex = (gridY - GridBagRowStart) * GridWidth + gridX
        private const int GridWidth        = 20;
        private const int GridBagRowStart  = 3;   // first grid row that is bag-backed
        private const int GridBagRowEnd    = 8;   // last  grid row that is bag-backed (inclusive)
        private const int GridColMin       = 0;
        private const int GridColMax       = 19;

        private readonly GameStateService _gameStateService;

        /// <summary>Production constructor — resolves GameStateService from Nez per call when null.</summary>
        public StencilBagSlotPreferenceProvider() { }

        /// <summary>Test constructor — injects a GameStateService directly.</summary>
        public StencilBagSlotPreferenceProvider(GameStateService gameStateService)
        {
            _gameStateService = gameStateService;
        }

        /// <inheritdoc/>
        public int GetPreferredEmptySlot(ItemBag bag, IItem item)
        {
            if (item == null) return -1;

            var stateService = _gameStateService ?? (Core.Instance != null ? Core.Services.GetService<GameStateService>() : null);
            if (stateService == null) return -1;

            var placedStencils = stateService.PlacedStencils;
            if (placedStencils == null || placedStencils.Count == 0) return -1;

            // Iterate placed stencils in list order (deterministic placement order).
            for (int s = 0; s < placedStencils.Count; s++)
            {
                var record  = placedStencils[s];
                var pattern = SynergyPatternRegistry.GetById(record.PatternId);
                if (pattern == null) continue;

                var offsets      = pattern.GridOffsets;
                var requiredKinds = pattern.RequiredKinds;

                for (int i = 0; i < offsets.Count; i++)
                {
                    // Only consider cells whose required kind matches the incoming item.
                    if (requiredKinds[i] != item.Kind) continue;

                    int gridX = record.AnchorX + offsets[i].X;
                    int gridY = record.AnchorY + offsets[i].Y;

                    // Only rows 3-8 map to bag slots; discard out-of-bounds coordinates.
                    if (gridY < GridBagRowStart || gridY > GridBagRowEnd) continue;
                    if (gridX < GridColMin      || gridX > GridColMax)    continue;

                    int bagIndex = (gridY - GridBagRowStart) * GridWidth + gridX;

                    // Return on the first empty matching cell.
                    if (bag.GetSlotItem(bagIndex) == null) return bagIndex;
                }
            }

            return -1;
        }
    }
}
