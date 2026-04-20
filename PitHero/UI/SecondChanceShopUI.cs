using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;

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
        private Skin _skin;
        private TextService _textService;
        private PauseService _pauseService;

        // Items tab components
        private VaultItemGrid _vaultItemGrid;
        private InventoryGrid _heroInventoryGrid;

        // Crystals tab components
        private VaultCrystalGrid _vaultCrystalGrid;
        private SecondChanceHeroCrystalPanel _heroCrystalPanel;

        // Which tab is currently active (0=Items, 1=Crystals)
        private int _activeTabIndex = 0;

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

            PopulateItemsTab(_itemsTab, skin);
            PopulateCrystalsTab(_crystalsTab, skin);

            _tabPane.AddTab(_itemsTab);
            _tabPane.AddTab(_crystalsTab);

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

            var heroComponent = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComponent != null)
                _heroInventoryGrid.ConnectToHero(heroComponent);

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

            var scrollPane = new ScrollPane(_vaultCrystalGrid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            var content = new Table();
            content.Top().Left().Pad(8f);
            content.Add(scrollPane).Size(297f, 198f).Top().Left();

            tab.ClearChildren();
            tab.Add(content).Expand().Fill();
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
            else // Crystals tab
            {
                _heroInventoryWindow?.SetVisible(false);
                _heroCrystalWindow?.SetVisible(true);
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
                else
                {
                    _heroInventoryWindow.SetVisible(false);
                    _heroCrystalWindow.SetVisible(true);
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

            bool isEquipSlot = destSlot.SlotData.SlotType == InventorySlotType.Equipment
                               && destSlot.SlotData.EquipmentSlot.HasValue;

            // For equipment slots, validate item type and job-class compatibility
            if (isEquipSlot)
            {
                var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
                if (!CanEquipInSlot(vaultStack.ItemTemplate, destSlot.SlotData.EquipmentSlot.Value, heroComp))
                {
                    InventoryDragManager.CancelDrag();
                    _vaultItemGrid?.ShowAllItemSprites();
                    return;
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
                    onCancel:  cancelAction);
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
            if (destSlot.SlotData.SlotType == InventorySlotType.Equipment && destSlot.SlotData.EquipmentSlot.HasValue)
            {
                // Equip slots always receive exactly 1 item
                heroComp.LinkedHero?.SetEquipmentSlot(destSlot.SlotData.EquipmentSlot.Value, item);
                vault.RemoveQuantity(vaultStack, 1);
            }
            else if (destSlot.SlotData.SlotType == InventorySlotType.Inventory && destSlot.SlotData.BagIndex.HasValue)
            {
                if (item is Consumable consumable)
                {
                    // The vault stores the consumable as a shared reference template and tracks the
                    // total count separately via StackedItem.Quantity.  Setting StackCount = 1
                    // normalises the template before placement, then TryAdd (qty-1) times stacks
                    // the same reference in the target slot until StackCount == qty.
                    // The vault never reads ItemTemplate.StackCount after insertion, so this
                    // intentional mutation is safe within the current vault design.
                    consumable.StackCount = 1;
                    heroComp.Bag?.SetSlotItem(destSlot.SlotData.BagIndex.Value, consumable);
                    for (int i = 1; i < qty; i++)
                        heroComp.Bag?.TryAdd(consumable);
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

            // Check slot-kind compatibility first
            bool slotCompatible;
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                case EquipmentSlot.WeaponShield2:
                    slotCompatible = gear.Kind == ItemKind.WeaponSword
                        || gear.Kind == ItemKind.WeaponKnife
                        || gear.Kind == ItemKind.WeaponKnuckle
                        || gear.Kind == ItemKind.WeaponStaff
                        || gear.Kind == ItemKind.WeaponRod
                        || gear.Kind == ItemKind.WeaponBow
                        || gear.Kind == ItemKind.WeaponHammer
                        || gear.Kind == ItemKind.Shield;
                    break;
                case EquipmentSlot.Armor:
                    slotCompatible = gear.Kind == ItemKind.ArmorMail
                        || gear.Kind == ItemKind.ArmorGi
                        || gear.Kind == ItemKind.ArmorRobe;
                    break;
                case EquipmentSlot.Hat:
                    slotCompatible = gear.Kind == ItemKind.HatHelm
                        || gear.Kind == ItemKind.HatHeadband
                        || gear.Kind == ItemKind.HatWizard
                        || gear.Kind == ItemKind.HatPriest;
                    break;
                case EquipmentSlot.Accessory1:
                case EquipmentSlot.Accessory2:
                    slotCompatible = gear.Kind == ItemKind.Accessory;
                    break;
                default:
                    return false;
            }

            if (!slotCompatible) return false;

            // Also check job-class restriction: the hero's job must allow this piece of gear
            if (heroComp?.LinkedHero != null)
                return heroComp.LinkedHero.CanEquipItem(gear);

            return true;
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
                gameState.Funds += price;
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

        // ──────────────────────────────────────────────────────────────────────────
        // Data refresh
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>Refreshes all shop data when the window is opened.</summary>
        private void RefreshShopData()
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();
            if (vault != null)
            {
                _vaultItemGrid?.RefreshFromVault(vault);
                _vaultCrystalGrid?.RefreshFromVault(vault);
            }

            var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComp != null)
                _heroInventoryGrid?.ConnectToHero(heroComp);

            _heroCrystalPanel?.RefreshAll();
        }
    }
}
