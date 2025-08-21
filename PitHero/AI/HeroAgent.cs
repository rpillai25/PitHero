using Nez;
using Nez.AI.GOAP;
using PitHero.ECS.Components;

namespace PitHero.AI
{
    public class HeroAgent : Agent
    {
        private readonly HeroComponent _hero;

        // Make planner accessible for debugging
        public ActionPlanner Planner => _planner;

        public HeroAgent(HeroComponent hero)
        {
            _hero = hero;

            // Add all available actions
            _planner.AddAction(new MoveLeftAction());
            _planner.AddAction(new JumpIntoPitAction());
        }

        public override WorldState GetWorldState()
        {
            var ws = WorldState.Create(_planner);

            // Only set the states that are actually true
            ws.Set("HeroInitialized", true);

            // Only set MovingLeft if actually moving left
            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null && tileMover.IsMoving && tileMover.CurrentDirection == Direction.Left)
            {
                ws.Set("MovingLeft", true);
            }

            // Only set pit states if they're actually true
            if (_hero.AdjacentToPitBoundaryFromOutside)
                ws.Set("AdjacentToPitBoundaryFromOutside", true);
            if (_hero.AdjacentToPitBoundaryFromInside)
                ws.Set("AdjacentToPitBoundaryFromInside", true);
            if (_hero.EnteredPit)
                ws.Set("EnteredPit", true);

            Debug.Log($"[GOAP] Actual state bits: HeroInit=true, others=false");
            return ws;
        }

        public override WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);

            // Final objective: hero ends up inside the pit
            goal.Set("EnteredPit", true);
            // (Optional) also require inside-boundary flag:
            // goal.Set("AdjacentToPitBoundaryFromInside", true);

            return goal;
        }

        // Debug helpers
        public string DescribePlanner() => _planner.Describe();
        public string DescribeCurrentState() => GetWorldState().Describe(_planner);
    }
}
