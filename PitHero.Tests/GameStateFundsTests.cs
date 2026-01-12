using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;

namespace PitHero.Tests
{
    [TestClass]
    public class GameStateFundsTests
    {
        [TestMethod]
        public void GameStateService_Funds_StartsAtZero()
        {
            // Arrange
            var gameState = new GameStateService();

            // Assert
            Assert.AreEqual(0, gameState.Funds, "Funds should start at 0");
        }

        [TestMethod]
        public void GameStateService_Funds_CanBeIncreased()
        {
            // Arrange
            var gameState = new GameStateService();

            // Act
            gameState.Funds += 100;

            // Assert
            Assert.AreEqual(100, gameState.Funds, "Funds should increase to 100");
        }

        [TestMethod]
        public void GameStateService_Funds_CanBeDecreased()
        {
            // Arrange
            var gameState = new GameStateService();
            gameState.Funds = 100;

            // Act
            gameState.Funds -= 30;

            // Assert
            Assert.AreEqual(70, gameState.Funds, "Funds should decrease to 70");
        }

        [TestMethod]
        public void GameStateService_Funds_PersistsAcrossMultipleOperations()
        {
            // Arrange
            var gameState = new GameStateService();

            // Act
            gameState.Funds += 50;  // Defeat monster worth 50 gold
            gameState.Funds += 30;  // Defeat monster worth 30 gold
            gameState.Funds -= 20;  // Buy something
            gameState.Funds += 100; // Defeat monster worth 100 gold

            // Assert
            Assert.AreEqual(160, gameState.Funds, "Funds should be 160 after all operations");
        }

        [TestMethod]
        public void GameStateService_Funds_SupportsLargeValues()
        {
            // Arrange
            var gameState = new GameStateService();

            // Act
            gameState.Funds = 999999;

            // Assert
            Assert.AreEqual(999999, gameState.Funds, "Funds should support large values");
        }
    }
}
