using System;
using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero;
using PitHero.Events;
using PitHero.ECS;
using PitHero.Components;

namespace PitHero.Tests
{
    /// <summary>
    /// MSTest tests to verify the ECS structure works without graphics
    /// </summary>
    [TestClass]
    public class ECSTest
    {
        [TestMethod]
        public void TestBasicEntityCreation()
        {
            Console.WriteLine("Testing basic entity creation...");
            
            var worldState = new WorldState();
            
            // Create a hero entity
            var hero = new Entity("TestHero");
            hero.Position = new Vector2(100, 200);
            
            var heroComponent = new HeroComponent { Health = 100f };
            var renderComponent = new BasicRenderableComponent { Color = Color.Blue };
            
            hero.AddComponent(heroComponent);
            hero.AddComponent(renderComponent);
            
            worldState.AddEntity(hero);
            
            // Verify entity was added
            var retrievedHero = worldState.GetEntity(hero.Id);
            Assert.IsNotNull(retrievedHero, "Failed to retrieve added entity");
                
            // Verify components
            var retrievedHeroComp = retrievedHero.GetComponent<HeroComponent>();
            var retrievedRenderComp = retrievedHero.GetComponent<BasicRenderableComponent>();
            
            Assert.IsNotNull(retrievedHeroComp, "Failed to retrieve hero component");
            Assert.IsNotNull(retrievedRenderComp, "Failed to retrieve render component");
            Assert.AreEqual(100f, retrievedHeroComp.Health, "Component data not preserved");
                
            Console.WriteLine("✓ Basic entity creation test passed");
        }
        
        [TestMethod]
        public void TestEventSystem()
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
            Assert.AreEqual(1, events.Count, "Event was not logged");
            Assert.AreEqual(spawnEvent.Id, events[0].Id, "Wrong event logged");
                
            Console.WriteLine("✓ Event system test passed");
        }
        
        [TestMethod]
        public void TestGameManager()
        {
            Console.WriteLine("Testing game manager...");
            
            var gameManager = new GameManager();
            
            // Start a new game
            gameManager.StartNewGame();
            
            // Verify initial state - should have spawned 1 hero
            Assert.IsTrue(gameManager.WorldState.EntityCount >= 1, "Game manager did not initialize properly");
                
            // Test spawning additional hero
            var initialCount = gameManager.WorldState.EntityCount;
            gameManager.SpawnHero(new Vector2(300, 300));
            
            Assert.AreEqual(initialCount + 1, gameManager.WorldState.EntityCount, "Game manager spawn hero failed");
                
            // Test building placement
            gameManager.PlaceBuilding(new Vector2(400, 400), "healing_tower");
            
            Assert.AreEqual(initialCount + 2, gameManager.WorldState.EntityCount, "Game manager place building failed");
                
            Console.WriteLine("✓ Game manager test passed");
        }
    }
}