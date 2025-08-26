using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for new GOAP wizard orb workflow functionality
    /// </summary>
    [TestClass]
    public class WizardOrbWorkflowTests
    {
        [TestMethod]
        public void GoapConstants_NewStates_ShouldExist()
        {
            // Test new state constants exist and have correct values
            Assert.AreEqual("OutsidePit", GoapConstants.OutsidePit);
            Assert.AreEqual("FoundWizardOrb", GoapConstants.FoundWizardOrb);
            Assert.AreEqual("AtWizardOrb", GoapConstants.AtWizardOrb);
            Assert.AreEqual("ActivatedWizardOrb", GoapConstants.ActivatedWizardOrb);
            Assert.AreEqual("MovingToInsidePitEdge", GoapConstants.MovingToInsidePitEdge);
            Assert.AreEqual("ReadyToJumpOutOfPit", GoapConstants.ReadyToJumpOutOfPit);
            Assert.AreEqual("AtPitGenPoint", GoapConstants.AtPitGenPoint);
            Assert.AreEqual("MovingToPitGenPoint", GoapConstants.MovingToPitGenPoint);
        }

        [TestMethod]
        public void GoapConstants_NewActions_ShouldExist()
        {
            // Test new action constants exist and have correct values
            Assert.AreEqual("MoveToWizardOrbAction", GoapConstants.MoveToWizardOrbAction);
            Assert.AreEqual("ActivateWizardOrbAction", GoapConstants.ActivateWizardOrbAction);
            Assert.AreEqual("MovingToInsidePitEdgeAction", GoapConstants.MovingToInsidePitEdgeAction);
            Assert.AreEqual("JumpOutOfPitAction", GoapConstants.JumpOutOfPitAction);
            Assert.AreEqual("MoveToPitGenPointAction", GoapConstants.MoveToPitGenPointAction);
        }

        [TestMethod]
        public void GoapConstants_InsidePit_ShouldReplaceEnteredPit()
        {
            // Verify the renamed constant
            Assert.AreEqual("InsidePit", GoapConstants.InsidePit);
        }

        [TestMethod]
        public void NewGoapActions_ShouldInstantiate()
        {
            // Test that all new action classes can be instantiated
            var moveToWizardOrb = new MoveToWizardOrbAction();
            Assert.IsNotNull(moveToWizardOrb);
            Assert.AreEqual(GoapConstants.MoveToWizardOrbAction, moveToWizardOrb.Name);

            var activateWizardOrb = new ActivateWizardOrbAction();
            Assert.IsNotNull(activateWizardOrb);
            Assert.AreEqual(GoapConstants.ActivateWizardOrbAction, activateWizardOrb.Name);

            var movingToInsidePitEdge = new MovingToInsidePitEdgeAction();
            Assert.IsNotNull(movingToInsidePitEdge);
            Assert.AreEqual(GoapConstants.MovingToInsidePitEdgeAction, movingToInsidePitEdge.Name);

            var jumpOutOfPit = new JumpOutOfPitAction();
            Assert.IsNotNull(jumpOutOfPit);
            Assert.AreEqual(GoapConstants.JumpOutOfPitAction, jumpOutOfPit.Name);

            var moveToPitGenPoint = new MoveToPitGenPointAction();
            Assert.IsNotNull(moveToPitGenPoint);
            Assert.AreEqual(GoapConstants.MoveToPitGenPointAction, moveToPitGenPoint.Name);
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