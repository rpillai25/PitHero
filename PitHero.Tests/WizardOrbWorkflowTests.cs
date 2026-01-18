using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for simplified GOAP wizard orb workflow functionality
    /// </summary>
    [TestClass]
    public class WizardOrbWorkflowTests
    {
        [TestMethod]
        public void GoapConstants_SimplifiedStates_ShouldExist()
        {
            // Test that the 7 core GOAP states exist and have correct values
            Assert.AreEqual("HeroInitialized", GoapConstants.HeroInitialized);
            Assert.AreEqual("PitInitialized", GoapConstants.PitInitialized);
            Assert.AreEqual("InsidePit", GoapConstants.InsidePit);
            Assert.AreEqual("OutsidePit", GoapConstants.OutsidePit);
            Assert.AreEqual("ExploredPit", GoapConstants.ExploredPit);
            Assert.AreEqual("FoundWizardOrb", GoapConstants.FoundWizardOrb);
            Assert.AreEqual("ActivatedWizardOrb", GoapConstants.ActivatedWizardOrb);
        }

        [TestMethod]
        public void GoapConstants_SimplifiedActions_ShouldExist()
        {
            // Test that the 4 core GOAP actions exist and have correct values
            Assert.AreEqual("JumpIntoPitAction", GoapConstants.JumpIntoPitAction);
            Assert.AreEqual("WanderPitAction", GoapConstants.WanderPitAction);
            Assert.AreEqual("ActivateWizardOrbAction", GoapConstants.ActivateWizardOrbAction);
            Assert.AreEqual("JumpOutOfPitForInnAction", GoapConstants.JumpOutOfPitForInnAction);
        }

        [TestMethod]
        public void SimplifiedGoapActions_ShouldInstantiate()
        {
            // Test that all simplified action classes can be instantiated
            var jumpIntoPit = new JumpIntoPitAction();
            Assert.IsNotNull(jumpIntoPit);
            Assert.AreEqual(GoapConstants.JumpIntoPitAction, jumpIntoPit.Name);

            var wander = new WanderPitAction();
            Assert.IsNotNull(wander);
            Assert.AreEqual(GoapConstants.WanderPitAction, wander.Name);

            var activateWizardOrb = new ActivateWizardOrbAction();
            Assert.IsNotNull(activateWizardOrb);
            Assert.AreEqual(GoapConstants.ActivateWizardOrbAction, activateWizardOrb.Name);

            var jumpOutOfPit = new JumpOutOfPitForInnAction();
            Assert.IsNotNull(jumpOutOfPit);
            Assert.AreEqual(GoapConstants.JumpOutOfPitForInnAction, jumpOutOfPit.Name);
        }

        [TestMethod]
        public void ActivateWizardOrbAction_QueuePitLevel_ShouldWork()
        {
            // Test that the queue service can be instantiated and used directly
            // Note: Core.Services is not available in test environment
            var queueService = new PitLevelQueueService();
            
            // Test queuing functionality directly
            Assert.IsFalse(queueService.HasQueuedLevel, "Should start empty");
            
            queueService.QueueLevel(50);
            Assert.IsTrue(queueService.HasQueuedLevel, "Should have queued level");
            
            var level = queueService.DequeueLevel();
            Assert.AreEqual(50, level, "Should dequeue correct level");
            Assert.IsFalse(queueService.HasQueuedLevel, "Should be empty after dequeue");
        }
    }
}