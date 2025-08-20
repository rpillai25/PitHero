using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.ECS;
using PitHero.Events;

namespace PitHero.Systems
{
    /// <summary>
    /// System that manages hero behavior and processing
    /// </summary>
    public class HeroSystem : BaseSystem
    {
        protected override void OnUpdate(WorldState worldState, float deltaTime)
        {
            var heroes = worldState.GetEntitiesWithComponent<HeroComponent>();
            
            foreach (var hero in heroes)
            {
                var heroComponent = hero.GetComponent<HeroComponent>();
                if (heroComponent == null || !heroComponent.IsAlive)
                    continue;

                // Update GOAP agent instead of simple AI
                var goapAgent = hero.GetComponent<HeroGoapAgent>();
                goapAgent?.Update();
            }
        }
        
        protected override void OnProcessEvent(IEvent gameEvent, WorldState worldState)
        {
            switch (gameEvent)
            {
                case HeroSpawnEvent spawnEvent:
                    HandleHeroSpawn(spawnEvent, worldState);
                    break;
                    
                case HeroMoveEvent moveEvent:
                    HandleHeroMove(moveEvent, worldState);
                    break;
                    
                case HeroDeathEvent deathEvent:
                    HandleHeroDeath(deathEvent, worldState);
                    break;
                    
                case PitEvent pitEvent:
                    HandlePitInteraction(pitEvent, worldState);
                    break;
            }
        }
        
        private void HandleHeroSpawn(HeroSpawnEvent spawnEvent, WorldState worldState)
        {
            var hero = new Entity($"Hero_{spawnEvent.HeroId}");
            hero.Position = spawnEvent.Position;
            
            var heroComponent = new HeroComponent
            {
                Health = spawnEvent.Health,
                MaxHealth = spawnEvent.Health,
                IsAtCenter = true // Hero starts at center
            };
            
            var historian = new Historian();
            var goapAgent = new HeroGoapAgent();
            
            var renderComponent = new BasicRenderableComponent
            {
                Color = GameConfig.HeroColor,
                RenderWidth = GameConfig.HeroWidth,
                RenderHeight = GameConfig.HeroHeight
            };
            
            // Add collider for trigger detection
            var collider = new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight);
            collider.PhysicsLayer = GameConfig.PhysicsHeroWorldLayer;
            
            hero.AddComponent(heroComponent);
            hero.AddComponent(historian);
            hero.AddComponent(goapAgent);
            hero.AddComponent(renderComponent);
            hero.AddComponent(collider);
            
            worldState.AddEntity(hero);
        }
        
        private void HandleHeroMove(HeroMoveEvent moveEvent, WorldState worldState)
        {
            var hero = worldState.GetEntity(moveEvent.HeroId);
            if (hero?.GetComponent<HeroComponent>() != null)
            {
                hero.Position = moveEvent.ToPosition;
                hero.GetComponent<HeroComponent>().Velocity = moveEvent.Velocity;
            }
        }
        
        private void HandleHeroDeath(HeroDeathEvent deathEvent, WorldState worldState)
        {
            var hero = worldState.GetEntity(deathEvent.HeroId);
            var heroComponent = hero?.GetComponent<HeroComponent>();
            
            if (heroComponent != null)
            {
                heroComponent.Health = 0f;
                heroComponent.Velocity = Vector2.Zero;
                
                // Display milestone summary on death
                var historian = hero.GetComponent<Historian>();
                if (historian != null)
                {
                    var summary = historian.GenerateSummary();
                    Debug.Log($"Hero died! {summary}");
                }
                
                // Could change render color to indicate death
                var renderComponent = hero.GetComponent<BasicRenderableComponent>();
                if (renderComponent != null)
                {
                    renderComponent.Color = Color.Gray;
                }
            }
        }
        
        private void HandlePitInteraction(PitEvent pitEvent, WorldState worldState)
        {
            // Check if any heroes are affected by this pit event
            var heroes = worldState.GetEntitiesWithComponent<HeroComponent>();
            
            foreach (var hero in heroes)
            {
                var heroComponent = hero.GetComponent<HeroComponent>();
                if (!heroComponent.IsAlive)
                    continue;
                    
                var distance = Vector2.Distance(hero.Position, pitEvent.Position);
                
                // If hero is close to the pit, they might be affected
                if (distance < 100f) // Effect radius
                {
                    if (pitEvent.EventType == "damage")
                    {
                        heroComponent.Health -= 10f * pitEvent.CrystalPower;
                        
                        if (heroComponent.Health <= 0)
                        {
                            // Hero died from pit damage - this would trigger a death event
                            heroComponent.Health = 0f;
                        }
                    }
                }
            }
        }
    }
}