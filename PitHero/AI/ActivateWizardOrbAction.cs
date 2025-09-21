using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;
using PitHero.Util;

namespace PitHero.AI
{
    /// <summary>
    /// Action that activates the wizard orb and queues the next pit level. Also clears remaining FogOfWar tiles in pit.
    /// </summary>
    public class ActivateWizardOrbAction : HeroActionBase
    {
        public ActivateWizardOrbAction() : base(GoapConstants.ActivateWizardOrbAction)
        {
            SetPrecondition(GoapConstants.InsidePit, true);
            SetPrecondition(GoapConstants.ExploredPit, true);
            SetPrecondition(GoapConstants.FoundWizardOrb, true);
            SetPostcondition(GoapConstants.ActivatedWizardOrb, true);
            SetPostcondition(GoapConstants.PitInitialized, false);
        }

        /// <summary>Execute in live scene.</summary>
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

            // Change the wizard orb tint to red to indicate activation
            var renderer = wizardOrbEntity.GetComponent<Nez.RenderableComponent>();
            if (renderer != null)
            {
                renderer.Color = Color.Red;
                Debug.Log("[ActivateWizardOrb] Changed wizard orb tint to red");
            }
            else
            {
                Debug.Warn("[ActivateWizardOrb] Wizard orb entity has no renderable component");
            }

            // Clear all remaining fog first so player sees fully cleared pit
            ClearAllFogOfWarInPit();

            // Queue the next pit level
            QueueNextPitLevel();

            // Set hero state flags according to specification
            hero.ActivatedWizardOrb = true;
            hero.PitInitialized = false;
            hero.FoundWizardOrb = false;  // Reset according to specification
            
            Debug.Log("[ActivateWizardOrb] Wizard orb activation complete - pit level queued and fog cleared");
            return true; // Action complete
        }

        /// <summary>Execute in virtual context.</summary>
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

            // Attempt to clear fog if underlying world state supports it (virtual only convenience)
            var vw = context.WorldState as VirtualGame.VirtualWorldState;
            if (vw != null)
            {
                vw.ClearAllFogInPit();
                context.LogDebug("[ActivateWizardOrbAction] Cleared all fog in virtual pit");
            }
            
            context.LogDebug($"[ActivateWizardOrbAction] Wizard orb activation complete - pit level {nextLevel} queued");
            return true; // Action complete
        }

        /// <summary>Find wizard orb entity.</summary>
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

        /// <summary>Queue next pit level for regeneration.</summary>
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

        /// <summary>Queue specific pit level.</summary>
        public static void QueuePitLevel(int level)
        {
            var queueService = Core.Services.GetService<PitLevelQueueService>();
            if (queueService == null)
            {
                queueService = new PitLevelQueueService();
                Core.Services.AddService(queueService);
            }
            
            queueService.QueueLevel(level);
            Debug.Log($"[ActivateWizardOrb] Pit level {level} added to queue");
        }

        /// <summary>Clear all fog tiles inside current pit bounds.</summary>
        private void ClearAllFogOfWarInPit()
        {
            var tms = Core.Services.GetService<TiledMapService>();
            if (tms == null || tms.CurrentMap == null)
            {
                Debug.Warn("[ActivateWizardOrb] TiledMapService or CurrentMap null - cannot clear fog");
                return;
            }

            var fogLayer = tms.CurrentMap.GetLayer<Nez.Tiled.TmxLayer>("FogOfWar");
            if (fogLayer == null)
            {
                Debug.Warn("[ActivateWizardOrb] FogOfWar layer not found");
                return;
            }

            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int pitWidthTiles = pitWidthManager?.CurrentPitRectWidthTiles ?? GameConfig.PitRectWidth;
            int pitHeightTiles = GameConfig.PitRectHeight;
            int startX = GameConfig.PitRectX + 1; // interior only
            int endX = GameConfig.PitRectX + pitWidthTiles - 2;
            int startY = GameConfig.PitRectY + 1;
            int endY = GameConfig.PitRectY + pitHeightTiles - 2;
            int clearedCount = 0;
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    var tile = fogLayer.GetTile(x, y);
                    if (tile != null)
                    {
                        tms.RemoveTile("FogOfWar", x, y);
                        clearedCount++;
                    }
                }
            }
            Debug.Log($"[ActivateWizardOrb] Cleared remaining fog tiles count={clearedCount} boundsInterior=({startX},{startY})-({endX},{endY})");
        }
    }

    /// <summary>Service to manage queued pit levels.</summary>
    public class PitLevelQueueService
    {
        private int? _queuedLevel;
        /// <summary>Queue a pit level.</summary>
        public void QueueLevel(int level)
        {
            _queuedLevel = level;
            Debug.Log($"[PitLevelQueue] Level {level} queued for regeneration");
        }
        /// <summary>Dequeue queued level (if any).</summary>
        public int? DequeueLevel()
        {
            var level = _queuedLevel;
            _queuedLevel = null;
            if (level.HasValue)
                Debug.Log($"[PitLevelQueue] Dequeued level {level.Value} for regeneration");
            return level;
        }
        /// <summary>Return true if a level is queued.</summary>
        public bool HasQueuedLevel => _queuedLevel.HasValue;
    }
}