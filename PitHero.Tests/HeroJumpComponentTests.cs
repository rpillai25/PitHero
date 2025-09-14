using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroJumpComponentTests
    {
        private Entity _heroEntity;
        private HeroAnimationComponent _heroAnimation;
        private HeroJumpComponent _jumpComponent;

        [TestInitialize]
        public void Setup()
        {
            // Create a test entity with required components - add all paperdoll layers for jump component
            _heroEntity = new Entity("test-hero");
            _heroAnimation = _heroEntity.AddComponent(new HeroBodyAnimationComponent()); // Keep reference to one for tests
            _heroEntity.AddComponent(new HeroHand2AnimationComponent());
            _heroEntity.AddComponent(new HeroPantsAnimationComponent());
            _heroEntity.AddComponent(new HeroShirtAnimationComponent());
            _heroEntity.AddComponent(new HeroHairAnimationComponent());
            _heroEntity.AddComponent(new HeroHand1AnimationComponent());
            _jumpComponent = _heroEntity.AddComponent(new HeroJumpComponent());
        }

        [TestCleanup]
        public void Cleanup()
        {
            _heroEntity?.Destroy();
        }

        [TestMethod]
        public void HeroJumpComponent_ShouldInitialize()
        {
            // Test that the component can be created and added to an entity
            Assert.IsNotNull(_jumpComponent);
            Assert.IsNotNull(_jumpComponent.Entity);
            Assert.AreEqual(_heroEntity, _jumpComponent.Entity);
        }

        [TestMethod]
        public void IsJumping_ShouldReturnFalseInitially()
        {
            // Test that the hero is not jumping initially
            Assert.IsFalse(_jumpComponent.IsJumping);
        }

        [TestMethod]
        public void StartJump_ShouldSetJumpingState()
        {
            // Test that calling StartJump sets the jumping state
            _jumpComponent.StartJump(Direction.Left, 1.0f);
            Assert.IsTrue(_jumpComponent.IsJumping);
        }

        [TestMethod]
        public void EndJump_ShouldClearJumpingState()
        {
            // Test that calling EndJump clears the jumping state
            _jumpComponent.StartJump(Direction.Right, 1.0f);
            Assert.IsTrue(_jumpComponent.IsJumping);
            
            _jumpComponent.EndJump();
            Assert.IsFalse(_jumpComponent.IsJumping);
        }

        [TestMethod]
        public void StartJump_WithAllDirections_ShouldNotThrow()
        {
            // Test that all directions can be used for jump animations
            var directions = new[] { 
                Direction.Up, Direction.Down, Direction.Left, Direction.Right,
                Direction.UpLeft, Direction.UpRight, Direction.DownLeft, Direction.DownRight
            };

            foreach (var direction in directions)
            {
                try
                {
                    _jumpComponent.StartJump(direction, 0.5f);
                    _jumpComponent.EndJump();
                }
                catch (System.Exception ex)
                {
                    Assert.Fail($"StartJump with direction {direction} should not throw exception: {ex.Message}");
                }
            }
        }
    }
}