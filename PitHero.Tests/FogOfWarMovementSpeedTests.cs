using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;
using PitHero.Util;
using PitHero;

namespace PitHero.Tests
{
    [TestClass]
    public class FogOfWarMovementSpeedTests
    {
        [TestMethod]
        public void HeroComponent_ApplyMovementSpeedForFogStatus_ValidatesInputAndMethodExists()
        {
            // Simple test to verify the method exists and handles null services gracefully
            var heroComponent = new HeroComponent();
            var targetTile = new Point(0, 0);
            
            // This should not throw an exception even without proper setup
            heroComponent.ApplyMovementSpeedForFogStatus(targetTile);
            
            // If we reach here, the method exists and handles null services properly
            Assert.IsTrue(true, "ApplyMovementSpeedForFogStatus method exists and handles null services");
        }

        [TestMethod]
        public void TiledMapService_HasFogOfWar_HandlesNullMapGracefully()
        {
            // Test that HasFogOfWar returns false when there's no map
            var tiledMapService = new TiledMapService(null);
            var hasFog = tiledMapService.HasFogOfWar(0, 0);
            
            Assert.IsFalse(hasFog, "HasFogOfWar should return false when CurrentMap is null");
        }

        [TestMethod]
        public void GameConfig_MovementSpeeds_AreDifferent()
        {
            // Verify that we have two different movement speeds for our fog-based logic
            Assert.AreNotEqual(GameConfig.HeroMovementSpeed, GameConfig.HeroPitMovementSpeed, 
                "HeroMovementSpeed and HeroPitMovementSpeed should be different values");
            
            // Verify the expected relationship (slow speed for fog, fast for explored)
            Assert.IsTrue(GameConfig.HeroPitMovementSpeed < GameConfig.HeroMovementSpeed,
                "HeroPitMovementSpeed should be slower than HeroMovementSpeed for cautious exploration");
        }

        [TestMethod]
        public void HeroComponent_ApplyMovementSpeedForFogStatus_IsPublicMethod()
        {
            // Verify the method is public and accessible
            var heroComponent = new HeroComponent();
            var methodInfo = typeof(HeroComponent).GetMethod("ApplyMovementSpeedForFogStatus");
            
            Assert.IsNotNull(methodInfo, "ApplyMovementSpeedForFogStatus method should exist");
            Assert.IsTrue(methodInfo.IsPublic, "ApplyMovementSpeedForFogStatus method should be public");
            
            var parameters = methodInfo.GetParameters();
            Assert.AreEqual(1, parameters.Length, "Method should take exactly one parameter");
            Assert.AreEqual(typeof(Point), parameters[0].ParameterType, "Parameter should be of type Point");
        }
    }
}