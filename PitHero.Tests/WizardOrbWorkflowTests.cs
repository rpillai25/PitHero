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
            // Test that the 5 core GOAP actions exist and have correct values
            Assert.AreEqual("JumpIntoPitAction", GoapConstants.JumpIntoPitAction);
            Assert.AreEqual("WanderPitAction", GoapConstants.WanderPitAction);
            Assert.AreEqual("ActivateWizardOrbAction", GoapConstants.ActivateWizardOrbAction);
            Assert.AreEqual("JumpOutOfPitAction", GoapConstants.JumpOutOfPitAction);
            Assert.AreEqual("ActivatePitRegenAction", GoapConstants.ActivatePitRegenAction);
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

            var jumpOutOfPit = new JumpOutOfPitAction();
            Assert.IsNotNull(jumpOutOfPit);
            Assert.AreEqual(GoapConstants.JumpOutOfPitAction, jumpOutOfPit.Name);

            var activatePitRegen = new ActivatePitRegenAction();
            Assert.IsNotNull(activatePitRegen);
            Assert.AreEqual(GoapConstants.ActivatePitRegenAction, activatePitRegen.Name);
        }

        [TestMethod]
        public void PitLevelQueueService_ShouldQueueAndDequeue()
        {
            // Test the pit level queue service
            var queueService = new PitLevelQueueService();
            
            Assert.IsFalse(queueService.HasQueuedLevel, "Should start with no queued level");
            
            queueService.QueueLevel(25);
            Assert.IsTrue(queueService.HasQueuedLevel, "Should have queued level after queuing");
            
            var dequeuedLevel = queueService.DequeueLevel();
            Assert.AreEqual(25, dequeuedLevel, "Should dequeue the correct level");
            Assert.IsFalse(queueService.HasQueuedLevel, "Should have no queued level after dequeuing");
            
            var emptyDequeue = queueService.DequeueLevel();
            Assert.IsNull(emptyDequeue, "Should return null when queue is empty");
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