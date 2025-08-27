using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using PitHero.AI;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests to validate that all GOAP actions support interface-based execution
    /// </summary>
    [TestClass]
    public class GoapActionInterfaceValidationTests
    {
        [TestMethod]
        public void AllCoreGoapActions_ShouldSupportInterfaceBasedExecution()
        {
            // Capture console output for validation
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                // Setup virtual context
                var virtualWorld = new VirtualWorldState();
                virtualWorld.RegeneratePit(10);
                var context = new VirtualGoapContext(virtualWorld);
                
                Console.WriteLine("=== GOAP Action Interface Validation ===");
                Console.WriteLine();
                
                // Test JumpIntoPitAction (replaces MoveToPitAction)
                Console.WriteLine("Testing JumpIntoPitAction interface support...");
                var jumpIntoPitAction = new JumpIntoPitAction();
                var result1 = jumpIntoPitAction.Execute(context);
                Assert.IsNotNull(result1, "JumpIntoPitAction should return a result");
                Console.WriteLine("✓ JumpIntoPitAction supports interface-based execution");
                
                // Setup hero inside pit for other actions
                var pitCenter = new Point(virtualWorld.PitBounds.X + 2, virtualWorld.PitBounds.Y + 2);
                context.HeroController.MoveTo(pitCenter);
                context.HeroController.InsidePit = true;
                
                // Test WanderPitAction  
                Console.WriteLine("Testing WanderPitAction interface support...");
                var wanderAction = new WanderPitAction();
                var result2 = wanderAction.Execute(context);
                Assert.IsNotNull(result2, "WanderPitAction should return a result");
                Console.WriteLine("✓ WanderPitAction supports interface-based execution");
                
                // Test MoveToWizardOrbAction
                Console.WriteLine("Testing ActivateWizardOrbAction interface support...");
                // Clear fog around wizard orb first
                if (virtualWorld.WizardOrbPosition.HasValue)
                {
                    context.WorldState.ClearFogOfWar(virtualWorld.WizardOrbPosition.Value, 1);
                }
                // Test ActivateWizardOrbAction (actual wizard orb activation)
                Console.WriteLine("Testing ActivateWizardOrbAction interface support...");
                var activateWizardOrbAction = new ActivateWizardOrbAction();
                var result3 = activateWizardOrbAction.Execute(context);
                Assert.IsNotNull(result3, "ActivateWizardOrbAction should return a result");
                Console.WriteLine("✓ ActivateWizardOrbAction supports interface-based execution");
                
                // Test ActivateWizardOrbAction
                Console.WriteLine("Testing ActivateWizardOrbAction interface support...");
                // Move hero to wizard orb position
                if (virtualWorld.WizardOrbPosition.HasValue)
                {
                    context.HeroController.MoveTo(virtualWorld.WizardOrbPosition.Value);
                }
                var activateOrbAction = new ActivateWizardOrbAction();
                var result4 = activateOrbAction.Execute(context);
                Assert.IsNotNull(result4, "ActivateWizardOrbAction should return a result");
                Console.WriteLine("✓ ActivateWizardOrbAction supports interface-based execution");
                
                // Validate that interface-based execution was used
                var output = stringWriter.ToString();
                bool usedInterfaceExecution = output.Contains("Starting execution with interface-based context");
                
                Console.WriteLine();
                Console.WriteLine($"Interface-based execution detected: {usedInterfaceExecution}");
                Console.WriteLine("All core GOAP actions support interface-based execution!");
                
                Assert.IsTrue(usedInterfaceExecution, "Actions should use interface-based execution when available");
                
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        
        [TestMethod]
        public void VirtualGoapContext_ShouldProvideAllRequiredInterfaces()
        {
            var virtualWorld = new VirtualWorldState();
            var context = new VirtualGoapContext(virtualWorld);
            
            // Verify all required interfaces are available
            Assert.IsNotNull(context.WorldState, "WorldState interface should be available");
            Assert.IsNotNull(context.HeroController, "HeroController interface should be available");
            Assert.IsNotNull(context.Pathfinder, "Pathfinder interface should be available");
            Assert.IsNotNull(context.PitLevelManager, "PitLevelManager interface should be available");
            
            // Verify interfaces are functional
            Assert.IsTrue(context.Pathfinder.IsInitialized, "Pathfinder should be initialized");
            Assert.IsTrue(context.WorldState.PitLevel > 0, "World state should have valid pit level");
            Assert.IsNotNull(context.GetGoapWorldState(), "Should provide GOAP world state");
            
            Console.WriteLine("✓ All required interfaces are available and functional");
        }
    }
}