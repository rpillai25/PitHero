using Nez;
using Nez.AI.GOAP;
using GoapWorldState = Nez.AI.GOAP.WorldState;

namespace PitHero.ECS.Components
{
    public class HeroGoapAgent : Component, IUpdatable
    {
        private HeroAgent _agent;
        private HeroComponent _hero;
        private Historian _historian;

        public override void OnAddedToEntity()
        {
            _hero = Entity.GetComponent<HeroComponent>();
            _historian = Entity.GetComponent<Historian>();
            _agent = new HeroAgent(_hero, _historian);
        }

        public void Update()
        {
            if (_agent == null || _hero == null)
                return;

            if (!_agent.HasActionPlan())
            {
                if (_agent.Plan())
                {
                    Debug.Log($"[GOAP] Plan: {string.Join(" -> ", _agent.Actions)}");
                }
                else
                {
                    var h = _hero;
                    Debug.Log($"[GOAP] No plan. Flags: Inside={h.IsInsidePit} Adjacent={h.IsAdjacentToPit} AtCenter={h.IsAtCenter} JustOut={h.JustJumpedOutOfPit}");
                    Debug.Log($"[GOAP] Actions Spec:\n{_agent.DescribePlanner()}");
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
        private readonly Historian _historian;

        public HeroAgent(HeroComponent hero, Historian historian)
        {
            _hero = hero;
            _historian = historian;

            _planner.AddAction(new MoveToPitAction());
            _planner.AddAction(new JumpIntoPitAction());
            _planner.AddAction(new JumpOutOfPitAction());
            _planner.AddAction(new MoveToCenterAction());
        }

        public override GoapWorldState GetWorldState()
        {
            var ws = GoapWorldState.Create(_planner);
            ws.Set("IsAtCenter", _hero.IsAtCenter);
            ws.Set("IsAdjacentToPit", _hero.IsAdjacentToPit);
            ws.Set("IsInsidePit", _hero.IsInsidePit);
            ws.Set("JustJumpedOutOfPit", _hero.JustJumpedOutOfPit);
            return ws;
        }

        public override GoapWorldState GetGoalState()
        {
            var goal = GoapWorldState.Create(_planner);
            goal.Set("IsAtCenter", true);
            return goal;
        }

        // Debug helpers
        public string DescribePlanner() => _planner.Describe();
        public string DescribeCurrentState() => GetWorldState().Describe(_planner);
    }
}