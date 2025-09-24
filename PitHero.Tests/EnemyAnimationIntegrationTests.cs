using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using RolePlayingFramework.Enemies;

namespace PitHero.Tests
{
    [TestClass]
    public class EnemyAnimationIntegrationTests
    {
        [TestMethod]
        public void SlimeEnemy_WithCompleteAnimationSystem_WorksProperly()
        {
            // Arrange - Create a complete enemy entity similar to what PitGenerator creates
            var enemyEntity = new Entity("TestSlimeEnemy");
            
            // Add all the components that PitGenerator would add for a slime
            var slime = new Slime(1);
            var enemyComponent = enemyEntity.AddComponent(new EnemyComponent(slime, isStationary: false));
            var slimeAnimation = enemyEntity.AddComponent(new SlimeAnimationComponent(Color.White));
            var enemyFacing = enemyEntity.AddComponent(new EnemyFacingComponent());
            var tileMover = enemyEntity.AddComponent(new TileByTileMover());

            // Act & Assert - Test the complete system integration
            
            // 1. Verify all components are present
            Assert.IsNotNull(enemyEntity.GetComponent<EnemyComponent>());
            Assert.IsNotNull(enemyEntity.GetComponent<SlimeAnimationComponent>());
            Assert.IsNotNull(enemyEntity.GetComponent<EnemyFacingComponent>());
            Assert.IsNotNull(enemyEntity.GetComponent<TileByTileMover>());

            // 2. Verify the enemy component has the correct slime
            Assert.AreEqual("Slime", enemyComponent.Enemy.Name);
            Assert.AreEqual(1, enemyComponent.Enemy.Level);

            // 3. Verify animation component has proper settings
            Assert.AreEqual(Color.White, slimeAnimation.ComponentColor);

            // 4. Verify facing component starts with proper defaults
            Assert.AreEqual(Direction.Down, enemyFacing.Facing);

            // 5. Test facing direction changes
            enemyFacing.Facing = Direction.Right;
            Assert.AreEqual(Direction.Right, enemyFacing.Facing);

            // 6. Test that changing direction marks as dirty
            Assert.IsTrue(enemyFacing.ConsumeDirtyFlag());
            Assert.IsFalse(enemyFacing.ConsumeDirtyFlag()); // Should be clean now

            // 7. Verify enemy is not stationary (can move)
            Assert.IsFalse(enemyComponent.IsStationary);
            Assert.IsFalse(enemyComponent.IsMoving);

            // Cleanup
            enemyEntity.Destroy();
        }

        [TestMethod]
        public void EnemyAnimationSystem_SupportsMultipleDirections()
        {
            // Arrange
            var enemyEntity = new Entity("MultiDirectionTest");
            var enemyFacing = enemyEntity.AddComponent(new EnemyFacingComponent());

            // Act & Assert - Test all cardinal directions
            var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
            
            foreach (var direction in directions)
            {
                enemyFacing.Facing = direction;
                Assert.AreEqual(direction, enemyFacing.Facing);
                Assert.IsTrue(enemyFacing.ConsumeDirtyFlag(), $"Direction {direction} should set dirty flag");
            }

            // Cleanup
            enemyEntity.Destroy();
        }
    }
}