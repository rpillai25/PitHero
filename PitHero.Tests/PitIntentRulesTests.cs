using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the pure pit-intent rules that let mercenaries key their jump-in/jump-out
    /// goals off the hero's plan instead of waiting for the hero's InsidePit flag to flip.
    /// </summary>
    [TestClass]
    public class PitIntentRulesTests
    {
        [TestMethod]
        public void EffectiveInsidePit_EnteringPit_OverridesActualFlag()
        {
            Assert.IsTrue(PitIntentRules.EffectiveInsidePit(HeroPitIntent.EnteringPit, false),
                "EnteringPit intent must read as inside even while the hero is still outside");
            Assert.IsTrue(PitIntentRules.EffectiveInsidePit(HeroPitIntent.EnteringPit, true),
                "EnteringPit intent with the hero already inside stays inside");
        }

        [TestMethod]
        public void EffectiveInsidePit_ExitingPit_OverridesActualFlag()
        {
            Assert.IsFalse(PitIntentRules.EffectiveInsidePit(HeroPitIntent.ExitingPit, true),
                "ExitingPit intent must read as outside even while the hero is still inside");
            Assert.IsFalse(PitIntentRules.EffectiveInsidePit(HeroPitIntent.ExitingPit, false),
                "ExitingPit intent with the hero already outside stays outside");
        }

        [TestMethod]
        public void EffectiveInsidePit_None_FollowsActualFlag()
        {
            Assert.IsTrue(PitIntentRules.EffectiveInsidePit(HeroPitIntent.None, true));
            Assert.IsFalse(PitIntentRules.EffectiveInsidePit(HeroPitIntent.None, false));
        }

        [TestMethod]
        public void Settle_ClearsIntentWhenDestinationReached()
        {
            Assert.AreEqual(HeroPitIntent.None, PitIntentRules.Settle(HeroPitIntent.EnteringPit, true),
                "EnteringPit settles once InsidePit becomes true");
            Assert.AreEqual(HeroPitIntent.None, PitIntentRules.Settle(HeroPitIntent.ExitingPit, false),
                "ExitingPit settles once InsidePit becomes false");
        }

        [TestMethod]
        public void Settle_KeepsIntentWhenDestinationNotReached()
        {
            Assert.AreEqual(HeroPitIntent.EnteringPit, PitIntentRules.Settle(HeroPitIntent.EnteringPit, false),
                "EnteringPit persists while the hero is still outside");
            Assert.AreEqual(HeroPitIntent.ExitingPit, PitIntentRules.Settle(HeroPitIntent.ExitingPit, true),
                "ExitingPit persists while the hero is still inside");
        }

        [TestMethod]
        public void Settle_None_StaysNone()
        {
            Assert.AreEqual(HeroPitIntent.None, PitIntentRules.Settle(HeroPitIntent.None, true));
            Assert.AreEqual(HeroPitIntent.None, PitIntentRules.Settle(HeroPitIntent.None, false));
        }

        [TestMethod]
        public void ComputePitIntentFromPlan_JumpIntoPit_ReturnsEnteringPit()
        {
            var plan = new Stack<Nez.AI.GOAP.Action>();
            plan.Push(new WanderPitAction());
            plan.Push(new JumpIntoPitAction());

            Assert.AreEqual(HeroPitIntent.EnteringPit, HeroStateMachine.ComputePitIntentFromPlan(plan));
        }

        [TestMethod]
        public void ComputePitIntentFromPlan_JumpOutForInn_ReturnsExitingPit()
        {
            var plan = new Stack<Nez.AI.GOAP.Action>();
            plan.Push(new SleepInBedAction());
            plan.Push(new JumpOutOfPitForInnAction());

            Assert.AreEqual(HeroPitIntent.ExitingPit, HeroStateMachine.ComputePitIntentFromPlan(plan));
        }

        [TestMethod]
        public void ComputePitIntentFromPlan_JumpOutForStop_ReturnsExitingPit()
        {
            var plan = new Stack<Nez.AI.GOAP.Action>();
            plan.Push(new WalkToTavernForStopAction());
            plan.Push(new JumpOutOfPitForStopAction());

            Assert.AreEqual(HeroPitIntent.ExitingPit, HeroStateMachine.ComputePitIntentFromPlan(plan));
        }

        [TestMethod]
        public void ComputePitIntentFromPlan_NoJumpActions_ReturnsNone()
        {
            var plan = new Stack<Nez.AI.GOAP.Action>();
            plan.Push(new WanderPitAction());

            Assert.AreEqual(HeroPitIntent.None, HeroStateMachine.ComputePitIntentFromPlan(plan));
        }
    }
}
