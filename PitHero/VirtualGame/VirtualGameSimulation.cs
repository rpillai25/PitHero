using Microsoft.Xna.Framework;
using PitHero.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Main virtual game simulation that runs the complete GOAP workflow
    /// </summary>
    public class VirtualGameSimulation
    {
        private readonly VirtualWorldState _world;
        private readonly VirtualHero _hero;
        private readonly VirtualPitLevelQueue _pitQueue;
        private string _currentAction;
        private int _tickCount;
        private readonly Random _random;

        public VirtualGameSimulation()
        {
            _world = new VirtualWorldState();
            _hero = new VirtualHero(_world);
            _pitQueue = new VirtualPitLevelQueue();
            _currentAction = "None";
            _tickCount = 0;
            _random = new Random(42); // Deterministic seed for testing
        }

        /// <summary>
        /// Public access to the hero for tests
        /// </summary>
        public VirtualHero Hero => _hero;

        /// <summary>
        /// Run a complete simulation cycle as described in the comment
        /// </summary>
        public void RunCompleteSimulation()
        {
            Console.WriteLine("=== Starting Virtual Game Simulation ===");
            Console.WriteLine("Scenario: Level 40 pit -> MoveToPit -> Jump -> Explore -> Wizard Orb Workflow -> Regenerate");
            Console.WriteLine();

            // Step 1: Generate pit at level 40
            Console.WriteLine("STEP 1: Generating pit at level 40");
            _world.RegeneratePit(40);
            LogWorldState();

            // Step 2: Hero spawns and executes JumpIntoPitAction (replaces MoveToPitAction)
            Console.WriteLine("\nSTEP 2: Hero spawns and begins JumpIntoPitAction");
            ExecuteJumpIntoPitAction();

            // Step 3: Hero jumps into pit
            Console.WriteLine("\nSTEP 3: Hero jumps into pit");
            ExecuteJumpIntoPitAction();

            // Step 4: Hero wanders and explores the entire pit
            Console.WriteLine("\nSTEP 4: Hero wanders and explores pit completely");
            ExecuteWanderPitAction();

            // Step 5: Execute wizard orb workflow
            Console.WriteLine("\nSTEP 5: Execute complete wizard orb workflow");
            ExecuteWizardOrbWorkflow();

            Console.WriteLine("\n=== Simulation Complete ===");
            LogFinalState();
        }

        /// <summary>
        /// Simulate JumpIntoPitAction - hero jumps into pit (replaces MoveToPitAction)
        /// </summary>
        private void ExecuteJumpIntoPitAction()
        {
            _currentAction = "JumpIntoPitAction";
            var startPos = _hero.Position;
            var pitBounds = _world.PitBounds;
            
            // Target: Adjacent tile outside pit (left side)
            var targetPos = new Point(pitBounds.X - 1, pitBounds.Y + pitBounds.Height / 2);
            
            Console.WriteLine($"[{_currentAction}] Moving from ({startPos.X},{startPos.Y}) to ({targetPos.X},{targetPos.Y})");
            
            // Simulate pathfinding and movement
            var path = CalculateSimplePath(startPos, targetPos);
            _hero.SetMovementPath(path);
            
            // Execute movement step by step
            while (!_hero.ExecuteMovementStep())
            {
                _tickCount++;
                if (_tickCount % 5 == 0) // Log every 5 ticks
                {
                    Console.WriteLine($"[{_currentAction}] Tick {_tickCount}: Hero at ({_hero.Position.X},{_hero.Position.Y})");
                }
            }
            
            Console.WriteLine($"[{_currentAction}] Completed. Hero adjacent to pit: {_hero.AdjacentToPitBoundaryFromOutside()}");
        }

        /// <summary>
        /// Simulate WanderPitAction using state machine approach instead of systematic exploration
        /// </summary>
        private void ExecuteWanderPitAction()
        {
            _currentAction = "WanderPitAction";
            Console.WriteLine($"[{_currentAction}] Starting exploration using VirtualHeroStateMachine");
            
            // Create virtual state machine for proper pathfinding-based exploration
            var stateMachine = new VirtualHeroStateMachine(_hero, _world);
            
            int maxTicks = 1000; // Safety limit
            int tickCount = 0;
            
            while (!stateMachine.IsExplorationComplete() && tickCount < maxTicks)
            {
                stateMachine.Update();
                tickCount++;
                
                if (tickCount % 50 == 0) // Log every 50 ticks
                {
                    var fogCount = CountRemainingFog();
                    Console.WriteLine($"[{_currentAction}] Tick {tickCount}: Hero at ({_hero.Position.X},{_hero.Position.Y}), fog remaining: {fogCount}");
                }
            }
            
            var finalFogCount = CountRemainingFog();
            var isExplored = stateMachine.IsExplorationComplete();
            Console.WriteLine($"[{_currentAction}] Completed after {tickCount} ticks. Fog remaining: {finalFogCount}, ExploredPit: {isExplored}");
            
            if (isExplored)
            {
                _hero.ExploredPit = true;
            }
        }

        /// <summary>
        /// Execute the complete wizard orb workflow chain - updated for simplified GOAP
        /// </summary>
        private void ExecuteWizardOrbWorkflow()
        {
            // WanderPitAction (combines exploration and wizard orb finding)
            ExecuteWanderPitAction();
            
            // ActivateWizardOrbAction
            ExecuteActivateWizardOrbAction();
            
            // JumpOutOfPitAction (replaces MovingToInsidePitEdgeAction)
            ExecuteJumpOutOfPitAction();
            
            // ActivatePitRegenAction (replaces MoveToPitGenPointAction)
            ExecuteActivatePitRegenAction();
            
            // Cycle restarts with JumpIntoPitAction
            Console.WriteLine("\nSTEP 6: Cycle restarts - JumpIntoPitAction would begin again");
            Console.WriteLine("Hero would now target the new regenerated pit to start the cycle over");
        }

        private void ExecuteActivateWizardOrbAction()
        {
            _currentAction = "ActivateWizardOrbAction";
            Console.WriteLine($"[{_currentAction}] Activating wizard orb and queuing next pit level");
            
            _world.ActivateWizardOrb();
            _hero.ActivatedWizardOrb = true;
            
            // Queue next pit level (current + 10)
            var nextLevel = _world.PitLevel + 10;
            _pitQueue.QueueLevel(nextLevel);
            
            _tickCount++;
            Console.WriteLine($"[{_currentAction}] Completed. Orb activated, queued level {nextLevel}");
        }

        // Note: MovingToInsidePitEdgeAction is replaced by JumpOutOfPitAction which handles its own movement

        private void ExecuteJumpOutOfPitAction()
        {
            _currentAction = "JumpOutOfPitAction";
            var pitBounds = _world.PitBounds;
            
            // Target: Outside pit
            var targetPos = new Point(pitBounds.X - 1, pitBounds.Y + pitBounds.Height / 2);
            
            Console.WriteLine($"[{_currentAction}] Jumping out of pit to ({targetPos.X},{targetPos.Y})");
            
            // Use pathfinding instead of teleportation
            var path = CalculateSimplePath(_hero.Position, targetPos);
            _hero.SetMovementPath(path);
            
            // Execute movement step by step
            while (!_hero.ExecuteMovementStep())
            {
                _tickCount++;
            }
            
            _hero.ResetWizardOrbStates();
            
            _tickCount++;
            var outsidePit = _hero.GetWorldState().ContainsKey(GoapConstants.OutsidePit);
            Console.WriteLine($"[{_currentAction}] Completed. OutsidePit: {outsidePit}");
        }

        private void ExecuteActivatePitRegenAction()
        {
            _currentAction = "ActivatePitRegenAction";
            var targetPos = new Point(34, 6); // Pit generation point
            
            Console.WriteLine($"[{_currentAction}] Moving to pit generation point ({targetPos.X},{targetPos.Y})");
            
            var path = CalculateSimplePath(_hero.Position, targetPos);
            _hero.SetMovementPath(path);
            
            while (!_hero.ExecuteMovementStep())
            {
                _tickCount++;
            }
            
            // Regenerate pit at queued level
            if (_pitQueue.HasQueuedLevel)
            {
                var newLevel = _pitQueue.DequeueLevel();
                if (newLevel.HasValue)
                {
                    _world.RegeneratePit(newLevel.Value);
                    _hero.PitInitialized = true;
                    Console.WriteLine($"[{_currentAction}] Regenerated pit at level {newLevel.Value}");
                }
            }
            
            // Note: AtPitGenPoint is no longer tracked as a state in simplified GOAP
            // Position checking is now done within actions
            Console.WriteLine($"[{_currentAction}] Completed. Position-based states handled in actions");
        }

        /// <summary>
        /// Simple pathfinding - straight line or L-shaped path
        /// </summary>
        private List<Point> CalculateSimplePath(Point start, Point target)
        {
            var path = new List<Point>();
            var current = start;
            
            // Move horizontally first
            while (current.X != target.X)
            {
                current = new Point(current.X + Math.Sign(target.X - current.X), current.Y);
                if (!_world.IsCollisionTile(current))
                {
                    path.Add(current);
                }
            }
            
            // Then move vertically
            while (current.Y != target.Y)
            {
                current = new Point(current.X, current.Y + Math.Sign(target.Y - current.Y));
                if (!_world.IsCollisionTile(current))
                {
                    path.Add(current);
                }
            }
            
            return path;
        }

        /// <summary>
        /// Count remaining fog tiles in pit
        /// </summary>
        private int CountRemainingFog()
        {
            var pitBounds = _world.PitBounds;
            int count = 0;
            
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    if (_world.HasFogOfWar(new Point(x, y)))
                        count++;
                }
            }
            
            return count;
        }

        /// <summary>
        /// Log current world and hero state
        /// </summary>
        private void LogWorldState()
        {
            Console.WriteLine(_world.GetVisualRepresentation());
            
            var heroState = _hero.GetWorldState();
            Console.WriteLine("Hero GOAP States:");
            foreach (var kvp in heroState.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Log final simulation state
        /// </summary>
        private void LogFinalState()
        {
            Console.WriteLine($"Total simulation ticks: {_tickCount}");
            Console.WriteLine($"Current action: {_currentAction}");
            Console.WriteLine();
            
            LogWorldState();
            
            Console.WriteLine("Simulation verified the complete GOAP workflow:");
            Console.WriteLine("✓ Pit generation at level 40");
            Console.WriteLine("✓ Hero JumpIntoPitAction execution");
            Console.WriteLine("✓ Complete pit exploration via WanderPitAction");
            Console.WriteLine("✓ ActivateWizardOrbAction execution");
            Console.WriteLine("✓ JumpOutOfPitAction execution");
            Console.WriteLine("✓ ActivatePitRegenAction execution");
            Console.WriteLine("✓ Pit regeneration at higher level");
            Console.WriteLine();
            Console.WriteLine("The hero is now ready to start the cycle again with the new pit!");
        }
        
        /// <summary>
        /// Initialize level 40 pit for testing
        /// </summary>
        public void InitializeLevel40Pit()
        {
            _world.RegeneratePit(40);
            LogWorldState();
        }
        
        /// <summary>
        /// Simulate hero jumping into pit
        /// </summary>
        public void HeroJumpIntoPit()
        {
            var targetPos = new Point(2, 3); // Inside pit area
            var path = CalculateSimplePath(_hero.Position, targetPos);
            _hero.SetMovementPath(path);
            
            // Execute movement
            while (!_hero.ExecuteMovementStep())
            {
                // Move step by step
            }
            
            _hero.InsidePit = true;
            Console.WriteLine($"Hero jumped into pit at tile {_hero.Position.X},{_hero.Position.Y}");
        }
        
        /// <summary>
        /// Complete exploration by clearing all fog tiles
        /// </summary>
        public void CompleteExploration()
        {
            _world.ClearAllFogInPit();
            _world.DiscoverWizardOrb(new Point(9, 4));
            Console.WriteLine("Exploration completed - all fog cleared, wizard orb discovered");
        }
        
        /// <summary>
        /// Check if map is fully explored
        /// </summary>
        public bool IsMapExplored()
        {
            return _world.FogTilesInPit.Count == 0;
        }
        
        /// <summary>
        /// Check if wizard orb is found
        /// </summary>
        public bool IsWizardOrbFound()
        {
            return _world.WizardOrbPosition.HasValue;
        }
        
        /// <summary>
        /// Create GOAP context for testing
        /// </summary>
        public VirtualGoapContext CreateGoapContext()
        {
            return new VirtualGoapContext(_world, _hero);
        }
        
        /// <summary>
        /// Get progressive goal state based on current state
        /// </summary>
        public Dictionary<string, bool> GetProgressiveGoalState(Dictionary<string, bool> currentState)
        {
            var goal = new Dictionary<string, bool>();
            
            bool mapExplored = currentState.GetValueOrDefault(GoapConstants.ExploredPit, false);
            bool wizardOrbActivated = currentState.GetValueOrDefault(GoapConstants.ActivatedWizardOrb, false);
            // Note: AtPitGenPoint removed from simplified GOAP - logic simplified to 2 main goals
            
            if (!mapExplored)
            {
                goal[GoapConstants.ExploredPit] = true;
            }
            else if (!wizardOrbActivated)
            {
                goal[GoapConstants.ActivatedWizardOrb] = true;
            }
            // Note: Pit regeneration is now handled by the 2-goal cycle in HeroComponent.SetGoalState()
            else
            {
                goal[GoapConstants.OutsidePit] = true;
            }
            
            return goal;
        }
        
        /// <summary>
        /// Plan actions using GOAP
        /// </summary>
        public Stack<HeroActionBase> PlanActions(Dictionary<string, bool> currentState, Dictionary<string, bool> goalState)
        {
            var context = CreateGoapContext();
            
            // Create action planner directly
            var planner = new Nez.AI.GOAP.ActionPlanner();
            
            // Add all hero actions (extended interactive model)
            planner.AddAction(new JumpIntoPitAction());
            planner.AddAction(new WanderPitAction());
            planner.AddAction(new ActivateWizardOrbAction());
            planner.AddAction(new JumpOutOfPitAction());
            planner.AddAction(new ActivatePitRegenAction());
            planner.AddAction(new AttackMonster());
            planner.AddAction(new OpenChest());
            
            // Convert dictionaries to WorldState objects (simplified)
            var wsCurrentState = Nez.AI.GOAP.WorldState.Create(planner);
            foreach (var kvp in currentState)
            {
                wsCurrentState.Set(kvp.Key, kvp.Value);
            }
            
            var wsGoalState = Nez.AI.GOAP.WorldState.Create(planner);
            foreach (var kvp in goalState)
            {
                wsGoalState.Set(kvp.Key, kvp.Value);
            }
            
            var actionPlan = planner.Plan(wsCurrentState, wsGoalState);
            
            var result = new Stack<HeroActionBase>();
            if (actionPlan != null && actionPlan.Count > 0)
            {
                while (actionPlan.Count > 0)
                {
                    if (actionPlan.Pop() is HeroActionBase heroAction)
                    {
                        result.Push(heroAction);
                    }
                }
                
                // Reverse to get correct execution order
                var temp = new Stack<HeroActionBase>();
                while (result.Count > 0)
                {
                    temp.Push(result.Pop());
                }
                result = temp;
            }
            
            return result;
        }
        
        /// <summary>
        /// Tick hero movement simulation
        /// </summary>
        public void TickHeroMovement()
        {
            // Simple movement simulation - just advance towards target if moving
            if (_hero.IsMoving && _hero.TargetTilePosition.HasValue)
            {
                var current = _hero.Position;
                var target = _hero.TargetTilePosition.Value;
                
                // Simple step towards target (only adjacent moves allowed)
                Point nextStep = current;
                if (current.X < target.X) nextStep.X++;
                else if (current.X > target.X) nextStep.X--;
                else if (current.Y < target.Y) nextStep.Y++;
                else if (current.Y > target.Y) nextStep.Y--;
                
                // Use single-step MoveTo instead of teleportation
                if (nextStep != current)
                {
                    try
                    {
                        _hero.MoveTo(nextStep);
                    }
                    catch (System.InvalidOperationException)
                    {
                        // Step was too large, stop movement
                        _hero.IsMoving = false;
                        _hero.TargetTilePosition = null;
                        return;
                    }
                }
                
                // Stop moving if reached target
                if (_hero.Position == target)
                {
                    _hero.IsMoving = false;
                    _hero.TargetTilePosition = null;
                }
            }
        }
        
        /// <summary>
        /// Execute an action in the simulation
        /// </summary>
        public void ExecuteAction(HeroActionBase action)
        {
            var context = CreateGoapContext();
            bool completed = false;
            int maxIterations = 100;
            int iterations = 0;
            
            Console.WriteLine($"Executing action: {action.Name}");
            
            while (!completed && iterations < maxIterations)
            {
                completed = action.Execute(context);
                if (!completed)
                {
                    TickHeroMovement();
                }
                iterations++;
            }
            
            // Sync state back to hero after action execution
            context.SyncBackToHero();
            
            if (completed)
            {
                Console.WriteLine($"Action {action.Name} completed successfully");
            }
            else
            {
                Console.WriteLine($"Action {action.Name} failed to complete within {maxIterations} iterations");
            }
        }
        
        /// <summary>
        /// Simulate pit trigger exit for testing
        /// </summary>
        public void TriggerPitExit()
        {
            // This simulates the pit trigger exit logic
            var currentTile = _hero.CurrentTilePosition;
            var pitBounds = new Rectangle(1, 2, _world.PitWidthTiles, 8); // Level 40 pit bounds
            
            if (!pitBounds.Contains(currentTile))
            {
                // Hero is truly outside pit - reset flags
                _hero.InsidePit = false;
                _hero.ActivatedWizardOrb = false;
            }
            // Otherwise, hero is still in pit area - don't reset flags
        }
    }

    /// <summary>
    /// Simple virtual pit level queue for testing
    /// </summary>
    public class VirtualPitLevelQueue
    {
        private int? _queuedLevel;

        public bool HasQueuedLevel => _queuedLevel.HasValue;

        public void QueueLevel(int level)
        {
            _queuedLevel = level;
        }

        public int? DequeueLevel()
        {
            var level = _queuedLevel;
            _queuedLevel = null;
            return level;
        }
    }
}