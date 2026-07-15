using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>UI shell for the Second Chance Shop - provides a button, a vault window, and a separate hero panel for purchasing items and crystals.</summary>
    public class SecondChanceShopUI
    {
        private Stage _stage;
        private HoverableImageButton _shopButton;

        // Left panel: shop window with vault grid tabs
        private Window _shopWindow;
        private TabPane _tabPane;
        private Tab _itemsTab;
        private Tab _crystalsTab;
        private Tab _seedsTab;

        // Right panel: separate windows for hero inventory and crystal panel
        private Window _heroInventoryWindow;
        private Window _heroCrystalWindow;

        // Merchant sprite shown between the two panels
        private Image _merchantSprite;

        private bool _windowVisible = false;
        private ImageButtonStyle _shopNormalStyle;
        private ImageButtonStyle _shopHalfStyle;
        private bool _styleChanged = false;
        private bool _isHiddenForPromotion = false;
        private SettingsUI _settingsUI;
        private HeroUI _heroUI;
        private MonsterUI _monsterUI;
        private FarmUI _farmUI;
        private Skin _skin;
        private TextService _textService;
        private PauseService _pauseService;

        // Items tab components
        private VaultItemGrid _vaultItemGrid;
        private InventoryGrid _heroInventoryGrid;
        private ItemCardTooltip _heroInventoryTooltip;

        // Crystals tab components
        private VaultCrystalGrid _vaultCrystalGrid;
        private SecondChanceHeroCrystalPanel _heroCrystalPanel;
        private HeroCrystalCard _vaultCrystalCard;
        private VaultCrystalCardDismissLayer _vaultCrystalCardDismissLayer;

        // Which tab is currently active (0=Items, 1=Crystals)
        private int _activeTabIndex = 0;
        private int _hoverCheckFrame;

        private enum ShopMode { Normal, Half }
        private ShopMode _currentShopMode = ShopMode.Normal;

        /// <summary>Whether the shop window is currently visible.</summary>
        public bool IsWindowVisible => _windowVisible;

        /// <summary>Initializes the shop UI and adds the button to the stage.</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            _skin = PitHeroSkin.CreateSkin();

            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            var sprite      = uiAtlas.GetSprite("UISecondChance");
            var sprite2x    = uiAtlas.GetSprite("UISecondChance2x");
            var highlight   = uiAtlas.GetSprite("UISecondChanceHighlight");
            var highlight2x = uiAtlas.GetSprite("UISecondChanceHighlight2x");
            var inverse     = uiAtlas.GetSprite("UISecondChanceInverse");
            var inverse2x   = uiAtlas.GetSprite("UISecondChanceInverse2x");

            _shopNormalStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };
            _shopHalfStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite2x),
                ImageDown = new SpriteDrawable(inverse2x),
                ImageOver = new SpriteDrawable(highlight2x)
            };

            _shopButton = new HoverableImageButton(_shopNormalStyle, GetText(TextType.UI, UITextKey.WindowSecondChanceShop));
            _shopButton.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _shopButton.OnClicked += (button) => TriggerToggle();

            // Load merchant sprite - 256x256 px; displayed between shop and hero panels
            var merchantSprite = uiAtlas.GetSprite("SecondChanceMerchant");
            _merchantSprite = new Image(new SpriteDrawable(merchantSprite));
            // Display at natural sprite dimensions so the full character is visible
            _merchantSprite.SetSize(merchantSprite.SourceRect.Width, merchantSprite.SourceRect.Height);

            CreateShopWindow(_skin);
            CreateHeroInventoryWindow(_skin);
            CreateHeroCrystalWindow(_skin);

            _pauseService = Core.Services?.GetService<PauseService>();
            _stage.AddElement(_shopButton);
        }

        /// <summary>Safely retrieves TextService, returns null if Core is not initialized.</summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService;
        }

        /// <summary>Gets localized text or falls back to key name if TextService unavailable.</summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Window creation
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Creates the shop window containing only the vault grid tabs.</summary>
        private void CreateShopWindow(Skin skin)
        {
            _shopWindow = new Window(GetText(TextType.UI, UITextKey.WindowSecondChanceShop), skin);
            _shopWindow.SetSize(GameConfig.SecondChanceShopWindowWidth, GameConfig.SecondChanceShopWindowHeight);
            _shopWindow.Pad(0); // tabs flush with window edges

            var tabWindowStyle = skin.Get<TabWindowStyle>("ph-default");
            _tabPane = new TabPane(tabWindowStyle);

            var tabStyle = new TabStyle { Background = null };
            _itemsTab    = new Tab(GetText(TextType.UI, UITextKey.TabItems),    tabStyle);
            _crystalsTab = new Tab(GetText(TextType.UI, UITextKey.TabCrystals), tabStyle);
            _seedsTab    = new Tab(GetText(TextType.UI, UITextKey.TabSeeds),    tabStyle);

            PopulateItemsTab(_itemsTab, skin);
            PopulateCrystalsTab(_crystalsTab, skin);
            PopulateSeedsTab(_seedsTab, skin);

            _tabPane.AddTab(_itemsTab);
            _tabPane.AddTab(_crystalsTab);
            _tabPane.AddTab(_seedsTab);

            // Wire tab button clicks to swap the right-side hero panel
            for (int i = 0; i < _tabPane.TabButtons.Count; i++)
            {
                int tabIndex = i;
                _tabPane.TabButtons[i].OnClick += () => HandleTabChanged(tabIndex);
            }

            _shopWindow.Add(_tabPane).Expand().Fill().Pad(0);
            _shopWindow.SetVisible(false);
        }

        /// <summary>Creates the hero inventory window (right panel for the Items tab).</summary>
        private void CreateHeroInventoryWindow(Skin skin)
        {
            _heroInventoryWindow = new Window("", skin);
            _heroInventoryWindow.Pad(0);
            _heroInventoryWindow.SetSize(GameConfig.SecondChanceHeroPanelWidth, GameConfig.SecondChanceHeroPanelHeight);

            _heroInventoryGrid = new InventoryGrid();
            _heroInventoryGrid.InitializeContextMenu(_stage, skin);
            _heroInventoryGrid.OnVaultItemDropRequested += HandleVaultItemDrop;
            _heroInventoryGrid.OnItemSoldToVault += HandleHeroInventorySell;
            _heroInventoryGrid.OnHeroItemDroppedOutside += HandleHeroItemDroppedOutside;

            var dummyTarget = new Element();
            dummyTarget.SetSize(0, 0);
            _heroInventoryTooltip = new ItemCardTooltip(dummyTarget, skin);
            _heroInventoryGrid.OnItemHovered  += HandleHeroInventoryItemHovered;
            _heroInventoryGrid.OnItemUnhovered += HandleHeroInventoryItemUnhovered;
            var heroComponent = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComponent != null)
            {
                _heroInventoryGrid.ConnectToHero(heroComponent);
                RefreshMercenaryEquipSlots();
            }

            var scrollPane = new ScrollPane(_heroInventoryGrid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);

            // Use Expand().Fill() to match HeroUI's PopulateInventoryTab layout so the
            // ScrollPane gets an explicit height (without Fill, InventoryGrid reports 0
            // preferred height and the ScrollPane collapses to nothing)
            var content = new Table();
            content.Add(scrollPane).Width(700f).Expand().Fill().Pad(0f);

            _heroInventoryWindow.Add(content).Expand().Fill();
            _heroInventoryWindow.SetVisible(false);
        }

        /// <summary>Creates the hero crystal window (right panel for the Crystals tab).</summary>
        private void CreateHeroCrystalWindow(Skin skin)
        {
            _heroCrystalWindow = new Window("", skin);
            _heroCrystalWindow.Pad(0);
            _heroCrystalWindow.SetSize(GameConfig.SecondChanceHeroPanelWidth, GameConfig.SecondChanceHeroPanelHeight);

            _heroCrystalPanel = new SecondChanceHeroCrystalPanel();
            _heroCrystalPanel.OnVaultCrystalDropRequested += HandleVaultCrystalDrop;
            _heroCrystalPanel.OnCrystalSlotClicked += HandleHeroCrystalSlotClicked;

            var heroPanel = _heroCrystalPanel.CreateContent(skin, _stage);

            var content = new Table();
            content.Add(heroPanel).Top().Left().Pad(4f);

            _heroCrystalWindow.Add(content).Expand().Fill();
            _heroCrystalWindow.SetVisible(false);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Tab population (vault grids only — no hero panel content here)
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Populates the Items tab with the vault item grid.</summary>
        private void PopulateItemsTab(Tab tab, Skin skin)
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();

            _vaultItemGrid = new VaultItemGrid();
            _vaultItemGrid.InitializeTooltip(_stage, skin);
            if (vault != null)
                _vaultItemGrid.RefreshFromVault(vault);

            // Cancel drag when it lands outside the hero inventory grid
            _vaultItemGrid.OnVaultSlotDragDropped += HandleVaultDragDropped;

            var scrollPane = new ScrollPane(_vaultItemGrid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            var content = new Table();
            content.Top().Left().Pad(8f);
            content.Add(scrollPane).Size(297f, 198f).Top().Left();

            tab.ClearChildren();
            tab.Add(content).Expand().Fill();
        }

        /// <summary>Populates the Crystals tab with the vault crystal grid.</summary>
        private void PopulateCrystalsTab(Tab tab, Skin skin)
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();

            _vaultCrystalGrid = new VaultCrystalGrid();
            _vaultCrystalGrid.InitializeTooltip(_stage, skin);
            if (vault != null)
                _vaultCrystalGrid.RefreshFromVault(vault);

            // Cancel drag when it lands outside the hero crystal panel
            _vaultCrystalGrid.OnVaultCrystalDragDropped += HandleVaultCrystalDragDropped;
            _vaultCrystalGrid.OnVaultCrystalSlotClicked += HandleVaultCrystalSlotClicked;

            // Create the crystal info card (same as HeroUI CrystalsTab)
            if (_vaultCrystalCard == null)
            {
                _vaultCrystalCard = new HeroCrystalCard(skin, _stage);
                _stage.AddElement(_vaultCrystalCard);
            }

            var scrollPane = new ScrollPane(_vaultCrystalGrid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            var content = new Table();
            content.Top().Left().Pad(8f);
            content.Add(scrollPane).Size(297f, 198f).Top().Left();

            tab.ClearChildren();
            tab.Add(content).Expand().Fill();
        }

        /// <summary>Populates the Seeds tab with a 4-per-row grid of purchasable crop seed slots.</summary>
        private void PopulateSeedsTab(Tab tab, Skin skin)
        {
            var cropsAtlas          = Core.Content?.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var cropPlantingService = Core.Services?.GetService<CropPlantingService>();
            var cropGrowthService   = Core.Services?.GetService<CropGrowthService>();

            var grid = new Table();
            grid.Top().Left().Pad(4f);

            int col = 0;
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                var crop = (CropType)i;
                string spriteName = CropConfig.GetFullyGrownSpriteName(crop);
                var sprite = cropsAtlas?.GetSprite(spriteName);

                string cropName = GetText(TextType.UI, CropConfig.GetDisplayNameKey(crop));
                int price       = CropConfig.GetSeedPrice(crop);
                string tooltip  = cropName + " - " + price + "G";

                var slot = new SeedShopSlot(sprite, crop, cropPlantingService, cropGrowthService, tooltip);
                slot.OnBuyClicked += HandleSeedBuyClicked;

                grid.Add(slot).Size(40f, 40f).Pad(2f);
                col++;
                if (col >= 4)
                {
                    grid.Row();
                    col = 0;
                }
            }

            var scrollPane = new ScrollPane(grid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            var content = new Table();
            content.Top().Left().Pad(8f);
            content.Add(scrollPane).Size(297f, 198f).Top().Left();

            tab.ClearChildren();
            tab.Add(content).Expand().Fill();
        }

        /// <summary>Opens the quantity dialog and executes a seed purchase when confirmed.</summary>
        private void HandleSeedBuyClicked(CropType crop)
        {
            var cropPlantingService = Core.Services?.GetService<CropPlantingService>();
            if (cropPlantingService == null) return;

            var gameState = Core.Services?.GetService<GameStateService>();
            if (gameState == null) return;

            int unitPrice = CropConfig.GetSeedPrice(crop);
            int ownedCount = cropPlantingService.SeedInventory != null
                ? cropPlantingService.SeedInventory[(int)crop]
                : 0;
            string shopTitle = GetText(TextType.UI, UITextKey.WindowSecondChanceShop);
            string cropName  = GetText(TextType.UI, CropConfig.GetDisplayNameKey(crop));

            // Outstanding planned demand not yet covered by owned seeds — drives the
            // "Need: N" row in the quantity dialog (omitted when zero).
            var cropGrowthService = Core.Services?.GetService<CropGrowthService>();
            int plannedDeficit = cropGrowthService != null
                ? cropPlantingService.CountUnplantedPlans(crop, cropGrowthService) - ownedCount
                : 0;
            if (plannedDeficit < 0) plannedDeficit = 0;

            var qtyDialog = new VaultBuyQuantityDialog(
                shopTitle,
                cropName,
                unitPrice,
                GameConfig.SeedShopMaxPurchaseQuantity,
                _skin,
                onConfirm: (qty) =>
                {
                    int totalPrice = unitPrice * qty;
                    if (gameState.Funds < totalPrice) return;
                    gameState.Funds -= totalPrice;
                    cropPlantingService.AddSeeds(crop, qty);
                    Core.Services?.GetService<FarmTaskCoordinator>()?.RescanForPlanting();
                },
                onCancel: null,
                ownedCount: ownedCount,
                availableFunds: gameState.Funds,
                plannedCount: plannedDeficit);
            qtyDialog.Show(_stage);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Tab switching
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Shows the correct right-side hero panel when the active tab changes.</summary>
        private void HandleTabChanged(int tabIndex)
        {
            _activeTabIndex = tabIndex;
            if (!_windowVisible) return;

            if (tabIndex == 0) // Items tab
            {
                _heroInventoryWindow?.SetVisible(true);
                _heroCrystalWindow?.SetVisible(false);
            }
            else if (tabIndex == 1) // Crystals tab
            {
                _heroInventoryWindow?.SetVisible(false);
                _heroCrystalWindow?.SetVisible(true);
            }
            else // Seeds tab — no hero-side panel
            {
                _heroInventoryWindow?.SetVisible(false);
                _heroCrystalWindow?.SetVisible(false);
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Show / hide
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Enforces single window policy then toggles the shop window.</summary>
        public void TriggerToggle()
        {
            _settingsUI?.ForceCloseSettings();
            _heroUI?.ForceCloseWindow();
            _monsterUI?.ForceCloseWindow();
            _farmUI?.DismissSubButtons();
            ToggleShopWindow();
        }

        private void ToggleShopWindow()
        {
            _windowVisible = !_windowVisible;
            if (_windowVisible)
            {
                UIWindowManager.OnUIWindowOpening();

                // Shop (left panel)
                _stage.AddElement(_shopWindow);
                _shopWindow.SetVisible(true);
                _shopWindow.SetPosition(GameConfig.SecondChanceShopWindowX, GameConfig.SecondChanceShopWindowY);
                _shopWindow.ToFront();

                // Merchant sprite (between panels)
                _stage.AddElement(_merchantSprite);
                _merchantSprite.SetPosition(GameConfig.SecondChanceMerchantSpriteX, GameConfig.SecondChanceMerchantSpriteY);
                _merchantSprite.ToFront();

                // Hero panel (right) — show the one matching the active tab
                _stage.AddElement(_heroInventoryWindow);
                _stage.AddElement(_heroCrystalWindow);
                _heroInventoryWindow.SetPosition(GameConfig.SecondChanceHeroPanelX, GameConfig.SecondChanceHeroPanelY);
                _heroCrystalWindow.SetPosition(GameConfig.SecondChanceHeroPanelX, GameConfig.SecondChanceHeroPanelY);

                if (_activeTabIndex == 0)
                {
                    _heroInventoryWindow.SetVisible(true);
                    _heroCrystalWindow.SetVisible(false);
                }
                else if (_activeTabIndex == 1)
                {
                    _heroInventoryWindow.SetVisible(false);
                    _heroCrystalWindow.SetVisible(true);
                }
                else // Seeds tab — no hero-side panel
                {
                    _heroInventoryWindow.SetVisible(false);
                    _heroCrystalWindow.SetVisible(false);
                }

                _heroInventoryWindow.ToFront();
                _heroCrystalWindow.ToFront();

                RefreshShopData();

                if (_pauseService != null)
                    _pauseService.IsPaused = true;
            }
            else
            {
                UIWindowManager.OnUIWindowClosing();
                _shopWindow.SetVisible(false);
                _shopWindow.Remove();
                _merchantSprite.Remove();
                _heroInventoryWindow.SetVisible(false);
                _heroInventoryWindow.Remove();
                _heroCrystalWindow.SetVisible(false);
                _heroCrystalWindow.Remove();
                _heroInventoryTooltip?.GetContainer().Remove();
                HideVaultCrystalCard();

                if (_pauseService != null)
                    _pauseService.IsPaused = false;
            }
        }

        /// <summary>Force-closes the shop window. Used by single window policy.</summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false;
                UIWindowManager.OnUIWindowClosing();
                _shopWindow?.SetVisible(false);
                _shopWindow?.Remove();
                _merchantSprite?.Remove();
                _heroInventoryWindow?.SetVisible(false);
                _heroInventoryWindow?.Remove();
                _heroCrystalWindow?.SetVisible(false);
                _heroCrystalWindow?.Remove();
                _heroInventoryTooltip?.GetContainer().Remove();
                HideVaultCrystalCard();

                if (_pauseService != null)
                    _pauseService.IsPaused = false;
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Positioning & style
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Sets the SettingsUI reference for single window policy.</summary>
        public void SetSettingsUI(SettingsUI settingsUI) { _settingsUI = settingsUI; }

        /// <summary>Sets the HeroUI reference for single window policy.</summary>
        public void SetHeroUI(HeroUI heroUI) { _heroUI = heroUI; }

        /// <summary>Sets the MonsterUI reference for single window policy.</summary>
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }
        public void SetFarmUI(FarmUI farmUI) { _farmUI = farmUI; }

        /// <summary>Sets the position of the shop icon button.</summary>
        public void SetPosition(float x, float y) => _shopButton?.SetPosition(x, y);

        /// <summary>Returns the width of the shop icon button.</summary>
        public float GetWidth() => _shopButton?.GetWidth() ?? 0f;

        /// <summary>Returns true once if the button style changed, then resets.</summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>Switches the shop button between normal and half-height styles based on window mode.</summary>
        public void UpdateButtonStyleIfNeeded()
        {
            ShopMode desired = WindowManager.IsHalfHeightMode() ? ShopMode.Half : ShopMode.Normal;
            if (desired == _currentShopMode) return;

            switch (desired)
            {
                case ShopMode.Normal:
                    _shopButton.SetStyle(_shopNormalStyle);
                    _shopButton.SetSize(
                        ((SpriteDrawable)_shopNormalStyle.ImageUp).Sprite.SourceRect.Width,
                        ((SpriteDrawable)_shopNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case ShopMode.Half:
                    _shopButton.SetStyle(_shopHalfStyle);
                    _shopButton.SetSize(
                        ((SpriteDrawable)_shopHalfStyle.ImageUp).Sprite.SourceRect.Width,
                        ((SpriteDrawable)_shopHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentShopMode = desired;
            _styleChanged = true;
        }

        /// <summary>Updates the shop UI each frame.</summary>
        public void Update()
        {
            UpdatePromotionVisibilityIfNeeded();
            UpdateButtonStyleIfNeeded();
            UpdateHeroInventoryTooltipPosition();

            if (_windowVisible && _stage != null)
            {
                var mousePos = _stage.GetMousePosition();
                if (_activeTabIndex == 0)
                {
                    _vaultItemGrid?.Update(mousePos);
                    PerformHeroInventoryPeriodicHoverCheck(mousePos);
                }
                else if (_activeTabIndex == 1)
                {
                    _vaultCrystalGrid?.Update(mousePos);
                    _heroCrystalPanel?.Update(mousePos);
                }
                // Seeds tab (index 2): no per-frame grid update needed
            }
        }

        /// <summary>Periodic safety-net hover check for the hero inventory tooltip in the shop.</summary>
        private void PerformHeroInventoryPeriodicHoverCheck(Vector2 mousePos)
        {
            _hoverCheckFrame++;
            if (_hoverCheckFrame % 5 != 0) return;
            if (_heroInventoryTooltip == null || _heroInventoryGrid == null) return;
            if (_heroInventoryTooltip.GetContainer().HasParent()) return;

            var slot = _heroInventoryGrid.GetSlotAtStagePosition(mousePos);
            if (slot != null && slot.SlotData.Item != null)
                HandleHeroInventoryItemHovered(slot.SlotData.Item, slot);
        }

        /// <summary>
        /// Hides/shows the shop button depending on whether the hero is in a post-death promotion state.
        /// Mirrors the same pattern used by StopAdventuringUI.
        /// </summary>
        private void UpdatePromotionVisibilityIfNeeded()
        {
            if (_shopButton == null || Core.Scene == null)
                return;

            var heroEntity = Core.Scene.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            bool shouldHide = heroComponent != null && heroComponent.NeedsCrystal;

            if (shouldHide == _isHiddenForPromotion)
                return;

            _isHiddenForPromotion = shouldHide;

            if (shouldHide)
            {
                // Auto-close the shop window when the hero dies
                if (_windowVisible)
                    ForceCloseWindow();

                _shopButton.SetVisible(false);
                _shopButton.SetTouchable(Touchable.Disabled);
            }
            else
            {
                _shopButton.SetVisible(true);
                _shopButton.SetTouchable(Touchable.Enabled);
            }
            _styleChanged = true; // Triggers SettingsUI layout reflow
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Hero inventory hover tooltip
        // ──────────────────────────────────────────────────────────────────────────

        private void HandleHeroInventoryItemHovered(IItem item, InventorySlot slot)
        {
            if (item == null || _heroInventoryTooltip == null) return;
            _heroInventoryTooltip.ShowItem(item, showBuyPrice: false);
            var container = _heroInventoryTooltip.GetContainer();
            if (container.GetParent() == null)
                _stage.AddElement(container);

            // Position at cursor immediately (same as HeroUI.HandleItemHovered)
            container.Validate();
            var mousePos = _stage.GetMousePosition();
            float stageH = _stage.GetHeight();
            float stageW = _stage.GetWidth();
            float tx = mousePos.X + 10f;
            float ty = mousePos.Y + 10f;
            if (ty + container.GetHeight() > stageH)
                ty = stageH - container.GetHeight();
            if (ty < 0) ty = 0;
            if (tx + container.GetWidth() > stageW)
                tx = mousePos.X - container.GetWidth() - 10f;
            container.SetPosition(tx, ty);
            container.ToFront();
        }

        private void HandleHeroInventoryItemUnhovered()
        {
            _heroInventoryTooltip?.GetContainer().Remove();
        }

        /// <summary>Keeps the hero inventory tooltip positioned near the cursor each frame.</summary>
        private void UpdateHeroInventoryTooltipPosition()
        {
            if (_heroInventoryTooltip == null) return;
            var container = _heroInventoryTooltip.GetContainer();
            if (container.GetParent() == null) return;

            var mousePos = _stage.GetMousePosition();
            container.Validate();
            float stageH = _stage.GetHeight();
            float stageW = _stage.GetWidth();
            float tx = mousePos.X + 10f;
            float ty = mousePos.Y + 10f;
            if (ty + container.GetHeight() > stageH)
                ty = stageH - container.GetHeight();
            if (ty < 0) ty = 0;
            if (tx + container.GetWidth() > stageW)
                tx = mousePos.X - container.GetWidth() - 10f;
            container.SetPosition(tx, ty);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Purchase handlers
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Called when a vault item is dropped onto a hero inventory or equipment slot.</summary>
        private void HandleVaultItemDrop(InventorySlot destSlot, SecondChanceMerchantVault.StackedItem vaultStack)
        {
            // Destination must be empty
            if (destSlot.SlotData.Item != null)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                return;
            }

            bool isEquipSlot = (destSlot.SlotData.SlotType == InventorySlotType.Equipment
                                || destSlot.SlotData.SlotType == InventorySlotType.MercenaryEquipment)
                               && destSlot.SlotData.EquipmentSlot.HasValue;

            // For equipment slots, validate item type and job-class compatibility
            if (isEquipSlot)
            {
                if (destSlot.SlotData.SlotType == InventorySlotType.MercenaryEquipment)
                {
                    var merc = destSlot.SlotData.MercenaryRef;
                    if (!CanEquipInSlot(vaultStack.ItemTemplate, destSlot.SlotData.EquipmentSlot.Value, merc))
                    {
                        InventoryDragManager.CancelDrag();
                        _vaultItemGrid?.ShowAllItemSprites();
                        return;
                    }
                }
                else
                {
                    var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
                    if (!CanEquipInSlot(vaultStack.ItemTemplate, destSlot.SlotData.EquipmentSlot.Value, heroComp))
                    {
                        InventoryDragManager.CancelDrag();
                        _vaultItemGrid?.ShowAllItemSprites();
                        return;
                    }
                }
            }

            var vault     = Core.Services?.GetService<SecondChanceMerchantVault>();
            var gameState = Core.Services?.GetService<GameStateService>();
            if (vault == null || gameState == null)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                return;
            }

            int unitPrice = vaultStack.ItemTemplate?.Price ?? 0;
            string shopTitle = GetText(TextType.UI, UITextKey.WindowSecondChanceShop);

            System.Action cancelAction = () =>
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
            };

            if (isEquipSlot || vaultStack.Quantity <= 1)
            {
                // Equip slot or single-item stack: plain confirm with unit price, always qty=1
                string promptText = string.Format(GetText(TextType.UI, UITextKey.SecondChanceBuyPrompt), unitPrice);
                var dialog = new ConfirmationDialog(shopTitle, promptText, _skin,
                    onYes: () => ExecuteItemPurchase(vaultStack, destSlot, 1, unitPrice, vault, gameState),
                    onNo:  cancelAction);
                dialog.Show(_stage);
            }
            else
            {
                // Multi-item stack dropped onto an inventory slot: show quantity selector
                var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
                int maxQty = ComputeMaxQtyForInventorySlot(vaultStack, heroComp);
                string itemName = vaultStack.ItemTemplate?.Name ?? "";
                var qtyDialog = new VaultBuyQuantityDialog(shopTitle, itemName, unitPrice, maxQty, _skin,
                    onConfirm: (qty) => ExecuteItemPurchase(vaultStack, destSlot, qty, unitPrice, vault, gameState),
                    onCancel:  cancelAction,
                    availableFunds: gameState.Funds);
                qtyDialog.Show(_stage);
            }
        }

        /// <summary>
        /// Computes the maximum purchasable quantity for a vault item dropped onto an inventory slot.
        /// Consumables are capped by their StackSize (all N fit in one bag slot).
        /// Gear is capped by vault quantity and available empty bag slots.
        /// </summary>
        private int ComputeMaxQtyForInventorySlot(
            SecondChanceMerchantVault.StackedItem vaultStack,
            HeroComponent heroComp)
        {
            int vaultQty = vaultStack.Quantity;
            var item = vaultStack.ItemTemplate;

            if (item is Consumable consumable)
                return System.Math.Min(vaultQty, consumable.StackSize);

            // Gear: one item per bag slot; cap by available empty slots (min 1)
            int bagFree = (heroComp?.Bag != null) ? (heroComp.Bag.Capacity - heroComp.Bag.Count) : 1;
            int gearMax = System.Math.Min(vaultQty, bagFree);
            return gearMax > 0 ? gearMax : 1;
        }

        /// <summary>
        /// Executes the item purchase: deducts total gold (unitPrice × qty), places items in hero
        /// inventory or equipment slot, removes qty from vault, and refreshes grids.
        /// </summary>
        private void ExecuteItemPurchase(
            SecondChanceMerchantVault.StackedItem vaultStack,
            InventorySlot destSlot,
            int qty,
            int unitPrice,
            SecondChanceMerchantVault vault,
            GameStateService gameState)
        {
            int totalPrice = unitPrice * qty;
            if (gameState.Funds < totalPrice)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                return;
            }

            var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComp == null)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                return;
            }

            gameState.Funds -= totalPrice;

            var item = vaultStack.ItemTemplate;
            if (destSlot.SlotData.SlotType == InventorySlotType.MercenaryEquipment && destSlot.SlotData.EquipmentSlot.HasValue)
            {
                // Mercenary equip slots always receive exactly 1 item
                var equipMerc = destSlot.SlotData.MercenaryRef;
                if (equipMerc != null && equipMerc.SetEquipmentSlot(destSlot.SlotData.EquipmentSlot.Value, item))
                    Services.Analytics.AnalyticsService.LogGearEquipped(equipMerc, destSlot.SlotData.EquipmentSlot.Value, item);
                vault.RemoveQuantity(vaultStack, 1);
            }
            else if (destSlot.SlotData.SlotType == InventorySlotType.Equipment && destSlot.SlotData.EquipmentSlot.HasValue)
            {
                // Hero equip slots always receive exactly 1 item
                var equipHero = heroComp.LinkedHero;
                if (equipHero != null && equipHero.SetEquipmentSlot(destSlot.SlotData.EquipmentSlot.Value, item))
                    Services.Analytics.AnalyticsService.LogGearEquipped(equipHero, destSlot.SlotData.EquipmentSlot.Value, item);
                vault.RemoveQuantity(vaultStack, 1);
            }
            else if (destSlot.SlotData.SlotType == InventorySlotType.Inventory && destSlot.SlotData.BagIndex.HasValue)
            {
                if (item is Consumable consumable)
                {
                    // Always create a fresh independent instance — never mutate or share the vault
                    // template reference, which may already exist in other bag slots from prior purchases.
                    var freshConsumable = consumable.CreateFreshInstance();
                    freshConsumable.StackCount = System.Math.Min(qty, freshConsumable.StackSize);
                    heroComp.Bag?.SetSlotItem(destSlot.SlotData.BagIndex.Value, freshConsumable);

                    // Handle the rare case where qty exceeds one stack (overflow → fill empty slots).
                    int overflow = qty - freshConsumable.StackCount;
                    for (int i = 0; i < heroComp.Bag.Capacity && overflow > 0; i++)
                    {
                        if (heroComp.Bag.GetSlotItem(i) == null)
                        {
                            var extra = consumable.CreateFreshInstance();
                            extra.StackCount = System.Math.Min(overflow, extra.StackSize);
                            heroComp.Bag.SetSlotItem(i, extra);
                            overflow -= extra.StackCount;
                        }
                    }
                }
                else
                {
                    // Gear: first copy to the dragged-onto slot, remaining to any empty bag slots
                    heroComp.Bag?.SetSlotItem(destSlot.SlotData.BagIndex.Value, item);
                    for (int i = 1; i < qty; i++)
                        heroComp.Bag?.TryAdd(item);
                }
                vault.RemoveQuantity(vaultStack, qty);
            }

            _vaultItemGrid?.RefreshFromVault(vault);
            InventorySelectionManager.OnInventoryChanged?.Invoke();
            InventoryDragManager.EndDrag();
        }

        /// <summary>Refreshes the vault item grid when the hero sells an item from their inventory.</summary>
        private void HandleHeroInventorySell()
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();
            if (vault != null)
                _vaultItemGrid?.RefreshFromVault(vault);
        }

        /// <summary>
        /// Called when a hero inventory item drag is released outside the inventory grid.
        /// If the drop position falls over the shop window while the Items tab is active, shows a sell confirmation dialog.
        /// </summary>
        private void HandleHeroItemDroppedOutside(InventorySlot sourceSlot, Vector2 stagePos)
        {
            if (_activeTabIndex != 0 || !_windowVisible) return;
            if (!IsPositionOverShopWindow(stagePos)) return;

            var item = sourceSlot.SlotData?.Item;
            if (item == null || !sourceSlot.SlotData.BagIndex.HasValue) return;
            int bagIndex = sourceSlot.SlotData.BagIndex.Value;

            int qty = (item is RolePlayingFramework.Equipment.Consumable c) ? c.StackCount : 1;
            int sellGold = item.GetSellPrice() * qty;

            _heroInventoryGrid.NotifyExternalDropHandled();

            string promptText = string.Format(GetText(TextType.UI, UITextKey.SecondChanceSellPrompt), sellGold);
            var dialog = new ConfirmationDialog(
                GetText(TextType.UI, UITextKey.DialogReallyDiscard),
                promptText,
                _skin,
                onYes: () =>
                {
                    _heroInventoryGrid.DiscardItem(bagIndex);
                    InventoryDragManager.EndDrag();
                },
                onNo: () => InventoryDragManager.CancelDrag()
            );
            dialog.Show(_stage);
        }

        /// <summary>Returns true if the given stage position falls within the shop window bounds.</summary>
        private bool IsPositionOverShopWindow(Vector2 stagePos)
        {
            if (_shopWindow == null) return false;
            var topLeft = _shopWindow.LocalToStageCoordinates(Vector2.Zero);
            return stagePos.X >= topLeft.X && stagePos.X <= topLeft.X + _shopWindow.GetWidth() &&
                   stagePos.Y >= topLeft.Y && stagePos.Y <= topLeft.Y + _shopWindow.GetHeight();
        }

        /// <summary>
        /// Called when a vault item drag is released. Checks whether the drop position hits a hero
        /// inventory slot; if so, starts the purchase confirmation flow. Otherwise cancels.
        /// </summary>
        private void HandleVaultDragDropped(VaultItemSlot slot, Vector2 stagePos)
        {
            if (!InventoryDragManager.IsDragging || !InventoryDragManager.IsVaultItemDrag)
                return;

            var vaultStack = InventoryDragManager.SourceVaultStack;
            var heroSlot = _heroInventoryGrid?.GetSlotAtStagePosition(stagePos);
            if (heroSlot != null && vaultStack != null)
            {
                HandleVaultItemDrop(heroSlot, vaultStack);
            }
            else
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
            }
        }

        /// <summary>Returns true if the given item can be placed in the specified equipment slot by this hero.</summary>
        private bool CanEquipInSlot(IItem item, EquipmentSlot slot, HeroComponent heroComp)
        {
            if (item == null) return false;
            var gear = item as Gear;
            if (gear == null) return false;

            if (!IsGearKindCompatibleWithSlot(gear, slot)) return false;

            // Also check job-class restriction: the hero's job must allow this piece of gear
            if (heroComp?.LinkedHero != null)
                return heroComp.LinkedHero.CanEquipItem(gear);

            return true;
        }

        /// <summary>Returns true if the given item can be placed in the specified equipment slot by this mercenary.</summary>
        private bool CanEquipInSlot(IItem item, EquipmentSlot slot, Mercenary merc)
        {
            if (item == null || merc == null) return false;
            var gear = item as Gear;
            if (gear == null) return false;

            if (!IsGearKindCompatibleWithSlot(gear, slot)) return false;

            // Check job-class restriction: the mercenary's job must allow this piece of gear
            return merc.CanEquipItem(gear);
        }

        /// <summary>Returns true if the gear's ItemKind is compatible with the given equipment slot.</summary>
        private static bool IsGearKindCompatibleWithSlot(Gear gear, EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                case EquipmentSlot.WeaponShield2:
                    return gear.Kind == ItemKind.WeaponSword
                        || gear.Kind == ItemKind.WeaponKnife
                        || gear.Kind == ItemKind.WeaponKnuckle
                        || gear.Kind == ItemKind.WeaponStaff
                        || gear.Kind == ItemKind.WeaponRod
                        || gear.Kind == ItemKind.WeaponBow
                        || gear.Kind == ItemKind.WeaponHammer
                        || gear.Kind == ItemKind.Shield;
                case EquipmentSlot.Armor:
                    return gear.Kind == ItemKind.ArmorMail
                        || gear.Kind == ItemKind.ArmorGi
                        || gear.Kind == ItemKind.ArmorRobe;
                case EquipmentSlot.Hat:
                    return gear.Kind == ItemKind.HatHelm
                        || gear.Kind == ItemKind.HatHeadband
                        || gear.Kind == ItemKind.HatWizard
                        || gear.Kind == ItemKind.HatPriest;
                case EquipmentSlot.Accessory1:
                case EquipmentSlot.Accessory2:
                    return gear.Kind == ItemKind.Accessory;
                default:
                    return false;
            }
        }

        /// <summary>Called when a vault crystal is dropped onto a hero crystal slot.</summary>
        private void HandleVaultCrystalDrop(CrystalSlotType destSlotType, int destSlotIdx, HeroCrystal crystal)
        {
            var vault         = Core.Services?.GetService<SecondChanceMerchantVault>();
            var gameState     = Core.Services?.GetService<GameStateService>();
            var crystalService = Core.Services?.GetService<CrystalCollectionService>();

            if (vault == null || gameState == null || crystalService == null)
            {
                InventoryDragManager.CancelDrag();
                _vaultCrystalGrid?.ShowAllCrystalSprites();
                return;
            }

            int price = crystal.Level * GameConfig.CrystalBuyBackBasePrice;
            string promptText = string.Format(GetText(TextType.UI, UITextKey.SecondChanceBuyPrompt), price);

            var dialog = new ConfirmationDialog(
                GetText(TextType.UI, UITextKey.WindowSecondChanceShop),
                promptText,
                _skin,
                onYes: () => ExecuteCrystalPurchase(crystal, destSlotType, destSlotIdx, price, vault, gameState, crystalService),
                onNo:  () =>
                {
                    InventoryDragManager.CancelDrag();
                    _vaultCrystalGrid?.ShowAllCrystalSprites();
                }
            );
            dialog.Show(_stage);
        }

        /// <summary>Executes the crystal purchase: deducts gold, places crystal, removes from vault, refreshes panels.</summary>
        private void ExecuteCrystalPurchase(
            HeroCrystal crystal,
            CrystalSlotType destSlotType,
            int destSlotIdx,
            int price,
            SecondChanceMerchantVault vault,
            GameStateService gameState,
            CrystalCollectionService crystalService)
        {
            if (gameState.Funds < price)
            {
                InventoryDragManager.CancelDrag();
                _vaultCrystalGrid?.ShowAllCrystalSprites();
                return;
            }

            gameState.Funds -= price;

            bool placed = crystalService.TryAddToInventory(crystal);
            if (!placed)
            {
                // Inventory full — refund gold
                gameState.AddFunds(price, "refund");
                InventoryDragManager.CancelDrag();
                _vaultCrystalGrid?.ShowAllCrystalSprites();
                return;
            }

            vault.RemoveCrystal(crystal);
            _vaultCrystalGrid?.RefreshFromVault(vault);
            _heroCrystalPanel?.RefreshAll();
            InventoryDragManager.EndDrag();
        }

        /// <summary>
        /// Called when a vault crystal drag is released. Checks whether the drop position hits a hero
        /// crystal slot; if so, starts the purchase confirmation flow. Otherwise cancels.
        /// </summary>
        private void HandleVaultCrystalDragDropped(VaultCrystalSlot slot, Vector2 stagePos)
        {
            if (!InventoryDragManager.IsDragging || !InventoryDragManager.IsVaultCrystalDrag)
                return;

            var crystal = InventoryDragManager.SourceVaultCrystal;
            if (crystal != null && _heroCrystalPanel != null &&
                _heroCrystalPanel.TryGetCrystalSlotAtStagePosition(stagePos,
                    out CrystalSlotType destSlotType, out int destSlotIdx, out CrystalSlotElement destSlot))
            {
                // Only accept drops on empty slots
                if (destSlot.Crystal != null)
                {
                    InventoryDragManager.CancelDrag();
                    _vaultCrystalGrid?.ShowAllCrystalSprites();
                    return;
                }
                HandleVaultCrystalDrop(destSlotType, destSlotIdx, crystal);
            }
            else
            {
                InventoryDragManager.CancelDrag();
                _vaultCrystalGrid?.ShowAllCrystalSprites();
            }
        }

        private void HandleVaultCrystalSlotClicked(VaultCrystalSlot slot)
        {
            if (slot.Crystal == null)
            {
                HideVaultCrystalCard();
                return;
            }
            ShowVaultCrystalCard(slot.Crystal);
        }

        private void HandleHeroCrystalSlotClicked(HeroCrystal crystal)
        {
            if (crystal == null)
            {
                HideVaultCrystalCard();
                return;
            }
            ShowVaultCrystalCard(crystal);
        }

        private void ShowVaultCrystalCard(HeroCrystal crystal)
        {
            if (_vaultCrystalCard == null) return;
            _vaultCrystalCard.ShowCrystal(crystal);
            _vaultCrystalCard.Pack();
            _vaultCrystalCard.PositionAtWindowLeft(_shopWindow);

            if (_vaultCrystalCardDismissLayer == null)
                _vaultCrystalCardDismissLayer = new VaultCrystalCardDismissLayer(HideVaultCrystalCard);
            if (_vaultCrystalCardDismissLayer.GetParent() == null)
                _stage.AddElement(_vaultCrystalCardDismissLayer);
            _vaultCrystalCardDismissLayer.SetSize(_stage.GetWidth(), _stage.GetHeight());
            _vaultCrystalCardDismissLayer.SetVisible(true);
            // Bring shop and hero-crystal windows above the dismiss layer so their
            // crystal-slot children receive native click/hover events. Set ChildrenOnly
            // touchable on both so empty background areas fall through to the dismiss layer.
            _shopWindow?.ToFront();
            _shopWindow?.SetTouchable(Touchable.ChildrenOnly);
            _heroCrystalWindow?.ToFront();
            _heroCrystalWindow?.SetTouchable(Touchable.ChildrenOnly);
            _vaultCrystalCard.ToFront();
        }

        private void HideVaultCrystalCard()
        {
            _vaultCrystalCard?.Hide();
            if (_vaultCrystalCardDismissLayer != null)
                _vaultCrystalCardDismissLayer.SetVisible(false);
            _shopWindow?.SetTouchable(Touchable.Enabled);
            _heroCrystalWindow?.SetTouchable(Touchable.Enabled);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Data refresh
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Gathers hired mercenaries and refreshes their equip slots in the inventory grid.</summary>
        private void RefreshMercenaryEquipSlots()
        {
            if (_heroInventoryGrid == null) return;

            var mercManager = Core.Services?.GetService<MercenaryManager>();
            List<Mercenary> hiredMercs = null;

            if (mercManager != null)
            {
                var hiredEntities = mercManager.GetHiredMercenaries();
                if (hiredEntities != null && hiredEntities.Count > 0)
                {
                    hiredMercs = new List<Mercenary>(hiredEntities.Count);
                    for (int i = 0; i < hiredEntities.Count; i++)
                    {
                        var mc = hiredEntities[i].GetComponent<MercenaryComponent>();
                        if (mc?.LinkedMercenary != null)
                            hiredMercs.Add(mc.LinkedMercenary);
                    }
                }
            }

            _heroInventoryGrid.RefreshMercenarySlots(hiredMercs);
        }

        /// <summary>Refreshes all shop data when the window is opened.</summary>
        private void RefreshShopData()
        {
            _heroInventoryTooltip?.InvalidateCache();

            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();
            if (vault != null)
            {
                _vaultItemGrid?.RefreshFromVault(vault);
                _vaultCrystalGrid?.RefreshFromVault(vault);
            }

            var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComp != null)
                _heroInventoryGrid?.ConnectToHero(heroComp);

            RefreshMercenaryEquipSlots();

            _heroCrystalPanel?.RefreshAll();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Inner helpers
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A single slot in the Seeds shop grid.  Draws the crop's fully-grown sprite, overlays
        /// a live owned-count badge (read each frame so it updates immediately after a purchase),
        /// shows a hover tooltip with crop name and price, and fires OnBuyClicked on left-mouse-up.
        /// When the player has fewer seeds than unplanted plans, the crop sprite gently pulses
        /// in size to draw attention to the purchase.
        /// </summary>
        private class SeedShopSlot : Element, IInputListener
        {
            // Inventory-slot background drawn at the same translucency as the inventory UI.
            private static readonly Color SlotBgColor       = new Color(255, 255, 255, 100);

            private readonly Sprite   _sprite;
            private readonly CropType _crop;
            // Not readonly: the slot is built during Scene.Initialize(), before LoadMap registers
            // CropGrowthService, so null services are re-resolved lazily on draw.
            private CropPlantingService _cropService;
            private CropGrowthService   _cropGrowth;
            private readonly SpriteDrawable      _draw;
            private SpriteDrawable _background;
            private Sprite _selectBox;
            private bool   _hovered;
            private readonly string _tooltipText;

            /// <summary>Fired when the player left-clicks this slot.</summary>
            public event System.Action<CropType> OnBuyClicked;

            public SeedShopSlot(Sprite sprite, CropType crop, CropPlantingService cropService, CropGrowthService cropGrowth, string tooltipText)
            {
                _sprite      = sprite;
                _crop        = crop;
                _cropService = cropService;
                _cropGrowth  = cropGrowth;
                _tooltipText = tooltipText;
                _draw        = sprite != null ? new SpriteDrawable(sprite) : null;
                SetTouchable(Touchable.Enabled);
                SetSize(40f, 40f);

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
                // Services may not have existed at construction time — resolve lazily.
                if (_cropService == null)
                    _cropService = Core.Services?.GetService<CropPlantingService>();
                if (_cropGrowth == null)
                    _cropGrowth = Core.Services?.GetService<CropGrowthService>();

                // Resolve live seed count up front so it can drive both the attention pulse and badge.
                int count = _cropService?.SeedInventory != null
                    ? _cropService.SeedInventory[(int)_crop]
                    : 0;

                // Attention pulse when there are more unplanted plans than owned seeds: the crop
                // sprite gently scales up and down around the slot center.
                int needed = (_cropService != null && _cropGrowth != null)
                    ? _cropService.CountUnplantedPlans(_crop, _cropGrowth)
                    : 0;

                _background?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), SlotBgColor);

                float spriteX = GetX(), spriteY = GetY(), spriteW = GetWidth(), spriteH = GetHeight();
                if (needed > count)
                {
                    float pulse = 1f + GameConfig.SeedShopPulseAmplitude * Mathf.Sin(Time.TotalTime * GameConfig.SeedShopPulseSpeed);
                    float pw = spriteW * pulse;
                    float ph = spriteH * pulse;
                    spriteX += (spriteW - pw) * 0.5f;
                    spriteY += (spriteH - ph) * 0.5f;
                    spriteW = pw;
                    spriteH = ph;
                }
                _draw?.Draw(batcher, spriteX, spriteY, spriteW, spriteH, Color.White);

                if (_hovered && _selectBox != null)
                    new SpriteDrawable(_selectBox).Draw(
                        batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                // Live count badge (read each frame — auto-updates after purchase)
                var font = Nez.Graphics.Instance?.BitmapFont;
                if (font != null)
                {
                    string countStr = count.ToString();
                    float tw = font.MeasureString(countStr).X;
                    var pos = new Vector2(GetX() + GetWidth() - tw - 2f, GetY() + GetHeight() - font.LineHeight - 1f);
                    StackCountText.Draw(batcher, font, countStr, pos, Color.White);
                }
            }

            void IInputListener.OnMouseEnter()
            {
                _hovered = true;
                if (!string.IsNullOrEmpty(_tooltipText))
                {
                    var stage = GetStage();
                    if (stage != null)
                    {
                        var mp = stage.GetMousePosition();
                        HoverTextManager.ShowHoverText(_tooltipText, mp.X + 12f, mp.Y - 4f);
                    }
                    else
                    {
                        HoverTextManager.ShowHoverText(_tooltipText, GetX(), GetY() + GetHeight() + 4f);
                    }
                }
            }

            void IInputListener.OnMouseExit()
            {
                _hovered = false;
                HoverTextManager.HideHoverText();
            }

            void IInputListener.OnMouseMoved(Vector2 mousePos) { }

            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;

            void IInputListener.OnLeftMouseUp(Vector2 mousePos) => OnBuyClicked?.Invoke(_crop);

            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;

            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }

            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }

        /// <summary>Full-stage transparent overlay that dismisses the vault crystal card on any click.
        /// Crystal-slot clicks are handled by the slots themselves (windows use ChildrenOnly touchable).</summary>
        private class VaultCrystalCardDismissLayer : Element, IInputListener
        {
            private readonly System.Action _onDismiss;

            public VaultCrystalCardDismissLayer(System.Action onDismiss)
            {
                _onDismiss = onDismiss;
                SetTouchable(Touchable.Enabled);
                SetVisible(false);
            }

            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)  { _onDismiss?.Invoke(); return true; }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) { _onDismiss?.Invoke(); return true; }
            void IInputListener.OnMouseEnter()  { }
            void IInputListener.OnMouseExit()   { }
            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            void IInputListener.OnLeftMouseUp(Vector2 mousePos)  { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
