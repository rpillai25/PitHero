#if DEBUG
using Microsoft.Xna.Framework.Input;
using Nez;
using PitHero.Services;

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
            int newLevel;
            if (number == 0)
            {
                newLevel = 1; // Reset to level 1
            }
            else
            {
                newLevel = number * 10; // 1 = level 10, 2 = level 20, etc.
            }

            Debug.Log($"[PitLevelTest] Queuing pit level {newLevel} (key {number} pressed)");
            
            // Use the new queuing functionality from ActivateWizardOrbAction
            PitHero.AI.ActivateWizardOrbAction.QueuePitLevel(newLevel);
            
            // Set GOAP states to trigger the wizard orb workflow
            var heroEntities = Core.Scene?.FindEntitiesWithTag(GameConfig.TAG_HERO);
            var heroEntity = heroEntities?.Count > 0 ? heroEntities[0] : null;
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            if (heroComponent != null)
            {
                // Simulate wizard orb activation state for testing
                Debug.Log("[PitLevelTest] Setting GOAP states for wizard orb workflow test");
                
                // This will trigger the MovingToInsidePitEdgeAction and subsequent actions
                heroComponent.PitInitialized = false; // Mark pit as needing regeneration
                
                Debug.Log($"[PitLevelTest] Pit level {newLevel} queued and workflow triggered");
            }
            else
            {
                Debug.Error("[PitLevelTest] Hero component not found");
            }
        }
    }
}
#endif