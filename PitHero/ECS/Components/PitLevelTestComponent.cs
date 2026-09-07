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

            // Debug input is still player input: route through the command queue so a debug session replays
            Services.Replay.PlayerCommandService.Dispatch(new Services.Replay.PlayerCommand(
                Services.Replay.PlayerCommandType.DebugQueuePitLevel, newLevel));
        }

        /// <summary>Queues the given pit level and flips the wizard-orb GOAP flags. Command handler entry point.</summary>
        public static void ApplyPitLevel(int newLevel)
        {
            Debug.Log($"[PitLevelTest] Queuing pit level {newLevel}");

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
                if (heroComponent.InsidePit)
                {

                    // This will trigger the MovingToInsidePitEdgeAction and subsequent actions
                    heroComponent.PitInitialized = false; // Mark pit as needing regeneration
                    heroComponent.ActivatedWizardOrb = true;
                    heroComponent.FoundWizardOrb = false;  // Reset according to specification
                    heroComponent.ExploredPit = true; // Pretend pit has been explored
                }
                else
                {
                    //If outside pit, just mark as needing regeneration
                    heroComponent.PitInitialized = false; // Mark pit as needing regeneration
                    heroComponent.InsidePit = false;
                    heroComponent.ActivatedWizardOrb = true;
                }

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