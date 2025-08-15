using System;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.ECS;
using PitHero.Events;

namespace PitHero.Systems
{
    /// <summary>
    /// System that manages pit behavior and crystal logic
    /// </summary>
    public class PitSystem : BaseSystem
    {
        private bool _hasActivePit;
        
        protected override void OnUpdate(WorldState worldState, float deltaTime)
        {
            // Ensure there's always one active pit
            if (!_hasActivePit)
            {
                SpawnMainPit(worldState);
                _hasActivePit = true;
            }
            
            // Update the single active pit
            var pits = worldState.GetEntitiesWithComponent<PitComponent>();
            foreach (var pit in pits)
            {
                var pitComponent = pit.GetComponent<PitComponent>();
                
                if (pitComponent.IsActive)
                {
                    // Update pit effects, crystal power changes, etc.
                    UpdatePitEffects(pit, pitComponent, worldState, deltaTime);
                }
            }
        }
        
        protected override void OnProcessEvent(IEvent gameEvent, WorldState worldState)
        {
            switch (gameEvent)
            {
                case PitEvent pitEvent:
                    HandlePitEvent(pitEvent, worldState);
                    break;
            }
        }
        
        private void SpawnMainPit(WorldState worldState)
        {
            // Create the single main pit at a fixed position with configured size
            var position = new Vector2(
                GameConfig.InternalWorldWidth / 2,
                GameConfig.InternalWorldHeight / 2
            );
            
            var pitId = (uint)(worldState.EntityCount + 1);
            
            // Create and process a pit spawn event
            var pitEvent = new PitEvent(Time.TotalTime, pitId, position, "spawn", 1f);
            HandlePitEvent(pitEvent, worldState);
        }
        
        private void HandlePitEvent(PitEvent pitEvent, WorldState worldState)
        {
            switch (pitEvent.EventType)
            {
                case "spawn":
                    CreatePit(pitEvent, worldState);
                    break;
                    
                case "activate":
                    ActivatePit(pitEvent, worldState);
                    break;
                    
                case "deactivate":
                    DeactivatePit(pitEvent, worldState);
                    break;
                    
                case "crystal_power_change":
                    UpdatePitCrystalPower(pitEvent, worldState);
                    break;
            }
        }
        
        private void CreatePit(PitEvent pitEvent, WorldState worldState)
        {
            var pit = new Entity($"Pit_{pitEvent.PitId}");
            pit.Position = pitEvent.Position;
            
            var pitComponent = new PitComponent
            {
                CrystalPower = pitEvent.CrystalPower,
                IsActive = true,
                EffectRadius = 100f
            };
            
            var renderComponent = new BasicRenderableComponent
            {
                Color = GameConfig.PitColor,
                RenderWidth = GameConfig.PitWidth,
                RenderHeight = GameConfig.PitHeight
            };
            
            pit.AddComponent(pitComponent);
            pit.AddComponent(renderComponent);
            
            worldState.AddEntity(pit);
        }
        
        private void ActivatePit(PitEvent pitEvent, WorldState worldState)
        {
            var pit = worldState.GetEntity(pitEvent.PitId);
            var pitComponent = pit?.GetComponent<PitComponent>();
            
            if (pitComponent != null)
            {
                pitComponent.IsActive = true;
                
                var renderComponent = pit.GetComponent<BasicRenderableComponent>();
                if (renderComponent != null)
                {
                    renderComponent.Color = GameConfig.PitColor;
                }
            }
        }
        
        private void DeactivatePit(PitEvent pitEvent, WorldState worldState)
        {
            var pit = worldState.GetEntity(pitEvent.PitId);
            var pitComponent = pit?.GetComponent<PitComponent>();
            
            if (pitComponent != null)
            {
                pitComponent.IsActive = false;
                
                var renderComponent = pit.GetComponent<BasicRenderableComponent>();
                if (renderComponent != null)
                {
                    renderComponent.Color = Color.DarkRed;
                }
            }
        }
        
        private void UpdatePitCrystalPower(PitEvent pitEvent, WorldState worldState)
        {
            var pit = worldState.GetEntity(pitEvent.PitId);
            var pitComponent = pit?.GetComponent<PitComponent>();
            
            if (pitComponent != null)
            {
                pitComponent.CrystalPower = pitEvent.CrystalPower;
            }
        }
        
        private void UpdatePitEffects(Entity pit, PitComponent pitComponent, WorldState worldState, float deltaTime)
        {
            // Example pit effect: periodically damage nearby heroes
            // This is just a simple example - in a real game this might be more complex
            
            if (pitComponent.IsActive && pitComponent.CrystalPower > 0)
            {
                // Could trigger periodic damage events, crystal growth, etc.
                // For now, just a placeholder for pit-specific update logic
            }
        }
    }
}