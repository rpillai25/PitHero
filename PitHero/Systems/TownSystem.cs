using Microsoft.Xna.Framework;
using Nez;
using PitHero.Components;
using PitHero.ECS;
using PitHero.Events;

namespace PitHero.Systems
{
    /// <summary>
    /// System that manages town buildings and their effects
    /// </summary>
    public class TownSystem : BaseSystem
    {
        protected override void OnUpdate(WorldState worldState, float deltaTime)
        {
            var buildings = worldState.GetEntitiesWithComponent<TownBuildingComponent>();
            
            foreach (var building in buildings)
            {
                var buildingComponent = building.GetComponent<TownBuildingComponent>();
                
                if (buildingComponent.IsConstructed)
                {
                    // Apply building effects to nearby entities
                    ApplyBuildingEffects(building, buildingComponent, worldState, deltaTime);
                }
            }
        }
        
        protected override void OnProcessEvent(IEvent gameEvent, WorldState worldState)
        {
            switch (gameEvent)
            {
                case BuildingPlaceEvent buildingEvent:
                    HandleBuildingPlacement(buildingEvent, worldState);
                    break;
            }
        }
        
        private void HandleBuildingPlacement(BuildingPlaceEvent buildingEvent, WorldState worldState)
        {
            var building = new Entity($"Building_{buildingEvent.BuildingId}");
            building.Position = buildingEvent.Position;
            
            var buildingComponent = new TownBuildingComponent
            {
                BuildingType = buildingEvent.BuildingType,
                IsConstructed = true,
                EffectRadius = GetBuildingEffectRadius(buildingEvent.BuildingType)
            };
            
            var renderComponent = new BasicRenderableComponent
            {
                Color = GameConfig.TownColor,
                RenderWidth = GameConfig.TownBuildingWidth,
                RenderHeight = GameConfig.TownBuildingHeight
            };
            
            building.AddComponent(buildingComponent);
            building.AddComponent(renderComponent);
            
            worldState.AddEntity(building);
        }
        
        private void ApplyBuildingEffects(Entity building, TownBuildingComponent buildingComponent, WorldState worldState, float deltaTime)
        {
            switch (buildingComponent.BuildingType)
            {
                case "healing_tower":
                    ApplyHealingTowerEffect(building, buildingComponent, worldState, deltaTime);
                    break;
                    
                case "defense_wall":
                    ApplyDefenseWallEffect(building, buildingComponent, worldState, deltaTime);
                    break;
                    
                case "speed_boost":
                    ApplySpeedBoostEffect(building, buildingComponent, worldState, deltaTime);
                    break;
                    
                default:
                    // Unknown building type
                    break;
            }
        }
        
        private void ApplyHealingTowerEffect(Entity building, TownBuildingComponent buildingComponent, WorldState worldState, float deltaTime)
        {
            var heroes = worldState.GetEntitiesWithComponent<HeroComponent>();
            
            foreach (var hero in heroes)
            {
                var heroComponent = hero.GetComponent<HeroComponent>();
                if (!heroComponent.IsAlive)
                    continue;
                    
                var distance = Vector2.Distance(hero.Position, building.Position);
                
                if (distance <= buildingComponent.EffectRadius)
                {
                    // Heal hero over time
                    var healAmount = 20f * deltaTime; // 20 HP per second
                    heroComponent.Health = MathHelper.Clamp(
                        heroComponent.Health + healAmount,
                        0f,
                        heroComponent.MaxHealth
                    );
                }
            }
        }
        
        private void ApplyDefenseWallEffect(Entity building, TownBuildingComponent buildingComponent, WorldState worldState, float deltaTime)
        {
            // Defense walls could reduce damage taken by nearby heroes
            // or block certain types of attacks
            var heroes = worldState.GetEntitiesWithComponent<HeroComponent>();
            
            foreach (var hero in heroes)
            {
                var heroComponent = hero.GetComponent<HeroComponent>();
                if (!heroComponent.IsAlive)
                    continue;
                    
                var distance = Vector2.Distance(hero.Position, building.Position);
                
                if (distance <= buildingComponent.EffectRadius)
                {
                    // Could apply a defense buff or damage reduction
                    // For now, just a placeholder for the effect
                }
            }
        }
        
        private void ApplySpeedBoostEffect(Entity building, TownBuildingComponent buildingComponent, WorldState worldState, float deltaTime)
        {
            var heroes = worldState.GetEntitiesWithComponent<HeroComponent>();
            
            foreach (var hero in heroes)
            {
                var heroComponent = hero.GetComponent<HeroComponent>();
                if (!heroComponent.IsAlive)
                    continue;
                    
                var distance = Vector2.Distance(hero.Position, building.Position);
                
                if (distance <= buildingComponent.EffectRadius)
                {
                    // Increase hero move speed
                    heroComponent.MoveSpeed = GameConfig.HeroMoveSpeed * 1.5f; // 50% speed boost
                }
                else
                {
                    // Reset to normal speed when out of range
                    heroComponent.MoveSpeed = GameConfig.HeroMoveSpeed;
                }
            }
        }
        
        private float GetBuildingEffectRadius(string buildingType)
        {
            return buildingType switch
            {
                "healing_tower" => 80f,
                "defense_wall" => 60f,
                "speed_boost" => 100f,
                _ => 50f
            };
        }
    }
}