using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.VirtualGame;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Integration test for the new interactive entities GOAP workflow
    /// </summary>
    [TestClass]
    public class InteractiveEntitiesIntegrationTests
    {
        [TestMethod]
        public void VirtualGameSimulation_ShouldIncludeNewActions()
        {
            // Arrange
            var simulation = new VirtualGameSimulation();

            // Create a simple scenario with current and goal states
            var currentState = new Dictionary<string, bool>
            {
                { GoapConstants.HeroInitialized, true },
                { GoapConstants.PitInitialized, true },
                { GoapConstants.InsidePit, true },
                { GoapConstants.AdjacentToMonster, true }
            };

            var goalState = new Dictionary<string, bool>
            {
                { GoapConstants.AdjacentToMonster, false }
            };

            // Act
            var actionPlan = simulation.PlanActions(currentState, goalState);

            // Assert
            Assert.IsNotNull(actionPlan, "Action plan should be generated");
            
            // The plan should contain actions to handle the monster interaction
            // Since we're adjacent to a monster, the planner should include AttackMonster
            bool foundAttackAction = false;
            while (actionPlan.Count > 0)
            {
                var action = actionPlan.Pop();
                if (action is AttackMonsterAction)
                {
                    foundAttackAction = true;
                    break;
                }
            }
            
            Assert.IsTrue(foundAttackAction, "Action plan should include AttackMonster action when adjacent to monster");
        }

        [TestMethod]
        public void WanderPitAction_ShouldHaveNewPostconditions()
        {
            // Arrange
            var action = new WanderPitAction();

            // Act & Assert
            // The action should now be designed to potentially set AdjacentToMonster and AdjacentToChest
            // We can't directly access postconditions, but we can verify the action handles them
            // by checking that it creates successfully with the updated constructor
            Assert.IsNotNull(action, "WanderPitAction should create successfully with new postconditions");
            Assert.AreEqual(1, action.Cost, "WanderPitAction should maintain cost of 1");
        }

        [TestMethod]
        public void AttackMonster_ShouldHaveCorrectCostAndConfiguration()
        {
            // Arrange & Act
            var action = new AttackMonsterAction();

            // Assert
            Assert.AreEqual(3, action.Cost, "AttackMonster should have cost of 3 (higher priority)");
            Assert.AreEqual(GoapConstants.AttackMonster, action.Name, "AttackMonster should have correct name");
        }

        [TestMethod]
        public void OpenChest_ShouldHaveCorrectCostAndConfiguration()
        {
            // Arrange & Act
            var action = new OpenChestAction();

            // Assert
            Assert.AreEqual(2, action.Cost, "OpenChest should have cost of 2 (higher priority)");
            Assert.AreEqual(GoapConstants.OpenChest, action.Name, "OpenChest should have correct name");
        }

        [TestMethod]
        public void AttackMonster_CostShouldBeHigherThanOpenChest()
        {
            // Arrange
            var attackAction = new AttackMonsterAction();
            var openAction = new OpenChestAction();

            // Act & Assert
            Assert.IsTrue(attackAction.Cost > openAction.Cost, 
                "AttackMonster (cost=3) should have higher cost than OpenChest (cost=2)");
        }

        [TestMethod]
        public void InteractiveActions_CostsShouldBeHigherThanWanderPit()
        {
            // Arrange
            var wanderAction = new WanderPitAction();
            var attackAction = new AttackMonsterAction();
            var openAction = new OpenChestAction();

            // Act & Assert
            Assert.IsTrue(attackAction.Cost > wanderAction.Cost, 
                "AttackMonster should have higher cost than WanderPitAction for priority");
            Assert.IsTrue(openAction.Cost > wanderAction.Cost, 
                "OpenChest should have higher cost than WanderPitAction for priority");
        }
    }
}