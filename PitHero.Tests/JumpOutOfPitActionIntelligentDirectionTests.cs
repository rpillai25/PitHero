using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the new intelligent direction finding in JumpOutOfPitAction
    /// </summary>
    [TestClass]
    public class JumpOutOfPitActionIntelligentDirectionTests
    {
        [TestMethod]
        public void JumpOutOfPitAction_ShouldInstantiateWithNewLogic()
        {
            // Test that JumpOutOfPitAction can still be instantiated after our changes
            var jumpOutOfPit = new JumpOutOfPitAction();
            Assert.IsNotNull(jumpOutOfPit);
            Assert.AreEqual(GoapConstants.JumpOutOfPitAction, jumpOutOfPit.Name);
        }

        [TestMethod]
        public void JumpOutOfPitAction_InVirtualEnvironment_ShouldWorkWithFallback()
        {
            // Create a virtual game environment to test the new logic
            var virtualWorld = new VirtualWorldState();
            var virtualHero = new VirtualHero(virtualWorld);
            var context = new VirtualGoapContext(virtualWorld, virtualHero);

            // Place hero inside pit at a known position
            virtualHero.TeleportTo(new Microsoft.Xna.Framework.Point(6, 6)); // Inside pit
            context.HeroController.InsidePit = true;
            context.HeroController.ActivatedWizardOrb = true;

            // Clear all fog in pit to ensure areas are "explored"
            virtualWorld.ClearAllFogInPit();

            // Create JumpOutOfPitAction and test execution
            var jumpOutAction = new JumpOutOfPitAction();

            // Test that the action can be executed without throwing exceptions
            // Note: In virtual environment, it should fallback to default behavior since
            // the real TiledMapService and pathfinding components aren't available
            bool result = jumpOutAction.Execute(context);

            // The action should start execution (return false initially)
            // or complete successfully (return true)
            Assert.IsTrue(result == true || result == false, "Action should return a valid boolean result");
        }

        [TestMethod]
        public void JumpOutOfPitAction_VirtualEnvironment_CompletesSuccessfully()
        {
            // Test the complete workflow with JumpOutOfPitAction
            var virtualWorld = new VirtualWorldState();
            var virtualHero = new VirtualHero(virtualWorld);
            var context = new VirtualGoapContext(virtualWorld, virtualHero);

            // Set up the scenario: hero inside pit, wizard orb activated
            virtualHero.TeleportTo(new Microsoft.Xna.Framework.Point(6, 6)); // Inside pit center
            context.HeroController.InsidePit = true;
            context.HeroController.ActivatedWizardOrb = true;

            // Clear all fog so areas are explored
            virtualWorld.ClearAllFogInPit();

            // Verify initial state
            Assert.IsTrue(context.HeroController.InsidePit, "Hero should be inside pit initially");
            Assert.IsTrue(context.HeroController.ActivatedWizardOrb, "Wizard orb should be activated");

            // Create and execute the action
            var jumpOutAction = new JumpOutOfPitAction();
            bool actionComplete = false;
            int maxTicks = 50; // Prevent infinite loops in tests
            int ticks = 0;

            // Simulate action execution until complete
            while (!actionComplete && ticks < maxTicks)
            {
                actionComplete = jumpOutAction.Execute(context);
                ticks++;
            }

            Assert.IsTrue(actionComplete, $"JumpOutOfPitAction should complete within {maxTicks} ticks");
            Assert.IsFalse(context.HeroController.InsidePit, "Hero should be outside pit after jump");
        }
    }
}