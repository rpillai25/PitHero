using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Services.Analytics;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;

namespace PitHero.UI
{
    /// <summary>
    /// Shows the harvested-crop storage viewer: a scrollable 8-column grid of slots (8×4 per Crop
    /// Storage building, stacked) displaying each harvested crop's sprite and stack count. Clicking an
    /// occupied slot opens a read-only name/description box. View-only — no editing.
    /// Mirrors the SeedPlantingModeOverlay UI pattern.
    /// </summary>
    public class HarvestedCropsModeOverlay
    {
        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;

        private Window _inventoryWindow;
        private Table _slotTable;
        private Table _outerTable;
        private Cell _scrollCell;

        private Window _descWindow;
        private Label _descNameLabel;
        private Label _descDescLabel;

        // The stack currently shown in the description dialog (for the Sell action).
        private int _descBuildingId;
        private int _descSlotIndex;
        private CropType _descCropType;
        private int _descCount;

        // Bottom-row buttons. "Sell all crops" (aggregate) shows only in the all-storages view;
        // "Move all crops" and "Sell these crops" act on the storage currently shown.
        private TextButton _sellAllButton;      // aggregate — sells across every storage
        private TextButton _moveAllButton;      // per-storage — redistribute to other storages
        private TextButton _sellStorageButton;  // per-storage — sell the shown storage's crops
        private TextButton _closeButton;
        private Table _buttonRow;

        // Aggregate view only: one page per Crop Storage building. Empty in the per-building view.
        private PagerRow _pagerRow;

        private const float SlotSize     = 40f;
        private const float WinPad       = 16f;
        private const int   Columns      = 8;
        private const float ButtonWidth  = 110f;
        // One page is 4 rows of 44px (40px slot + 2px pad each side), so 224 fits a full storage at the
        // 360px design height without scrolling. ShowInventoryWindow shrinks it when the configured
        // GameConfig.VirtualHeight is shorter (the grid then scrolls).
        private const float ScrollHeight = 224f;
        // Upward nudge off centre so the window clears the bottom UI bars.
        private const float CenterYBias  = 30f;

        // When >= 0, only this Crop Storage building's slots are shown (UniqueId).
        private int _filterBuildingId = -1;

        // Reusable display view: physical slots minus units held for transfer by carrying
        // runners (issue #386) — the viewer only ever shows and sells available crops.
        private readonly HarvestSlot[] _displaySlots = new HarvestSlot[CropStorageInventoryService.SlotsPerBuilding];

        /// <summary>Fired when the player dismisses the viewer; caller should exit harvested-crops mode.</summary>
        public event System.Action RequestExitHarvestedCropsMode;

        public HarvestedCropsModeOverlay(Scene scene, Stage stage)
        {
            _stage      = stage;
            _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            CreateInventoryWindow();
            CreateDescriptionWindow();
        }

        private TextService _textService;

        /// <summary>Resolves a localized UI string, falling back to the key if the service is unavailable.</summary>
        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>Localized harvested-product name for a crop (e.g. Apple Tree → "Apple").</summary>
        private string GetHarvestName(CropType crop) => GetText(CropConfig.GetHarvestDisplayNameKey(crop));

        /// <summary>Called when the player enters harvested-crops mode; rebuilds and shows the grid.</summary>
        public void OnEnterHarvestedCropsMode()
        {
            RefreshViewer();
        }

        /// <summary>
        /// Re-lays out the pager and button row for the storage now being shown, rebuilds its slot grid
        /// and re-packs the window. The pager is laid out first because everything downstream reads the
        /// page index, which Configure clamps.
        /// </summary>
        private void RefreshViewer()
        {
            LayoutPager();
            LayoutButtonRow();
            RebuildSlots();
            ShowInventoryWindow();
        }

        /// <summary>
        /// Points the pager at one page per Crop Storage. Only the aggregate view is paged — the
        /// per-building view opened from the building context menu already shows a single storage.
        /// </summary>
        private void LayoutPager()
        {
            int pageCount = _filterBuildingId >= 0
                ? 0
                : GetStorageBuildings(Core.Services?.GetService<BuildingService>()).Count;
            _pagerRow.Configure(pageCount);
        }

        /// <summary>
        /// Rebuilds the bottom button row. "Sell all crops" (every storage at once) is offered only in
        /// the aggregate view; "Move all crops" (when another storage exists) and "Sell these crops"
        /// act on the storage currently shown, and appear only while it holds crops.
        /// </summary>
        private void LayoutButtonRow()
        {
            _buttonRow.Clear();
            var storage = Core.Services.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services.GetService<BuildingService>();

            if (_filterBuildingId < 0 && AnyStorageHasCrops(storage, buildingService))
                _buttonRow.Add(_sellAllButton).Width(ButtonWidth).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadRight(8f);

            int shownId = CurrentPageBuildingId;
            bool hasCrops = storage != null && shownId >= 0 && storage.HasAvailableCrops(shownId);
            bool otherStorageExists = (buildingService?.CropStorageCount ?? 0) > 1;

            if (hasCrops && otherStorageExists)
                _buttonRow.Add(_moveAllButton).Width(ButtonWidth).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadRight(8f);
            if (hasCrops)
                _buttonRow.Add(_sellStorageButton).Width(ButtonWidth).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadRight(8f);

            _buttonRow.Add(_closeButton).Width(100f).SetMinHeight(GameConfig.DialogButtonMinHeight);
        }

        /// <summary>
        /// Crop Storage buildings in stable UniqueId order — the order pages are numbered in. Collapses
        /// to the single filtered storage in the per-building view.
        /// </summary>
        private System.Collections.Generic.List<PlacedBuilding> GetStorageBuildings(BuildingService buildingService)
        {
            var result = new System.Collections.Generic.List<PlacedBuilding>();
            if (buildingService == null)
                return result;

            var all = buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type != BuildingType.CropStorage)
                    continue;
                if (_filterBuildingId >= 0 && all[i].UniqueId != _filterBuildingId)
                    continue;
                result.Add(all[i]);
            }
            result.Sort((a, b) => a.UniqueId.CompareTo(b.UniqueId));
            return result;
        }

        /// <summary>
        /// UniqueId of the storage the per-storage actions apply to: the filtered building in the
        /// per-building view, or the current page's building in the aggregate view. -1 when no storage
        /// is being shown.
        /// </summary>
        private int CurrentPageBuildingId
        {
            get
            {
                if (_filterBuildingId >= 0)
                    return _filterBuildingId;

                var buildings = GetStorageBuildings(Core.Services?.GetService<BuildingService>());
                int page = _pagerRow?.PageIndex ?? 0;
                return (page >= 0 && page < buildings.Count) ? buildings[page].UniqueId : -1;
            }
        }

        /// <summary>True if any Crop Storage building has at least one available (non-held) harvested crop.</summary>
        private static bool AnyStorageHasCrops(CropStorageInventoryService storage, BuildingService buildingService)
        {
            if (storage == null || buildingService == null)
                return false;
            var all = buildingService.GetAll();
            for (int b = 0; b < all.Count; b++)
                if (all[b].Type == BuildingType.CropStorage && storage.HasAvailableCrops(all[b].UniqueId))
                    return true;
            return false;
        }

        /// <summary>Redistributes the shown storage's crops across the other storages (with confirmation).</summary>
        private void OnMoveAllStorageClicked()
        {
            int buildingId = CurrentPageBuildingId;
            if (buildingId < 0)
                return;

            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonMoveAllCrops),
                GetText(UITextKey.DialogMoveCropsPrompt), PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    Core.Services.GetService<CropStorageInventoryService>()
                        ?.MoveAllCropsToOtherStorages(buildingId);
                    _descWindow?.SetVisible(false);
                    RefreshViewer();
                });
            dialog.Show(_stage);
        }

        /// <summary>Sells every harvested crop in the shown storage (with confirmation).</summary>
        private void OnSellStorageClicked()
        {
            int buildingId = CurrentPageBuildingId;
            var storage = Core.Services.GetService<CropStorageInventoryService>();
            if (buildingId < 0 || storage == null)
                return;

            storage.CopyDisplaySlots(buildingId, _displaySlots);
            int gold = 0;
            for (int s = 0; s < _displaySlots.Length; s++)
                if (!_displaySlots[s].IsEmpty)
                    gold += CropConfig.GetHarvestStackSellPrice(_displaySlots[s].Type, _displaySlots[s].Count);

            int totalGold = gold;
            string prompt = string.Format(GetText(UITextKey.DialogSellStorageCropsPrompt), totalGold);
            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonSellTheseCrops), prompt,
                PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    // Sell first, then pay exactly what was realized — the quoted totalGold is a
                    // snapshot from before the dialog opened and may no longer be there.
                    int realized = SellAvailableInBuilding(storage, buildingId);
                    if (realized > 0)
                    {
                        Core.Services.GetService<GameStateService>()?.AddFunds(realized, "sell_crops");
                        Core.GetGlobalManager<SoundEffectManager>()?.PlaySound(SoundEffectType.ItemSell);
                    }
                    _descWindow?.SetVisible(false);
                    RefreshViewer();
                });
            dialog.YesButton.SuppressGlobalClick = true;
            dialog.Show(_stage);
        }

        /// <summary>
        /// Sells every AVAILABLE crop unit in the building — units held for transfer by a
        /// carrying runner stay in their slots. Re-reads live display counts (auto-sell may have
        /// emptied slots while a confirm dialog was open) and logs one crop_sold line per stack.
        ///
        /// Returns the gold actually realized. Callers MUST pay out this figure rather than a total
        /// priced before the dialog opened: the storage can empty under an open confirmation (auto-sell,
        /// or a second sell dialog stacked on the same storage), and paying the stale snapshot would
        /// mint gold for crops that no longer exist.
        /// </summary>
        private int SellAvailableInBuilding(CropStorageInventoryService storage, int buildingId)
        {
            storage.CopyDisplaySlots(buildingId, _displaySlots);
            int realized = 0;
            for (int s = 0; s < _displaySlots.Length; s++)
            {
                if (_displaySlots[s].IsEmpty)
                    continue;
                int sold = storage.TakeFromSlot(buildingId, s, _displaySlots[s].Count);
                if (sold > 0)
                {
                    int stackGold = CropConfig.GetHarvestStackSellPrice(_displaySlots[s].Type, sold);
                    realized += stackGold;
                    AnalyticsService.LogCropSold(_displaySlots[s].Type.ToString(), sold, stackGold, "manual");
                }
            }
            return realized;
        }

        /// <summary>Sells every harvested crop across all Crop Storage buildings (with confirmation).</summary>
        private void OnSellAllClicked()
        {
            var storage = Core.Services.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services.GetService<BuildingService>();
            if (storage == null || buildingService == null)
                return;

            var all = buildingService.GetAll();
            int gold = 0;
            for (int b = 0; b < all.Count; b++)
            {
                if (all[b].Type != BuildingType.CropStorage)
                    continue;
                storage.CopyDisplaySlots(all[b].UniqueId, _displaySlots);
                for (int s = 0; s < _displaySlots.Length; s++)
                    if (!_displaySlots[s].IsEmpty)
                        gold += CropConfig.GetHarvestStackSellPrice(_displaySlots[s].Type, _displaySlots[s].Count);
            }

            int totalGold = gold;
            string prompt = string.Format(GetText(UITextKey.DialogSellAllCropsPrompt), totalGold);
            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonSellAllCrops), prompt,
                PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    // Sell first, then pay exactly what was realized across every storage — the
                    // quoted totalGold is a pre-dialog snapshot and may no longer be there.
                    int realized = 0;
                    for (int b = 0; b < all.Count; b++)
                        if (all[b].Type == BuildingType.CropStorage)
                            realized += SellAvailableInBuilding(storage, all[b].UniqueId);
                    if (realized > 0)
                    {
                        Core.Services.GetService<GameStateService>()?.AddFunds(realized, "sell_crops");
                        Core.GetGlobalManager<SoundEffectManager>()?.PlaySound(SoundEffectType.ItemSell);
                    }
                    _descWindow?.SetVisible(false);
                    RefreshViewer();
                });
            dialog.YesButton.SuppressGlobalClick = true;
            dialog.Show(_stage);
        }

        /// <summary>Called when the player exits harvested-crops mode; hides the windows.</summary>
        public void OnExitHarvestedCropsMode()
        {
            _inventoryWindow?.SetVisible(false);
            _descWindow?.SetVisible(false);
            _filterBuildingId = -1;
            _pagerRow?.Reset();
        }

        /// <summary>
        /// Restricts the next viewer open to a single Crop Storage building (by UniqueId). Cleared
        /// automatically when the viewer exits. Set before entering harvested-crops mode.
        /// </summary>
        public void SetBuildingFilter(int buildingId)
        {
            _filterBuildingId = buildingId;
        }

        // ── Inventory window ──────────────────────────────────────────────────────

        private void CreateInventoryWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _inventoryWindow = new Window(GetText(UITextKey.WindowHarvestedCrops), skin, "ph-default");
            _inventoryWindow.SetMovable(false);
            _inventoryWindow.SetResizable(false);

            var outer = new Table();
            _outerTable = outer;
            outer.Pad(WinPad);

            _slotTable = new Table();
            var scroll = new ScrollPane(_slotTable, skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            _scrollCell = outer.Add(scroll).Width(SlotSize * Columns + 48f).Height(ScrollHeight);
            outer.Row();

            _pagerRow = new PagerRow(skin);
            _pagerRow.OnPageChanged += RefreshViewer;
            outer.Add(_pagerRow);
            outer.Row();

            _buttonRow = new Table();
            _sellAllButton = new TextButton(GetText(UITextKey.ButtonSellAllCrops), skin, "ph-default");
            _sellAllButton.OnClicked += (_) => OnSellAllClicked();
            _moveAllButton = new TextButton(GetText(UITextKey.ButtonMoveAllCrops), skin, "ph-default");
            _moveAllButton.OnClicked += (_) => OnMoveAllStorageClicked();
            _sellStorageButton = new TextButton(GetText(UITextKey.ButtonSellTheseCrops), skin, "ph-default");
            _sellStorageButton.OnClicked += (_) => OnSellStorageClicked();
            _closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            _closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            _closeButton.OnClicked += (_) => RequestExitHarvestedCropsMode?.Invoke();
            outer.Add(_buttonRow).SetPadTop(8f);

            _inventoryWindow.Add(outer).Expand().Fill();
            _inventoryWindow.SetVisible(false);
            _stage.AddElement(_inventoryWindow);
        }

        private void RebuildSlots()
        {
            _slotTable.Clear();

            var storage = Core.Services.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services.GetService<BuildingService>();
            if (storage == null || buildingService == null)
                return;

            // One storage per page, so the grid is always a single 8x4 block belonging to one building.
            int buildingId = CurrentPageBuildingId;
            if (buildingId < 0)
                return;

            storage.CopyDisplaySlots(buildingId, _displaySlots);
            var slots = _displaySlots;
            int col = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                var slot = slots[s];
                if (slot.IsEmpty)
                {
                    // Empty slot: render the inventory-slot background only.
                    var blank = new HarvestSlotButton(null, slot.Type, 0, null);
                    _slotTable.Add(blank).Size(SlotSize, SlotSize).Pad(2f);
                }
                else
                {
                    var sprite = _cropsAtlas.GetSprite(CropConfig.GetHarvestSpriteName(slot.Type));
                    var cell = new HarvestSlotButton(sprite, slot.Type, slot.Count,
                        GetHarvestName(slot.Type));
                    int capturedBuildingId = buildingId;
                    int capturedSlot = s;
                    var captured = slot.Type;
                    int capturedCount = slot.Count;
                    cell.OnClicked += () => ShowDescription(capturedBuildingId, capturedSlot, captured, capturedCount);
                    _slotTable.Add(cell).Size(SlotSize, SlotSize).Pad(2f);
                }

                col++;
                if (col % Columns == 0)
                    _slotTable.Row();
            }
        }

        private void ShowInventoryWindow()
        {
            _descWindow?.SetVisible(false);
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            // Shrink the slot grid when the configured design height cannot fit the full window, then
            // sit slightly above centre to clear the bottom UI bars (clamped to the stage).
            UILayout.FitScrollCellToStage(_inventoryWindow, _outerTable, _scrollCell, ScrollHeight, stageH, GameConfig.UIStageMargin);
            float w = _inventoryWindow.GetWidth();
            float h = _inventoryWindow.GetHeight();
            float x = (stageW - w) / 2f;
            float y = UILayout.CenterY(h, stageH, CenterYBias);
            if (x < 0f)
                x = 0f;

            _inventoryWindow.SetPosition(x, y);
            _inventoryWindow.SetVisible(true);
        }

        // ── Description window ────────────────────────────────────────────────────

        private void CreateDescriptionWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _descWindow = new Window("", skin, "ph-default");
            _descWindow.SetMovable(false);
            _descWindow.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            _descNameLabel = new Label("", skin, "ph-default");
            content.Add(_descNameLabel).SetPadBottom(6f);
            content.Row();

            _descDescLabel = new Label("", skin, "ph-default");
            _descDescLabel.SetWrap(true);
            content.Add(_descDescLabel).Width(200f).SetPadBottom(10f);
            content.Row();

            var sellButton = new TextButton(GetText(UITextKey.ButtonSell), skin, "ph-default");
            sellButton.OnClicked += (_) => OnSellStackClicked();
            content.Add(sellButton).Width(80f).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadBottom(4f);
            content.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => _descWindow.SetVisible(false);
            content.Add(closeButton).Width(80f).SetMinHeight(GameConfig.DialogButtonMinHeight);

            _descWindow.Add(content).Expand().Fill();
            _descWindow.Pack();
            _descWindow.SetVisible(false);
            _stage.AddElement(_descWindow);
        }

        private void ShowDescription(int buildingId, int slotIndex, CropType crop, int count)
        {
            _descBuildingId = buildingId;
            _descSlotIndex  = slotIndex;
            _descCropType   = crop;
            _descCount      = count;

            _descNameLabel.SetText(GetHarvestName(crop));
            _descDescLabel.SetText(GetText(CropConfig.GetDescriptionKey(crop)));
            _descWindow.Pack();
            float w = _descWindow.GetWidth();
            float h = _descWindow.GetHeight();
            _descWindow.SetPosition(
                (_stage.GetWidth()  - w) / 2f,
                (_stage.GetHeight() - h) / 2f);
            _descWindow.SetVisible(true);
            _descWindow.ToFront();
        }

        /// <summary>Sells the single stack currently shown in the description dialog (with confirmation).</summary>
        private void OnSellStackClicked()
        {
            int gold = CropConfig.GetHarvestStackSellPrice(_descCropType, _descCount);
            int buildingId = _descBuildingId;
            int slotIndex = _descSlotIndex;

            string prompt = string.Format(GetText(UITextKey.DialogSellCropStackPrompt),
                GetHarvestName(_descCropType), gold);
            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonSell), prompt,
                PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    var storage = Core.Services.GetService<CropStorageInventoryService>();
                    var gameState = Core.Services.GetService<GameStateService>();
                    // Re-read the DISPLAY slot: auto-sell may have emptied it while the dialog
                    // was open, and units held for transfer by a runner must not be sold.
                    HarvestSlot liveSlot = default;
                    if (storage != null)
                    {
                        storage.CopyDisplaySlots(buildingId, _displaySlots);
                        liveSlot = _displaySlots[slotIndex];
                    }
                    if (!liveSlot.IsEmpty && liveSlot.Type == _descCropType)
                    {
                        int sold = storage.TakeFromSlot(buildingId, slotIndex, liveSlot.Count);
                        if (sold > 0)
                        {
                            int liveGold = CropConfig.GetHarvestStackSellPrice(liveSlot.Type, sold);
                            gameState?.AddFunds(liveGold, "sell_crops");
                            Core.GetGlobalManager<SoundEffectManager>()?.PlaySound(SoundEffectType.ItemSell);
                            AnalyticsService.LogCropSold(liveSlot.Type.ToString(), sold, liveGold, "manual");
                        }
                    }
                    _descWindow.SetVisible(false);
                    RefreshViewer();
                });
            dialog.YesButton.SuppressGlobalClick = true;
            dialog.Show(_stage);
        }

    }
}
