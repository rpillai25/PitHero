using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class PaperdollAnimationSystemTests
    {
        private Entity _heroEntity;
        private List<HeroAnimationComponent> _paperdollLayers;

        [TestInitialize]
        public void Setup()
        {
            // Create a test entity with all paperdoll layers
            _heroEntity = new Entity("paperdoll-test-hero");
            
            _paperdollLayers = new List<HeroAnimationComponent>
            {
                _heroEntity.AddComponent(new HeroHand2AnimationComponent(Color.White)),
                _heroEntity.AddComponent(new HeroBodyAnimationComponent(Color.White)),
                _heroEntity.AddComponent(new HeroPantsAnimationComponent(Color.White)),
                _heroEntity.AddComponent(new HeroShirtAnimationComponent(Color.White)),
                _heroEntity.AddComponent(new HeroHairAnimationComponent(Color.White)),
                _heroEntity.AddComponent(new HeroHand1AnimationComponent(Color.White))
            };
            
            _heroEntity.AddComponent(new TileByTileMover());
        }

        [TestCleanup]
        public void Cleanup()
        {
            _heroEntity?.Destroy();
        }

        [TestMethod]
        public void PaperdollSystem_AllLayersCreated_ShouldHaveCorrectCount()
        {
            Assert.AreEqual(6, _paperdollLayers.Count, "Should have exactly 6 paperdoll layers");
            Assert.IsTrue(_paperdollLayers.All(layer => layer != null), "All layers should be non-null");
        }

        [TestMethod]
        public void PaperdollSystem_DefaultAnimations_ShouldBeCorrect()
        {
            var expectedAnimations = new[]
            {
                "HeroHand2WalkDown",
                "HeroBodyWalkDown", 
                "HeroPantsWalkDown",
                "HeroShirtWalkDown",
                "HeroHairWalkDown",
                "HeroHand1WalkDown"
            };

            for (int i = 0; i < _paperdollLayers.Count; i++)
            {
                var layer = _paperdollLayers[i];
                var layerType = layer.GetType();
                var defaultAnimationProperty = layerType.GetProperty("DefaultAnimation", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                Assert.IsNotNull(defaultAnimationProperty);
                var defaultAnimation = defaultAnimationProperty.GetValue(layer) as string;
                Assert.AreEqual(expectedAnimations[i], defaultAnimation, 
                    $"Layer {i} ({layerType.Name}) should have correct default animation");
            }
        }

        [TestMethod]
        public void PaperdollSystem_JumpComponent_ShouldWorkWithAllLayers()
        {
            var jumpComponent = _heroEntity.AddComponent(new HeroJumpComponent());
            
            // Should not throw when starting/ending jumps
            jumpComponent.StartJump(Direction.Up, 1.0f);
            Assert.IsTrue(jumpComponent.IsJumping);
            
            jumpComponent.EndJump();
            Assert.IsFalse(jumpComponent.IsJumping);
        }

        [TestMethod]
        public void PaperdollSystem_AnimationDirections_ShouldWorkForAllLayers()
        {
            var directions = new[]
            {
                Direction.Up, Direction.Down, Direction.Left, Direction.Right,
                Direction.UpLeft, Direction.UpRight, Direction.DownLeft, Direction.DownRight
            };

            foreach (var direction in directions)
            {
                foreach (var layer in _paperdollLayers)
                {
                    // Should not throw when updating animation for any direction
                    try
                    {
                        layer.UpdateAnimationForDirection(direction);
                        // If we get here, no exception was thrown - that's good
                    }
                    catch (System.Exception ex)
                    {
                        Assert.Fail($"UpdateAnimationForDirection should not throw for direction {direction} on layer {layer.GetType().Name}: {ex.Message}");
                    }
                    
                    // Should return valid jump animation name
                    var jumpAnimName = layer.GetJumpAnimationNameForDirection(direction);
                    Assert.IsNotNull(jumpAnimName);
                    Assert.IsTrue(jumpAnimName.StartsWith("Hero") && jumpAnimName.Contains("Jump"),
                        $"Jump animation name should be valid for {layer.GetType().Name}");
                }
            }
        }
    }
}