using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using System.Collections.Generic;
using PitHero.ECS.Components;

namespace PitHero.UI
{
    /// <summary>
    /// UI for Hero button with pit priority reorder functionality
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

        // Priority reorder window components
        private Window _priorityWindow;
        private ReorderableTableList<string> _priorityList;
        private List<string> _priorityItems;
        private bool _windowVisible = false;

        public HeroUI()
        {
        }

        /// <summary>
        /// Initializes the Hero button and adds it to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            // Use default skin
            var skin = Skin.CreateDefaultSkin();

            // Create Hero button
            CreateHeroButton(skin);

            // Create priority window
            CreatePriorityWindow(skin);

            // Add button to stage
            _stage.AddElement(_heroButton);
        }

        private void CreateHeroButton(Skin skin)
        {
            // Load the UI atlas and get the Hero sprites
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

            // Base styles for each sprite with proper ImageDown and ImageOver
            _heroNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(heroSprite),
                ImageDown = new SpriteDrawable(heroInverse),
                ImageOver = new SpriteDrawable(heroHighlight)
            };

            _heroHalfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(heroSprite2x),
                ImageDown = new SpriteDrawable(heroInverse2x),
                ImageOver = new SpriteDrawable(heroHighlight2x)
            };

            _heroQuarterStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(heroSprite4x),
                ImageDown = new SpriteDrawable(heroInverse4x),
                ImageOver = new SpriteDrawable(heroHighlight4x)
            };

            _heroButton = new HoverableImageButton(_heroNormalStyle, "Hero");
            // Explicitly size to the image
            _heroButton.SetSize(heroSprite.SourceRect.Width, heroSprite.SourceRect.Height);

            // Handle click to toggle priority window
            _heroButton.OnClicked += (button) => HandleHeroButtonClick();
        }

        private void HandleHeroButtonClick()
        {
            // Close SettingsUI window if it's open before opening Hero window (single window policy)
            // Use a simple approach - look for any visible Window that's not our priority window
            var allElements = _stage.GetElements();
            for (int i = 0; i < allElements.Count; i++)
            {
                var element = allElements[i];
                if (element is Window window && window.IsVisible() && window != _priorityWindow)
                {
                    // This is likely the Settings window - force close it
                    window.SetVisible(false);
                    // Also need to unpause the game since Settings window manages pause state
                    var pauseService = Core.Services.GetService<PauseService>();
                    if (pauseService != null)
                        pauseService.IsPaused = false;
                    Debug.Log("[HeroUI] Closed other UI window to enforce single window policy");
                    break;
                }
            }

            // Toggle priority window visibility
            TogglePriorityWindow();
        }

        private void CreatePriorityWindow(Skin skin)
        {
            // Initialize priority items from current hero priorities
            InitializePriorityItems();

            // Create reorderable list
            _priorityList = new ReorderableTableList<string>(skin, _priorityItems, OnPriorityReordered);

            // Create window
            _priorityWindow = new Window("Pit Priorities", skin);
            _priorityWindow.SetSize(300f, 200f);
            
            // Add padding at the top before the priority list
            _priorityWindow.Add(_priorityList).Expand().Fill().SetPadTop(15f);

            // Add close button
            var closeButton = new TextButton("Close", skin);
            closeButton.OnClicked += (btn) => TogglePriorityWindow();
            _priorityWindow.Row();
            _priorityWindow.Add(closeButton).SetPadTop(10f);

            _priorityWindow.SetVisible(false);
        }

        private void InitializePriorityItems()
        {
            // Ensure we reuse the existing list instance so ReorderableTableList keeps referencing the same list
            if (_priorityItems == null)
                _priorityItems = new List<string>(3);
            else
                _priorityItems.Clear();

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
            // Update hero component priorities in real-time using the mutated shared list
            UpdateHeroPriorities();
        }

        private void TogglePriorityWindow()
        {
            if (_priorityWindow == null) return;

            _windowVisible = !_windowVisible;
            
            if (_windowVisible)
            {
                // Use centralized UI window manager for opening behavior
                UIWindowManager.OnUIWindowOpening();
                
                // Refresh priority items from current hero state (mutates existing list)
                InitializePriorityItems();
                _priorityList.Rebuild();
                
                // Position window next to Hero button
                PositionPriorityWindow();
                
                // Add to stage and show
                _stage.AddElement(_priorityWindow);
                _priorityWindow.SetVisible(true);
                _priorityWindow.ToFront();
                
                // Pause the game when window opens
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = true;
                
                Debug.Log("Priority window opened and game paused");
            }
            else
            {
                // Use centralized UI window manager for closing behavior
                UIWindowManager.OnUIWindowClosing();
                
                // Hide and remove from stage
                _priorityWindow.SetVisible(false);
                _priorityWindow.Remove();
                
                // Unpause the game when window closes
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
                
                Debug.Log("Priority window closed and game unpaused");
            }
        }

        private void PositionPriorityWindow()
        {
            if (_priorityWindow == null || _heroButton == null) return;

            // Ensure window dimensions are calculated
            _priorityWindow.Validate();

            float heroX = _heroButton.GetX();
            float heroY = _heroButton.GetY();
            float heroW = _heroButton.GetWidth();
            float winW = _priorityWindow.GetWidth();
            float winH = _priorityWindow.GetHeight();

            const float padding = 4f;
            float targetX = heroX + heroW + padding;
            float targetY = heroY + padding;

            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            // If window would go off right edge, position to the left of button
            if (targetX + winW > stageW)
                targetX = heroX - padding - winW;

            // Clamp to stage bounds
            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            if (targetY + winH > stageH) targetY = stageH - winH;

            _priorityWindow.SetPosition(targetX, targetY);
        }

        private HeroComponent GetHeroComponent()
        {
            // Find the hero entity in the current scene
            var heroEntity = Core.Scene?.FindEntity("hero");
            return heroEntity?.GetComponent<HeroComponent>();
        }

        private void UpdateHeroPriorities()
        {
            var hero = GetHeroComponent();
            if (hero == null)
            {
                Debug.Log("Could not find hero component to update priorities");
                return;
            }

            // Convert string list back to HeroPitPriority array
            var newPriorities = new HeroPitPriority[3];
            for (int i = 0; i < _priorityItems.Count && i < 3; i++)
            {
                if (System.Enum.TryParse(_priorityItems[i], out HeroPitPriority priority))
                {
                    newPriorities[i] = priority;
                }
                else
                {
                    Debug.Log($"Failed to parse priority: {_priorityItems[i]}");
                    return;
                }
            }

            // Update hero component
            hero.SetPrioritiesInOrder(newPriorities);
            Debug.Log($"Updated hero priorities: {newPriorities[0]}, {newPriorities[1]}, {newPriorities[2]}");
        }

        /// <summary>
        /// Update button style based on current window shrink mode
        /// </summary>
        public void UpdateButtonStyleIfNeeded()
        {
            // Determine desired mode based on current shrink mode
            HeroMode desired;
            if (WindowManager.IsQuarterHeightMode())
                desired = HeroMode.Quarter;
            else if (WindowManager.IsHalfHeightMode())
                desired = HeroMode.Half;
            else
                desired = HeroMode.Normal;

            if (desired == _currentHeroMode)
                return; // no change needed

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
                case HeroMode.Quarter:
                    _heroButton.SetStyle(_heroQuarterStyle);
                    _heroButton.SetSize(((SpriteDrawable)_heroQuarterStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroQuarterStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentHeroMode = desired;
            _styleChanged = true;
        }

        /// <summary>
        /// Position the button at the specified coordinates
        /// </summary>
        public void SetPosition(float x, float y)
        {
            _heroButton?.SetPosition(x, y);
        }

        /// <summary>
        /// Get the button X position
        /// </summary>
        public float GetX()
        {
            return _heroButton?.GetX() ?? 0f;
        }

        /// <summary>
        /// Get the button Y position
        /// </summary>
        public float GetY()
        {
            return _heroButton?.GetY() ?? 0f;
        }

        /// <summary>
        /// Get the button width
        /// </summary>
        public float GetWidth()
        {
            return _heroButton?.GetWidth() ?? 0f;
        }

        /// <summary>
        /// Get the button height
        /// </summary>
        public float GetHeight()
        {
            return _heroButton?.GetHeight() ?? 0f;
        }

        /// <summary>
        /// Consume style changed flag
        /// </summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update method (can be called from main update loop if needed)
        /// </summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();
        }

        /// <summary>
        /// Gets whether the priority window is currently visible
        /// </summary>
        public bool IsWindowVisible => _windowVisible;

        /// <summary>
        /// Forces the priority window to close without triggering normal events
        /// </summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false;
                
                // Use centralized UI window manager for closing behavior
                UIWindowManager.OnUIWindowClosing();
                
                // Hide and remove from stage
                _priorityWindow?.SetVisible(false);
                _priorityWindow?.Remove();
                
                // Unpause the game when window closes
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
                
                Debug.Log("[HeroUI] Priority window force closed by single window policy");
            }
        }
    }
}