using System;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Components;
using PitHero.ECS;
using PitHero.Events;

namespace PitHero.Systems
{
    /// <summary>
    /// System that manages pit behavior and crystal logic
    /// </summary>
    public class PitSystem : BaseSystem
    {
        private float _timeSinceLastPitSpawn;
        
        protected override void OnUpdate(WorldState worldState, float deltaTime)
        {
            _timeSinceLastPitSpawn += deltaTime;
            
            // Automatically spawn pits at intervals
            if (_timeSinceLastPitSpawn >= GameConfig.PitSpawnInterval)
            {
                SpawnRandomPit(worldState);
                _timeSinceLastPitSpawn = 0f;
            }
            
            // Update existing pits
            var pits = worldState.GetEntitiesWithComponent<PitComponent>();
            foreach (var pit in pits)
            {
                var pitComponent = pit.GetComponent<PitComponent>();
                
                if (pitComponent.IsActive)
                {
                    // Pits could have periodic effects, crystal power changes, etc.
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
        
        private void SpawnRandomPit(WorldState worldState)
        {
            var random = new System.Random();
            var position = new Vector2(
                random.Next(GameConfig.PitWidth, GameConfig.InternalWorldWidth - GameConfig.PitWidth),
                random.Next(GameConfig.PitHeight, GameConfig.InternalWorldHeight - GameConfig.PitHeight)
            );
            
            var pitId = (uint)(worldState.EntityCount + 1);
            
            // Create and process a pit spawn event
            var pitEvent = new PitEvent(worldState.GameTime, pitId, position, "spawn", 1f);
            // Note: In a real implementation, this would go through the EventProcessor
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
            
            var renderComponent = new RenderComponent
            {
                Color = GameConfig.PitColor,
                Width = GameConfig.PitWidth,
                Height = GameConfig.PitHeight,
                ZOrder = 1
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
                
                var renderComponent = pit.GetComponent<RenderComponent>();
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
                
                var renderComponent = pit.GetComponent<RenderComponent>();
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