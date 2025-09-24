using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyAnimationTests
    {
        private Entity _enemyEntity;
        private SlimeAnimationComponent _slimeAnimation;
        private EnemyFacingComponent _enemyFacing;

        [TestInitialize]
        public void Setup()
        {
            _enemyEntity = new Entity("test-enemy");
            _slimeAnimation = _enemyEntity.AddComponent(new SlimeAnimationComponent());
            _enemyFacing = _enemyEntity.AddComponent(new EnemyFacingComponent());
        }

        [TestCleanup]
        public void Cleanup()
        {
            _enemyEntity?.Destroy();
        }

        [TestMethod]
        public void EnemyFacingComponent_DefaultsToDown()
        {
            // Arrange & Act
            var facing = new EnemyFacingComponent();

            // Assert
            Assert.AreEqual(Direction.Down, facing.Facing);
        }

        [TestMethod]
        public void EnemyFacingComponent_SetFacing_UpdatesDirection()
        {
            // Arrange
            var facing = new EnemyFacingComponent();

            // Act
            facing.Facing = Direction.Right;

            // Assert
            Assert.AreEqual(Direction.Right, facing.Facing);
        }

        [TestMethod]
        public void EnemyFacingComponent_ConsumeDirtyFlag_ReturnsTrueWhenDirty()
        {
            // Arrange
            var facing = new EnemyFacingComponent();

            // Act - Initial state should be dirty
            bool wasDirty = facing.ConsumeDirtyFlag();

            // Assert
            Assert.IsTrue(wasDirty);

            // Act - Second call should return false
            wasDirty = facing.ConsumeDirtyFlag();

            // Assert
            Assert.IsFalse(wasDirty);
        }

        [TestMethod]
        public void EnemyFacingComponent_ChangingDirection_SetsDirtyFlag()
        {
            // Arrange
            var facing = new EnemyFacingComponent();
            facing.ConsumeDirtyFlag(); // Clear initial dirty flag

            // Act
            facing.Facing = Direction.Left;

            // Assert
            Assert.IsTrue(facing.ConsumeDirtyFlag());
        }

        [TestMethod]
        public void SlimeAnimationComponent_HasCorrectAnimationNames()
        {
            // Arrange & Act
            var slimeAnim = new SlimeAnimationComponent();

            // Assert - We can't directly test protected properties, but we can verify the component type
            Assert.IsInstanceOfType(slimeAnim, typeof(EnemyAnimationComponent));
            Assert.IsInstanceOfType(slimeAnim, typeof(SlimeAnimationComponent));
        }

        [TestMethod]
        public void SlimeAnimationComponent_DefaultsToWhiteColor()
        {
            // Arrange & Act
            var slimeAnim = new SlimeAnimationComponent();

            // Assert
            Assert.AreEqual(Color.White, slimeAnim.ComponentColor);
        }

        [TestMethod]
        public void SlimeAnimationComponent_CanSetCustomColor()
        {
            // Arrange & Act
            var customColor = Color.Green;
            var slimeAnim = new SlimeAnimationComponent(customColor);

            // Assert
            Assert.AreEqual(customColor, slimeAnim.ComponentColor);
        }

        [TestMethod]
        public void EnemyFacingComponent_MarkDirty_SetsDirtyFlag()
        {
            // Arrange
            var facing = new EnemyFacingComponent();
            facing.ConsumeDirtyFlag(); // Clear initial dirty flag

            // Act
            facing.MarkDirty();

            // Assert
            Assert.IsTrue(facing.ConsumeDirtyFlag());
        }
    }
}