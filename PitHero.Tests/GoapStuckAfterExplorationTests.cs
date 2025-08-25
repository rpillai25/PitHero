using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.AI;
using PitHero.VirtualGame;
using System.Linq;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests to reproduce and verify the fix for the issue where hero gets stuck after exploration
    /// with "No action plan satisfied our goals" due to InsidePit being incorrectly reset to false
    /// </summary>
    [TestClass]
    public class GoapStuckAfterExplorationTests
    {
        /// <summary>
        /// Test that reproduces the exact scenario from the user's log:
        /// - Level 40 pit (pit width expanded)
        /// - Hero finishes exploration (0 fog tiles remaining)
        /// - Wizard orb found at tile 9,4
        /// - Verify that hero maintains InsidePit=true and can proceed to wizard orb activation
        /// </summary>
        [TestMethod]
        public void Level40Pit_HeroFinishesExploration_ShouldMaintainInsidePitAndProceedToWizardOrb()
        {
            // Arrange - Set up level 40 pit scenario exactly as described in the log
            var simulation = new VirtualGameSimulation();
            simulation.InitializeLevel40Pit();
            
            // Simulate hero jumping into pit and starting exploration
            simulation.HeroJumpIntoPit();
            
            // Act - Simulate complete exploration of the pit (clearing all fog)
            simulation.CompleteExploration();
            
            // Verify the state matches the log: exploration complete, wizard orb found
            Assert.IsTrue(simulation.IsMapExplored(), "Map should be fully explored");
            Assert.IsTrue(simulation.IsWizardOrbFound(), "Wizard orb should be found at tile 9,4");
            
            // Critical verification: Hero should still be InsidePit=true after exploration
            Assert.IsTrue(simulation.Hero.InsidePit, "Hero should remain InsidePit=true after exploration completion");
            
            // Act - Try to get GOAP action plan for next objective
            var goapContext = simulation.CreateGoapContext();
            var virtualWorldState = goapContext.WorldState as VirtualWorldState;
            var currentState = virtualWorldState.GetCurrentState();
            var goalState = simulation.GetProgressiveGoalState(currentState);
            
            // The goal should now be ActivatedWizardOrb since map is explored
            Assert.IsTrue(goalState.ContainsKey(GoapConstants.ActivatedWizardOrb), "Goal should be ActivatedWizardOrb");
            Assert.IsTrue(goalState[GoapConstants.ActivatedWizardOrb], "Goal should be ActivatedWizardOrb=true");
            
            // Critical test: GOAP should be able to find a valid action plan
            var actionPlan = simulation.PlanActions(currentState, goalState);
            Assert.IsNotNull(actionPlan, "GOAP should find a valid action plan after exploration");
            Assert.IsTrue(actionPlan.Count > 0, "Action plan should not be empty");
            
            // The first action should be MoveToWizardOrbAction
            var firstAction = actionPlan.Peek().Name;
            Assert.AreEqual(GoapConstants.MoveToWizardOrbAction, firstAction, 
                "First action should be MoveToWizardOrbAction");
            
            // Act - Execute the MoveToWizardOrbAction to ensure it works
            var moveToOrbAction = actionPlan.Pop() as MoveToWizardOrbAction;
            Assert.IsNotNull(moveToOrbAction, "First action should be MoveToWizardOrbAction");
            
            // Execute the action and verify it progresses correctly
            bool actionCompleted = false;
            int maxIterations = 100; // Prevent infinite loops
            int iterations = 0;
            
            while (!actionCompleted && iterations < maxIterations)
            {
                actionCompleted = moveToOrbAction.Execute(goapContext);
                if (!actionCompleted)
                {
                    simulation.TickHeroMovement(); // Simulate movement progress
                }
                iterations++;
            }
            
            Assert.IsTrue(actionCompleted, "MoveToWizardOrbAction should complete successfully");
            Assert.IsTrue(simulation.Hero.CurrentTilePosition.X == 9 && simulation.Hero.CurrentTilePosition.Y == 4, 
                "Hero should reach wizard orb at tile 9,4");
        }
        
        /// <summary>
        /// Test that verifies the complete workflow from exploration to pit regeneration
        /// </summary>
        [TestMethod]
        public void Level40Pit_CompleteWorkflowAfterExploration_ShouldSucceed()
        {
            // Arrange
            var simulation = new VirtualGameSimulation();
            simulation.InitializeLevel40Pit();
            simulation.HeroJumpIntoPit();
            simulation.CompleteExploration();
            
            // Verify starting conditions
            Assert.IsTrue(simulation.IsMapExplored());
            Assert.IsTrue(simulation.Hero.InsidePit);
            
            // Act & Assert - Execute the workflow step by step
            
            // Step 1: Should move to wizard orb
            var goapContext = simulation.CreateGoapContext();
            var virtualWorldState = goapContext.WorldState as VirtualWorldState;
            var currentState = virtualWorldState.GetCurrentState();
            var goalState = simulation.GetProgressiveGoalState(currentState);
            
            var actionPlan = simulation.PlanActions(currentState, goalState);
            Assert.IsNotNull(actionPlan, "Should find initial action plan");
            Assert.IsTrue(actionPlan.Count > 0, "Initial action plan should not be empty");
            
            var firstAction = actionPlan.Peek();
            Assert.AreEqual(GoapConstants.MoveToWizardOrbAction, firstAction.Name, "First action should be MoveToWizardOrbAction");
            simulation.ExecuteAction(firstAction);
            
            // Step 2: Should activate wizard orb
            goapContext = simulation.CreateGoapContext();
            virtualWorldState = goapContext.WorldState as VirtualWorldState;
            currentState = virtualWorldState.GetCurrentState();
            goalState = simulation.GetProgressiveGoalState(currentState);
            
            actionPlan = simulation.PlanActions(currentState, goalState);
            Assert.IsNotNull(actionPlan, "Should find action plan for wizard orb activation");
            
            var secondAction = actionPlan.Peek();
            Assert.AreEqual(GoapConstants.ActivateWizardOrbAction, secondAction.Name, "Second action should be ActivateWizardOrbAction");
            simulation.ExecuteAction(secondAction);
            
            // After wizard orb activation, verify hero state has been updated
            Assert.IsTrue(simulation.Hero.ActivatedWizardOrb, "Hero should have ActivatedWizardOrb flag set");
            Assert.IsTrue(simulation.Hero.MovingToInsidePitEdge, "Hero should have MovingToInsidePitEdge flag set");
            
            // The remaining workflow would continue, but for this test we've verified the critical fix:
            // The hero no longer gets stuck after exploration and can proceed to wizard orb activation
        }
        
        /// <summary>
        /// Test the specific pit trigger exit logic fix
        /// </summary>
        [TestMethod]
        public void PitTriggerExit_HeroStillInPitArea_ShouldNotResetInsidePitFlag()
        {
            // Arrange
            var simulation = new VirtualGameSimulation();
            simulation.InitializeLevel40Pit();
            simulation.HeroJumpIntoPit();
            
            // Hero is now inside pit and InsidePit should be true
            Assert.IsTrue(simulation.Hero.InsidePit);
            
            // Act - Simulate a spurious trigger exit event while hero is still in pit area
            simulation.Hero.CurrentTilePosition = new Point(9, 5); // Inside pit area
            simulation.TriggerPitExit(); // This simulates OnTriggerExit being called
            
            // Assert - InsidePit should remain true because hero is still in pit area
            Assert.IsTrue(simulation.Hero.InsidePit, 
                "InsidePit should remain true when hero is still inside pit area");
            
            // Act - Now simulate hero actually leaving pit area
            simulation.Hero.CurrentTilePosition = new Point(34, 6); // Outside pit area
            simulation.TriggerPitExit();
            
            // Assert - Now InsidePit should be false because hero is truly outside
            Assert.IsFalse(simulation.Hero.InsidePit,
                "InsidePit should be false when hero is truly outside pit area");
        }
    }
}