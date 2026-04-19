using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>UI shell for the Second Chance Shop - provides a button and tabbed window for purchasing items and crystals.</summary>
    public class SecondChanceShopUI
    {
        private Stage _stage;
        private HoverableImageButton _shopButton;
        private Window _shopWindow;
        private bool _windowVisible = false;
        private ImageButtonStyle _shopNormalStyle;
        private ImageButtonStyle _shopHalfStyle;
        private bool _styleChanged = false;
        private TabPane _tabPane;
        private Tab _itemsTab;
        private Tab _crystalsTab;
        private Image _merchantSprite;
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

            // Load merchant sprite for display beside the window
            var merchantSprite = uiAtlas.GetSprite("SecondChanceMerchant");
            _merchantSprite = new Image(new SpriteDrawable(merchantSprite));

            CreateShopWindow(_skin);
            PopulateItemsTab(_itemsTab, _skin);
            PopulateCrystalsTab(_crystalsTab, _skin);
            _pauseService = Core.Services?.GetService<PauseService>();
            _stage.AddElement(_shopButton);
        }

        /// <summary>Safely retrieves TextService, returns null if Core is not initialized.</summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>Gets localized text or falls back to key name if TextService unavailable.</summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key;
        }

        /// <summary>Creates the shop window with Items and Crystals tabs.</summary>
        private void CreateShopWindow(Skin skin)
        {
            _shopWindow = new Window(GetText(TextType.UI, UITextKey.WindowSecondChanceShop), skin);
            _shopWindow.SetSize(505f, 348f);

            var tabWindowStyle = skin.Get<TabWindowStyle>("ph-default");
            _tabPane = new TabPane(tabWindowStyle);

            var tabStyle = new TabStyle { Background = null };

            _itemsTab = new Tab(GetText(TextType.UI, UITextKey.TabItems), tabStyle);

            _crystalsTab = new Tab(GetText(TextType.UI, UITextKey.TabCrystals), tabStyle);

            _tabPane.AddTab(_itemsTab);
            _tabPane.AddTab(_crystalsTab);

            _shopWindow.Add(_tabPane).Expand().Fill().Pad(0);
            _shopWindow.SetVisible(false);
        }

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
                _stage.AddElement(_shopWindow);
                _shopWindow.SetVisible(true);
                _shopWindow.ToFront();
                PositionWindow();
                _stage.AddElement(_merchantSprite);
                _merchantSprite.SetPosition(GameConfig.SecondChanceMerchantSpriteX, GameConfig.SecondChanceMerchantSpriteY);
                _merchantSprite.ToFront();
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
                if (_pauseService != null)
                    _pauseService.IsPaused = false;
            }
        }

        private void PositionWindow()
        {
            if (_shopButton == null || _shopWindow == null) return;
            float btnX = _shopButton.GetX();
            float btnW = _shopButton.GetWidth();
            float winW = _shopWindow.GetWidth();
            float winH = _shopWindow.GetHeight();
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            float winX = btnX + btnW + 4f;
            if (winX + winW > stageW) winX = btnX - 4f - winW;
            if (winX < 0) winX = 0;

            float winY = _shopButton.GetY() + 4f;
            if (winY + winH > stageH) winY = stageH - winH;
            if (winY < 0) winY = 0;

            _shopWindow.SetPosition(winX, winY);
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
                if (_pauseService != null)
                    _pauseService.IsPaused = false;
                Debug.Log("[SecondChanceShopUI] Shop window force closed by single window policy");
            }
        }

        /// <summary>Sets the SettingsUI reference for single window policy.</summary>
        public void SetSettingsUI(SettingsUI settingsUI) { _settingsUI = settingsUI; }

        /// <summary>Sets the HeroUI reference for single window policy.</summary>
        public void SetHeroUI(HeroUI heroUI) { _heroUI = heroUI; }

        /// <summary>Sets the MonsterUI reference for single window policy.</summary>
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }

        /// <summary>Sets the position of the shop icon button.</summary>
        public void SetPosition(float x, float y)
        {
            _shopButton?.SetPosition(x, y);
            if (_windowVisible) PositionWindow();
        }

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
            UpdateButtonStyleIfNeeded();
            if (_windowVisible) PositionWindow();
        }

        /// <summary>Populates the Items tab with a vault grid on the left and hero inventory on the right.</summary>
        private void PopulateItemsTab(Tab tab, Skin skin)
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();
            var heroComponent = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();

            var content = new Table();
            content.Top().Left().Pad(4f);

            // Left: vault item grid (scrollable)
            _vaultItemGrid = new VaultItemGrid();
            _vaultItemGrid.InitializeTooltip(_stage, skin);
            if (vault != null)
                _vaultItemGrid.RefreshFromVault(vault);

            var scrollPane = new ScrollPane(_vaultItemGrid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            // 9 slots * 33px = 297px wide, 6 rows * 33px = 198px tall
            content.Add(scrollPane).Size(297f, 198f).Top().Left().Pad(4f);

            // Right: hero inventory grid
            _heroInventoryGrid = new InventoryGrid();
            if (heroComponent != null)
                _heroInventoryGrid.ConnectToHero(heroComponent);
            _heroInventoryGrid.InitializeContextMenu(_stage, skin);
            _heroInventoryGrid.OnVaultItemDropRequested += HandleVaultItemDrop;

            var heroScrollPane = new ScrollPane(_heroInventoryGrid, skin, "ph-default");
            heroScrollPane.SetScrollingDisabled(true, false);

            content.Add(heroScrollPane).Width(700f).Top().Left().Pad(4f);

            tab.ClearChildren();
            tab.Add(content).Expand().Fill();

            // If the drag is dropped somewhere not on the hero grid, cancel it
            _vaultItemGrid.OnVaultSlotDragDropped += HandleVaultDragDropped;
        }

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

            // For equipment slots, validate item type compatibility
            if (destSlot.SlotData.SlotType == InventorySlotType.Equipment && destSlot.SlotData.EquipmentSlot.HasValue)
            {
                var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
                if (!CanEquipInSlot(vaultStack.ItemTemplate, destSlot.SlotData.EquipmentSlot.Value, heroComp))
                {
                    InventoryDragManager.CancelDrag();
                    _vaultItemGrid?.ShowAllItemSprites();
                    return;
                }
            }

            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();
            var gameState = Core.Services?.GetService<GameStateService>();
            if (vault == null || gameState == null)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                return;
            }

            int price = vaultStack.ItemTemplate?.Price ?? 0;
            string promptText = string.Format(GetText(TextType.UI, UITextKey.SecondChanceBuyPrompt), price);

            var dialog = new ConfirmationDialog(
                GetText(TextType.UI, UITextKey.WindowSecondChanceShop),
                promptText,
                _skin,
                onYes: () => ExecuteItemPurchase(vaultStack, destSlot, price, vault, gameState),
                onNo: () =>
                {
                    InventoryDragManager.CancelDrag();
                    _vaultItemGrid?.ShowAllItemSprites();
                }
            );
            dialog.Show(_stage);
        }

        /// <summary>Executes the item purchase: deducts gold, places item, removes from vault, refreshes grids.</summary>
        private void ExecuteItemPurchase(
            SecondChanceMerchantVault.StackedItem vaultStack,
            InventorySlot destSlot,
            int price,
            SecondChanceMerchantVault vault,
            GameStateService gameState)
        {
            if (gameState.Funds < price)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                Debug.Log("[SecondChanceShopUI] Purchase failed: insufficient gold");
                return;
            }

            var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComp == null)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
                return;
            }

            gameState.Funds -= price;

            var item = vaultStack.ItemTemplate;
            if (destSlot.SlotData.SlotType == InventorySlotType.Equipment && destSlot.SlotData.EquipmentSlot.HasValue)
            {
                // Equip directly to hero using the validated SetEquipmentSlot method
                heroComp.LinkedHero?.SetEquipmentSlot(destSlot.SlotData.EquipmentSlot.Value, item);
            }
            else if (destSlot.SlotData.SlotType == InventorySlotType.Inventory && destSlot.SlotData.BagIndex.HasValue)
            {
                heroComp.Bag?.SetSlotItem(destSlot.SlotData.BagIndex.Value, item);
            }

            vault.RemoveQuantity(vaultStack, 1);

            _vaultItemGrid?.RefreshFromVault(vault);
            InventorySelectionManager.OnInventoryChanged?.Invoke();
            InventoryDragManager.EndDrag();

            Debug.Log("[SecondChanceShopUI] Purchased " + (item?.Name ?? "unknown") + " for " + price + " gold");
        }

        /// <summary>Called when the vault slot fires a drop event with no hero grid target — cancels the drag.</summary>
        private void HandleVaultDragDropped(VaultItemSlot slot)
        {
            if (InventoryDragManager.IsDragging && InventoryDragManager.IsVaultItemDrag)
            {
                InventoryDragManager.CancelDrag();
                _vaultItemGrid?.ShowAllItemSprites();
            }
        }

        /// <summary>Returns true if the given item can be placed in the specified equipment slot.</summary>
        private bool CanEquipInSlot(IItem item, EquipmentSlot slot, HeroComponent heroComp)
        {
            if (item == null) return false;
            var gear = item as Gear;
            if (gear == null) return false;

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

        /// <summary>Populates the Crystals tab with a vault crystal grid on the left and hero crystal panel on the right.</summary>
        private void PopulateCrystalsTab(Tab tab, Skin skin)
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();

            var content = new Table();
            content.Top().Left().Pad(4f);

            // Left: vault crystal grid (scrollable)
            _vaultCrystalGrid = new VaultCrystalGrid();
            _vaultCrystalGrid.InitializeTooltip(_stage, skin);
            if (vault != null)
                _vaultCrystalGrid.RefreshFromVault(vault);

            var scrollPane = new ScrollPane(_vaultCrystalGrid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);
            content.Add(scrollPane).Size(297f, 198f).Top().Left().Pad(4f);

            // Right: hero crystal panel
            _heroCrystalPanel = new SecondChanceHeroCrystalPanel();
            _heroCrystalPanel.OnVaultCrystalDropRequested += HandleVaultCrystalDrop;

            var heroPanel = _heroCrystalPanel.CreateContent(skin, _stage);
            content.Add(heroPanel).Top().Left().Pad(4f);

            tab.ClearChildren();
            tab.Add(content).Expand().Fill();

            _vaultCrystalGrid.OnVaultCrystalDragDropped += HandleVaultCrystalDragDropped;
        }

        /// <summary>Called when a vault crystal is dropped onto a hero crystal slot.</summary>
        private void HandleVaultCrystalDrop(CrystalSlotType destSlotType, int destSlotIdx, HeroCrystal crystal)
        {
            var vault = Core.Services?.GetService<SecondChanceMerchantVault>();
            var gameState = Core.Services?.GetService<GameStateService>();
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
                onNo: () =>
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
                Debug.Log("[SecondChanceShopUI] Crystal purchase failed: insufficient gold");
                return;
            }

            gameState.Funds -= price;

            // Place crystal into inventory (TryAddToInventory finds first free slot)
            bool placed = crystalService.TryAddToInventory(crystal);

            if (!placed)
            {
                // Inventory full — refund gold
                gameState.Funds += price;
                InventoryDragManager.CancelDrag();
                _vaultCrystalGrid?.ShowAllCrystalSprites();
                Debug.Log("[SecondChanceShopUI] Crystal purchase failed: inventory full");
                return;
            }

            vault.RemoveCrystal(crystal);

            _vaultCrystalGrid?.RefreshFromVault(vault);
            _heroCrystalPanel?.RefreshAll();
            InventoryDragManager.EndDrag();

            Debug.Log("[SecondChanceShopUI] Crystal purchased: " + crystal.Name + " for " + price + " gold");
        }

        /// <summary>Called when the vault crystal grid fires a drop event with no hero panel target — cancels the drag.</summary>
        private void HandleVaultCrystalDragDropped(VaultCrystalSlot slot)
        {
            if (InventoryDragManager.IsDragging && InventoryDragManager.IsVaultCrystalDrag)
            {
                InventoryDragManager.CancelDrag();
                _vaultCrystalGrid?.ShowAllCrystalSprites();
            }
        }

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
