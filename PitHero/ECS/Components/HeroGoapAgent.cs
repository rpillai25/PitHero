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
                if (_agent.Plan())
                {
                    Debug.Log($"[GOAP] Plan created: {string.Join(" -> ", _agent.Actions)}");
                }
                else
                {
                    Debug.Log($"[GOAP] No plan found. Current state: {_agent.DescribeCurrentState()}");
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

        public HeroAgent(HeroComponent hero)
        {
            _hero = hero;

            // Only add the MoveLeft action
            _planner.AddAction(new MoveLeftAction());
        }

        public override GoapWorldState GetWorldState()
        {
            var ws = GoapWorldState.Create(_planner);
            
            // Set the hero as initialized (always true once the component is added)
            ws.Set("HeroInitialized", true);
            
            // Set moving left state (initially false)
            ws.Set("MovingLeft", false);
            
            return ws;
        }

        public override GoapWorldState GetGoalState()
        {
            var goal = GoapWorldState.Create(_planner);
            
            // Goal is to be moving left
            goal.Set("MovingLeft", true);
            
            return goal;
        }

        // Debug helpers
        public string DescribePlanner() => _planner.Describe();
        public string DescribeCurrentState() => GetWorldState().Describe(_planner);
    }
}