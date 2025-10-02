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
        
        // Priority reorder components (moved to priorities tab)
        private ReorderableTableList<string> _priorityList;
        private List<string> _priorityItems;

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
            _heroWindow.SetSize(285f, 350f);
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
            _inventoryGrid = new InventoryGrid();
            _inventoryGrid.OnItemHovered += HandleItemHovered;
            _inventoryGrid.OnItemUnhovered += HandleItemUnhovered;
            _inventoryGrid.OnItemSelected += HandleItemSelected;
            _inventoryGrid.OnItemDeselected += HandleItemDeselected;
            
            // Initialize context menu
            _inventoryGrid.InitializeContextMenu(_stage, skin);
            
            var heroComponent = GetHeroComponent();
            if (heroComponent != null)
                _inventoryGrid.ConnectToHero(heroComponent);
            var scrollPane = new ScrollPane(_inventoryGrid, skin);
            scrollPane.SetScrollingDisabled(true, false);
            inventoryTab.Add(scrollPane).Expand().Fill().Pad(10f);
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
            if (_windowVisible && _inventoryGrid != null) 
            {
                _inventoryGrid.HandleKeyboardShortcuts();
                
                // Update tooltip position if visible
                if (_itemTooltip != null && _itemTooltip.GetContainer().HasParent())
                {
                    var mousePos = _stage.GetMousePosition();
                    _itemTooltip.GetContainer().SetPosition(mousePos.X + 10, mousePos.Y + 10);
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
        }

        private void HandleItemUnhovered()
        {
            // Hide tooltip when no item is hovered
            if (!(_inventoryGrid != null && _inventoryGrid.HasAnyHoveredSlot()))
            {
                _itemTooltip.GetContainer().Remove();
            }
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