using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using Nez;

namespace PitHero.Tests
{
    [TestClass]
    public class FundsLabelIntegrationTests
    {
        [TestMethod]
        public void GameStateService_FundsUpdates_ReflectInLabel()
        {
            // This is a conceptual test showing how the Funds label integrates
            // The actual label update happens in MainGameScene.UpdateFundsLabel()
            
            // Arrange
            var gameState = new GameStateService();
            gameState.Funds = 0;
            
            // Act - Simulate defeating a level 10 monster
            var goldEarned = RolePlayingFramework.Balance.BalanceConfig.CalculateMonsterGoldYield(10);
            gameState.Funds += goldEarned;
            
            // Assert - Label would show "Gold: 35" (5 + 10*3)
            Assert.AreEqual(35, gameState.Funds, "Funds should be 35 after defeating level 10 monster");
            
            // Act - Simulate defeating another monster
            var moreGold = RolePlayingFramework.Balance.BalanceConfig.CalculateMonsterGoldYield(25);
            gameState.Funds += moreGold;
            
            // Assert - Label would show "Gold: 115" (35 + 80)
            Assert.AreEqual(115, gameState.Funds, "Funds should be 115 after defeating both monsters");
        }

        [TestMethod]
        public void GameStateService_FundsSpending_DecreasesTotal()
        {
            // Arrange
            var gameState = new GameStateService();
            gameState.Funds = 1000;
            
            // Act - Simulate buying an item for 250 gold
            gameState.Funds -= 250;
            
            // Assert - Label would show "Gold: 750"
            Assert.AreEqual(750, gameState.Funds, "Funds should be 750 after purchase");
        }

        [TestMethod]
        public void GameStateService_MultipleFundsOperations_TrackCorrectly()
        {
            // Arrange
            var gameState = new GameStateService();
            gameState.Funds = 0;
            
            // Act - Simulate a game session
            gameState.Funds += 50;  // Defeat monster
            gameState.Funds += 30;  // Defeat another monster
            gameState.Funds -= 20;  // Buy potion
            gameState.Funds += 100; // Defeat boss
            gameState.Funds -= 80;  // Buy equipment
            
            // Assert - Label would show "Gold: 80"
            Assert.AreEqual(80, gameState.Funds, "Funds should correctly track all operations");
        }
    }
}
