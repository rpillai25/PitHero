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
        private ImageButtonStyle _heroQuarterStyle;
        private enum HeroMode { Normal, Half, Quarter }
        private HeroMode _currentHeroMode = HeroMode.Normal;
        private bool _styleChanged = false;

        // Tabbed window components
        private Window _heroWindow;
        private TabPane _tabPane;
        private Tab _inventoryTab;
        private Tab _prioritiesTab;
        private bool _windowVisible = false;
        
        // Inventory tab content
        private InventoryGrid _inventoryGrid;
        
        // Item card for selection only (hover uses tooltip)
        private ItemCard _selectedItemCard;
        
        // Tooltip for hovering over items
        private ItemCardTooltip _itemTooltip;
        
        // Tooltip for showing equip preview comparison
        private EquipPreviewTooltip _equipPreviewTooltip;
        
        // Priority reorder components (moved to priorities tab)
        private ReorderableTableList<string> _priorityList;
        private List<string> _priorityItems;

        // Sort buttons
        private HoverableImageButton _sortTimeButton;
        private HoverableImageButton _sortTypeButton;
        private HoverableImageButton _sortNameButton;
        private Image _sortTimeArrow;
        private Image _sortTypeArrow;
        private Image _sortNameArrow;
        // Arrow drawables to control flipping
        private SpriteDrawable _sortTimeArrowDrawable;
        private SpriteDrawable _sortTypeArrowDrawable;
        private SpriteDrawable _sortNameArrowDrawable;
        
        // Sort button styles (normal and pressed)
        private ImageButtonStyle _sortTimeNormalStyle;
        private ImageButtonStyle _sortTimePressedStyle;
        private ImageButtonStyle _sortTypeNormalStyle;
        private ImageButtonStyle _sortTypePressedStyle;
        private ImageButtonStyle _sortNameNormalStyle;
        private ImageButtonStyle _sortNamePressedStyle;

        // Tracks whether we have applied the initial default sort once
        private bool _appliedInitialSort = false;

        public HeroUI() { }

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
            var heroSprite4x = uiAtlas.GetSprite("UIHero4x");
            var heroHighlight = uiAtlas.GetSprite("UIHeroHighlight");
            var heroHighlight2x = uiAtlas.GetSprite("UIHeroHighlight2x");
            var heroHighlight4x = uiAtlas.GetSprite("UIHeroHighlight4x");
            var heroInverse = uiAtlas.GetSprite("UIHeroInverse");
            var heroInverse2x = uiAtlas.GetSprite("UIHeroInverse2x");
            var heroInverse4x = uiAtlas.GetSprite("UIHeroInverse4x");

            _heroNormalStyle = new ImageButtonStyle { ImageUp = new SpriteDrawable(heroSprite), ImageDown = new SpriteDrawable(heroInverse), ImageOver = new SpriteDrawable(heroHighlight) };
            _heroHalfStyle = new ImageButtonStyle { ImageUp = new SpriteDrawable(heroSprite2x), ImageDown = new SpriteDrawable(heroInverse2x), ImageOver = new SpriteDrawable(heroHighlight2x) };
            _heroQuarterStyle = new ImageButtonStyle { ImageUp = new SpriteDrawable(heroSprite4x), ImageDown = new SpriteDrawable(heroInverse4x), ImageOver = new SpriteDrawable(heroHighlight4x) };

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
            // Widen window to fit 8 inventory columns and sort controls
            _heroWindow.SetSize(360f, 350f);
            var tabWindowStyle = CreateTabWindowStyle(skin);
            _tabPane = new TabPane(tabWindowStyle);
            var tabStyle = CreateTabStyle(skin);
            _inventoryTab = new Tab("Inventory", tabStyle);
            _prioritiesTab = new Tab("Pit Priorities", tabStyle);
            PopulateInventoryTab(_inventoryTab, skin);
            PopulatePrioritiesTab(_prioritiesTab, skin);
            _tabPane.AddTab(_inventoryTab);
            _tabPane.AddTab(_prioritiesTab);
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

        private Table CreateSortButtons(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            
            // Load sort sprites
            var sortTimeNormal = uiAtlas.GetSprite("UISortTime");
            var sortTimeHighlight = uiAtlas.GetSprite("UISortTimeHighlight");
            var sortTimeInverse = uiAtlas.GetSprite("UISortTimeInverse");
            var sortTypeNormal = uiAtlas.GetSprite("UISortType");
            var sortTypeHighlight = uiAtlas.GetSprite("UISortTypeHighlight");
            var sortTypeInverse = uiAtlas.GetSprite("UISortTypeInverse");
            var sortNameNormal = uiAtlas.GetSprite("UISortAlpha");
            var sortNameHighlight = uiAtlas.GetSprite("UISortAlphaHighlight");
            var sortNameInverse = uiAtlas.GetSprite("UISortAlphaInverse");
            var sortArrowSprite = uiAtlas.GetSprite("UISortOrderArrow");
            
            // Create button styles (normal and pressed)
            _sortTimeNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sortTimeNormal),
                ImageDown = new SpriteDrawable(sortTimeInverse),
                ImageOver = new SpriteDrawable(sortTimeHighlight)
            };
            
            _sortTimePressedStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sortTimeInverse),
                ImageDown = new SpriteDrawable(sortTimeNormal),
                ImageOver = new SpriteDrawable(sortTimeHighlight)
            };
            
            _sortTypeNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sortTypeNormal),
                ImageDown = new SpriteDrawable(sortTypeInverse),
                ImageOver = new SpriteDrawable(sortTypeHighlight)
            };
            
            _sortTypePressedStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sortTypeInverse),
                ImageDown = new SpriteDrawable(sortTypeNormal),
                ImageOver = new SpriteDrawable(sortTypeHighlight)
            };
            
            _sortNameNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sortNameNormal),
                ImageDown = new SpriteDrawable(sortNameInverse),
                ImageOver = new SpriteDrawable(sortNameHighlight)
            };
            
            _sortNamePressedStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sortNameInverse),
                ImageDown = new SpriteDrawable(sortNameNormal),
                ImageOver = new SpriteDrawable(sortNameHighlight)
            };
            
            // Create buttons with default pressed style for Time (since it's the default sort)
            _sortTimeButton = new HoverableImageButton(_sortTimePressedStyle, "By Time Obtained");
            _sortTimeButton.SetSize(sortTimeNormal.SourceRect.Width, sortTimeNormal.SourceRect.Height);
            _sortTimeButton.OnClicked += (btn) => OnSortButtonClicked(InventorySortOrder.Time);
            
            _sortTypeButton = new HoverableImageButton(_sortTypeNormalStyle, "By Type");
            _sortTypeButton.SetSize(sortTypeNormal.SourceRect.Width, sortTypeNormal.SourceRect.Height);
            _sortTypeButton.OnClicked += (btn) => OnSortButtonClicked(InventorySortOrder.Type);
            
            _sortNameButton = new HoverableImageButton(_sortNameNormalStyle, "By Name");
            _sortNameButton.SetSize(sortNameNormal.SourceRect.Width, sortNameNormal.SourceRect.Height);
            _sortNameButton.OnClicked += (btn) => OnSortButtonClicked(InventorySortOrder.Name);
            
            // Create arrow images using stored drawables so we can flip via SpriteEffects
            _sortTimeArrowDrawable = new SpriteDrawable(sortArrowSprite);
            _sortTypeArrowDrawable = new SpriteDrawable(sortArrowSprite);
            _sortNameArrowDrawable = new SpriteDrawable(sortArrowSprite);
            _sortTimeArrow = new Image(_sortTimeArrowDrawable);
            _sortTypeArrow = new Image(_sortTypeArrowDrawable);
            _sortNameArrow = new Image(_sortNameArrowDrawable);
            // Ensure arrows do not intercept input
            _sortTimeArrow.SetTouchable(Touchable.Disabled);
            _sortTypeArrow.SetTouchable(Touchable.Disabled);
            _sortNameArrow.SetTouchable(Touchable.Disabled);
            
            // Container for sort buttons
            var container = new Table();
            
            // Add "Sort Order" label
            var label = new Label("Sort Order", skin);
            container.Add(label).Center().SetPadBottom(4f);
            container.Row();
            
            // Time row: button + arrow to the right
            var timeRow = new Table();
            timeRow.Add(_sortTimeButton).Left();
            timeRow.Add(_sortTimeArrow).Right().SetPadLeft(4f);
            container.Add(timeRow).SetPadBottom(8f);
            container.Row();
            
            // Type row: button + arrow to the right
            var typeRow = new Table();
            typeRow.Add(_sortTypeButton).Left();
            typeRow.Add(_sortTypeArrow).Right().SetPadLeft(4f);
            container.Add(typeRow).SetPadBottom(8f);
            container.Row();
            
            // Name row: button + arrow to the right
            var nameRow = new Table();
            nameRow.Add(_sortNameButton).Left();
            nameRow.Add(_sortNameArrow).Right().SetPadLeft(4f);
            container.Add(nameRow);
            
            // Initialize button states (Time descending is default)
            UpdateSortButtonStates();
            
            return container;
        }

        private void OnSortButtonClicked(InventorySortOrder sortOrder)
        {
            if (_inventoryGrid == null) return;
            
            var currentSort = _inventoryGrid.GetCurrentSortOrder();
            var currentDirection = _inventoryGrid.GetCurrentSortDirection();
            
            // If clicking the same button, toggle direction
            if (currentSort == sortOrder)
            {
                var newDirection = currentDirection == SortDirection.Descending 
                    ? SortDirection.Ascending 
                    : SortDirection.Descending;
                _inventoryGrid.SortInventory(sortOrder, newDirection);
            }
            else
            {
                // Different button clicked, use descending as default
                _inventoryGrid.SortInventory(sortOrder, SortDirection.Descending);
            }
        }

        private void HandleSortOrderChanged(InventorySortOrder sortOrder, SortDirection sortDirection)
        {
            UpdateSortButtonStates();
        }

        private void UpdateSortButtonStates()
        {
            if (_inventoryGrid == null) return;
            
            var currentSort = _inventoryGrid.GetCurrentSortOrder();
            var currentDirection = _inventoryGrid.GetCurrentSortDirection();
            
            // Update button styles based on active sort
            switch (currentSort)
            {
                case InventorySortOrder.Time:
                    _sortTimeButton.SetStyle(_sortTimePressedStyle);
                    _sortTypeButton.SetStyle(_sortTypeNormalStyle);
                    _sortNameButton.SetStyle(_sortNameNormalStyle);
                    break;
                case InventorySortOrder.Type:
                    _sortTimeButton.SetStyle(_sortTimeNormalStyle);
                    _sortTypeButton.SetStyle(_sortTypePressedStyle);
                    _sortNameButton.SetStyle(_sortNameNormalStyle);
                    break;
                case InventorySortOrder.Name:
                    _sortTimeButton.SetStyle(_sortTimeNormalStyle);
                    _sortTypeButton.SetStyle(_sortTypeNormalStyle);
                    _sortNameButton.SetStyle(_sortNamePressedStyle);
                    break;
            }
            
            // Hide all arrows initially
            _sortTimeArrow.SetVisible(false);
            _sortTypeArrow.SetVisible(false);
            _sortNameArrow.SetVisible(false);
            
            // Show arrow on active button and flip vertically based on direction
            Image activeArrow = null;
            SpriteDrawable activeDrawable = null;
            switch (currentSort)
            {
                case InventorySortOrder.Time:
                    activeArrow = _sortTimeArrow;
                    activeDrawable = _sortTimeArrowDrawable;
                    break;
                case InventorySortOrder.Type:
                    activeArrow = _sortTypeArrow;
                    activeDrawable = _sortTypeArrowDrawable;
                    break;
                case InventorySortOrder.Name:
                    activeArrow = _sortNameArrow;
                    activeDrawable = _sortNameArrowDrawable;
                    break;
            }
            
            if (activeArrow != null)
            {
                bool ascending = currentDirection == SortDirection.Ascending;
                // Flip vertically using SpriteEffects so it always renders up/down properly
                if (activeDrawable != null)
                {
                    activeDrawable.SpriteEffects = ascending 
                        ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipVertically 
                        : Microsoft.Xna.Framework.Graphics.SpriteEffects.None;
                }
                activeArrow.SetVisible(true);
            }
        }

        private void PopulateInventoryTab(Tab inventoryTab, Skin skin)
        {
            _inventoryGrid = new InventoryGrid();
            _inventoryGrid.OnItemHovered += HandleItemHovered;
            _inventoryGrid.OnItemUnhovered += HandleItemUnhovered;
            _inventoryGrid.OnItemSelected += HandleItemSelected;
            _inventoryGrid.OnItemDeselected += HandleItemDeselected;
            _inventoryGrid.OnSortOrderChanged += HandleSortOrderChanged;
            
            // Initialize context menu
            _inventoryGrid.InitializeContextMenu(_stage, skin);
            
            var heroComponent = GetHeroComponent();
            if (heroComponent != null)
                _inventoryGrid.ConnectToHero(heroComponent);
            
            // Create main container with horizontal layout
            var mainContainer = new Table();
            
            // Left side: inventory grid in scroll pane
            var scrollPane = new ScrollPane(_inventoryGrid, skin);
            scrollPane.SetScrollingDisabled(true, false);
            mainContainer.Add(scrollPane).Expand().Fill().Pad(10f);
            
            // Right side: sort buttons
            var sortButtonContainer = CreateSortButtons(skin);

            // Compute top padding so buttons begin next to grid row 3 (skip equip rows 0-2)
            // InventoryGrid slot metrics are 32px size with 1px padding between rows
            const float slotSize = 32f;
            const float slotPad = 1f;
            float topPad = 3f * (slotSize + slotPad); // shift down past rows 0,1,2
            const float leftGap = 12f; // spacing between rightmost grid column and buttons

            mainContainer.Add(sortButtonContainer)
                .Top()
                .Right()
                .SetPadTop(topPad)
                .SetPadLeft(leftGap)
                .SetPadRight(10f);
            
            inventoryTab.Add(mainContainer).Expand().Fill();
        }

        private void PopulatePrioritiesTab(Tab prioritiesTab, Skin skin)
        {
            InitializePriorityItems();
            _priorityList = new ReorderableTableList<string>(skin, _priorityItems, OnPriorityReordered);
            prioritiesTab.Add(_priorityList).Expand().Fill().Pad(15f);
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

                // Apply default sort only on the first open; thereafter, preserve last selection
                if (_inventoryGrid != null && !_appliedInitialSort)
                {
                    _inventoryGrid.SortInventory(InventorySortOrder.Time, SortDirection.Descending);
                    _appliedInitialSort = true;
                }

                UpdateSortButtonStates(); // Update sort button states when opening
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
            if (WindowManager.IsQuarterHeightMode()) desired = HeroMode.Quarter; else if (WindowManager.IsHalfHeightMode()) desired = HeroMode.Half; else desired = HeroMode.Normal;
            if (desired == _currentHeroMode) return;
            switch (desired)
            {
                case HeroMode.Normal: _heroButton.SetStyle(_heroNormalStyle); _heroButton.SetSize(((SpriteDrawable)_heroNormalStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroNormalStyle.ImageUp).Sprite.SourceRect.Height); break;
                case HeroMode.Half: _heroButton.SetStyle(_heroHalfStyle); _heroButton.SetSize(((SpriteDrawable)_heroHalfStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroHalfStyle.ImageUp).Sprite.SourceRect.Height); break;
                case HeroMode.Quarter: _heroButton.SetStyle(_heroQuarterStyle); _heroButton.SetSize(((SpriteDrawable)_heroQuarterStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroQuarterStyle.ImageUp).Sprite.SourceRect.Height); break;
            }
            _currentHeroMode = desired; _styleChanged = true;
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
            
            // Handle keyboard shortcuts even when window is closed (they're shortcuts after all!)
            if (_inventoryGrid != null)
            {
                _inventoryGrid.HandleKeyboardShortcuts();
            }
            
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
            }
        }

        public bool IsWindowVisible => _windowVisible;

        /// <summary>Force close window</summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false; UIWindowManager.OnUIWindowClosing(); _selectedItemCard?.Hide(); _heroWindow?.SetVisible(false); _heroWindow?.Remove(); var pauseService = Core.Services.GetService<PauseService>(); if (pauseService != null) pauseService.IsPaused = false; Debug.Log("[HeroUI] Hero window force closed by single window policy");
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