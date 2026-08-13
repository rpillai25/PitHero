using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.AI.GOAP;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// Planner-level tests for the manual job change chain (issue #379): with NeedsCrystal set
    /// while inside the pit, the hero plans jump-out → walk-to-statue; outside, the walk alone.
    /// </summary>
    [TestClass]
    public class JobChangeGoapPlanTests
    {
        private static ActionPlanner CreatePlanner()
        {
            var planner = new ActionPlanner();
            planner.AddAction(new JumpOutOfPitForJobChangeAction());
            planner.AddAction(new WalkToStatueForCrystalAction());
            return planner;
        }

        private static WorldState CreateState(ActionPlanner planner, bool insidePit)
        {
            var state = WorldState.Create(planner);
            state.Set(GoapConstants.HeroInitialized, true);
            state.Set(GoapConstants.InsidePit, insidePit);
            state.Set(GoapConstants.OutsidePit, !insidePit);
            state.Set(GoapConstants.NeedsCrystal, true);
            return state;
        }

        private static WorldState CreateGoal(ActionPlanner planner)
        {
            var goal = WorldState.Create(planner);
            goal.Set(GoapConstants.HasArrivedAtStatueForCrystal, true);
            return goal;
        }

        [TestMethod]
        public void NeedsCrystalInsidePit_PlansJumpOutThenWalkToStatue()
        {
            var planner = CreatePlanner();
            var plan = planner.Plan(CreateState(planner, insidePit: true), CreateGoal(planner));

            Assert.IsNotNull(plan, "Hero inside the pit should chain a jump-out before the statue walk");
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(GoapConstants.JumpOutOfPitForJobChangeAction, plan.Pop().Name);
            Assert.AreEqual(GoapConstants.WalkToStatueForCrystalAction, plan.Pop().Name);
        }

        [TestMethod]
        public void NeedsCrystalOutsidePit_PlansWalkToStatueOnly()
        {
            var planner = CreatePlanner();
            var plan = planner.Plan(CreateState(planner, insidePit: false), CreateGoal(planner));

            Assert.IsNotNull(plan, "Hero outside the pit should walk straight to the statue");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(GoapConstants.WalkToStatueForCrystalAction, plan.Pop().Name);
        }

        [TestMethod]
        public void NoNeedsCrystal_NoStatuePlan()
        {
            var planner = CreatePlanner();
            var state = WorldState.Create(planner);
            state.Set(GoapConstants.HeroInitialized, true);
            state.Set(GoapConstants.InsidePit, true);
            state.Set(GoapConstants.OutsidePit, false);
            state.Set(GoapConstants.NeedsCrystal, false);

            var plan = planner.Plan(state, CreateGoal(planner));

            Assert.IsTrue(plan == null || plan.Count == 0,
                "Without NeedsCrystal neither the jump nor the walk should be plannable");
        }
    }
}
