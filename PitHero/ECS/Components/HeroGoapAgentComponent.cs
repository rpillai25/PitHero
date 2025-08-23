using Nez;
using PitHero.AI;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    public class HeroGoapAgentComponent : Component, IUpdatable, IPausableComponent
    {
        private HeroAgent _agent;
        private HeroComponent _hero;

        /// <summary>
        /// Gets whether this component should respect the global pause state
        /// </summary>
        public bool ShouldPause => true;

        public override void OnAddedToEntity()
        {
            _hero = Entity.GetComponent<HeroComponent>();
            _agent = new HeroAgent(_hero);
        }

        /// <summary>
        /// Reset the current action plan, typically called after pit regeneration
        /// This forces the agent to replan and recalculate paths with updated world state
        /// </summary>
        public void ResetActionPlan()
        {
            Debug.Log("[HeroGoapAgent] Resetting action plan after pit regeneration");
            
            if (_agent?.Actions != null)
            {
                // Check if current action is WanderAction and reset its state before clearing plan
                if (_agent.Actions.Count > 0)
                {
                    var currentAction = _agent.Actions.Peek();
                    if (currentAction is WanderAction wanderAction)
                    {
                        wanderAction.ResetActionState();
                    }
                }
                
                // Clear current action plan - this will force replanning on next update
                _agent.Actions.Clear();
            }
        }

        public void Update()
        {
            // Check if game is paused
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

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