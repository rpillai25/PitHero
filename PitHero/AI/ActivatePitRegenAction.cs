using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;

namespace PitHero.AI
{
    /// <summary>
    /// Action that regenerates the queued pit level
    /// </summary>
    public class ActivatePitRegenAction : HeroActionBase
    {
        public ActivatePitRegenAction() : base(GoapConstants.ActivatePitRegenAction, 1)
        {
            // Preconditions: Wizard orb must be activated and hero must be outside pit
            SetPrecondition(GoapConstants.ActivatedWizardOrb, true);
            SetPrecondition(GoapConstants.OutsidePit, true);
            
            // Postcondition: Pit is initialized (regenerated)
            SetPostcondition(GoapConstants.PitInitialized, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            Debug.Log("[ActivatePitRegen] Starting pit regeneration");

            // Regenerate the queued pit level
            if (!RegenerateQueuedPitLevel())
            {
                Debug.Warn("[ActivatePitRegen] Failed to regenerate pit level");
                return true; // Complete as failed
            }

            // Set hero state flags according to specification
            hero.PitInitialized = true;
            hero.ExploredPit = false;  // Reset upon ActivatePitRegenAction execution
            hero.ActivatedWizardOrb = false;  // Reset upon ActivatePitRegenAction execution
            
            Debug.Log("[ActivatePitRegen] Pit regeneration complete");
            return true; // Action complete
        }

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[ActivatePitRegenAction] Starting execution with interface-based context");

            // Regenerate pit in virtual world
            var nextLevel = context.PitLevelManager.DequeueLevel();
            if (nextLevel.HasValue)
            {
                context.PitGenerator.RegenerateForLevel(nextLevel.Value);
                context.LogDebug($"[ActivatePitRegenAction] Generated pit level {nextLevel.Value}");
            }
            else
            {
                context.LogWarning("[ActivatePitRegenAction] No queued pit level found");
                return true; // Complete as failed
            }

            // Set hero state flags according to specification
            context.HeroController.PitInitialized = true;
            context.HeroController.ExploredPit = false;  // Reset upon ActivatePitRegenAction execution
            context.HeroController.ActivatedWizardOrb = false;  // Reset upon ActivatePitRegenAction execution
            
            context.LogDebug("[ActivatePitRegenAction] Pit regeneration complete");
            return true; // Action complete
        }

        /// <summary>
        /// Regenerate the queued pit level using PitWidthManager by reinitializing right edge and setting level
        /// </summary>
        private bool RegenerateQueuedPitLevel()
        {
            // Get the queued level from the service
            var queueService = Core.Services.GetService<PitLevelQueueService>();
            if (queueService == null)
            {
                Debug.Error("[ActivatePitRegen] PitLevelQueueService not found");
                return false;
            }

            var nextLevel = queueService.DequeueLevel();
            if (!nextLevel.HasValue)
            {
                Debug.Warn("[ActivatePitRegen] No queued pit level found");
                return false;
            }

            // Use PitWidthManager to apply level change instead of PitGenerator
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
            {
                Debug.Error("[ActivatePitRegen] PitWidthManager not found");
                return false;
            }

            // Reinitialize right edge then set the new level (which will trigger regeneration)
            pitWidthManager.SetPitLevel(nextLevel.Value);
            Debug.Log($"[ActivatePitRegen] Successfully set pit level {nextLevel.Value} via PitWidthManager");
            
            return true;
        }
    }
}