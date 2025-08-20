using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// GOAP Agent component for Hero AI
    /// </summary>
    public class HeroGoapAgent : Component
    {
        private Agent _agent;
        private HeroComponent _heroComponent;
        private Historian _historian;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            
            _heroComponent = Entity.GetComponent<HeroComponent>();
            _historian = Entity.GetComponent<Historian>();
            
            // Initialize GOAP agent with actions
            _agent = new HeroAgent(_heroComponent, _historian);
        }

        public void Update()
        {
            if (_agent != null)
            {
                // Plan and execute actions
                if (!_agent.HasActionPlan())
                {
                    var hasPlans = _agent.Plan();
                    if (!hasPlans)
                    {
                        // No valid plan found, wait or create default behavior
                        return;
                    }
                }

                // Execute current action
                if (_agent.HasActionPlan())
                {
                    var currentAction = _agent.Actions.Peek();
                    if (currentAction is HeroActionBase heroAction)
                    {
                        if (heroAction.Execute(_heroComponent))
                        {
                            // Action completed, remove from stack
                            _agent.Actions.Pop();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Concrete GOAP Agent implementation for Hero
    /// </summary>
    public class HeroAgent : Agent
    {
        private readonly HeroComponent _heroComponent;
        private readonly Historian _historian;

        public HeroAgent(HeroComponent heroComponent, Historian historian)
        {
            _heroComponent = heroComponent;
            _historian = historian;

            // Add all available actions
            _planner.AddAction(new MoveToPitAction());
            _planner.AddAction(new JumpIntoPitAction());
            _planner.AddAction(new JumpOutOfPitAction());
            _planner.AddAction(new MoveToCenterAction());
        }

        public override Nez.AI.GOAP.WorldState GetWorldState()
        {
            var worldState = Nez.AI.GOAP.WorldState.Create(_planner);
            
            // Set current state based on hero position and status
            worldState.Set("IsAtCenter", _heroComponent.IsAtCenter);
            worldState.Set("IsAdjacentToPit", _heroComponent.IsAdjacentToPit);
            worldState.Set("IsInsidePit", _heroComponent.IsInsidePit);
            worldState.Set("JustJumpedOutOfPit", _heroComponent.JustJumpedOutOfPit);
            
            return worldState;
        }

        public override Nez.AI.GOAP.WorldState GetGoalState()
        {
            var goalState = Nez.AI.GOAP.WorldState.Create(_planner);
            
            // Goal: Return to center after jumping out of pit
            goalState.Set("IsAtCenter", true);
            
            return goalState;
        }
    }
}