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
                
                if (!heroComponent.IsAlive)
                    continue;
                    
                // Update hero position based on velocity
                if (heroComponent.Velocity != Vector2.Zero)
                {
                    var newPosition = hero.Position + heroComponent.Velocity * deltaTime;
                    
                    // Keep hero within world bounds
                    newPosition.X = MathHelper.Clamp(newPosition.X, 0, GameConfig.InternalWorldWidth - GameConfig.HeroWidth);
                    newPosition.Y = MathHelper.Clamp(newPosition.Y, 0, GameConfig.InternalWorldHeight - GameConfig.HeroHeight);
                    
                    if (newPosition != hero.Position)
                    {
                        var oldPosition = hero.Position;
                        hero.Position = newPosition;
                        
                        // Could emit a HeroMoveEvent here for detailed tracking
                    }
                }
                
                // Simple AI: heroes automatically move towards the right
                if (heroComponent.Velocity == Vector2.Zero)
                {
                    heroComponent.Velocity = new Vector2(heroComponent.MoveSpeed, 0);
                }
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
                MaxHealth = spawnEvent.Health
            };
            
            var renderComponent = new BasicRenderableComponent
            {
                Color = GameConfig.HeroColor,
                RenderWidth = GameConfig.HeroWidth,
                RenderHeight = GameConfig.HeroHeight
            };
            
            hero.AddComponent(heroComponent);
            hero.AddComponent(renderComponent);
            
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