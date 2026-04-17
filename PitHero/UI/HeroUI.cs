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
        private bool _windowVisible = false;

        // Reference to SettingsUI for single window policy enforcement
        private SettingsUI _settingsUI;

        // Inventory tab content
        private InventoryGrid _inventoryGrid;
        private TextButton _viewStencilsButton;
        private TextButton _moveStencilsButton;
        private TextButton _removeStencilButton;

        // Stencil system
        private StencilLibraryPanel _stencilLibraryPanel;
        private List<RolePlayingFramework.Synergies.SynergyPattern> _allSynergyPatterns;

        // Item card for selection only (hover uses tooltip)
        private ItemCard _selectedItemCard;

        // Tooltip for hovering over items
        private ItemCardTooltip _itemTooltip;
        
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

        // Auto-equip option controls
        private CheckBox _autoEquipHeroCheckBox;
        private CheckBox _autoEquipMercsCheckBox;

        // Hero Crystal tab component
        private HeroCrystalTab _heroCrystalTab;
        
        // Crystals Collection tab component
        private CrystalsTab _crystalsTabComponent;
        private Tab _crystalsCollectionTab;

        // Mercenaries tab component
        private MercenariesTab _mercenariesTabComponent;

        private const float HERO_WINDOW_WIDTH = 870f;

        public HeroUI()
        {
            // Initialize all synergy patterns - must include ALL patterns for proper sprite display
            _allSynergyPatterns = new List<RolePlayingFramework.Synergies.SynergyPattern>();

            // Knight patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateHolyStrike());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateIaidoSlash());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateShadowSlash());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateSpellblade());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateArmorMastery());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateSwordProficiency());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateGuardiansResolve());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateBerserkerRage());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateShieldMastery());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.KnightSynergyPatterns.CreateHeavyFortification());

            // Mage patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateMeteor());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateShadowBolt());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateElementalVolley());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateBlitz());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateArcaneFocus());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateElementalMastery());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateSpellWeaving());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateManaConvergence());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MageSynergyPatterns.CreateRodFocus());

            // Priest patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateAuraHeal());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreatePurify());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateSacredStrike());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateLifeLeech());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateDivineProtection());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateHealingAmplification());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateHolyAura());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateSanctifiedMind());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.PriestSynergyPatterns.CreateDivineVestments());

            // Monk patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateDragonClaw());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateEnergyBurst());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateDragonKick());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateSneakPunch());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateIronFist());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateMartialFocus());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateKiMastery());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateEvasionTraining());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.MonkSynergyPatterns.CreateBalanceTraining());

            // Thief patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateSmokeBomb());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreatePoisonArrow());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateFade());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateKiCloak());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateShadowStep());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateLockpicking());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateTrapMastery());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ThiefSynergyPatterns.CreateAssassinsEdge());

            // Archer patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreatePiercingArrow());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateLightshot());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateKiArrow());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateArrowFlurry());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateMarksman());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateSharpAim());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateRangersPath());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ArcherSynergyPatterns.CreateWindArcher());

            // Cross-class patterns
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateSacredBlade());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateFlashStrike());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateSoulWard());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateDragonBolt());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateElementalStorm());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateBattleMage());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateHolyWarrior());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateShadowMaster());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateArcaneProtector());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.CrossClassSynergyPatterns.CreateElementalChampion());
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

            _heroButton = new HoverableImageButton(_heroNormalStyle, "Hero");
            _heroButton.SetSize(heroSprite.SourceRect.Width, heroSprite.SourceRect.Height);
            _heroButton.OnClicked += (button) => HandleHeroButtonClick();
        }

        /// <summary>Sets the reference to SettingsUI for single window policy enforcement.</summary>
        public void SetSettingsUI(SettingsUI settingsUI) { _settingsUI = settingsUI; }

        private void HandleHeroButtonClick()
        {
            // Properly close Settings UI if it's open (single window policy)
            _settingsUI?.ForceCloseSettings();
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
            
            // Add Crystals Collection tab after Hero Info tab
            _crystalsCollectionTab = new Tab(GetText(TextType.UI, UITextKey.TabCrystals), tabStyle);
            PopulateCrystalsCollectionTab(_crystalsCollectionTab, skin);
            _tabPane.AddTab(_crystalsCollectionTab);
            
            _tabPane.AddTab(_mercenariesTab);
            _tabPane.AddTab(_prioritiesTab);
            
            // Hook into tab button clicks to adjust window width
            for (int i = 0; i < _tabPane.TabButtons.Count; i++)
            {
                var tabButton = _tabPane.TabButtons[i];
                var tab = _tabPane.Tabs[i];
                tabButton.OnClick += () => HandleTabChanged(tab);
            }
            
            _heroWindow.Add(_tabPane).Expand().Fill().Pad(0); // No cell padding - tabs flush with window edges
            _heroWindow.SetVisible(false);
        }

        /// <summary>Adjusts window width when tabs are changed.</summary>
        private void HandleTabChanged(Tab selectedTab)
        {
            if (_heroWindow == null) return;

            float newWidth;
            if (selectedTab == _inventoryTab)
            {
                // Inventory tab needs full width for 20-column grid
                newWidth = HERO_WINDOW_WIDTH;
            }
            else if (selectedTab == _crystalsCollectionTab)
            {
                // Crystals tab needs extra width so all 5 tab buttons fit with ≥23px side padding
                newWidth = 490f;
                // Refresh crystal slots so any crystals loaded from save are visible
                _crystalsTabComponent?.RefreshAll();
            }
            else
            {
                // All other tabs use the same width as Crystals tab so the window looks consistent
                newWidth = 490f;
            }

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
            _inventoryGrid.OnItemHovered += HandleItemHovered;
            _inventoryGrid.OnItemUnhovered += HandleItemUnhovered;
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

            _viewStencilsButton = new TextButton("View Stencils", skin, "ph-default");
            _viewStencilsButton.OnClicked += HandleViewStencilsClicked;
            buttonTable.Add(_viewStencilsButton);
            buttonTable.Row();

            _moveStencilsButton = new TextButton("Move Stencils", skin, "ph-default");
            _moveStencilsButton.OnClicked += HandleMoveStencilsClicked;
            buttonTable.Add(_moveStencilsButton);
            buttonTable.Row();

            _removeStencilButton = new TextButton("Remove Stencil", skin, "ph-default");
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

                _stencilLibraryPanel.SetPosition(100f, 100f);
                _stage.AddElement(_stencilLibraryPanel);
                _stencilLibraryPanel.SetVisible(true);
            }
        }

        private void HandleMoveStencilsClicked(Button button)
        {
            if (_inventoryGrid != null)
            {
                // If remove mode is active, exit it first
                if (_inventoryGrid.IsRemoveStencilsModeActive())
                {
                    _inventoryGrid.SetRemoveStencilsMode(false);
                    _removeStencilButton.SetText("Remove Stencil");
                }

                bool newMode = !_inventoryGrid.IsMoveStencilsModeActive();
                _inventoryGrid.SetMoveStencilsMode(newMode);

                // Update button appearance to show mode
                if (newMode)
                {
                    _moveStencilsButton.SetText("Exit Move Mode");
                }
                else
                {
                    _moveStencilsButton.SetText("Move Stencils");
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
                    _moveStencilsButton.SetText("Move Stencils");
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
                    _removeStencilButton.SetText("Exit Remove Mode");
                    Debug.Log("Remove Stencils mode activated - click a stencil to remove it");
                }
                else
                {
                    // Exiting remove mode - just exit without showing any dialog
                    _inventoryGrid.SetRemoveStencilsMode(false);
                    _removeStencilButton.SetText("Remove Stencil");
                    Debug.Log("Exited Remove Stencils mode");
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
                    _removeStencilButton.SetText("Remove Stencil");
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
            if (_inventoryGrid != null)
            {
                // Try to find the first empty inventory slot (row 2+, any column)
                Point? targetAnchor = FindFirstEmptyInventorySlot();

                if (!targetAnchor.HasValue)
                {
                    // No empty slots found, use default position (top-left of inventory area, row 3)
                    targetAnchor = new Point(0, 3);
                    Debug.Log($"No empty inventory slots found, placing stencil at default position: {targetAnchor.Value}");
                }
                else
                {
                    Debug.Log($"Found empty inventory slot at ({targetAnchor.Value.X},{targetAnchor.Value.Y}) for stencil placement");
                }

                _inventoryGrid.PlaceStencil(pattern, targetAnchor.Value);
                Debug.Log($"Activated stencil: {pattern.Name}");
            }
        }

        /// <summary>Finds the first empty inventory slot in the grid.</summary>
        private Point? FindFirstEmptyInventorySlot()
        {
            if (_inventoryGrid == null) return null;

            // Get the available slot from the inventory grid
            var availableSlot = _inventoryGrid.FindNextAvailableSlot();
            if (availableSlot != null)
            {
                return new Point(availableSlot.X, availableSlot.Y);
            }

            return null;
        }

        private void PopulatePrioritiesTab(Tab prioritiesTab, Skin skin)
        {
            // Create a vertical container for all behavior content
            var container = new Table();
            container.SetFillParent(true);

            // Pit Priority section (extra top padding to clear tab buttons)
            var pitPriorityLabel = new Label("Pit Priority", skin, "ph-default");
            container.Add(pitPriorityLabel).SetAlign(Align.Left).SetPadTop(90f).SetPadBottom(5f);
            container.Row();

            InitializePriorityItems();
            _priorityList = new ReorderableTableList<string>(skin, _priorityItems, OnPriorityReordered);
            container.Add(_priorityList).SetExpandX().SetFillX().SetPadBottom(15f);
            container.Row();

            // Heal Priority section
            var healPriorityLabel = new Label("Heal Priority", skin, "ph-default");
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

            _blitzButton = new CheckBox("Blitz", skin, "ph-default");
            _strategicButton = new CheckBox("Strategic", skin, "ph-default");
            _defensiveButton = new CheckBox("Defensive", skin, "ph-default");

            _battleTacticButtonGroup.Add(_blitzButton);
            _battleTacticButtonGroup.Add(_strategicButton);
            _battleTacticButtonGroup.Add(_defensiveButton);

            var blitzTable = new Table();
            blitzTable.Add(_blitzButton).Left();
            blitzTable.Row();
            blitzTable.Add(new Label("  Max damage, ignore healing", skin, "ph-default")).Left().SetPadLeft(20);
            container.Add(blitzTable).Left().SetPadBottom(8);
            container.Row();

            var strategicTable = new Table();
            strategicTable.Add(_strategicButton).Left();
            strategicTable.Row();
            strategicTable.Add(new Label("  Balanced attacks and healing", skin, "ph-default")).Left().SetPadLeft(20);
            container.Add(strategicTable).Left().SetPadBottom(8);
            container.Row();

            var defensiveTable = new Table();
            defensiveTable.Add(_defensiveButton).Left();
            defensiveTable.Row();
            defensiveTable.Add(new Label("  Prioritize survival, heal at 60%", skin, "ph-default")).Left().SetPadLeft(20);
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

            // Auto Equip Options section
            var autoEquipLabel = new Label("Auto Equip Options", skin, "ph-default");
            container.Add(autoEquipLabel).SetAlign(Align.Left).SetPadBottom(5f);
            container.Row();

            _autoEquipHeroCheckBox = new CheckBox("Auto Equip Hero", skin, "ph-default");
            _autoEquipHeroCheckBox.IsChecked = true;
            _autoEquipHeroCheckBox.OnChanged += (isChecked) =>
            {
                var heroComp = GetHeroComponent();
                if (heroComp != null) heroComp.AutoEquipHero = isChecked;
            };
            container.Add(_autoEquipHeroCheckBox).Left().SetPadBottom(8);
            container.Row();

            _autoEquipMercsCheckBox = new CheckBox("Auto Equip Mercenaries", skin, "ph-default");
            _autoEquipMercsCheckBox.IsChecked = true;
            _autoEquipMercsCheckBox.OnChanged += (isChecked) =>
            {
                var heroComp = GetHeroComponent();
                if (heroComp != null) heroComp.AutoEquipMercenaries = isChecked;
            };
            container.Add(_autoEquipMercsCheckBox).Left().SetPadBottom(15);
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
                _healPriorityItems.Add(HeroHealPriority.Inn.ToString());
                _healPriorityItems.Add(HeroHealPriority.HealingItem.ToString());
                _healPriorityItems.Add(HeroHealPriority.HealingSkill.ToString());
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
                _inventoryGrid?.ClearSelection();
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

            // Refresh auto-equip option checkboxes
            _autoEquipHeroCheckBox.IsChecked = heroComp.AutoEquipHero;
            _autoEquipMercsCheckBox.IsChecked = heroComp.AutoEquipMercenaries;
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

                    // Update equip preview tooltip position if visible
                    if (_equipPreviewTooltip != null && _equipPreviewTooltip.GetContainer().HasParent())
                    {
                        var itemTooltipContainer = _itemTooltip.GetContainer();
                        float previewX = itemTooltipContainer.GetX() + itemTooltipContainer.GetWidth() + 5;
                        float previewY = itemTooltipContainer.GetY();
                        _equipPreviewTooltip.GetContainer().SetPosition(previewX, previewY);
                    }
                }

                // Update hero crystal tab tooltip
                if (_heroCrystalTab != null)
                    _heroCrystalTab.Update();
            }
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
                _windowVisible = false; UIWindowManager.OnUIWindowClosing(); _selectedItemCard?.Hide(); _inventoryGrid?.ClearSelection(); _heroWindow?.SetVisible(false); _heroWindow?.Remove(); var pauseService = Core.Services.GetService<PauseService>(); if (pauseService != null) pauseService.IsPaused = false; Debug.Log("[HeroUI] Hero window force closed by single window policy");
            }
        }

        private void HandleItemHovered(IItem item, InventorySlot slot)
        {
            if (item == null) return;

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

            // Show equip preview tooltip if item is qualifying gear
            if (item is IGear hoveredGear)
            {
                // Do not show preview for accessories
                if (hoveredGear.Kind == ItemKind.Accessory)
                {
                    // ensure any previous preview is removed
                    if (_equipPreviewTooltip != null)
                        _equipPreviewTooltip.GetContainer().Remove();
                    return;
                }
                var heroComponent = GetHeroComponent();
                if (heroComponent != null && heroComponent.LinkedHero != null)
                {
                    var equippedGear = GetCurrentlyEquippedGear(hoveredGear, heroComponent.LinkedHero);
                    if (equippedGear != null)
                    {
                        _equipPreviewTooltip.ShowComparison(hoveredGear, equippedGear);
                        if (_equipPreviewTooltip.GetContainer().GetParent() == null)
                        {
                            _stage.AddElement(_equipPreviewTooltip.GetContainer());
                        }

                        // Position equip preview tooltip to the right of item tooltip (using item tooltip's clamped Y)
                        var itemTooltipContainer = _itemTooltip.GetContainer();
                        float previewX = itemTooltipContainer.GetX() + itemTooltipContainer.GetWidth() + 5;
                        float previewY = itemTooltipContainer.GetY();
                        _equipPreviewTooltip.GetContainer().SetPosition(previewX, previewY);
                        _equipPreviewTooltip.GetContainer().ToFront();
                    }
                }
            }
        }

        private void HandleItemUnhovered()
        {
            // Hide tooltip when no item is hovered
            if (!(_inventoryGrid != null && _inventoryGrid.HasAnyHoveredSlot()))
            {
                _itemTooltip.GetContainer().Remove();
                _equipPreviewTooltip.GetContainer().Remove();
            }
        }

        /// <summary>Gets the currently equipped gear for the same slot as the hovered gear.</summary>
        private IGear GetCurrentlyEquippedGear(IGear hoveredGear, RolePlayingFramework.Heroes.Hero hero)
        {
            if (hoveredGear == null || hero == null) return null;

            // Determine which slot this gear would equip to
            var kind = hoveredGear.Kind;

            if (kind == ItemKind.HatHelm || kind == ItemKind.HatHeadband || kind == ItemKind.HatWizard || kind == ItemKind.HatPriest)
            {
                return hero.Hat as IGear;
            }
            else if (kind == ItemKind.ArmorMail || kind == ItemKind.ArmorRobe || kind == ItemKind.ArmorGi)
            {
                return hero.Armor as IGear;
            }
            else if (kind == ItemKind.WeaponSword || kind == ItemKind.WeaponKnife || kind == ItemKind.WeaponKnuckle || kind == ItemKind.WeaponStaff || kind == ItemKind.WeaponRod || kind == ItemKind.WeaponHammer)
            {
                return hero.WeaponShield1 as IGear;
            }
            else if (kind == ItemKind.Shield)
            {
                return hero.WeaponShield2 as IGear;
            }
            else if (kind == ItemKind.Accessory)
            {
                // For accessories do not show preview
                return null;
            }

            return null;
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