using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Mercenaries;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// UI for Hero button with tabbed interface for Inventory and Pit Priorities
    /// </summary>
    public class HeroUI
    {
        private Stage _stage;
        private HoverableImageButton _heroButton;

        private ImageButtonStyle _heroNormalStyle;
        private ImageButtonStyle _heroHalfStyle;
        private enum HeroMode { Normal, Half }
        private HeroMode _currentHeroMode = HeroMode.Normal;
        private bool _styleChanged = false;

        // Tabbed window components
        private Window _heroWindow;
        private TabPane _tabPane;
        private Tab _inventoryTab;
        private Tab _prioritiesTab;
        private Tab _crystalTab;
        private Tab _mercenariesTab;
        private Tab _foodTab;
        private bool _windowVisible = false;

        // References for single window policy enforcement
        private SettingsUI _settingsUI;
        private SecondChanceShopUI _secondChanceShopUI;
        private MonsterUI _monsterUI;
        private FarmUI _farmUI;

        // Inventory tab content
        private InventoryGrid _inventoryGrid;
        private TextButton _viewStencilsButton;
        private TextButton _moveStencilsButton;
        private TextButton _removeStencilButton;

        // Stencil system
        private StencilLibraryPanel _stencilLibraryPanel;
        private uint _stencilPanelShownFrame;
        private List<RolePlayingFramework.Synergies.SynergyPattern> _allSynergyPatterns;

        // Item card for selection only (hover uses tooltip)
        private ItemCard _selectedItemCard;

        // Tooltip for hovering over items
        private ItemCardTooltip _itemTooltip;
        private int _hoverCheckFrame;
        
        // Text service for localization
        private TextService _textService;

        // Tooltip for showing equip preview comparison
        private EquipPreviewTooltip _equipPreviewTooltip;

        // Priority reorder components (moved to priorities tab)
        private ReorderableTableList<string> _priorityList;
        private List<string> _priorityItems;

        // Heal priority reorder components
        private ReorderableTableList<string> _healPriorityList;
        private List<string> _healPriorityItems;

        // Battle tactic and consumable option controls
        private ButtonGroup _battleTacticButtonGroup;
        private CheckBox _blitzButton;
        private CheckBox _strategicButton;
        private CheckBox _defensiveButton;
        private CheckBox _useConsumablesOnMercsCheckBox;
        private CheckBox _mercsCanUseConsumablesCheckBox;

        // Hero Crystal tab component
        private HeroCrystalTab _heroCrystalTab;
        
        // Crystals Collection tab component
        private CrystalsTab _crystalsTabComponent;
        private Tab _crystalsCollectionTab;

        // Mercenaries tab component
        private MercenariesTab _mercenariesTabComponent;
        private FoodTab _foodTabComponent;

        private const float HERO_WINDOW_WIDTH = 870f;
        private const float COMPACT_WINDOW_WIDTH = 490f;
        private const float TAB_STRIP_MARGIN = 24f; // breathing room beside the tab button strip
        private float _minTabStripWidth; // computed from the real tab buttons so new tabs can't overflow

        public HeroUI()
        {
            // Populate from the shared registry so the stencil library panel slot order is authoritative.
            _allSynergyPatterns = new List<RolePlayingFramework.Synergies.SynergyPattern>();
            var all = RolePlayingFramework.Synergies.SynergyPatternRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                _allSynergyPatterns.Add(all[i]);
            }
        }

        /// <summary>
        /// Safely retrieves TextService. Returns null if Core is not initialized (e.g., in unit tests).
        /// </summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>
        /// Gets localized text or falls back to key name if TextService unavailable.
        /// </summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key.ToString();
        }

        /// <summary>Initializes the Hero button and adds it to the stage</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            var skin = PitHeroSkin.CreateSkin();
            CreateHeroButton(skin);
            CreateHeroWindow(skin);
            CreateItemCards(skin);
            _stage.AddElement(_heroButton);
        }

        private void CreateHeroButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var heroSprite = uiAtlas.GetSprite("UIHero");
            var heroSprite2x = uiAtlas.GetSprite("UIHero2x");
            var heroHighlight = uiAtlas.GetSprite("UIHeroHighlight");
            var heroHighlight2x = uiAtlas.GetSprite("UIHeroHighlight2x");
            var heroInverse = uiAtlas.GetSprite("UIHeroInverse");
            var heroInverse2x = uiAtlas.GetSprite("UIHeroInverse2x");

            _heroNormalStyle = new ImageButtonStyle { ImageUp = new SpriteDrawable(heroSprite), ImageDown = new SpriteDrawable(heroInverse), ImageOver = new SpriteDrawable(heroHighlight) };
            _heroHalfStyle = new ImageButtonStyle { ImageUp = new SpriteDrawable(heroSprite2x), ImageDown = new SpriteDrawable(heroInverse2x), ImageOver = new SpriteDrawable(heroHighlight2x) };

            _heroButton = new HoverableImageButton(_heroNormalStyle, "Party");
            _heroButton.ClickSoundCategory = ButtonClickCategory.TopBar;
            _heroButton.SetSize(heroSprite.SourceRect.Width, heroSprite.SourceRect.Height);
            _heroButton.OnClicked += (button) => HandleHeroButtonClick();
        }

        /// <summary>Sets the reference to SettingsUI for single window policy enforcement.</summary>
        public void SetSettingsUI(SettingsUI settingsUI) { _settingsUI = settingsUI; }

        /// <summary>Sets the reference to SecondChanceShopUI for single window policy enforcement.</summary>
        public void SetSecondChanceShopUI(SecondChanceShopUI secondChanceShopUI) { _secondChanceShopUI = secondChanceShopUI; }
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }
        public void SetFarmUI(FarmUI farmUI) { _farmUI = farmUI; }

        private void HandleHeroButtonClick()
        {
            // Properly close Settings UI if it's open (single window policy)
            _settingsUI?.ForceCloseSettings();
            _secondChanceShopUI?.ForceCloseWindow();
            _monsterUI?.ForceCloseWindow();
            _farmUI?.DismissSubButtons();
            ToggleHeroWindow();
        }

        private void CreateHeroWindow(Skin skin)
        {
            _heroWindow = new Window("", skin); // Empty title since tabs provide context
            _heroWindow.Pad(0); // Remove all window padding so tabs are flush with edges
            // Start with inventory tab width (850px)
            // Width will be adjusted dynamically when tabs change
            _heroWindow.SetSize(HERO_WINDOW_WIDTH, 350f);
            var tabWindowStyle = skin.Get<TabWindowStyle>(); // Use skin's tab window style
            _tabPane = new TabPane(tabWindowStyle);
            var tabStyle = CreateTabStyle(skin);
            _inventoryTab = new Tab(GetText(TextType.UI, UITextKey.TabInventory), tabStyle);
            _prioritiesTab = new Tab(GetText(TextType.UI, UITextKey.TabBehavior), tabStyle);
            _crystalTab = new Tab(GetText(TextType.UI, UITextKey.TabHeroInfo), tabStyle);
            _mercenariesTab = new Tab(GetText(TextType.UI, UITextKey.TabMercenaries), tabStyle);
            PopulateInventoryTab(_inventoryTab, skin);
            PopulatePrioritiesTab(_prioritiesTab, skin);
            PopulateCrystalTab(_crystalTab, skin);
            PopulateMercenariesTab(_mercenariesTab, skin);
            _tabPane.AddTab(_inventoryTab);
            _tabPane.AddTab(_crystalTab);
            _tabPane.AddTab(_mercenariesTab);

            _crystalsCollectionTab = new Tab(GetText(TextType.UI, UITextKey.TabCrystals), tabStyle);
            PopulateCrystalsCollectionTab(_crystalsCollectionTab, skin);
            _tabPane.AddTab(_crystalsCollectionTab);

            _foodTab = new Tab(GetText(TextType.UI, UITextKey.TabFood), tabStyle);
            PopulateFoodTab(_foodTab, skin);
            _tabPane.AddTab(_foodTab);

            _tabPane.AddTab(_prioritiesTab);
            
            // Hook into tab button clicks to adjust window width
            for (int i = 0; i < _tabPane.TabButtons.Count; i++)
            {
                var tabButton = _tabPane.TabButtons[i];
                var tab = _tabPane.Tabs[i];
                tabButton.OnClick += () => HandleTabChanged(tab);
            }

            // Minimum window width that fits every tab button — measured from the real buttons
            // so adding a tab can never push the strip outside the window again.
            _minTabStripWidth = TAB_STRIP_MARGIN;
            for (int i = 0; i < _tabPane.TabButtons.Count; i++)
                _minTabStripWidth += _tabPane.TabButtons[i].PreferredWidth;
            
            _heroWindow.Add(_tabPane).Expand().Fill().Pad(0); // No cell padding - tabs flush with window edges
            _heroWindow.SetVisible(false);
        }

        /// <summary>Adjusts window width when tabs are changed.</summary>
        private void HandleTabChanged(Tab selectedTab)
        {
            if (_heroWindow == null) return;

            _stencilLibraryPanel?.SetVisible(false);

            float newWidth;
            if (selectedTab == _inventoryTab)
            {
                // Inventory tab needs full width for 20-column grid
                newWidth = HERO_WINDOW_WIDTH;
            }
            else if (selectedTab == _crystalsCollectionTab)
            {
                newWidth = COMPACT_WINDOW_WIDTH;
                // Refresh crystal slots so any crystals loaded from save are visible
                _crystalsTabComponent?.RefreshAll();
            }
            else if (selectedTab == _foodTab)
            {
                newWidth = COMPACT_WINDOW_WIDTH;
                // Sync favorite/checkbox state in case a save was loaded after UI creation
                _foodTabComponent?.RefreshFromService();
            }
            else
            {
                // All other tabs share the compact width so the window looks consistent
                newWidth = COMPACT_WINDOW_WIDTH;
            }

            // The window must never be narrower than the tab button strip
            if (newWidth < _minTabStripWidth)
                newWidth = _minTabStripWidth;

            _heroWindow.SetSize(newWidth, 350f);
            PositionHeroWindow(); // Reposition after resize to keep it on screen
        }

        private void CreateItemCards(Skin skin)
        {
            _selectedItemCard = new ItemCard(skin);
            _selectedItemCard.SetVisible(false);

            // Create a dummy element for the tooltip target (the tooltip will follow the cursor)
            var dummyTarget = new Element();
            dummyTarget.SetSize(0, 0);
            _itemTooltip = new ItemCardTooltip(dummyTarget, skin);

            // Create equip preview tooltip
            var dummyTarget2 = new Element();
            dummyTarget2.SetSize(0, 0);
            _equipPreviewTooltip = new EquipPreviewTooltip(dummyTarget2, skin);
        }

        private TabStyle CreateTabStyle(Skin skin) => new TabStyle { Background = null };


        private void PopulateInventoryTab(Tab inventoryTab, Skin skin)
        {
            var container = new Table();

            // Create horizontal container for inventory grid and buttons
            var inventoryContainer = new Table();

            _inventoryGrid = new InventoryGrid();
            _inventoryGrid.ShowUnviewedGearSparkles = true;
            _inventoryGrid.SyncStencilsToGameState = true;
            _inventoryGrid.OnItemHovered += HandleItemHovered;
            _inventoryGrid.OnItemUnhovered += HandleItemUnhovered;
            _inventoryGrid.OnDragEquipTargetChanged += HandleDragEquipTargetChanged;
            _inventoryGrid.OnItemSelected += HandleItemSelected;
            _inventoryGrid.OnItemDeselected += HandleItemDeselected;
            _inventoryGrid.OnStencilRemovalRequested += HandleStencilRemovalRequested;
            _inventoryGrid.OnSynergiesChanged += HandleSynergiesChanged;

            // Initialize context menu
            _inventoryGrid.InitializeContextMenu(_stage, skin);

            var heroComponent = GetHeroComponent();
            if (heroComponent != null)
                _inventoryGrid.ConnectToHero(heroComponent);

            // Create scroll pane for inventory grid
            var scrollPane = new ScrollPane(_inventoryGrid, skin);
            scrollPane.SetScrollingDisabled(true, false);

            // Add scroll pane to left side with explicit width to ensure rightmost column is clickable
            // Grid is 692px wide (20 columns � 33px + 32px left padding)
            inventoryContainer.Add(scrollPane).Width(700f).Expand().Fill().Pad(0f);

            // Add stencil control buttons vertically on the right
            var buttonTable = new Table();
            buttonTable.Defaults().Width(120f).Height(30f).Pad(5f);

            // Add top spacer to move buttons down 64 pixels
            buttonTable.Add().Height(64f);
            buttonTable.Row();

            _viewStencilsButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonViewStencils), skin, "ph-default");
            _viewStencilsButton.OnClicked += HandleViewStencilsClicked;
            buttonTable.Add(_viewStencilsButton);
            buttonTable.Row();

            _moveStencilsButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonMoveStencils), skin, "ph-default");
            _moveStencilsButton.OnClicked += HandleMoveStencilsClicked;
            buttonTable.Add(_moveStencilsButton);
            buttonTable.Row();

            _removeStencilButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonRemoveStencil), skin, "ph-default");
            _removeStencilButton.OnClicked += HandleRemoveStencilClicked;
            buttonTable.Add(_removeStencilButton);

            // Add button table to right side with left padding of 40px
            var buttonCell = inventoryContainer.Add(buttonTable).Top();
            buttonCell.SetPadRight(20f);

            container.Add(inventoryContainer).Expand().Fill();

            inventoryTab.Add(container).Expand().Fill();

            // Create stencil library panel
            _stencilLibraryPanel = new StencilLibraryPanel(skin);
            _stencilLibraryPanel.OnStencilActivated += HandleStencilActivated;
            _stencilLibraryPanel.SetVisible(false);

            UpdateStencilButtonStates();
        }

        private void HandleStencilRemovalRequested(PlacedStencil stencil)
        {
            // Show confirmation dialog immediately when stencil is clicked
            ShowRemoveStencilConfirmation(stencil);
        }

        private void HandleSynergiesChanged()
        {
            // Refresh Hero Crystal tab when synergies change
            var heroComponent = GetHeroComponent();
            if (heroComponent != null && _heroCrystalTab != null)
            {
                _heroCrystalTab.UpdateWithHero(heroComponent);
                Debug.Log("[HeroUI] Refreshed Hero Crystal tab after synergies changed");
            }

            // Refresh tooltip with fresh synergies if an item is currently hovered
            if (_itemTooltip != null && _itemTooltip.GetContainer().HasParent())
            {
                var hoveredSlot = _inventoryGrid?.GetHoveredSlot();
                if (hoveredSlot != null && hoveredSlot.SlotData.Item != null)
                {
                    var synergies = _inventoryGrid.GetSynergiesForSlot(hoveredSlot);
                    _itemTooltip.ShowItem(hoveredSlot.SlotData.Item, synergies);
                    Debug.Log($"[HeroUI] Refreshed tooltip synergies: {synergies?.Count ?? 0} synergies");
                }
            }
        }

        private void HandleViewStencilsClicked(Button button)
        {
            if (_stencilLibraryPanel != null && !_stencilLibraryPanel.IsVisible())
            {
                var gameStateService = Core.Services.GetService<GameStateService>();
                if (gameStateService != null)
                {
                    _stencilLibraryPanel.UpdateWithGameState(gameStateService, _allSynergyPatterns);
                    // Refresh to show any newly discovered stencils
                    _stencilLibraryPanel.Refresh();
                }

                _stencilLibraryPanel.ResetSelection();
                // Dock flush against the Hero window's left edge so the two never overlap;
                // fall back to its right edge when there is no room on the left
                float panelX = _heroWindow.GetX() - _stencilLibraryPanel.GetWidth();
                if (panelX < 0f)
                    panelX = _heroWindow.GetX() + _heroWindow.GetWidth();
                float panelY = _heroWindow.GetY();
                float maxY = _stage.GetHeight() - _stencilLibraryPanel.GetHeight();
                if (panelY > maxY) panelY = maxY;
                if (panelY < 0f) panelY = 0f;
                _stencilLibraryPanel.SetPosition(panelX, panelY);
                _stage.AddElement(_stencilLibraryPanel);
                _stencilLibraryPanel.SetVisible(true);
                // Stamp the frame so the same click that opened the panel can't also dismiss it
                _stencilPanelShownFrame = Time.FrameCount;
            }
        }

        /// <summary>
        /// Dismisses the stencil library panel on any click outside it. Polls global mouse state
        /// instead of using a click-consuming overlay so the same click still reaches whatever was
        /// under it (top bar buttons, tabs, other windows). Clicking View Stencils while the panel
        /// is open closes it via this path, making the button an effective toggle.
        /// </summary>
        private void DismissStencilPanelOnOutsideClick()
        {
            if (_stencilLibraryPanel == null || !_stencilLibraryPanel.IsVisible()) return;
            if (!Input.LeftMouseButtonPressed && !Input.RightMouseButtonPressed) return;
            if (Time.FrameCount == _stencilPanelShownFrame) return;

            var mousePos = _stage.GetMousePosition();
            bool insidePanel = mousePos.X >= _stencilLibraryPanel.GetX()
                && mousePos.X <= _stencilLibraryPanel.GetX() + _stencilLibraryPanel.GetWidth()
                && mousePos.Y >= _stencilLibraryPanel.GetY()
                && mousePos.Y <= _stencilLibraryPanel.GetY() + _stencilLibraryPanel.GetHeight();
            if (!insidePanel)
                _stencilLibraryPanel.SetVisible(false);
        }

        private void HandleMoveStencilsClicked(Button button)
        {
            if (_inventoryGrid != null)
            {
                // If remove mode is active, exit it first
                if (_inventoryGrid.IsRemoveStencilsModeActive())
                {
                    _inventoryGrid.SetRemoveStencilsMode(false);
                    _removeStencilButton.SetText(GetText(TextType.UI, UITextKey.ButtonRemoveStencil));
                }

                bool newMode = !_inventoryGrid.IsMoveStencilsModeActive();
                _inventoryGrid.SetMoveStencilsMode(newMode);

                // Update button appearance to show mode
                if (newMode)
                {
                    _moveStencilsButton.SetText(GetText(TextType.UI, UITextKey.ButtonExitMoveMode));
                }
                else
                {
                    _moveStencilsButton.SetText(GetText(TextType.UI, UITextKey.ButtonMoveStencils));
                }
            }
        }

        private void HandleRemoveStencilClicked(Button button)
        {
            if (_inventoryGrid != null)
            {
                // If move mode is active, exit it first
                if (_inventoryGrid.IsMoveStencilsModeActive())
                {
                    _inventoryGrid.SetMoveStencilsMode(false);
                    _moveStencilsButton.SetText(GetText(TextType.UI, UITextKey.ButtonMoveStencils));
                }

                // Check if we're currently in remove mode
                bool currentlyInRemoveMode = _inventoryGrid.IsRemoveStencilsModeActive();

                if (!currentlyInRemoveMode)
                {
                    // Entering remove mode
                    var placedStencils = _inventoryGrid.GetPlacedStencils();
                    if (placedStencils.Count == 0)
                    {
                        Debug.Log("No stencils to remove");
                        return;
                    }

                    // Activate remove mode - user must now click a stencil
                    _inventoryGrid.SetRemoveStencilsMode(true);
                    _removeStencilButton.SetText(GetText(TextType.UI, UITextKey.ButtonExitRemoveMode));
                    Debug.Log("Remove Stencils mode activated - click a stencil to remove it");
                }
                else
                {
                    // Exiting remove mode - just exit without showing any dialog
                    _inventoryGrid.SetRemoveStencilsMode(false);
                    _removeStencilButton.SetText(GetText(TextType.UI, UITextKey.ButtonRemoveStencil));
                    Debug.Log("Exited Remove Stencils mode");
                }
            }
        }

        /// <summary>Grays out Move/Remove buttons when no stencils are placed; exits any active mode.</summary>
        private void UpdateStencilButtonStates()
        {
            bool hasStencils = _inventoryGrid != null && _inventoryGrid.GetPlacedStencils().Count > 0;
            var skin = PitHeroSkin.CreateSkin();

            if (_moveStencilsButton != null)
            {
                SettingsUI.SetButtonActive(_moveStencilsButton, hasStencils, skin);
                if (!hasStencils && _inventoryGrid != null && _inventoryGrid.IsMoveStencilsModeActive())
                {
                    _inventoryGrid.SetMoveStencilsMode(false);
                    _moveStencilsButton.SetText(GetText(TextType.UI, UITextKey.ButtonMoveStencils));
                }
            }

            if (_removeStencilButton != null)
            {
                SettingsUI.SetButtonActive(_removeStencilButton, hasStencils, skin);
                if (!hasStencils && _inventoryGrid != null && _inventoryGrid.IsRemoveStencilsModeActive())
                {
                    _inventoryGrid.SetRemoveStencilsMode(false);
                    _removeStencilButton.SetText(GetText(TextType.UI, UITextKey.ButtonRemoveStencil));
                }
            }
        }

        private void ShowRemoveStencilConfirmation(PlacedStencil stencil)
        {
            var skin = PitHeroSkin.CreateSkin();
            var message = $"Remove stencil '{stencil.Pattern.Name}'?";

            var dialog = new ConfirmationDialog("Remove Stencil", message, skin,
                onYes: () =>
                {
                    _inventoryGrid.RemoveStencil(stencil);
                    Debug.Log($"Removed stencil: {stencil.Pattern.Name}");

                    // Exit remove mode after removal
                    _inventoryGrid.SetRemoveStencilsMode(false);
                    _removeStencilButton.SetText(GetText(TextType.UI, UITextKey.ButtonRemoveStencil));
                    UpdateStencilButtonStates();
                },
                onNo: () =>
                {
                    // If user cancels, stay in remove mode so they can try again
                    Debug.Log("Stencil removal cancelled");
                });

            dialog.Show(_stage);
        }

        private void ShowStencilSelectionDialog()
        {
            // For now, just remove the first stencil with confirmation
            var placedStencils = _inventoryGrid.GetPlacedStencils();
            if (placedStencils.Count > 0)
            {
                ShowRemoveStencilConfirmation(placedStencils[0]);
            }
        }

        private void HandleStencilActivated(RolePlayingFramework.Synergies.SynergyPattern pattern)
        {
            if (_inventoryGrid == null) return;

            // Prefers all-empty cells, falls back to item-occupied cells; never overlaps another stencil
            var targetAnchor = _inventoryGrid.FindFreeStencilAnchor(pattern);
            if (!targetAnchor.HasValue)
            {
                var skin = PitHeroSkin.CreateSkin();
                var dialog = new MessageDialog(GetText(TextType.UI, UITextKey.ButtonActivateStencil),
                    GetText(TextType.UI, UITextKey.StencilNoFreeSlots), skin);
                dialog.Show(_stage);
                Debug.Log($"No free stencil slots for: {pattern.Name}");
                return;
            }

            _inventoryGrid.PlaceStencil(pattern, targetAnchor.Value);
            Debug.Log($"Activated stencil {pattern.Name} at ({targetAnchor.Value.X},{targetAnchor.Value.Y})");
            UpdateStencilButtonStates();
        }

        private void PopulatePrioritiesTab(Tab prioritiesTab, Skin skin)
        {
            // Create a vertical container for all behavior content
            var container = new Table();
            container.SetFillParent(true);

            // Pit Priority section (extra top padding to clear tab buttons)
            var pitPriorityLabel = new HoverableLabel("Pit Priority", skin, "ph-default", GetText(TextType.UI, UITextKey.BehaviorPitPriorityTooltip), _stage);
            container.Add(pitPriorityLabel).SetAlign(Align.Left).SetPadTop(172f).SetPadBottom(5f);
            container.Row();

            InitializePriorityItems();
            _priorityList = new ReorderableTableList<string>(skin, _priorityItems, OnPriorityReordered);
            container.Add(_priorityList).SetExpandX().SetFillX().SetPadBottom(15f);
            container.Row();

            // Heal Priority section
            var healPriorityLabel = new HoverableLabel("Heal Priority", skin, "ph-default", GetText(TextType.UI, UITextKey.BehaviorHealPriorityTooltip), _stage);
            container.Add(healPriorityLabel).SetAlign(Align.Left).SetPadBottom(5f);
            container.Row();

            InitializeHealPriorityItems();
            _healPriorityList = new ReorderableTableList<string>(skin, _healPriorityItems, OnHealPriorityReordered);
            container.Add(_healPriorityList).SetExpandX().SetFillX().SetPadBottom(15f);
            container.Row();

            // Battle Tactics section
            var tacticsLabel = new Label("Battle Tactics", skin, "ph-default");
            container.Add(tacticsLabel).SetAlign(Align.Left).SetPadBottom(5f);
            container.Row();

            _battleTacticButtonGroup = new ButtonGroup();

            _blitzButton = new CheckBox("Blitz", skin, "ph-radio");
            _strategicButton = new CheckBox("Strategic", skin, "ph-radio");
            _defensiveButton = new CheckBox("Defensive", skin, "ph-radio");

            _battleTacticButtonGroup.Add(_blitzButton);
            _battleTacticButtonGroup.Add(_strategicButton);
            _battleTacticButtonGroup.Add(_defensiveButton);

            var blitzTable = new Table();
            blitzTable.Add(_blitzButton).Left();
            blitzTable.Row();
            blitzTable.Add(new Label(GetText(TextType.UI, UITextKey.BehaviorTacticBlitzDesc), skin, "ph-default")).Left().SetPadLeft(20);
            container.Add(blitzTable).Left().SetPadBottom(8);
            container.Row();

            var strategicTable = new Table();
            strategicTable.Add(_strategicButton).Left();
            strategicTable.Row();
            strategicTable.Add(new Label(GetText(TextType.UI, UITextKey.BehaviorTacticStrategicDesc), skin, "ph-default")).Left().SetPadLeft(20);
            container.Add(strategicTable).Left().SetPadBottom(8);
            container.Row();

            var defensiveTable = new Table();
            defensiveTable.Add(_defensiveButton).Left();
            defensiveTable.Row();
            defensiveTable.Add(new Label(GetText(TextType.UI, UITextKey.BehaviorTacticDefensiveDesc), skin, "ph-default")).Left().SetPadLeft(20);
            container.Add(defensiveTable).Left().SetPadBottom(15);
            container.Row();

            // Default to Strategic
            _strategicButton.IsChecked = true;

            // Wire up battle tactic events
            _blitzButton.OnChanged += (isChecked) =>
            {
                if (isChecked)
                {
                    var heroComp = GetHeroComponent();
                    if (heroComp != null) heroComp.CurrentBattleTactic = BattleTactic.Blitz;
                }
            };
            _strategicButton.OnChanged += (isChecked) =>
            {
                if (isChecked)
                {
                    var heroComp = GetHeroComponent();
                    if (heroComp != null) heroComp.CurrentBattleTactic = BattleTactic.Strategic;
                }
            };
            _defensiveButton.OnChanged += (isChecked) =>
            {
                if (isChecked)
                {
                    var heroComp = GetHeroComponent();
                    if (heroComp != null) heroComp.CurrentBattleTactic = BattleTactic.Defensive;
                }
            };

            // Consumable Options section
            var consumableLabel = new Label("Consumable Options", skin, "ph-default");
            container.Add(consumableLabel).SetAlign(Align.Left).SetPadBottom(5f);
            container.Row();

            _useConsumablesOnMercsCheckBox = new CheckBox("Use consumable items on mercenaries", skin, "ph-default");
            _useConsumablesOnMercsCheckBox.IsChecked = true;
            _useConsumablesOnMercsCheckBox.OnChanged += (isChecked) =>
            {
                var heroComp = GetHeroComponent();
                if (heroComp != null) heroComp.UseConsumablesOnMercenaries = isChecked;
            };
            container.Add(_useConsumablesOnMercsCheckBox).Left().SetPadBottom(8);
            container.Row();

            _mercsCanUseConsumablesCheckBox = new CheckBox("Mercenaries can use consumable items", skin, "ph-default");
            _mercsCanUseConsumablesCheckBox.IsChecked = true;
            _mercsCanUseConsumablesCheckBox.OnChanged += (isChecked) =>
            {
                var heroComp = GetHeroComponent();
                if (heroComp != null) heroComp.MercenariesCanUseConsumables = isChecked;
            };
            container.Add(_mercsCanUseConsumablesCheckBox).Left().SetPadBottom(15);
            container.Row();

            // Wrap in scroll pane so all content is accessible
            var scrollPane = new ScrollPane(container, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);

            prioritiesTab.Add(scrollPane).Expand().Fill().Pad(15f);
        }

        private void PopulateCrystalTab(Tab crystalTab, Skin skin)
        {
            _heroCrystalTab = new HeroCrystalTab();
            var content = _heroCrystalTab.CreateContent(skin, _stage);
            crystalTab.Add(content).Expand().Fill();
        }

        /// <summary>Populates the Crystals collection tab with the CrystalsTab component.</summary>
        private void PopulateCrystalsCollectionTab(Tab tab, Skin skin)
        {
            _crystalsTabComponent = new CrystalsTab();
            var content = _crystalsTabComponent.CreateContent(skin, _stage, _heroWindow);
            tab.Add(content).Expand().Fill();
        }

        private void PopulateMercenariesTab(Tab mercenariesTab, Skin skin)
        {
            _mercenariesTabComponent = new MercenariesTab();
            _mercenariesTabComponent.OnDismissRequested += OnMercenaryDismissRequested;
            var content = _mercenariesTabComponent.CreateContent(skin, _stage);
            mercenariesTab.Add(content).Expand().Fill();
        }

        private void PopulateFoodTab(Tab foodTab, Skin skin)
        {
            _foodTabComponent = new FoodTab();
            var content = _foodTabComponent.CreateContent(skin, _stage);
            foodTab.Add(content).Expand().Fill();
        }

        private void InitializePriorityItems()
        {
            if (_priorityItems == null) _priorityItems = new List<string>(3); else _priorityItems.Clear();
            var hero = GetHeroComponent();
            if (hero != null)
            {
                var priorities = hero.GetPrioritiesInOrder();
                _priorityItems.Add(priorities[0].ToString());
                _priorityItems.Add(priorities[1].ToString());
                _priorityItems.Add(priorities[2].ToString());
            }
            else
            {
                _priorityItems.Add(HeroPitPriority.Treasure.ToString());
                _priorityItems.Add(HeroPitPriority.Battle.ToString());
                _priorityItems.Add(HeroPitPriority.Advance.ToString());
            }
        }

        private void OnPriorityReordered(int from, int to, string item)
        {
            Debug.Log($"Priority reordered: {item} moved from position {from + 1} to {to + 1}");
            UpdateHeroPriorities();
        }

        private void InitializeHealPriorityItems()
        {
            if (_healPriorityItems == null) _healPriorityItems = new List<string>(3); else _healPriorityItems.Clear();
            var hero = GetHeroComponent();
            if (hero != null)
            {
                var healPriorities = hero.GetHealPrioritiesInOrder();
                _healPriorityItems.Add(healPriorities[0].ToString());
                _healPriorityItems.Add(healPriorities[1].ToString());
                _healPriorityItems.Add(healPriorities[2].ToString());
            }
            else
            {
                _healPriorityItems.Add(HeroHealPriority.HealingItem.ToString());
                _healPriorityItems.Add(HeroHealPriority.HealingSkill.ToString());
                _healPriorityItems.Add(HeroHealPriority.Inn.ToString());
            }
        }

        private void OnHealPriorityReordered(int from, int to, string item)
        {
            Debug.Log($"Heal priority reordered: {item} moved from position {from + 1} to {to + 1}");
            UpdateHeroHealPriorities();
            UpdateHealActionCosts();
        }

        private void UpdateHeroHealPriorities()
        {
            var hero = GetHeroComponent();
            if (hero == null) return;

            var healPriorities = new HeroHealPriority[3];
            for (int i = 0; i < 3; i++)
            {
                if (System.Enum.TryParse<HeroHealPriority>(_healPriorityItems[i], out var priority))
                {
                    healPriorities[i] = priority;
                }
            }

            hero.SetHealPrioritiesInOrder(healPriorities);
            Debug.Log($"[HeroUI] Updated heal priorities: {healPriorities[0]}, {healPriorities[1]}, {healPriorities[2]}");
        }

        private void UpdateHealActionCosts()
        {
            // Find the HeroStateMachine to update action costs
            var scene = Core.Scene;
            if (scene == null) return;

            var heroEntity = scene.FindEntity("hero");
            if (heroEntity == null) return;

            var stateMachine = heroEntity.GetComponent<AI.HeroStateMachine>();
            if (stateMachine != null)
            {
                stateMachine.UpdateHealingActionCosts();
            }
        }

        private void ToggleHeroWindow()
        {
            if (_heroWindow == null) return;
            _windowVisible = !_windowVisible;
            if (_windowVisible)
            {
                UIWindowManager.OnUIWindowOpening();
                _itemTooltip?.InvalidateCache();
                InitializePriorityItems();
                _priorityList?.Rebuild();
                InitializeHealPriorityItems();
                _healPriorityList?.Rebuild();
                RefreshBehaviorUIFromHero();
                var heroComponent = GetHeroComponent();
                
                // Don't open UI if hero is dead or dying (has death component or HP <= 0)
                if (heroComponent != null)
                {
                    var deathComponent = heroComponent.Entity.GetComponent<HeroDeathComponent>();
                    bool isDying = deathComponent != null;
                    bool isDead = heroComponent.LinkedHero?.CurrentHP <= 0;
                    
                    if (isDying || isDead)
                    {
                        Debug.Log("[HeroUI] Cannot open Hero UI - hero is dead or dying");
                        _windowVisible = false;
                        UIWindowManager.OnUIWindowClosing();
                        return;
                    }
                    
                    // Always reconnect to hero to refresh inventory (in case hero died and items were cleared)
                    if (_inventoryGrid != null)
                    {
                        _inventoryGrid.ConnectToHero(heroComponent);
                        RefreshMercenaryEquipSlots();
                        UpdateStencilButtonStates();
                    }
                }
                else
                {
                    // No hero found - cannot open UI
                    Debug.Log("[HeroUI] Cannot open Hero UI - no hero found");
                    _windowVisible = false;
                    UIWindowManager.OnUIWindowClosing();
                    return;
                }

                // Update Hero Info tab with current hero
                if (heroComponent != null && _heroCrystalTab != null)
                {
                    _heroCrystalTab.UpdateWithHero(heroComponent);

                    // Update hero sprite preview using design service
                    var designService = Core.Services?.GetService<HeroDesignService>();
                    if (designService != null && designService.HasDesign)
                    {
                        var design = designService.GetDesign();
                        _heroCrystalTab.UpdateHeroPreview(design.SkinColor, design.HairColor, design.ShirtColor, design.HairstyleIndex);
                    }
                }

                // Update Mercenaries tab
                RefreshMercenariesTab();

                PositionHeroWindow();
                _stage.AddElement(_heroWindow);
                _heroWindow.SetVisible(true);
                _heroWindow.ToFront();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null) pauseService.IsPaused = true;
                Debug.Log("Hero window opened and game paused");
            }
            else
            {
                UIWindowManager.OnUIWindowClosing();
                _selectedItemCard?.Hide();
                _crystalsTabComponent?.Cleanup();
                _stencilLibraryPanel?.SetVisible(false);
                _heroWindow.SetVisible(false);
                _heroWindow.Remove();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null) pauseService.IsPaused = false;
                Debug.Log("Hero window closed and game unpaused");
            }
        }

        private void PositionHeroWindow()
        {
            if (_heroWindow == null || _heroButton == null) return;
            _heroWindow.Validate();
            float heroX = _heroButton.GetX();
            float heroY = _heroButton.GetY();
            float heroW = _heroButton.GetWidth();
            float winW = _heroWindow.GetWidth();
            float winH = _heroWindow.GetHeight();
            const float padding = 4f; float targetX = heroX + heroW + padding; float targetY = heroY + padding;
            float stageW = _stage.GetWidth(); float stageH = _stage.GetHeight();
            if (targetX + winW > stageW) targetX = heroX - padding - winW;
            if (targetX < 0) targetX = 0; if (targetY < 0) targetY = 0; if (targetY + winH > stageH) targetY = stageH - winH;
            _heroWindow.SetPosition(targetX, targetY);
        }

        private HeroComponent GetHeroComponent()
        {
            var heroEntity = Core.Scene?.FindEntity("hero");
            return heroEntity?.GetComponent<HeroComponent>();
        }

        /// <summary>Gathers hired mercenaries and refreshes their equip slots in the inventory grid.</summary>
        private void RefreshMercenaryEquipSlots()
        {
            if (_inventoryGrid == null) return;

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

            _inventoryGrid.RefreshMercenarySlots(hiredMercs);
        }

        /// <summary>Gathers hired mercenaries and refreshes the Mercenaries tab.</summary>
        private void RefreshMercenariesTab()
        {
            if (_mercenariesTabComponent == null) return;

            var mercManager = Core.Services?.GetService<MercenaryManager>();
            List<Mercenary> hiredMercs = null;
            List<MercenaryAppearance> appearances = null;
            List<Entity> hiredEntities = null;

            if (mercManager != null)
            {
                var entities = mercManager.GetHiredMercenaries();
                if (entities != null && entities.Count > 0)
                {
                    hiredEntities = entities;
                    hiredMercs = new List<Mercenary>(entities.Count);
                    appearances = new List<MercenaryAppearance>(entities.Count);
                    for (int i = 0; i < entities.Count; i++)
                    {
                        var mc = entities[i].GetComponent<MercenaryComponent>();
                        if (mc?.LinkedMercenary != null)
                        {
                            hiredMercs.Add(mc.LinkedMercenary);
                            appearances.Add(new MercenaryAppearance
                            {
                                SkinColor = mc.SkinColor,
                                HairColor = mc.HairColor,
                                HairstyleIndex = mc.HairstyleIndex,
                                ShirtColor = mc.ShirtColor
                            });
                        }
                    }
                }
            }

            _mercenariesTabComponent.UpdateWithMercenaries(hiredMercs, appearances, hiredEntities);
        }

        /// <summary>Shows a Yes/No confirmation dialog for dismissing a hired mercenary.</summary>
        private void OnMercenaryDismissRequested(Entity mercEntity)
        {
            if (mercEntity == null) return;

            var textService = Core.Services?.GetService<TextService>();
            if (textService == null) return;

            var mc = mercEntity.GetComponent<MercenaryComponent>();
            var mercName = mc?.LinkedMercenary?.Name ?? "this mercenary";

            var title = textService.DisplayText(TextType.UI, UITextKey.DialogConfirmDismissMercenary);
            var message = string.Format(textService.DisplayText(TextType.UI, UITextKey.ConfirmDismissMercenaryMessage), mercName);
            var skin = PitHeroSkin.CreateSkin();

            var dialog = new ConfirmationDialog(title, message, skin, onYes: () =>
            {
                var mercManager = Core.Services?.GetService<MercenaryManager>();
                mercManager?.DismissPartyMercenary(mercEntity);
                RefreshMercenariesTab();
                RefreshMercenaryEquipSlots();
                // Notify inventory grid that items were added to the bag so they appear immediately
                InventorySelectionManager.OnInventoryChanged?.Invoke();
            });

            dialog.Show(_stage);
        }

        /// <summary>Refreshes battle tactic radio buttons and consumable checkboxes from HeroComponent state.</summary>
        private void RefreshBehaviorUIFromHero()
        {
            var heroComp = GetHeroComponent();
            if (heroComp == null) return;

            // Refresh battle tactic radio buttons
            switch (heroComp.CurrentBattleTactic)
            {
                case BattleTactic.Blitz:
                    _blitzButton.IsChecked = true;
                    break;
                case BattleTactic.Strategic:
                    _strategicButton.IsChecked = true;
                    break;
                case BattleTactic.Defensive:
                    _defensiveButton.IsChecked = true;
                    break;
            }

            // Refresh consumable option checkboxes
            _useConsumablesOnMercsCheckBox.IsChecked = heroComp.UseConsumablesOnMercenaries;
            _mercsCanUseConsumablesCheckBox.IsChecked = heroComp.MercenariesCanUseConsumables;
        }

        private void UpdateHeroPriorities()
        {
            var hero = GetHeroComponent();
            if (hero == null) { Debug.Log("Could not find hero component to update priorities"); return; }
            var newPriorities = new HeroPitPriority[3];
            for (int i = 0; i < _priorityItems.Count && i < 3; i++)
            {
                if (System.Enum.TryParse(_priorityItems[i], out HeroPitPriority priority)) newPriorities[i] = priority; else { Debug.Log($"Failed to parse priority: {_priorityItems[i]}"); return; }
            }
            hero.SetPrioritiesInOrder(newPriorities);
            Debug.Log($"Updated hero priorities: {newPriorities[0]}, {newPriorities[1]}, {newPriorities[2]}");
        }

        /// <summary>Update button style based on shrink mode</summary>
        public void UpdateButtonStyleIfNeeded()
        {
            HeroMode desired;
            if (WindowManager.IsHalfHeightMode())
                desired = HeroMode.Half;
            else
                desired = HeroMode.Normal;

            if (desired == _currentHeroMode)
                return;

            switch (desired)
            {
                case HeroMode.Normal:
                    _heroButton.SetStyle(_heroNormalStyle);
                    _heroButton.SetSize(((SpriteDrawable)_heroNormalStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case HeroMode.Half:
                    _heroButton.SetStyle(_heroHalfStyle);
                    _heroButton.SetSize(((SpriteDrawable)_heroHalfStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentHeroMode = desired;
            _styleChanged = true;
        }

        public void SetPosition(float x, float y) => _heroButton?.SetPosition(x, y);

        /// <summary>Enables/disables hit-testing; disabled while the top UI bar is hidden off-screen.</summary>
        public void SetTouchable(Touchable touchable) => _heroButton?.SetTouchable(touchable);
        public float GetX() => _heroButton?.GetX() ?? 0f;
        public float GetY() => _heroButton?.GetY() ?? 0f;
        public float GetWidth() => _heroButton?.GetWidth() ?? 0f;
        public float GetHeight() => _heroButton?.GetHeight() ?? 0f;
        public bool ConsumeStyleChangedFlag() { if (_styleChanged) { _styleChanged = false; return true; } return false; }

        /// <summary>Main update</summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();

            // Note: Keyboard shortcuts are now handled by ShortcutBar in MainGameScene

            // Safety net: the equip preview only exists while dragging (covers cancel paths and force-close)
            if (_equipPreviewTooltip != null && !InventoryDragManager.IsDragging
                && _equipPreviewTooltip.GetContainer().HasParent())
                _equipPreviewTooltip.GetContainer().Remove();

            if (_windowVisible && _inventoryGrid != null)
            {

                // Update tooltip position if visible
                if (_itemTooltip != null && _itemTooltip.GetContainer().HasParent())
                {
                    var mousePos = _stage.GetMousePosition();
                    var tooltipContainer = _itemTooltip.GetContainer();
                    tooltipContainer.Validate(); // Ensure size is calculated
                    
                    float tooltipX = mousePos.X + 10;
                    float tooltipY = mousePos.Y + 10;
                    
                    // Clamp Y to prevent tooltip from bleeding off bottom of screen
                    float stageHeight = _stage.GetHeight();
                    float tooltipHeight = tooltipContainer.GetHeight();
                    tooltipY = Mathf.Clamp(tooltipY, 0, stageHeight - tooltipHeight);
                    
                    tooltipContainer.SetPosition(tooltipX, tooltipY);
                }

                // Update hero crystal tab tooltip
                if (_heroCrystalTab != null)
                    _heroCrystalTab.Update();

                // Update crystals collection tab hover check
                _crystalsTabComponent?.Update();

                // Dismiss stencil library panel on outside click
                DismissStencilPanelOnOutsideClick();

                // Periodic hover check — safety net for missed hover events
                _hoverCheckFrame++;
                if (_hoverCheckFrame % 5 == 0)
                    PerformPeriodicHoverCheck();
            }
        }

        /// <summary>Periodic safety-net hover check: ensures the item tooltip appears if the mouse is
        /// inside a slot but the hover event was not delivered. Runs every 5 frames.</summary>
        private void PerformPeriodicHoverCheck()
        {
            if (_itemTooltip == null || _inventoryGrid == null) return;
            if (_itemTooltip.GetContainer().HasParent()) return;

            var mousePos = _stage.GetMousePosition();
            var slot = _inventoryGrid.GetSlotAtStagePosition(mousePos);
            if (slot != null && slot.SlotData.Item != null)
                HandleItemHovered(slot.SlotData.Item, slot);
        }

        public bool IsWindowVisible => _windowVisible;

        /// <summary>Gets the inventory grid reference for shortcut bar integration.</summary>
        public InventoryGrid GetInventoryGrid() => _inventoryGrid;

        /// <summary>Gets the hero crystal tab reference for UI reconnection.</summary>
        public HeroCrystalTab GetCrystalTab() => _heroCrystalTab;

        /// <summary>Force close window</summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false; UIWindowManager.OnUIWindowClosing(); _selectedItemCard?.Hide(); _crystalsTabComponent?.Cleanup(); _stencilLibraryPanel?.SetVisible(false); _heroWindow?.SetVisible(false); _heroWindow?.Remove(); var pauseService = Core.Services.GetService<PauseService>(); if (pauseService != null) pauseService.IsPaused = false; Debug.Log("[HeroUI] Hero window force closed by single window policy");
            }
        }

        private void HandleItemHovered(IItem item, InventorySlot slot)
        {
            // Suppress hover tooltips during drags (PerformPeriodicHoverCheck bypasses hover events)
            if (item == null || InventoryDragManager.IsDragging) return;

            // Hovering the item acknowledges new gear and stops its sparkle
            UnviewedGearTracker.MarkViewed(item);

            // Get synergies for the hovered slot (passed directly, no search needed)
            var synergies = slot != null ? _inventoryGrid?.GetSynergiesForSlot(slot) : null;

            // Show tooltip with item info and synergies immediately
            _itemTooltip.ShowItem(item, synergies);
            if (_itemTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_itemTooltip.GetContainer());
            }

            // Position tooltip at mouse cursor with clamping
            var mousePos = _stage.GetMousePosition();
            var tooltipContainer = _itemTooltip.GetContainer();
            tooltipContainer.Validate(); // Ensure size is calculated
            
            float tooltipX = mousePos.X + 10;
            float tooltipY = mousePos.Y + 10;
            
            // Clamp Y to prevent tooltip from bleeding off bottom of screen
            float stageHeight = _stage.GetHeight();
            float tooltipHeight = tooltipContainer.GetHeight();
            if (tooltipY + tooltipHeight > stageHeight)
            {
                tooltipY = stageHeight - tooltipHeight;
            }
            if (tooltipY < 0) tooltipY = 0;          
            
            tooltipContainer.SetPosition(tooltipX, tooltipY);
            tooltipContainer.ToFront();
        }

        private void HandleItemUnhovered()
        {
            // Hide tooltip when no item is hovered
            if (!(_inventoryGrid != null && _inventoryGrid.HasAnyHoveredSlot()))
            {
                _itemTooltip.GetContainer().Remove();
            }
        }

        /// <summary>Shows/hides the equip preview beside the drag's current valid equip target slot.</summary>
        private void HandleDragEquipTargetChanged(InventorySlot targetSlot, IGear draggedGear)
        {
            if (targetSlot == null || draggedGear == null)
            {
                _equipPreviewTooltip?.GetContainer().Remove();
                return;
            }

            var equippedGear = targetSlot.SlotData.Item as IGear; // null = empty slot, all bonuses show as gains
            _equipPreviewTooltip.ShowComparison(draggedGear, equippedGear);

            if (!_equipPreviewTooltip.HasChanges())
            {
                _equipPreviewTooltip.GetContainer().Remove();
                return;
            }

            var container = _equipPreviewTooltip.GetContainer();
            if (container.GetParent() == null)
                _stage.AddElement(container);
            container.Validate();

            // Anchor beside the target slot; flip to the left side if overflowing the right edge, clamp Y
            var slotTopLeft = targetSlot.LocalToStageCoordinates(Vector2.Zero);
            const float pad = 4f;
            float x = slotTopLeft.X + targetSlot.GetWidth() + pad;
            if (x + container.GetWidth() > _stage.GetWidth())
                x = slotTopLeft.X - container.GetWidth() - pad;
            float y = Mathf.Clamp(slotTopLeft.Y, 0f, _stage.GetHeight() - container.GetHeight());
            container.SetPosition(x, y);
            container.ToFront();
        }

        private void HandleItemSelected(IItem item)
        {
            if (item == null) return;
            _selectedItemCard.ShowItem(item);
            if (_selectedItemCard.GetParent() == null) _stage.AddElement(_selectedItemCard);
            PositionItemCards();
        }

        private void HandleItemDeselected()
        {
            _selectedItemCard.Hide();
            PositionItemCards();
        }

        private void PositionItemCards()
        {
            if (_heroWindow == null) return;
            float heroWindowRight = _heroWindow.GetX() + _heroWindow.GetWidth();
            float heroWindowY = _heroWindow.GetY();
            float cardSpacing = 10f;
            if (_selectedItemCard.IsVisible())
            {
                _selectedItemCard.SetPosition(heroWindowRight + cardSpacing, heroWindowY);
                _selectedItemCard.ToFront();
            }
        }

        /// <summary>
        /// Triggers the hero button click handler (single window policy + toggle).
        /// </summary>
        public void TriggerToggle()
        {
            HandleHeroButtonClick();
        }

        /// <summary>
        /// Switches the TabPane to the given tab and notifies HandleTabChanged.
        /// </summary>
        private void SwitchToTab(Tab targetTab)
        {
            if (_tabPane == null || targetTab == null) return;
            var index = _tabPane.Tabs.IndexOf(targetTab);
            if (index < 0) return;
            _tabPane.SetActiveTab(index);
            HandleTabChanged(targetTab);
        }

        /// <summary>
        /// Opens the hero window if it is closed, then switches to the given tab.
        /// </summary>
        private void OpenAndSwitchToTab(Tab targetTab)
        {
            if (!_windowVisible)
                HandleHeroButtonClick();
            if (_windowVisible)
                SwitchToTab(targetTab);
        }

        /// <summary>Opens the hero window to the Inventory tab.</summary>
        public void OpenToInventoryTab() => OpenAndSwitchToTab(_inventoryTab);

        /// <summary>Opens the hero window to the Hero Info tab.</summary>
        public void OpenToHeroInfoTab() => OpenAndSwitchToTab(_crystalTab);

        /// <summary>Opens the hero window to the Behavior tab.</summary>
        public void OpenToBehaviorTab() => OpenAndSwitchToTab(_prioritiesTab);
    }
}