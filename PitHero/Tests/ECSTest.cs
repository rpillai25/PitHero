using System;
using Microsoft.Xna.Framework;
using Nez;
using PitHero;
using PitHero.Events;
using PitHero.ECS;
using PitHero.Components;

namespace PitHero.Tests
{
    /// <summary>
    /// Simple test to verify the ECS structure works without graphics
    /// </summary>
    public class ECSTest
    {
        public static void RunTests()
        {
            Console.WriteLine("Starting ECS Tests...");
            
            TestBasicEntityCreation();
            TestEventSystem();
            TestGameManager();
            
            Console.WriteLine("All ECS tests passed!");
        }
        
        private static void TestBasicEntityCreation()
        {
            Console.WriteLine("Testing basic entity creation...");
            
            var worldState = new WorldState();
            
            // Create a hero entity
            var hero = new Entity("TestHero");
            hero.Position = new Vector2(100, 200);
            
            var heroComponent = new HeroComponent { Health = 100f };
            var renderComponent = new RenderComponent { Color = Color.Blue };
            
            hero.AddComponent(heroComponent);
            hero.AddComponent(renderComponent);
            
            worldState.AddEntity(hero);
            
            // Verify entity was added
            var retrievedHero = worldState.GetEntity(hero.Id);
            if (retrievedHero == null)
                throw new Exception("Failed to retrieve added entity");
                
            // Verify components
            var retrievedHeroComp = retrievedHero.GetComponent<HeroComponent>();
            var retrievedRenderComp = retrievedHero.GetComponent<RenderComponent>();
            
            if (retrievedHeroComp == null || retrievedRenderComp == null)
                throw new Exception("Failed to retrieve entity components");
                
            if (retrievedHeroComp.Health != 100f)
                throw new Exception("Component data not preserved");
                
            Console.WriteLine("✓ Basic entity creation test passed");
        }
        
        private static void TestEventSystem()
        {
            Console.WriteLine("Testing event system...");
            
            var eventLog = new EventLog();
            var worldState = new WorldState();
            var eventProcessor = new EventProcessor(eventLog, worldState);
            
            // Create a spawn event
            var spawnEvent = new HeroSpawnEvent(0.0, 1, new Vector2(50, 50), 100f);
            
            // Process the event
            eventProcessor.ProcessEvent(spawnEvent);
            
            // Verify event was logged
            var events = eventLog.GetAllEvents();
            if (events.Count != 1)
                throw new Exception("Event was not logged");
                
            if (events[0].Id != spawnEvent.Id)
                throw new Exception("Wrong event logged");
                
            Console.WriteLine("✓ Event system test passed");
        }
        
        private static void TestGameManager()
        {
            Console.WriteLine("Testing game manager...");
            
            var gameManager = new GameManager();
            
            // Start a new game
            gameManager.StartNewGame();
            
            // Verify initial state
            if (gameManager.WorldState.EntityCount < 2) // Should have spawned 2 heroes
                throw new Exception("Game manager did not initialize properly");
                
            // Test spawning additional hero
            var initialCount = gameManager.WorldState.EntityCount;
            gameManager.SpawnHero(new Vector2(300, 300));
            
            if (gameManager.WorldState.EntityCount != initialCount + 1)
                throw new Exception("Game manager spawn hero failed");
                
            // Test building placement
            gameManager.PlaceBuilding(new Vector2(400, 400), "healing_tower");
            
            if (gameManager.WorldState.EntityCount != initialCount + 2)
                throw new Exception("Game manager place building failed");
                
            Console.WriteLine("✓ Game manager test passed");
        }
    }
}