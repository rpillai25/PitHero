using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using System.Collections.Generic;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;

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
        private bool _windowVisible = false;
        
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
        
        // Tooltip for showing equip preview comparison
        private EquipPreviewTooltip _equipPreviewTooltip;
        
        // Priority reorder components (moved to priorities tab)
        private ReorderableTableList<string> _priorityList;
        private List<string> _priorityItems;
        
        // Hero Crystal tab component
        private HeroCrystalTab _heroCrystalTab;

        public HeroUI() 
        {
            // Initialize all synergy patterns
            _allSynergyPatterns = new List<RolePlayingFramework.Synergies.SynergyPattern>();
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ExampleSynergyPatterns.CreateSwordShieldMastery());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ExampleSynergyPatterns.CreateMagesFocus());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ExampleSynergyPatterns.CreateMonksBalance());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ExampleSynergyPatterns.CreateHeavyArmorSet());
            _allSynergyPatterns.Add(RolePlayingFramework.Synergies.ExampleSynergyPatterns.CreatePriestsDevotion());
        }

        /// <summary>Initializes the Hero button and adds it to the stage</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            var skin = Skin.CreateDefaultSkin();
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

        private void HandleHeroButtonClick()
        {
            var allElements = _stage.GetElements();
            for (int i = 0; i < allElements.Count; i++)
            {
                var element = allElements[i];
                if (element is Window window && window.IsVisible() && window != _heroWindow)
                {
                    window.SetVisible(false);
                    var pauseService = Core.Services.GetService<PauseService>();
                    if (pauseService != null)
                        pauseService.IsPaused = false;
                    Debug.Log("[HeroUI] Closed other UI window to enforce single window policy");
                    break;
                }
            }
            ToggleHeroWindow();
        }

        private void CreateHeroWindow(Skin skin)
        {
            _heroWindow = new Window("Hero", skin);
            // Widen window to fit 20 inventory columns (20 * 33 = 660 pixels + padding)
            _heroWindow.SetSize(700f, 350f);
            var tabWindowStyle = CreateTabWindowStyle(skin);
            _tabPane = new TabPane(tabWindowStyle);
            var tabStyle = CreateTabStyle(skin);
            _inventoryTab = new Tab("Inventory", tabStyle);
            _prioritiesTab = new Tab("Pit Priorities", tabStyle);
            _crystalTab = new Tab("Hero Crystal", tabStyle);
            PopulateInventoryTab(_inventoryTab, skin);
            PopulatePrioritiesTab(_prioritiesTab, skin);
            PopulateCrystalTab(_crystalTab, skin);
            _tabPane.AddTab(_inventoryTab);
            _tabPane.AddTab(_prioritiesTab);
            _tabPane.AddTab(_crystalTab);
            _heroWindow.Add(_tabPane).Expand().Fill();
            _heroWindow.SetVisible(false);
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

        private TabWindowStyle CreateTabWindowStyle(Skin skin)
        {
            var tabButtonStyle = new TabButtonStyle { LabelStyle = skin.Get<LabelStyle>() };
            var buttonStyle = skin.Get<TextButtonStyle>();
            tabButtonStyle.Inactive = buttonStyle.Up;
            tabButtonStyle.Active = buttonStyle.Down;
            return new TabWindowStyle { TabButtonStyle = tabButtonStyle, Background = skin.Get<WindowStyle>().Background };
        }

        private TabStyle CreateTabStyle(Skin skin) => new TabStyle { Background = null };


        private void PopulateInventoryTab(Tab inventoryTab, Skin skin)
        {
            var container = new Table();
            
            _inventoryGrid = new InventoryGrid();
            _inventoryGrid.OnItemHovered += HandleItemHovered;
            _inventoryGrid.OnItemUnhovered += HandleItemUnhovered;
            _inventoryGrid.OnItemSelected += HandleItemSelected;
            _inventoryGrid.OnItemDeselected += HandleItemDeselected;
            _inventoryGrid.OnStencilRemovalRequested += HandleStencilRemovalRequested;
            
            // Initialize context menu
            _inventoryGrid.InitializeContextMenu(_stage, skin);
            
            var heroComponent = GetHeroComponent();
            if (heroComponent != null)
                _inventoryGrid.ConnectToHero(heroComponent);
            
            // Create scroll pane for inventory grid
            var scrollPane = new ScrollPane(_inventoryGrid, skin);
            scrollPane.SetScrollingDisabled(true, false);
            
            container.Add(scrollPane).Expand().Fill().Pad(10f);
            container.Row();
            
            // Add stencil control buttons
            var buttonTable = new Table();
            buttonTable.Pad(5f);
            
            _viewStencilsButton = new TextButton("View Stencils", skin);
            _viewStencilsButton.OnClicked += HandleViewStencilsClicked;
            buttonTable.Add(_viewStencilsButton).Width(120f).Height(30f).Pad(5f);
            
            _moveStencilsButton = new TextButton("Move Stencils", skin);
            _moveStencilsButton.OnClicked += HandleMoveStencilsClicked;
            buttonTable.Add(_moveStencilsButton).Width(120f).Height(30f).Pad(5f);
            
            _removeStencilButton = new TextButton("Remove Stencil", skin);
            _removeStencilButton.OnClicked += HandleRemoveStencilClicked;
            buttonTable.Add(_removeStencilButton).Width(120f).Height(30f).Pad(5f);
            
            container.Add(buttonTable).Fill();
            
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
        
        private void HandleViewStencilsClicked(Button button)
        {
            if (_stencilLibraryPanel != null && !_stencilLibraryPanel.IsVisible())
            {
                var gameStateService = Core.Services.GetService<GameStateService>();
                if (gameStateService != null)
                {
                    _stencilLibraryPanel.UpdateWithGameState(gameStateService, _allSynergyPatterns);
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
            var skin = Skin.CreateDefaultSkin();
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
                // Place stencil at default position (top-left of inventory area, row 2)
                var anchor = new Point(0, 2);
                _inventoryGrid.PlaceStencil(pattern, anchor);
                Debug.Log($"Activated stencil: {pattern.Name}");
            }
        }

        private void PopulatePrioritiesTab(Tab prioritiesTab, Skin skin)
        {
            InitializePriorityItems();
            _priorityList = new ReorderableTableList<string>(skin, _priorityItems, OnPriorityReordered);
            prioritiesTab.Add(_priorityList).Expand().Fill().Pad(15f);
        }
        
        private void PopulateCrystalTab(Tab crystalTab, Skin skin)
        {
            _heroCrystalTab = new HeroCrystalTab();
            var content = _heroCrystalTab.CreateContent(skin, _stage);
            crystalTab.Add(content).Expand().Fill();
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

        private void ToggleHeroWindow()
        {
            if (_heroWindow == null) return;
            _windowVisible = !_windowVisible;
            if (_windowVisible)
            {
                UIWindowManager.OnUIWindowOpening();
                InitializePriorityItems();
                _priorityList?.Rebuild();
                var heroComponent = GetHeroComponent();
                if (heroComponent != null && _inventoryGrid != null)
                    _inventoryGrid.ConnectToHero(heroComponent);
                
                // Update Hero Crystal tab with current hero
                if (heroComponent != null && _heroCrystalTab != null)
                    _heroCrystalTab.UpdateWithHero(heroComponent);

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
                    _itemTooltip.GetContainer().SetPosition(mousePos.X + 10, mousePos.Y + 10);
                    
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

        /// <summary>Force close window</summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false; UIWindowManager.OnUIWindowClosing(); _selectedItemCard?.Hide(); _inventoryGrid?.ClearSelection(); _heroWindow?.SetVisible(false); _heroWindow?.Remove(); var pauseService = Core.Services.GetService<PauseService>(); if (pauseService != null) pauseService.IsPaused = false; Debug.Log("[HeroUI] Hero window force closed by single window policy");
            }
        }

        private void HandleItemHovered(IItem item)
        {
            if (item == null) return;
          
            // Show tooltip at cursor position
            _itemTooltip.ShowItem(item);
            if (_itemTooltip.GetContainer().GetParent() == null)
            {
                _stage.AddElement(_itemTooltip.GetContainer());
            }
            
            // Position tooltip at mouse cursor
            var mousePos = _stage.GetMousePosition();
            _itemTooltip.GetContainer().SetPosition(mousePos.X + 10, mousePos.Y + 10);
            _itemTooltip.GetContainer().ToFront();
            
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
                        
                        // Position equip preview tooltip to the right of item tooltip
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
            else if (kind == ItemKind.WeaponSword || kind == ItemKind.WeaponKnuckle || kind == ItemKind.WeaponStaff || kind == ItemKind.WeaponRod)
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
    }
}