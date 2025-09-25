using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to explore the pit by moving to the nearest unknown tile
    /// </summary>
    public class WanderPitAction : HeroActionBase
    {
        public WanderPitAction() : base(GoapConstants.WanderPitAction, 1)
        {
            SetPrecondition(GoapConstants.InsidePit, true);
            SetPrecondition(GoapConstants.ExploredPit, false);
            SetPrecondition(GoapConstants.AdjacentToChest, false);
            SetPrecondition(GoapConstants.AdjacentToMonster, false);

            SetPostcondition(GoapConstants.FoundWizardOrb, true);
            SetPostcondition(GoapConstants.ExploredPit, true);
            SetPostcondition(GoapConstants.AdjacentToChest, true);
            SetPostcondition(GoapConstants.AdjacentToMonster, true);
        }

        /// <summary>
        /// Execute the simplified action - clear fog around current location and check wizard orb
        /// Movement is now handled by the GoTo state in HeroStateMachine
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover == null)
            {
                Debug.Warn("WanderPitAction: Hero entity missing TileByTileMover component");
                return true;
            }

            // Get current tile position
            var currentTile = tileMover.GetCurrentTileCoordinates();
            Debug.Log($"[WanderPitAction] Executing at tile ({currentTile.X},{currentTile.Y})");

            // Clear fog of war around this tile using hero's UncoverRadius
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService != null)
            {
                bool fogCleared = tiledMapService.ClearFogOfWarAroundTile(currentTile.X, currentTile.Y, hero.UncoverRadius);
                
                // Trigger fog cooldown if fog was cleared
                if (fogCleared)
                {
                    hero.TriggerFogCooldown();
                }

                // Check for adjacent monsters and chests after clearing fog
                hero.AdjacentToMonster = hero.CheckAdjacentToMonster();
                hero.AdjacentToChest = hero.CheckAdjacentToChest();

                // Check if wizard orb was uncovered (fog cleared at orb tile)
                CheckWizardOrbFound(hero, tiledMapService, currentTile);
               
                
                Debug.Log($"[WanderPitAction] Adjacent check: Monster={hero.AdjacentToMonster}, Chest={hero.AdjacentToChest}");
            }
            else
            {
                Debug.Warn("[WanderPitAction] No TiledMapService available");
            }

            Debug.Log("[WanderPitAction] Action completed");
            return true; // Action completed
        }

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug($"[WanderPitAction] Starting execution with interface-based context");

            // Get current tile position
            var currentTile = context.HeroController.CurrentTilePosition;
            context.LogDebug($"[WanderPitAction] Executing at tile ({currentTile.X},{currentTile.Y})");

            // Clear fog of war around this tile
            context.WorldState.ClearFogOfWar(currentTile, 1);
            
            context.LogDebug("[WanderPitAction] Action completed");
            return true; // Action completed
        }

        /// <summary>
        /// Check if wizard orb has been found (fog cleared at orb tile), independent of hero position
        /// </summary>
        private void CheckWizardOrbFound(HeroComponent hero, TiledMapService tiledMapService, Point position)
        {
            if (hero.FoundWizardOrb)
            {
                Debug.Log("[Wander] CheckWizardOrbFound: Already found");
                return;
            }

            var scene = Core.Scene;
            if (scene == null)
            {
                Debug.Log("[Wander] CheckWizardOrbFound: No active scene");
                return;
            }

            // Locate the wizard orb entity
            var wizardOrbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            if (wizardOrbEntities.Count == 0)
            {
                Debug.Log("[Wander] CheckWizardOrbFound: No wizard orb entities found");
                return;
            }

            var wizardOrbEntity = wizardOrbEntities[0];
            var worldPos = wizardOrbEntity.Transform.Position;
            var orbTile = new Point((int)(worldPos.X / GameConfig.TileSize), (int)(worldPos.Y / GameConfig.TileSize));
            Debug.Log($"[Wander] CheckWizardOrbFound: Orb at world {worldPos.X},{worldPos.Y} tile {orbTile.X},{orbTile.Y}");

            // Inspect FogOfWar layer at the orb tile
            var fogLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("FogOfWar");
            if (fogLayer == null)
            {
                Debug.Log("[Wander] CheckWizardOrbFound: No FogOfWar layer found - assuming orb discovered");
                hero.FoundWizardOrb = true;
                return;
            }

            if (orbTile.X >= 0 && orbTile.Y >= 0 && orbTile.X < fogLayer.Width && orbTile.Y < fogLayer.Height)
            {
                var fogTile = fogLayer.GetTile(orbTile.X, orbTile.Y);
                Debug.Log($"[Wander] CheckWizardOrbFound: Fog tile at orb {orbTile.X},{orbTile.Y}: {(fogTile == null ? "NULL (cleared)" : "EXISTS (not cleared)")}");

                if (fogTile == null)
                {
                    hero.FoundWizardOrb = true;
                    Debug.Log($"[Wander] *** WIZARD ORB FOUND *** Setting FoundWizardOrb=true at tile {orbTile.X},{orbTile.Y}");
                }
            }
            else
            {
                Debug.Warn($"[Wander] CheckWizardOrbFound: Orb tile {orbTile.X},{orbTile.Y} out of fog layer bounds {fogLayer.Width},{fogLayer.Height}");
            }
        }
    }
}