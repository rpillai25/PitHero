using Nez;
using PitHero.AI;

namespace PitHero.ECS.Components
{
    public class HeroGoapAgentComponent : Component, IUpdatable
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
}