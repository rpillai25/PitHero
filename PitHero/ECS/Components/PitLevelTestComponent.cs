#if DEBUG
using Microsoft.Xna.Framework.Input;
using Nez;
using PitHero.Services;
using PitHero.ECS.Scenes;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for testing pit level changes via keyboard input
    /// Press 1-9 keys to set pit level to 10, 20, 30, etc.
    /// Press 0 to reset to level 1
    /// Only available in DEBUG builds and only works when Settings Menu is active (game paused)
    /// </summary>
    public class PitLevelTestComponent : Component, IUpdatable, IPausableComponent
    {
        private KeyboardState _lastKeyboardState;
        
        /// <summary>
        /// This component should respect pause state - only test during active gameplay
        /// </summary>
        public bool ShouldPause => true;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _lastKeyboardState = Keyboard.GetState();
            Debug.Log("[PitLevelTest] Component added - Press 1-9 keys to set pit level (10, 20, 30, etc.), 0 to reset to level 1");
        }

        public void Update()
        {
            // Only function when settings menu is active (game is paused)
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService == null || !pauseService.IsPaused)
            {
                return; // Don't process inputs unless game is paused (settings menu active)
            }

            // Check if hero is in pit - if so, don't allow testing
            //var heroEntity = Entity.Scene.FindEntity("hero");
            //if (heroEntity != null)
            //{
            //    var heroComponent = heroEntity.GetComponent<HeroComponent>();
            //    if (heroComponent != null && heroComponent.CheckInsidePit(heroEntity.Transform.Position))
            //    {
            //        // Hero is in pit, don't allow pit level testing
            //        return;
            //    }
            //}

            var currentKeyboardState = Keyboard.GetState();
            
            // Check for number key presses
            for (int i = 0; i <= 9; i++)
            {
                Keys key = (Keys)((int)Keys.D0 + i);
                
                if (currentKeyboardState.IsKeyDown(key) && !_lastKeyboardState.IsKeyDown(key))
                {
                    HandleNumberKeyPress(i);
                }
            }
            
            _lastKeyboardState = currentKeyboardState;
        }

        private void HandleNumberKeyPress(int number)
        {
            var scene = Entity.Scene as MainGameScene;
            if (scene == null)
            {
                Debug.Error("[PitLevelTest] Not in MainGameScene, cannot test pit levels");
                return;
            }

            int newLevel;
            if (number == 0)
            {
                newLevel = 1; // Reset to level 1
            }
            else
            {
                newLevel = number * 10; // 1 = level 10, 2 = level 20, etc.
            }

            Debug.Log($"[PitLevelTest] Setting pit level to {newLevel} (key {number} pressed) using fresh map regeneration");
            scene.ReloadMapAndRegeneratePit(newLevel);

            // Log the current state after regeneration
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
            {
                var extensionTiles = ((int)(newLevel / 10)) * 2;
                Debug.Log($"[PitLevelTest] Level {newLevel}: extending by {extensionTiles} tiles, right edge now at x={pitWidthManager.CurrentPitRightEdge}");
            }
        }
    }
}
#endif