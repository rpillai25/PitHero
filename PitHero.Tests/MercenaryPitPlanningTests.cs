using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.AI.GOAP;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// Planner-level tests for the mercenary GOAP action set: mercs enter and exit the pit
    /// through their own walk-to-edge + jump actions (triggered by the hero's effective pit
    /// state), and following is blocked until the merc's pit state matches the hero's.
    /// </summary>
    [TestClass]
    public class MercenaryPitPlanningTests
    {
        private static ActionPlanner CreatePlanner()
        {
            var planner = new ActionPlanner();
            planner.AddAction(new FollowTargetAction());
            planner.AddAction(new WalkToPitEdgeAction());
            planner.AddAction(new MercenaryJumpIntoPitAction());
            planner.AddAction(new MercenaryJumpOutOfPitAction());
            return planner;
        }

        private static WorldState CreateState(ActionPlanner planner, bool mercInPit, bool heroEffectiveInPit, bool atPitEdge, bool pitStateMatchesHero)
        {
            var state = WorldState.Create(planner);
            state.Set(GoapConstants.HeroInitialized, true);
            state.Set(GoapConstants.PitInitialized, true);
            state.Set(GoapConstants.IsAlive, true);
            state.Set(GoapConstants.MercenaryInsidePit, mercInPit);
            state.Set(GoapConstants.TargetInsidePit, heroEffectiveInPit);
            state.Set(GoapConstants.MercenaryAtPitEdge, atPitEdge);
            state.Set(GoapConstants.PitStateMatchesHero, pitStateMatchesHero);
            return state;
        }

        private static WorldState CreateGoal(ActionPlanner planner, bool heroEffectiveInPit)
        {
            var goal = WorldState.Create(planner);
            goal.Set(GoapConstants.MercenaryFollowingTarget, true);
            goal.Set(GoapConstants.MercenaryInsidePit, heroEffectiveInPit);
            return goal;
        }

        [TestMethod]
        public void HeroIntendsPit_MercOutside_PlansWalkJumpFollow()
        {
            // Hero formed a dive plan (effective inside) but hasn't landed yet (actual outside,
            // so pit states still match). The merc heads for the edge and jumps on its own.
            var planner = CreatePlanner();
            var state = CreateState(planner, mercInPit: false, heroEffectiveInPit: true, atPitEdge: false, pitStateMatchesHero: true);
            var goal = CreateGoal(planner, heroEffectiveInPit: true);

            var plan = planner.Plan(state, goal);

            // The jump itself posts MercenaryFollowingTarget, so the plan ends at the jump;
            // following is planned separately once the hero's actual pit state matches
            Assert.IsNotNull(plan, "Merc should plan its own pit entry as soon as the hero intends to dive");
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(GoapConstants.WalkToPitEdgeAction, plan.Pop().Name);
            Assert.AreEqual(GoapConstants.MercenaryJumpIntoPitAction, plan.Pop().Name);
        }

        [TestMethod]
        public void HeroIntendsExit_MercInside_PlansJumpOutThenFollow()
        {
            // Hero formed a jump-out plan (effective outside) while both are still inside
            // (states match). The merc walks to the inside edge and jumps out independently.
            var planner = CreatePlanner();
            var state = CreateState(planner, mercInPit: true, heroEffectiveInPit: false, atPitEdge: false, pitStateMatchesHero: true);
            var goal = CreateGoal(planner, heroEffectiveInPit: false);

            var plan = planner.Plan(state, goal);

            // The jump itself posts MercenaryFollowingTarget, so the plan is the jump alone
            Assert.IsNotNull(plan, "Merc should plan its own pit exit as soon as the hero intends to leave");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(GoapConstants.MercenaryJumpOutOfPitAction, plan.Pop().Name);
        }

        [TestMethod]
        public void PitStatesMatch_PlansFollowOnly()
        {
            var planner = CreatePlanner();
            var state = CreateState(planner, mercInPit: true, heroEffectiveInPit: true, atPitEdge: false, pitStateMatchesHero: true);
            var goal = CreateGoal(planner, heroEffectiveInPit: true);

            var plan = planner.Plan(state, goal);

            Assert.IsNotNull(plan, "Matched pit states should allow plain following");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(GoapConstants.FollowTargetAction, plan.Pop().Name);
        }

        [TestMethod]
        public void MercLandedBeforeHero_NoPlanUntilHeroCatchesUp()
        {
            // Merc already jumped in, hero still walking to the rim: goal is satisfied on
            // MercenaryInsidePit but following is blocked by PitStateMatchesHero=false. The
            // deliberate result is NO plan — the state machine's Idle retry waits it out.
            var planner = CreatePlanner();
            var state = CreateState(planner, mercInPit: true, heroEffectiveInPit: true, atPitEdge: false, pitStateMatchesHero: false);
            var goal = CreateGoal(planner, heroEffectiveInPit: true);

            var plan = planner.Plan(state, goal);

            Assert.IsTrue(plan == null || plan.Count == 0,
                "A merc that crossed ahead of the hero must wait, never follow across the boundary");
        }

        [TestMethod]
        public void MercExitedBeforeHero_NoPlanUntilHeroCatchesUp()
        {
            var planner = CreatePlanner();
            var state = CreateState(planner, mercInPit: false, heroEffectiveInPit: false, atPitEdge: false, pitStateMatchesHero: false);
            var goal = CreateGoal(planner, heroEffectiveInPit: false);

            var plan = planner.Plan(state, goal);

            Assert.IsTrue(plan == null || plan.Count == 0,
                "A merc that exited ahead of the hero must wait, never follow across the boundary");
        }

        [TestMethod]
        public void FollowTargetAction_RequiresPitStateMatchesHero()
        {
            var action = new FollowTargetAction();
            Assert.AreEqual(GoapConstants.FollowTargetAction, action.Name);
            // Covered behaviorally by the planning tests above; this guards the constant itself
            Assert.AreEqual("PitStateMatchesHero", GoapConstants.PitStateMatchesHero);
        }
    }
}
