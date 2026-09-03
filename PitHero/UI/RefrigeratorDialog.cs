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
    /// The Refrigerator window (issue #386), opened by clicking the kitchen fridge. Shows the
    /// fridge's single 8×4 page of crop stacks (same look as the crop storage viewer), a
    /// Pre-Stock Stack Size slider (1-4), and a Close button. Clicking an occupied stack opens a
    /// dialog with Send to Crop Storage / Sell / Close. The grid live-refreshes while runners and
    /// cooks mutate the fridge.
    /// </summary>
    public class RefrigeratorDialog
    {
        private const float SlotSize = HarvestSlotButton.DefaultSlotSize;
        private const float WinPad = 16f;
        private const int Columns = 8;
        private const float ScrollHeight = 224f;

        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;
        private readonly Skin _skin;

        private Window _window;
        private Table _slotTable;
        private Table _outerTable;
        private Cell _scrollCell;
        private HoverableLabel _preStockLabel;
        private EnhancedSlider _preStockSlider;
        private uint _shownFrame;
        private int _lastSeenVersion = -1;

        private Window _descWindow;
        private Label _descNameLabel;
        private Label _descDescLabel;
        private TextButton _sendButton;

        // The stack currently shown in the description dialog.
        private int _descSlotIndex;
        private CropType _descCropType;

        // Both windows belong to this UI: a click inside either must not dismiss it.
        private readonly System.Collections.Generic.List<Element> _dismissEnvelope =
            new System.Collections.Generic.List<Element>(2);

        private TextService _textService;

        public RefrigeratorDialog(Stage stage)
        {
            _stage = stage;
            _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            _skin = PitHeroSkin.CreateSkin();
            CreateWindow();
            CreateDescriptionWindow();
            _dismissEnvelope.Add(_window);
            _dismissEnvelope.Add(_descWindow);
        }

        /// <summary>Resolves a localized UI string, falling back to the key if the service is unavailable.</summary>
        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>Localized harvested-product name for a crop (e.g. Apple Tree → "Apple").</summary>
        private string GetHarvestName(CropType crop) => GetText(CropConfig.GetHarvestDisplayNameKey(crop));

        private void CreateWindow()
        {
            _window = new Window(GetText(UITextKey.WindowRefrigerator), _skin, "ph-default");
            _window.SetMovable(false);
            _window.SetResizable(false);

            var outer = new Table();
            _outerTable = outer;
            outer.Pad(WinPad);

            _slotTable = new Table();
            var scroll = new ScrollPane(_slotTable, _skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            _scrollCell = outer.Add(scroll).Width(SlotSize * Columns + 48f).Height(ScrollHeight);
            outer.Row();

            var sliderRow = new Table();
            _preStockLabel = new HoverableLabel(
                string.Format(GetText(UITextKey.FridgePreStockStackSize), 1),
                _skin, "ph-default", GetText(UITextKey.FridgePreStockStackSizeTooltip), _stage);
            sliderRow.Add(_preStockLabel).Left().SetPadRight(12f);

            _preStockSlider = new EnhancedSlider(GameConfig.KitchenPreStockStackSizeMin,
                GameConfig.KitchenPreStockStackSizeMax, 1, false, _skin, null, false);
            _preStockSlider.SetValueAndCommit(1);
            _preStockSlider.OnChanged += (value) =>
            {
                _preStockLabel.SetText(string.Format(GetText(UITextKey.FridgePreStockStackSize), (int)value));
            };
            _preStockSlider.OnValueCommitted += (value) =>
            {
                var svc = Core.Services?.GetService<FridgeInventoryService>();
                if (svc != null)
                    svc.PreStockStackSize = (int)value;
                // Raising the target means new deficits — queue the runner trips right away
                Core.Services?.GetService<KitchenTaskCoordinator>()?.RecomputePreStockDeficits();
            };
            sliderRow.Add(_preStockSlider).Width(160f).Left();

            outer.Add(sliderRow).SetPadTop(10f);
            outer.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), _skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Hide();
            outer.Add(closeButton).Width(100f).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadTop(8f);

            _window.Add(outer).Expand().Fill();
            _window.SetVisible(false);
            _stage.AddElement(_window);
        }

        private void RebuildSlots()
        {
            _slotTable.Clear();

            var fridge = Core.Services?.GetService<FridgeInventoryService>();
            if (fridge == null)
                return;
            _lastSeenVersion = fridge.Version;

            var slots = fridge.GetSlots();
            int col = 0;
            for (int s = 0; s < slots.Count; s++)
            {
                var slot = slots[s];
                if (slot.IsEmpty)
                {
                    var blank = new HarvestSlotButton(null, slot.Type, 0, null);
                    _slotTable.Add(blank).Size(SlotSize, SlotSize).Pad(2f);
                }
                else
                {
                    var sprite = _cropsAtlas.GetSprite(CropConfig.GetHarvestSpriteName(slot.Type));
                    var cell = new HarvestSlotButton(sprite, slot.Type, slot.Count,
                        GetHarvestName(slot.Type));
                    int capturedSlot = s;
                    var capturedCrop = slot.Type;
                    cell.OnClicked += () => ShowDescription(capturedSlot, capturedCrop);
                    _slotTable.Add(cell).Size(SlotSize, SlotSize).Pad(2f);
                }

                col++;
                if (col % Columns == 0)
                    _slotTable.Row();
            }
        }

        /// <summary>Syncs the slider from the service, rebuilds the grid and shows the window centered.</summary>
        public void Show()
        {
            var svc = Core.Services?.GetService<FridgeInventoryService>();
            if (svc != null)
            {
                _preStockSlider.SetValueAndCommit(svc.PreStockStackSize);
                _preStockLabel.SetText(string.Format(GetText(UITextKey.FridgePreStockStackSize), svc.PreStockStackSize));
            }
            RebuildSlots();
            // Shrink the slot grid when the configured design height cannot fit the full window
            float stageH = _stage.GetHeight();
            UILayout.FitScrollCellToStage(_window, _outerTable, _scrollCell, ScrollHeight, stageH, GameConfig.UIStageMargin);
            _window.SetPosition(
                (_stage.GetWidth() - _window.GetWidth()) / 2f,
                UILayout.CenterY(_window.GetHeight(), stageH, 0f));
            _window.SetVisible(true);
            _window.ToFront();
            _shownFrame = Time.FrameCount;
        }

        /// <summary>Hides the window and its stack dialog.</summary>
        public void Hide()
        {
            _window?.SetVisible(false);
            _descWindow?.SetVisible(false);
        }

        /// <summary>True while the refrigerator window is visible.</summary>
        public bool IsVisible() => _window != null && _window.IsVisible();

        /// <summary>
        /// Per-frame poll: dismisses on outside clicks (guarded so a confirmation dialog's clicks
        /// don't close the fridge under it) and live-refreshes the grid when runners or cooks
        /// change the fridge while the window is open.
        /// </summary>
        public void Update()
        {
            if (!IsVisible())
                return;

            if (!ConfirmationDialog.AnyVisible
                && OutsideClickDismissal.ShouldDismiss(_dismissEnvelope, _stage, _shownFrame))
            {
                Hide();
                return;
            }

            var fridge = Core.Services?.GetService<FridgeInventoryService>();
            if (fridge != null && fridge.Version != _lastSeenVersion)
            {
                RebuildSlots();
                if (_descWindow.IsVisible())
                    RefreshDescriptionButtons();
            }
        }

        // ── Stack dialog (Send to Crop Storage / Sell / Close) ──────────────────

        private void CreateDescriptionWindow()
        {
            _descWindow = new Window("", _skin, "ph-default");
            _descWindow.SetMovable(false);
            _descWindow.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            _descNameLabel = new Label("", _skin, "ph-default");
            content.Add(_descNameLabel).SetPadBottom(6f);
            content.Row();

            _descDescLabel = new Label("", _skin, "ph-default");
            _descDescLabel.SetWrap(true);
            content.Add(_descDescLabel).Width(200f).SetPadBottom(10f);
            content.Row();

            _sendButton = new TextButton(GetText(UITextKey.ButtonSendToCropStorage), _skin, "ph-default");
            _sendButton.OnClicked += (_) => OnSendToStorageClicked();
            content.Add(_sendButton).Width(150f).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadBottom(4f);
            content.Row();

            var sellButton = new TextButton(GetText(UITextKey.ButtonSell), _skin, "ph-default");
            sellButton.OnClicked += (_) => OnSellStackClicked();
            content.Add(sellButton).Width(150f).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadBottom(4f);
            content.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), _skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => _descWindow.SetVisible(false);
            content.Add(closeButton).Width(150f).SetMinHeight(GameConfig.DialogButtonMinHeight);

            _descWindow.Add(content).Expand().Fill();
            _descWindow.Pack();
            _descWindow.SetVisible(false);
            _stage.AddElement(_descWindow);
        }

        private void ShowDescription(int slotIndex, CropType crop)
        {
            _descSlotIndex = slotIndex;
            _descCropType = crop;

            _descNameLabel.SetText(GetHarvestName(crop));
            _descDescLabel.SetText(GetText(CropConfig.GetDescriptionKey(crop)));
            RefreshDescriptionButtons();

            _descWindow.Pack();
            _descWindow.SetPosition(
                (_stage.GetWidth() - _descWindow.GetWidth()) / 2f,
                UILayout.CenterY(_descWindow.GetHeight(), _stage.GetHeight(), 0f));
            _descWindow.SetVisible(true);
            _descWindow.ToFront();
        }

        /// <summary>
        /// Grays "Send to Crop Storage" out while no crop storage can accept the shown crop
        /// (all full, or none built).
        /// </summary>
        private void RefreshDescriptionButtons()
        {
            SettingsUI.SetButtonActive(_sendButton, AnyStorageHasRoom(_descCropType), _skin);
        }

        private static bool AnyStorageHasRoom(CropType crop)
        {
            var storage = Core.Services?.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services?.GetService<BuildingService>();
            if (storage == null || buildingService == null)
                return false;
            var all = buildingService.GetAll();
            for (int b = 0; b < all.Count; b++)
                if (all[b].Type == BuildingType.CropStorage && storage.HasCapacityFor(all[b].UniqueId, crop))
                    return true;
            return false;
        }

        /// <summary>
        /// Immediately moves the shown stack into the next crop storage with room (no runner walk,
        /// no confirmation — the issue calls for an instant transfer).
        /// </summary>
        private void OnSendToStorageClicked()
        {
            // Lands on a deterministic tick via the command queue (replay system)
            Services.Replay.PlayerCommandService.Dispatch(new Services.Replay.PlayerCommand(
                Services.Replay.PlayerCommandType.FridgeReturnSlot, _descSlotIndex, (int)_descCropType));
            _descWindow.SetVisible(false);
        }

        /// <summary>Moves the given fridge slot's stack into crop storage if it still holds that crop. Command handler entry point.</summary>
        public void ApplyReturnSlot(int slotIndex, CropType expectedType)
        {
            var fridge = Core.Services?.GetService<FridgeInventoryService>();
            var storage = Core.Services?.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services?.GetService<BuildingService>();
            if (fridge == null || storage == null || buildingService == null)
                return;
            var slots = fridge.GetSlots();
            if (slotIndex < 0 || slotIndex >= slots.Count)
                return;

            // Re-read the live slot: runners/cooks may have changed it while the dialog was open
            var liveSlot = slots[slotIndex];
            if (liveSlot.IsEmpty || liveSlot.Type != expectedType)
            {
                RebuildSlots();
                return;
            }

            int remaining = liveSlot.Count;
            var all = buildingService.GetAll();
            for (int b = 0; b < all.Count && remaining > 0; b++)
            {
                if (all[b].Type != BuildingType.CropStorage)
                    continue;
                remaining -= storage.DepositReturningStored(all[b].UniqueId, liveSlot.Type, remaining);
            }

            int moved = liveSlot.Count - remaining;
            if (moved > 0)
            {
                // Take the moved units out of this slot (whole stack when everything fit)
                fridge.ClearSlot(slotIndex);
                if (remaining > 0)
                    fridge.Deposit(liveSlot.Type, remaining);
                Core.GetGlobalManager<SoundEffectManager>()?.PlaySound(SoundEffectType.StoreCrop);
                AnalyticsService.LogCropFridgeReturned(liveSlot.Type.ToString(), moved);
            }

            RebuildSlots();
        }

        /// <summary>Sells the given fridge slot's stack if it still holds that crop. Command handler entry point.</summary>
        public void ApplySellSlot(int slotIndex, CropType expectedType)
        {
            var liveFridge = Core.Services?.GetService<FridgeInventoryService>();
            var gameState = Core.Services?.GetService<GameStateService>();
            if (liveFridge == null)
                return;
            var slots = liveFridge.GetSlots();
            if (slotIndex < 0 || slotIndex >= slots.Count)
                return;
            // Re-read the slot: the kitchen may have consumed it while the dialog was open.
            var liveSlot = slots[slotIndex];
            if (!liveSlot.IsEmpty && liveSlot.Type == expectedType)
            {
                int liveGold = CropConfig.GetHarvestStackSellPrice(liveSlot.Type, liveSlot.Count);
                gameState?.AddFunds(liveGold, "sell_crops");
                Core.GetGlobalManager<SoundEffectManager>()?.PlaySound(SoundEffectType.ItemSell);
                AnalyticsService.LogCropSold(liveSlot.Type.ToString(), liveSlot.Count, liveGold, "manual");
                liveFridge.ClearSlot(slotIndex);
            }
            RebuildSlots();
        }

        /// <summary>Sells the stack currently shown in the dialog (with confirmation), like crop storage.</summary>
        private void OnSellStackClicked()
        {
            var fridge = Core.Services?.GetService<FridgeInventoryService>();
            if (fridge == null)
                return;
            var shownSlot = fridge.GetSlots()[_descSlotIndex];
            if (shownSlot.IsEmpty || shownSlot.Type != _descCropType)
                return;

            int gold = CropConfig.GetHarvestStackSellPrice(shownSlot.Type, shownSlot.Count);
            int slotIndex = _descSlotIndex;

            string prompt = string.Format(GetText(UITextKey.DialogSellCropStackPrompt),
                GetHarvestName(_descCropType), gold);
            var dialog = new ConfirmationDialog(GetText(UITextKey.ButtonSell), prompt,
                PitHeroSkin.CreateSkin(),
                onYes: () =>
                {
                    // Lands on a deterministic tick via the command queue; ApplySellSlot re-reads the slot (replay system)
                    Services.Replay.PlayerCommandService.Dispatch(new Services.Replay.PlayerCommand(
                        Services.Replay.PlayerCommandType.FridgeSellSlot, slotIndex, (int)_descCropType));
                    _descWindow.SetVisible(false);
                });
            dialog.YesButton.SuppressGlobalClick = true;
            dialog.Show(_stage);
        }
    }
}
