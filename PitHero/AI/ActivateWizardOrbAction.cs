using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Util;
using System.Collections;

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

            QueueNextPitLevel();

            // Regenerate the queued pit level immediately
            if (!RegenerateQueuedPitLevel())
            {
                Debug.Warn("[ActivateWizardOrb] Failed to regenerate pit level");
                return true; // Complete as failed
            }

            // Reposition hero to starting position inside pit (where they normally land when jumping in)
            RepositionHeroToStartPosition(hero);

            // Set hero state flags - values from ActivateWizardOrbAction
            hero.FoundWizardOrb = false;  // Reset according to specification

            // Set hero state flags - values from ActivatePitRegenAction (these take priority)
            hero.PitInitialized = true;
            hero.ExploredPit = false;  // Reset upon regeneration
            hero.ActivatedWizardOrb = false;  // Reset upon regeneration

            Debug.Log("[ActivateWizardOrb] Wizard orb activation complete - pit regenerated and hero repositioned");
            return true; // Action complete
        }

        /// <summary>Execute in virtual context.</summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[ActivateWizardOrbAction] Starting execution with interface-based context");

            // Activate wizard orb in virtual world
            context.WorldState.ActivateWizardOrb();

            // Regenerate pit immediately instead of just queueing
            var nextLevel = context.PitLevelManager.DequeueLevel();
            if (nextLevel.HasValue)
            {
                context.PitGenerator.RegenerateForLevel(nextLevel.Value);
                context.LogDebug($"[ActivateWizardOrbAction] Generated pit level {nextLevel.Value}");
            }
            else
            {
                context.LogWarning("[ActivateWizardOrbAction] No queued pit level found");
                return true; // Complete as failed
            }

            // Attempt to clear fog if underlying world state supports it (virtual only convenience)
            var vw = context.WorldState as VirtualGame.VirtualWorldState;
            if (vw != null)
            {
                vw.ClearAllFogInPit();
                context.LogDebug("[ActivateWizardOrbAction] Cleared all fog in virtual pit");
            }

            // Reposition hero to starting position inside pit
            var pitRightEdge = context.PitWidthManager.CurrentPitRightEdge;
            var startTileX = pitRightEdge - 2;
            var startTileY = 6; // GameConfig.PitCenterTileY
            var startTile = new Point(startTileX, startTileY);
            context.HeroController.MoveTo(startTile);
            context.LogDebug($"[ActivateWizardOrbAction] Repositioned hero to starting position ({startTileX},{startTileY})");

            // Set hero state flags - values from ActivateWizardOrbAction
            context.HeroController.ActivatedWizardOrb = true;
            context.HeroController.PitInitialized = false;
            context.HeroController.FoundWizardOrb = false;  // Reset according to specification

            // Set hero state flags - values from ActivatePitRegenAction (these take priority)
            context.HeroController.PitInitialized = true;
            context.HeroController.ExploredPit = false;  // Reset upon regeneration
            context.HeroController.ActivatedWizardOrb = false;  // Reset upon regeneration

            context.LogDebug($"[ActivateWizardOrbAction] Wizard orb activation complete - pit regenerated and hero repositioned");
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

        /// <summary>
        /// Regenerate the queued pit level using PitWidthManager by reinitializing right edge and setting level
        /// </summary>
        private bool RegenerateQueuedPitLevel()
        {
            // Get the queued level from the service
            var queueService = Core.Services.GetService<PitLevelQueueService>();
            if (queueService == null)
            {
                Debug.Error("[ActivateWizardOrb] PitLevelQueueService not found");
                return false;
            }

            var nextLevel = queueService.DequeueLevel();
            if (!nextLevel.HasValue)
            {
                Debug.Warn("[ActivateWizardOrb] No queued pit level found");
                return false;
            }

            // Use PitWidthManager to apply level change
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
            {
                Debug.Error("[ActivateWizardOrb] PitWidthManager not found");
                return false;
            }

            // Set the new level (which will trigger regeneration)
            pitWidthManager.SetPitLevel(nextLevel.Value);
            Debug.Log($"[ActivateWizardOrb] Successfully set pit level {nextLevel.Value} via PitWidthManager");

            return true;
        }

        /// <summary>
        /// Reposition hero to the starting position inside the pit (where they normally land when jumping in)
        /// </summary>
        private void RepositionHeroToStartPosition(HeroComponent hero)
        {
            // Calculate the starting position inside the pit (2 tiles from the right edge)
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var pitRightEdge = pitWidthManager?.CurrentPitRightEdge ?? (GameConfig.PitRectX + GameConfig.PitRectWidth);

            // Hero starts at the right edge minus 2 tiles (inside the pit), at center Y
            var targetTile = new Point(pitRightEdge - 2, GameConfig.PitCenterTileY);
            var targetPosition = TileToWorldPosition(targetTile);

            Debug.Log($"[ActivateWizardOrb] Repositioning hero to starting position at tile ({targetTile.X},{targetTile.Y})");

            // Set hero position
            hero.Entity.Transform.Position = targetPosition;

            // Snap to tile grid for precision
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SnapToTileGrid();
                // Force trigger update so the pit enter trigger updates immediately
                tileMover.UpdateTriggersAfterTeleport();

                // Disable movement for 1 second after repositioning
                tileMover.Enabled = false;
                Core.StartCoroutine(EnableMoverAfterDelay(tileMover));
            }

            // Clear fog of war around the landing position
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            bool fogCleared = tiledMapService?.ClearFogOfWarAroundTile(targetTile.X, targetTile.Y, hero) ?? false;

            // Trigger fog cooldown if fog was cleared
            if (fogCleared)
            {
                hero.TriggerFogCooldown();
            }

            // Check for adjacent monsters and chests after repositioning
            hero.AdjacentToMonster = hero.CheckAdjacentToMonster();
            hero.AdjacentToChest = hero.CheckAdjacentToChest();

            Debug.Log($"[ActivateWizardOrb] Hero repositioned to ({targetPosition.X},{targetPosition.Y})");
        }

        /// <summary>Coroutine to re-enable TileByTileMover after 1 second delay</summary>
        private IEnumerator EnableMoverAfterDelay(TileByTileMover tileMover)
        {
            yield return Coroutine.WaitForSeconds(1.0f);
            tileMover.Enabled = true;
            Debug.Log("[ActivateWizardOrb] Hero movement re-enabled after repositioning delay");
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