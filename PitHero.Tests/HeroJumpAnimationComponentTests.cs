using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class HeroJumpAnimationComponentTests
    {
        private Entity _heroEntity;
        private HeroAnimationComponent _heroAnimation;
        private HeroJumpAnimationComponent _jumpAnimation;

        [TestInitialize]
        public void Setup()
        {
            // Create a test entity with required components
            _heroEntity = new Entity("test-hero");
            _heroAnimation = _heroEntity.AddComponent(new HeroAnimationComponent());
            _jumpAnimation = _heroEntity.AddComponent(new HeroJumpAnimationComponent());
        }

        [TestCleanup]
        public void Cleanup()
        {
            _heroEntity?.Destroy();
        }

        [TestMethod]
        public void HeroJumpAnimationComponent_ShouldInitialize()
        {
            // Test that the component can be created and added to an entity
            Assert.IsNotNull(_jumpAnimation);
            Assert.IsNotNull(_jumpAnimation.Entity);
            Assert.AreEqual(_heroEntity, _jumpAnimation.Entity);
        }

        [TestMethod]
        public void IsJumping_ShouldReturnFalseInitially()
        {
            // Test that the hero is not jumping initially
            Assert.IsFalse(_jumpAnimation.IsJumping);
        }

        [TestMethod]
        public void StartJump_ShouldSetJumpingState()
        {
            // Test that calling StartJump sets the jumping state
            _jumpAnimation.StartJump(Direction.Left, 1.0f);
            Assert.IsTrue(_jumpAnimation.IsJumping);
        }

        [TestMethod]
        public void EndJump_ShouldClearJumpingState()
        {
            // Test that calling EndJump clears the jumping state
            _jumpAnimation.StartJump(Direction.Right, 1.0f);
            Assert.IsTrue(_jumpAnimation.IsJumping);
            
            _jumpAnimation.EndJump();
            Assert.IsFalse(_jumpAnimation.IsJumping);
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
                    _jumpAnimation.StartJump(direction, 0.5f);
                    _jumpAnimation.EndJump();
                }
                catch (System.Exception ex)
                {
                    Assert.Fail($"StartJump with direction {direction} should not throw exception: {ex.Message}");
                }
            }
        }
    }
}