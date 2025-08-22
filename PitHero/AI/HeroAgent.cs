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
            _planner.AddAction(new MoveToPitAction());
            _planner.AddAction(new JumpIntoPitAction());
        }

        public override WorldState GetWorldState()
        {
            var ws = WorldState.Create(_planner);

            // Only set the states that are actually true
            ws.Set(GoapConstants.HeroInitialized, true);

            if (_hero.PitInitialized)
                ws.Set(GoapConstants.PitInitialized, true);

            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null && tileMover.IsMoving && !_hero.AdjacentToPitBoundaryFromOutside)
            {
                ws.Set(GoapConstants.MovingToPit, true);
            }

            if (_hero.AdjacentToPitBoundaryFromOutside)
                ws.Set(GoapConstants.AdjacentToPitBoundaryFromOutside, true);
            if (_hero.AdjacentToPitBoundaryFromInside)
                ws.Set(GoapConstants.AdjacentToPitBoundaryFromInside, true);
            if (_hero.EnteredPit)
                ws.Set(GoapConstants.EnteredPit, true);

            Debug.Log($"[GOAP] State: PitInitialized={_hero.PitInitialized}, " +
                      $"AdjOut={_hero.AdjacentToPitBoundaryFromOutside}, " +
                      $"AdjIn={_hero.AdjacentToPitBoundaryFromInside}, " +
                      $"EnteredPit={_hero.EnteredPit}");
            return ws;
        }

        public override WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);
            goal.Set(GoapConstants.EnteredPit, true);
            return goal;
        }

        public string DescribePlanner() => _planner.Describe();
        public string DescribeCurrentState() => GetWorldState().Describe(_planner);
    }
}
