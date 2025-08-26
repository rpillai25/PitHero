using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;

namespace PitHero.AI
{
    /// <summary>
    /// Action that activates the wizard orb and queues the next pit level
    /// Changes wizard orb tint to purple and triggers pit regeneration queue
    /// </summary>
    public class ActivateWizardOrbAction : HeroActionBase
    {
        public ActivateWizardOrbAction() : base(GoapConstants.ActivateWizardOrbAction, 1)
        {
            // Preconditions: Hero must be inside pit, exploration complete, and wizard orb found
            SetPrecondition(GoapConstants.InsidePit, true);
            SetPrecondition(GoapConstants.ExploredPit, true);
            SetPrecondition(GoapConstants.FoundWizardOrb, true);
            
            // Postconditions: Wizard orb activated and pit no longer initialized
            SetPostcondition(GoapConstants.ActivatedWizardOrb, true);
            SetPostcondition(GoapConstants.PitInitialized, false);
        }

        public override bool Execute(HeroComponent hero)
        {
            Debug.Log("[ActivateWizardOrb] Starting wizard orb activation");

            // Find the wizard orb entity
            var wizardOrbEntity = FindWizardOrbEntity();
            if (wizardOrbEntity == null)
            {
                Debug.Warn("[ActivateWizardOrb] Could not find wizard orb entity");
                return true; // Complete as failed
            }

            // Change the wizard orb tint to purple to indicate activation
            var renderer = wizardOrbEntity.GetComponent<Nez.RenderableComponent>();
            if (renderer != null)
            {
                renderer.Color = Color.Purple;
                Debug.Log("[ActivateWizardOrb] Changed wizard orb tint to purple");
            }
            else
            {
                Debug.Warn("[ActivateWizardOrb] Wizard orb entity has no renderable component");
            }

            // Queue the next pit level
            QueueNextPitLevel();

            // Set hero state flags according to specification
            hero.ActivatedWizardOrb = true;
            hero.PitInitialized = false;
            hero.FoundWizardOrb = false;  // Reset according to specification
            
            Debug.Log("[ActivateWizardOrb] Wizard orb activation complete - pit level queued");
            return true; // Action complete
        }

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[ActivateWizardOrbAction] Starting execution with interface-based context");

            // Activate wizard orb in virtual world
            context.WorldState.ActivateWizardOrb();

            // Queue the next pit level
            var nextLevel = context.PitLevelManager.CurrentLevel + 1;
            context.PitLevelManager.QueueLevel(nextLevel);

            // Set hero state flags according to specification
            context.HeroController.ActivatedWizardOrb = true;
            context.HeroController.PitInitialized = false;
            context.HeroController.FoundWizardOrb = false;  // Reset according to specification
            
            context.LogDebug($"[ActivateWizardOrbAction] Wizard orb activation complete - pit level {nextLevel} queued");
            return true; // Action complete
        }

        /// <summary>
        /// Find the wizard orb entity in the scene
        /// </summary>
        private Entity FindWizardOrbEntity()
        {
            var scene = Core.Scene;
            if (scene == null)
            {
                Debug.Warn("[ActivateWizardOrb] No active scene found");
                return null;
            }

            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0)
            {
                Debug.Warn("[ActivateWizardOrb] No wizard orb entities found");
                return null;
            }

            return wizardOrbEntities[0]; // Should only be one wizard orb
        }

        /// <summary>
        /// Queue the next pit level for regeneration
        /// This will be processed when the hero reaches the pit generation point
        /// </summary>
        private void QueueNextPitLevel()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
            {
                Debug.Error("[ActivateWizardOrb] PitWidthManager service not found");
                return;
            }

            // Queue the next level (current level + 1)
            var nextLevel = pitWidthManager.CurrentPitLevel + 1;
            QueuePitLevel(nextLevel);
            
            Debug.Log($"[ActivateWizardOrb] Queued pit level {nextLevel} for regeneration");
        }

        /// <summary>
        /// Queue a specific pit level for regeneration
        /// This functionality can be used by PitLevelTestComponent as well
        /// </summary>
        public static void QueuePitLevel(int level)
        {
            // Store the queued level in a service for later processing
            var queueService = Core.Services.GetService<PitLevelQueueService>();
            if (queueService == null)
            {
                // Create the service if it doesn't exist
                queueService = new PitLevelQueueService();
                Core.Services.AddService(queueService);
            }
            
            queueService.QueueLevel(level);
            Debug.Log($"[ActivateWizardOrb] Pit level {level} added to queue");
        }
    }

    /// <summary>
    /// Service to manage queued pit levels for regeneration
    /// </summary>
    public class PitLevelQueueService
    {
        private int? _queuedLevel;

        /// <summary>
        /// Queue a pit level for regeneration
        /// </summary>
        public void QueueLevel(int level)
        {
            _queuedLevel = level;
            Debug.Log($"[PitLevelQueue] Level {level} queued for regeneration");
        }

        /// <summary>
        /// Get the queued pit level and clear the queue
        /// </summary>
        public int? DequeueLevel()
        {
            var level = _queuedLevel;
            _queuedLevel = null;
            if (level.HasValue)
            {
                Debug.Log($"[PitLevelQueue] Dequeued level {level.Value} for regeneration");
            }
            return level;
        }

        /// <summary>
        /// Check if there is a queued pit level
        /// </summary>
        public bool HasQueuedLevel => _queuedLevel.HasValue;
    }
}