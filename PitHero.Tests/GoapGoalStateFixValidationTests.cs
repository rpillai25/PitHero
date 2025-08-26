using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Focused test demonstrating the GOAP goal state fix
    /// This test validates that the progressive goal system prevents the "No action plan satisfied our goals" issue
    /// </summary>
    [TestClass]
    public class GoapGoalStateFixValidationTests
    {
        [TestMethod]
        public void GoapGoalState_AfterExploration_ShouldProgressToWizardOrbGoal()
        {
            Console.WriteLine("=== GOAP GOAL STATE FIX VALIDATION ===");
            Console.WriteLine("Testing that hero can find next goal after completing exploration");
            Console.WriteLine();
            
            // STEP 1: Initialize virtual world with completed exploration
            var virtualWorld = new VirtualWorldState();
            virtualWorld.RegeneratePit(40);
            var context = new VirtualGoapContext(virtualWorld);
            
            // Place hero inside pit
            var pitCenter = new Point(virtualWorld.PitBounds.X + 2, virtualWorld.PitBounds.Y + 2);
            context.HeroController.MoveTo(pitCenter);
            context.HeroController.InsidePit = true;
            
            // Complete exploration by clearing all fog
            var pitBounds = virtualWorld.PitBounds;
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    context.WorldState.ClearFogOfWar(new Point(x, y), 0);
                }
            }
            
            // Ensure wizard orb is found
            if (virtualWorld.WizardOrbPosition.HasValue)
            {
                context.WorldState.ClearFogOfWar(virtualWorld.WizardOrbPosition.Value, 1);
            }
            
            Console.WriteLine($"State after exploration completion:");
            Console.WriteLine($"- Map explored: {context.WorldState.IsMapExplored}");
            Console.WriteLine($"- Wizard orb found: {context.WorldState.IsWizardOrbFound}");
            Console.WriteLine($"- Wizard orb activated: {context.WorldState.IsWizardOrbActivated}");
            Console.WriteLine();
            
            // STEP 2: Test that MoveToWizardOrbAction can execute
            Console.WriteLine("Testing MoveToWizardOrbAction execution:");
            var moveToOrbAction = new MoveToWizardOrbAction();
            
            // Execute the action once - it should start successfully
            var actionResult = moveToOrbAction.Execute(context);
            
            Console.WriteLine($"MoveToWizardOrbAction execution result: {actionResult}");
            Console.WriteLine($"Hero is moving: {context.HeroController.IsMoving}");
            Console.WriteLine();
            
            // STEP 3: Move hero to wizard orb and test ActivateWizardOrbAction
            Console.WriteLine("Testing ActivateWizardOrbAction execution:");
            if (virtualWorld.WizardOrbPosition.HasValue)
            {
                context.HeroController.MoveTo(virtualWorld.WizardOrbPosition.Value);
                Console.WriteLine($"Moved hero to wizard orb at {virtualWorld.WizardOrbPosition.Value}");
                
                var activateOrbAction = new ActivateWizardOrbAction();
                var activateResult = activateOrbAction.Execute(context);
                
                Console.WriteLine($"ActivateWizardOrbAction execution result: {activateResult}");
                Console.WriteLine($"Wizard orb activated: {context.WorldState.IsWizardOrbActivated}");
                Console.WriteLine($"Moving to inside pit edge: {context.HeroController.MovingToInsidePitEdge}");
                Console.WriteLine();
            }
            
            // STEP 4: Validate the workflow progression
            Console.WriteLine("Workflow validation:");
            Console.WriteLine($"✓ Exploration completed: {context.WorldState.IsMapExplored}");
            Console.WriteLine($"✓ Wizard orb found: {context.WorldState.IsWizardOrbFound}");
            Console.WriteLine($"✓ Wizard orb activated: {context.WorldState.IsWizardOrbActivated}");
            Console.WriteLine($"✓ Next workflow step initiated: {context.HeroController.MovingToInsidePitEdge}");
            Console.WriteLine();
            
            Console.WriteLine("=== FIX VALIDATION COMPLETE ===");
            Console.WriteLine("SUCCESS: Hero successfully progresses from exploration to wizard orb workflow!");
            Console.WriteLine("The progressive goal state system prevents the stuck hero issue.");
            
            // Assertions
            Assert.IsTrue(context.WorldState.IsMapExplored, "Map should be explored");
            Assert.IsTrue(context.WorldState.IsWizardOrbFound, "Wizard orb should be found");
            Assert.IsTrue(context.WorldState.IsWizardOrbActivated, "Wizard orb should be activated");
            Assert.IsTrue(context.HeroController.MovingToInsidePitEdge, "Hero should be moving to inside pit edge");
        }

        [TestMethod] 
        public void GoapGoalState_ProgressivePlanning_ShouldAlwaysHaveValidGoal()
        {
            Console.WriteLine("=== PROGRESSIVE GOAL STATE VALIDATION ===");
            Console.WriteLine("Testing that GOAP always has a valid goal at each workflow stage");
            Console.WriteLine();
            
            var virtualWorld = new VirtualWorldState();
            virtualWorld.RegeneratePit(40);
            var context = new VirtualGoapContext(virtualWorld);
            
            // Test planner with all actions
            var planner = CreateTestPlanner();
            
            // SCENARIO 1: Initial state (nothing explored)
            Console.WriteLine("SCENARIO 1: Initial state");
            var initialState = CreateWorldState(planner, context, false, false, false, false);
            var initialGoal = CreateProgressiveGoal(planner, context, false, false, false);
            var initialPlan = planner.Plan(initialState, initialGoal);
            
            Console.WriteLine($"Initial goal: {initialGoal.Describe(planner)}");
            Console.WriteLine($"Action plan found: {initialPlan != null && initialPlan.Count > 0}");
            if (initialPlan != null && initialPlan.Count > 0)
            {
                Console.WriteLine($"Plan: {string.Join(" -> ", initialPlan)}");
            }
            Console.WriteLine();
            
            // SCENARIO 2: Exploration complete, wizard orb not activated
            Console.WriteLine("SCENARIO 2: Exploration complete, wizard orb workflow pending");
            var exploredState = CreateWorldState(planner, context, true, true, false, false);
            var exploredGoal = CreateProgressiveGoal(planner, context, true, false, false);
            var exploredPlan = planner.Plan(exploredState, exploredGoal);
            
            Console.WriteLine($"Explored goal: {exploredGoal.Describe(planner)}");
            Console.WriteLine($"Action plan found: {exploredPlan != null && exploredPlan.Count > 0}");
            if (exploredPlan != null && exploredPlan.Count > 0)
            {
                Console.WriteLine($"Plan: {string.Join(" -> ", exploredPlan)}");
            }
            Console.WriteLine();
            
            // SCENARIO 3: Wizard orb activated, moving to regeneration
            Console.WriteLine("SCENARIO 3: Wizard orb activated, moving to regeneration point");
            var activatedState = CreateWorldState(planner, context, true, true, true, false);
            var activatedGoal = CreateProgressiveGoal(planner, context, true, true, false);
            var activatedPlan = planner.Plan(activatedState, activatedGoal);
            
            Console.WriteLine($"Activated goal: {activatedGoal.Describe(planner)}");
            Console.WriteLine($"Action plan found: {activatedPlan != null && activatedPlan.Count > 0}");
            if (activatedPlan != null && activatedPlan.Count > 0)
            {
                Console.WriteLine($"Plan: {string.Join(" -> ", activatedPlan)}");
            }
            Console.WriteLine();
            
            Console.WriteLine("=== PROGRESSIVE PLANNING VALIDATION COMPLETE ===");
            Console.WriteLine("SUCCESS: GOAP planner finds valid action plans at every workflow stage!");
            
            // Assertions
            Assert.IsTrue(initialPlan != null && initialPlan.Count > 0, "Should have plan for initial state");
            Assert.IsTrue(exploredPlan != null && exploredPlan.Count > 0, "Should have plan after exploration");
            Assert.IsTrue(activatedPlan != null && activatedPlan.Count > 0, "Should have plan after wizard orb activation");
        }
        
        private Nez.AI.GOAP.ActionPlanner CreateTestPlanner()
        {
            var planner = new Nez.AI.GOAP.ActionPlanner();
            
            // Add all hero actions
            planner.AddAction(new MoveToPitAction());
            planner.AddAction(new JumpIntoPitAction());
            planner.AddAction(new WanderAction());
            planner.AddAction(new MoveToWizardOrbAction());
            planner.AddAction(new ActivateWizardOrbAction());
            planner.AddAction(new MovingToInsidePitEdgeAction());
            planner.AddAction(new JumpOutOfPitAction());
            planner.AddAction(new MoveToPitGenPointAction());
            
            return planner;
        }
        
        private Nez.AI.GOAP.WorldState CreateWorldState(Nez.AI.GOAP.ActionPlanner planner, VirtualGoapContext context, 
            bool mapExplored, bool wizardOrbFound, bool wizardOrbActivated, bool atPitGenPoint)
        {
            var ws = Nez.AI.GOAP.WorldState.Create(planner);
            
            ws.Set(GoapConstants.HeroInitialized, true);
            ws.Set(GoapConstants.PitInitialized, true);
            ws.Set(GoapConstants.InsidePit, true);
            
            if (mapExplored)
                ws.Set(GoapConstants.MapExplored, true);
            if (wizardOrbFound)
                ws.Set(GoapConstants.FoundWizardOrb, true);
            if (wizardOrbActivated)
                ws.Set(GoapConstants.ActivatedWizardOrb, true);
            if (atPitGenPoint)
                ws.Set(GoapConstants.AtPitGenPoint, true);
            
            return ws;
        }
        
        private Nez.AI.GOAP.WorldState CreateProgressiveGoal(Nez.AI.GOAP.ActionPlanner planner, VirtualGoapContext context,
            bool mapExplored, bool wizardOrbActivated, bool atPitGenPoint)
        {
            var goal = Nez.AI.GOAP.WorldState.Create(planner);
            
            // Implement the same progressive logic as the fixed HeroStateMachine
            if (!mapExplored)
            {
                goal.Set(GoapConstants.MapExplored, true);
            }
            else if (!wizardOrbActivated)
            {
                goal.Set(GoapConstants.ActivatedWizardOrb, true);
            }
            else if (!atPitGenPoint)
            {
                goal.Set(GoapConstants.AtPitGenPoint, true);
            }
            else
            {
                goal.Set(GoapConstants.OutsidePit, true);
            }
            
            return goal;
        }
    }
}