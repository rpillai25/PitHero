using Nez;
using Nez.AI.GOAP;
using GoapWorldState = Nez.AI.GOAP.WorldState;

namespace PitHero.ECS.Components
{
    public class HeroGoapAgent : Component, IUpdatable
    {
        private HeroAgent _agent;
        private HeroComponent _hero;

        public override void OnAddedToEntity()
        {
            _hero = Entity.GetComponent<HeroComponent>();
            _agent = new HeroAgent(_hero);
        }

        public void Update()
        {
            if (_agent == null || _hero == null)
                return;

            if (!_agent.HasActionPlan())
            {
                // Enable debug planning to see what's happening
                if (_agent.Plan(debugPlan: true))
                {
                    Debug.Log($"[GOAP] Plan created: {string.Join(" -> ", _agent.Actions)}");
                }
                else
                {
                    Debug.Log($"[GOAP] No plan found. Current state: {_agent.DescribeCurrentState()}");
                    Debug.Log($"[GOAP] Goal state: {_agent.GetGoalState().Describe(_agent.Planner)}");
                    Debug.Log($"[GOAP] Planner description:\n{_agent.DescribePlanner()}");
                }
            }

            if (_agent.HasActionPlan())
            {
                var action = _agent.Actions.Peek();
                if (action is HeroActionBase heroAction && heroAction.Execute(_hero))
                    _agent.Actions.Pop();
            }
        }
    }

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

        public override GoapWorldState GetWorldState()
        {
            var ws = GoapWorldState.Create(_planner);
            
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

        public override GoapWorldState GetGoalState()
        {
            var goal = GoapWorldState.Create(_planner);
            
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