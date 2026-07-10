using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

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

        private Window _descWindow;
        private Label _descNameLabel;
        private Label _descDescLabel;

        // The stack currently shown in the description dialog (for the Sell action).
        private int _descBuildingId;
        private int _descSlotIndex;
        private CropType _descCropType;
        private int _descCount;

        // Bottom-row buttons. "Sell all" (aggregate) shows only in the all-storages view; "Move all"
        // and "Sell all" (this storage) show only in the per-storage view.
        private TextButton _sellAllButton;      // aggregate — sells across every storage
        private TextButton _moveAllButton;      // per-storage — redistribute to other storages
        private TextButton _sellStorageButton;  // per-storage — sell this storage's crops
        private TextButton _closeButton;
        private Table _buttonRow;

        private const float SlotSize = 40f;
        private const float WinPad   = 16f;
        private const int   Columns  = 8;

        // When >= 0, only this Crop Storage building's slots are shown (UniqueId).
        private int _filterBuildingId = -1;

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
            LayoutButtonRow();
            RebuildSlots();
            ShowInventoryWindow();
        }

        /// <summary>
        /// Rebuilds the bottom button row. The all-storages (aggregate) view offers "Sell all crops"
        /// across every storage; the per-storage view offers "Move all crops" (when another storage
        /// exists) and "Sell all crops" for that one storage — both only while it holds crops.
        /// </summary>
        private void LayoutButtonRow()
        {
            _buttonRow.Clear();
            var storage = Core.Services.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services.GetService<BuildingService>();
            if (_filterBuildingId < 0)
            {
                // Aggregate view: offer "Sell all" only when at least one storage holds crops.
                if (AnyStorageHasCrops(storage, buildingService))
                    _buttonRow.Add(_sellAllButton).Width(120f).SetPadRight(8f);
            }
            else
            {
                bool hasCrops = storage != null && !storage.IsEmpty(_filterBuildingId);
                bool otherStorageExists = (buildingService?.CropStorageCount ?? 0) > 1;

                if (hasCrops && otherStorageExists)
                    _buttonRow.Add(_moveAllButton).Width(120f).SetPadRight(8f);
                if (hasCrops)
                    _buttonRow.Add(_sellStorageButton).Width(120f).SetPadRight(8f);
            }
            _buttonRow.Add(_closeButton).Width(100f);
        }

        /// <summary>True if any Crop Storage building currently holds at least one harvested crop.</summary>
        private static bool AnyStorageHasCrops(CropStorageInventoryService storage, BuildingService buildingService)
        {
            if (storage == null || buildingService == null)
                return false;
            var all = buildingService.GetAll();
            for (int b = 0; b < all.Count; b++)
                if (all[b].Type == BuildingType.CropStorage && !storage.IsEmpty(all[b].UniqueId))
                    return true;
            return false;
        }

        /// <summary>Redistributes this storage's crops across the other storages (with confirmation).</summary>
        private void OnMoveAllStorageClicked()
        {
            int buildingId = _filterBuildingId;
            if (buildingId < 0)
                return;

            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonMoveAllCrops),
                GetText(UITextKey.DialogMoveCropsPrompt), PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    Core.Services.GetService<CropStorageInventoryService>()
                        ?.MoveAllCropsToOtherStorages(buildingId);
                    _descWindow?.SetVisible(false);
                    LayoutButtonRow();
                    RebuildSlots();
                    ShowInventoryWindow();
                });
            dialog.Show(_stage);
        }

        /// <summary>Sells every harvested crop in this one storage (with confirmation).</summary>
        private void OnSellStorageClicked()
        {
            int buildingId = _filterBuildingId;
            var storage = Core.Services.GetService<CropStorageInventoryService>();
            if (buildingId < 0 || storage == null)
                return;

            var slots = storage.GetSlots(buildingId);
            int gold = 0;
            for (int s = 0; s < slots.Count; s++)
                if (!slots[s].IsEmpty)
                    gold += CropConfig.GetHarvestStackSellPrice(slots[s].Type, slots[s].Count);

            int totalGold = gold;
            string prompt = string.Format(GetText(UITextKey.DialogSellStorageCropsPrompt), totalGold);
            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonSellAllCrops), prompt,
                PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    var gameState = Core.Services.GetService<GameStateService>();
                    gameState?.AddFunds(totalGold, "sell_crops");
                    storage.ClearBuilding(buildingId);
                    _descWindow?.SetVisible(false);
                    LayoutButtonRow();
                    RebuildSlots();
                    ShowInventoryWindow();
                });
            dialog.Show(_stage);
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
                var slots = storage.GetSlots(all[b].UniqueId);
                for (int s = 0; s < slots.Count; s++)
                    if (!slots[s].IsEmpty)
                        gold += CropConfig.GetHarvestStackSellPrice(slots[s].Type, slots[s].Count);
            }

            int totalGold = gold;
            string prompt = string.Format(GetText(UITextKey.DialogSellAllCropsPrompt), totalGold);
            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonSellAllCrops), prompt,
                PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    var gameState = Core.Services.GetService<GameStateService>();
                    gameState?.AddFunds(totalGold, "sell_crops");
                    for (int b = 0; b < all.Count; b++)
                        if (all[b].Type == BuildingType.CropStorage)
                            storage.ClearBuilding(all[b].UniqueId);
                    _descWindow?.SetVisible(false);
                    LayoutButtonRow();
                    RebuildSlots();
                    ShowInventoryWindow();
                });
            dialog.Show(_stage);
        }

        /// <summary>Called when the player exits harvested-crops mode; hides the windows.</summary>
        public void OnExitHarvestedCropsMode()
        {
            _inventoryWindow?.SetVisible(false);
            _descWindow?.SetVisible(false);
            _filterBuildingId = -1;
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
            outer.Pad(WinPad);

            _slotTable = new Table();
            var scroll = new ScrollPane(_slotTable, skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            outer.Add(scroll).Width(SlotSize * Columns + 48f).Height(240f);
            outer.Row();

            _buttonRow = new Table();
            _sellAllButton = new TextButton(GetText(UITextKey.ButtonSellAllCrops), skin, "ph-default");
            _sellAllButton.OnClicked += (_) => OnSellAllClicked();
            _moveAllButton = new TextButton(GetText(UITextKey.ButtonMoveAllCrops), skin, "ph-default");
            _moveAllButton.OnClicked += (_) => OnMoveAllStorageClicked();
            _sellStorageButton = new TextButton(GetText(UITextKey.ButtonSellAllCrops), skin, "ph-default");
            _sellStorageButton.OnClicked += (_) => OnSellStorageClicked();
            _closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
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

            // Crop Storage buildings in stable UniqueId order so the grid layout never reshuffles.
            var all = buildingService.GetAll();
            var storageBuildings = new System.Collections.Generic.List<PlacedBuilding>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Type != BuildingType.CropStorage)
                    continue;
                if (_filterBuildingId >= 0 && all[i].UniqueId != _filterBuildingId)
                    continue;
                storageBuildings.Add(all[i]);
            }
            storageBuildings.Sort((a, b) => a.UniqueId.CompareTo(b.UniqueId));

            int col = 0;
            for (int b = 0; b < storageBuildings.Count; b++)
            {
                var slots = storage.GetSlots(storageBuildings[b].UniqueId);
                for (int s = 0; s < slots.Count; s++)
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
                        int capturedBuildingId = storageBuildings[b].UniqueId;
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
        }

        private void ShowInventoryWindow()
        {
            _descWindow?.SetVisible(false);
            _inventoryWindow.Pack();
            float w = _inventoryWindow.GetWidth();
            float h = _inventoryWindow.GetHeight();
            _inventoryWindow.SetPosition(
                (_stage.GetWidth()  - w) / 2f,
                (_stage.GetHeight() - h) / 2f - 30f);
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
            content.Add(sellButton).Width(80f).SetPadBottom(4f);
            content.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.OnClicked += (_) => _descWindow.SetVisible(false);
            content.Add(closeButton).Width(80f);

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
                    gameState?.AddFunds(gold, "sell_crops");
                    storage?.ClearSlot(buildingId, slotIndex);
                    _descWindow.SetVisible(false);
                    LayoutButtonRow();
                    RebuildSlots();
                    ShowInventoryWindow();
                });
            dialog.Show(_stage);
        }

        // ── Slot element ──────────────────────────────────────────────────────────

        private class HarvestSlotButton : Element, IInputListener
        {
            // Inventory-slot background drawn at the same translucency as the inventory UI.
            private static readonly Color SlotBgColor = new Color(255, 255, 255, 100);

            private readonly SpriteDrawable _draw;
            private readonly int _count;
            private readonly string _tooltipText;
            private SpriteDrawable _background;
            private Sprite _selectBox;
            private bool _hovered;

            public event System.Action OnClicked;

            public HarvestSlotButton(Sprite sprite, CropType crop, int count, string tooltipText)
            {
                _draw        = sprite != null ? new SpriteDrawable(sprite) : null;
                _count       = count;
                _tooltipText = tooltipText;
                // Empty slots show the background only — no hover/click.
                SetTouchable(sprite != null ? Touchable.Enabled : Touchable.Disabled);
                SetSize(SlotSize, SlotSize);

                if (Core.Content != null)
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var bgSprite   = itemsAtlas?.GetSprite("Inventory");
                    if (bgSprite != null)
                        _background = new SpriteDrawable(bgSprite);

                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    _selectBox  = uiAtlas?.GetSprite("SelectBox");
                }
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                _background?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), SlotBgColor);

                _draw?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                if (_hovered && _selectBox != null)
                    new SpriteDrawable(_selectBox).Draw(
                        batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                var font = Nez.Graphics.Instance?.BitmapFont;
                if (font != null && _count > 1)
                {
                    string countStr = _count.ToString();
                    float tw = font.MeasureString(countStr).X;
                    StackCountText.Draw(batcher, font, countStr,
                        new Vector2(GetX() + GetWidth() - tw - 2f, GetY() + GetHeight() - font.LineHeight - 1f),
                        Color.White);
                }
            }

            void IInputListener.OnMouseEnter()
            {
                _hovered = true;
                if (!string.IsNullOrEmpty(_tooltipText))
                {
                    var stage = GetStage();
                    var mp = stage != null ? stage.GetMousePosition() : new Vector2(GetX(), GetY());
                    HoverTextManager.ShowHoverText(_tooltipText, mp.X + 12f, mp.Y - 4f);
                }
            }

            void IInputListener.OnMouseExit()
            {
                _hovered = false;
                HoverTextManager.HideHoverText();
            }

            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) => OnClicked?.Invoke();
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
