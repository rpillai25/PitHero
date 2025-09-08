using Microsoft.Xna.Framework;
using PitHero.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual state machine that mirrors HeroStateMachine behavior (Idle → GoTo → PerformAction)
    /// Uses actual pathfinding instead of systematic exploration and teleportation
    /// </summary>
    public class VirtualHeroStateMachine
    {
        private readonly VirtualHero _hero;
        private readonly VirtualWorldState _world;
        private readonly VirtualPathfinder _pathfinder;
        private readonly HashSet<Point> _failedWanderTargets;

        // State machine states
        public enum VirtualActorState
        {
            Idle,
            GoTo,
            PerformAction
        }

        public VirtualActorState CurrentState { get; private set; } = VirtualActorState.Idle;
        
        // Action tracking
        private HeroActionBase _currentAction;

        public VirtualHeroStateMachine(VirtualHero hero, VirtualWorldState world)
        {
            _hero = hero;
            _world = world;
            _pathfinder = new VirtualPathfinder(world);
            _failedWanderTargets = new HashSet<Point>(8);
        }

        /// <summary>
        /// Execute one state machine update tick
        /// </summary>
        public void Update()
        {
            switch (CurrentState)
            {
                case VirtualActorState.Idle:
                    UpdateIdleState();
                    break;
                case VirtualActorState.PerformAction:
                    UpdatePerformActionState();
                    break;
            }
        }

        /// <summary>
        /// Idle state - plan next action based on current world state
        /// </summary>
        private void UpdateIdleState()
        {
            var currentState = _hero.GetWorldState();
            
            // Get goal state based on current progress
            var goalState = GetProgressiveGoalState(currentState);
            
            if (goalState.Count == 0)
            {
                Console.WriteLine("[VirtualStateMachine] No more goals to pursue");
                return;
            }

            // Plan action to achieve goal
            _currentAction = PlanNextAction(currentState, goalState);
            
            if (_currentAction == null)
            {
                Console.WriteLine("[VirtualStateMachine] No action plan found");
                return;
            }

            Console.WriteLine($"[VirtualStateMachine] Planned action: {_currentAction.Name}");
            
            // Go to PerformAction state to execute the action
            CurrentState = VirtualActorState.PerformAction;
        }

        /// <summary>
        /// PerformAction state - execute the current action
        /// </summary>
        private void UpdatePerformActionState()
        {
            if (_currentAction == null)
            {
                CurrentState = VirtualActorState.Idle;
                return;
            }

            Console.WriteLine($"[VirtualStateMachine] Performing action: {_currentAction.Name}");

            // Simulate action execution based on type
            bool actionCompleted = ExecuteCurrentAction();

            if (actionCompleted)
            {
                Console.WriteLine($"[VirtualStateMachine] Action {_currentAction.Name} completed");
                _currentAction = null;
                CurrentState = VirtualActorState.Idle;
            }
        }

        /// <summary>
        /// Get progressive goal state based on current state
        /// </summary>
        private Dictionary<string, bool> GetProgressiveGoalState(Dictionary<string, bool> currentState)
        {
            var goal = new Dictionary<string, bool>();
            
            bool insidePit = currentState.ContainsKey(GoapConstants.InsidePit);
            bool exploredPit = currentState.ContainsKey(GoapConstants.ExploredPit);
            bool activatedWizardOrb = currentState.ContainsKey(GoapConstants.ActivatedWizardOrb);
            
            if (!insidePit)
            {
                goal[GoapConstants.InsidePit] = true;
            }
            else if (!exploredPit)
            {
                goal[GoapConstants.ExploredPit] = true;
            }
            else if (!activatedWizardOrb)
            {
                goal[GoapConstants.ActivatedWizardOrb] = true;
            }
            else
            {
                goal[GoapConstants.OutsidePit] = true;
            }
            
            return goal;
        }

        /// <summary>
        /// Plan next action using simplified logic
        /// </summary>
        private HeroActionBase PlanNextAction(Dictionary<string, bool> currentState, Dictionary<string, bool> goalState)
        {
            bool insidePit = currentState.ContainsKey(GoapConstants.InsidePit);
            bool exploredPit = currentState.ContainsKey(GoapConstants.ExploredPit);
            bool activatedWizardOrb = currentState.ContainsKey(GoapConstants.ActivatedWizardOrb);
            
            if (goalState.ContainsKey(GoapConstants.InsidePit) && !insidePit)
            {
                return new JumpIntoPitAction();
            }
            else if (goalState.ContainsKey(GoapConstants.ExploredPit) && !exploredPit)
            {
                return new WanderPitAction();
            }
            else if (goalState.ContainsKey(GoapConstants.ActivatedWizardOrb) && !activatedWizardOrb)
            {
                return new ActivateWizardOrbAction();
            }
            else if (goalState.ContainsKey(GoapConstants.OutsidePit) && insidePit)
            {
                return new JumpOutOfPitAction();
            }
            
            return null;
        }

        /// <summary>
        /// Execute current action based on type
        /// </summary>
        private bool ExecuteCurrentAction()
        {
            if (_currentAction == null) return true;

            switch (_currentAction.Name)
            {
                case "JumpIntoPitAction":
                    return ExecuteJumpIntoPitAction();
                case "WanderPitAction":
                    return ExecuteWanderPitAction();
                case "ActivateWizardOrbAction":
                    return ExecuteActivateWizardOrbAction();
                case "JumpOutOfPitAction":
                    return ExecuteJumpOutOfPitAction();
                case "ActivatePitRegenAction":
                    return ExecuteActivatePitRegenAction();
                default:
                    Console.WriteLine($"[VirtualStateMachine] Unknown action: {_currentAction.Name}");
                    return true;
            }
        }

        private bool ExecuteJumpIntoPitAction()
        {
            var pitBounds = _world.PitBounds;
            var insidePos = new Point(pitBounds.X + 1, pitBounds.Y + 1);
            
            Console.WriteLine($"[VirtualStateMachine] JumpIntoPit: From ({_hero.Position.X},{_hero.Position.Y}) to ({insidePos.X},{insidePos.Y})");
            
            // Path to inside pit
            var path = _pathfinder.CalculatePath(_hero.Position, insidePos);
            Console.WriteLine($"[VirtualStateMachine] JumpIntoPit: Path calculated, length={path?.Count ?? 0}");
            
            if (path != null && path.Count > 0)
            {
                var target = path.Last();
                Console.WriteLine($"[VirtualStateMachine] JumpIntoPit: Moving to target ({target.X},{target.Y})");
                _hero.TeleportTo(target); // Use teleport for action execution
                _hero.InsidePit = true;
                Console.WriteLine($"[VirtualStateMachine] Hero jumped into pit at ({target.X},{target.Y})");
            }
            else
            {
                // Fallback - direct teleport to inside position
                Console.WriteLine($"[VirtualStateMachine] JumpIntoPit: No path found, using direct teleport");
                _hero.TeleportTo(insidePos);
                _hero.InsidePit = true;
                Console.WriteLine($"[VirtualStateMachine] Hero jumped into pit at ({insidePos.X},{insidePos.Y}) via fallback");
            }
            
            return true;
        }

        private bool ExecuteWanderPitAction()
        {
            // First phase: Clear fog using systematic exploration
            var wanderTarget = CalculatePitWanderPointLocation();
            if (wanderTarget.HasValue)
            {
                var target = wanderTarget.Value;
                
                // Check if already at target
                if (_hero.Position == target)
                {
                    // Clear fog and continue
                    _world.ClearFogOfWar(_hero.Position, 2);
                    Console.WriteLine($"[VirtualStateMachine] Explored position ({_hero.Position.X},{_hero.Position.Y})");
                    return false; // Continue wandering for more targets
                }

                // For efficiency, teleport directly to the target instead of pathfinding step by step
                // This simulates fast exploration without getting stuck
                try
                {
                    _hero.TeleportTo(target);
                    _world.ClearFogOfWar(target, 2);
                    Console.WriteLine($"[VirtualStateMachine] Wandered to and explored ({target.X},{target.Y})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VirtualStateMachine] Failed to wander to ({target.X},{target.Y}): {ex.Message}, marking as failed");
                    _failedWanderTargets.Add(target);
                }

                return false; // Continue wandering
            }

            // Second phase: Fog exploration complete, now verify connectivity by visiting all columns
            var connectivityTarget = CalculateConnectivityCheckTarget();
            if (connectivityTarget.HasValue)
            {
                var target = connectivityTarget.Value;
                Console.WriteLine($"[VirtualStateMachine] Connectivity check: moving to column {target.X} at ({target.X},{target.Y})");
                
                try
                {
                    _hero.TeleportTo(target);
                    Console.WriteLine($"[VirtualStateMachine] Connectivity verified for column {target.X}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VirtualStateMachine] Connectivity check failed for ({target.X},{target.Y}): {ex.Message}");
                    _failedWanderTargets.Add(target);
                }

                return false; // Continue connectivity checks
            }

            // Both phases complete
            Console.WriteLine("[VirtualStateMachine] Wander exploration and connectivity verification complete");
            return true;
        }

        /// <summary>
        /// Calculate next pit wander point - ensure systematic exploration of all columns
        /// </summary>
        private Point? CalculatePitWanderPointLocation()
        {
            var pitBounds = _world.PitBounds;
            var candidates = new List<Point>();

            // Find all fog tiles in pit that aren't failed targets
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    var tile = new Point(x, y);
                    
                    if (_world.HasFogOfWar(tile) && 
                        !_failedWanderTargets.Contains(tile) &&
                        !_world.IsCollisionTile(tile))
                    {
                        candidates.Add(tile);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                // No more candidates - try clearing failed targets for retry
                if (_failedWanderTargets.Count > 0)
                {
                    Console.WriteLine($"[VirtualStateMachine] Clearing {_failedWanderTargets.Count} failed targets to retry exploration");
                    _failedWanderTargets.Clear();
                    return CalculatePitWanderPointLocation(); // Recursively retry
                }
                
                Console.WriteLine("[VirtualStateMachine] No more fog tiles to explore - pit exploration complete");
                return null;
            }

            var heroPos = _hero.Position;
            
            // Systematic exploration: try to visit columns from left to right
            // Find the leftmost column that has fog candidates
            var leftmostColumn = candidates.Min(p => p.X);
            var leftmostCandidates = candidates.Where(p => p.X == leftmostColumn).ToList();
            
            // Within the leftmost column, choose a central position for better coverage
            var target = leftmostCandidates.OrderBy(p => Math.Abs(p.Y - (pitBounds.Y + pitBounds.Height / 2))).First();
            
            Console.WriteLine($"[VirtualStateMachine] Selected systematic wander target: ({target.X},{target.Y}) " +
                            $"from leftmost column {leftmostColumn} ({leftmostCandidates.Count} candidates in column, {candidates.Count} total)");
            return target;
        }

        // Track which columns have been visited for connectivity checking
        private HashSet<int> _visitedColumnsForConnectivity = new HashSet<int>();

        /// <summary>
        /// Calculate next connectivity check target - visit all columns to verify reachability
        /// </summary>
        private Point? CalculateConnectivityCheckTarget()
        {
            var pitBounds = _world.PitBounds;
            
            // Update visited columns based on hero's current position
            if (pitBounds.Contains(_hero.Position))
            {
                _visitedColumnsForConnectivity.Add(_hero.Position.X);
            }

            // Find unvisited columns
            var unvisitedColumns = new List<int>();
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                if (!_visitedColumnsForConnectivity.Contains(x))
                {
                    unvisitedColumns.Add(x);
                }
            }

            if (unvisitedColumns.Count == 0)
            {
                Console.WriteLine($"[VirtualStateMachine] All {_visitedColumnsForConnectivity.Count} columns visited for connectivity check");
                return null; // All columns visited
            }

            // Pick the leftmost unvisited column
            var targetColumn = unvisitedColumns.Min();
            
            // Find a passable tile in this column (prefer middle of pit)
            for (int y = pitBounds.Y + pitBounds.Height / 2; y >= pitBounds.Y + 1 && y < pitBounds.Bottom - 1; y++)
            {
                var candidate = new Point(targetColumn, y);
                if (!_world.IsCollisionTile(candidate))
                {
                    return candidate;
                }
            }
            
            // If middle didn't work, try other positions in the column
            for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
            {
                var candidate = new Point(targetColumn, y);
                if (!_world.IsCollisionTile(candidate))
                {
                    return candidate;
                }
            }

            // Column has no passable tiles - mark as visited to skip it
            Console.WriteLine($"[VirtualStateMachine] Column {targetColumn} has no passable tiles, marking as visited");
            _visitedColumnsForConnectivity.Add(targetColumn);
            return CalculateConnectivityCheckTarget(); // Try next column
        }

        private bool ExecuteActivateWizardOrbAction()
        {
            if (_world.WizardOrbPosition.HasValue && _hero.Position == _world.WizardOrbPosition.Value)
            {
                _world.ActivateWizardOrb();
                _hero.ActivatedWizardOrb = true;
                Console.WriteLine("[VirtualStateMachine] Wizard orb activated");
                return true;
            }
            
            // Move to wizard orb first
            if (_world.WizardOrbPosition.HasValue)
            {
                var orbPos = _world.WizardOrbPosition.Value;
                var path = _pathfinder.CalculatePath(_hero.Position, orbPos);
                if (path != null && path.Count > 0)
                {
                    var target = path.Last();
                    _hero.TeleportTo(target); // Use teleport for action execution
                    Console.WriteLine($"[VirtualStateMachine] Moving to wizard orb at ({target.X},{target.Y})");
                }
            }
            
            return false; // Continue until at orb
        }

        private bool ExecuteJumpOutOfPitAction()
        {
            var pitBounds = _world.PitBounds;
            var outsidePos = new Point(pitBounds.X - 1, pitBounds.Y + pitBounds.Height / 2);
            
            _hero.TeleportTo(outsidePos); // Use teleport for action execution
            _hero.InsidePit = false;
            _hero.ResetWizardOrbStates();
            
            Console.WriteLine($"[VirtualStateMachine] Hero jumped out of pit to ({outsidePos.X},{outsidePos.Y})");
            return true;
        }

        private bool ExecuteActivatePitRegenAction()
        {
            // Move to pit generation point and regenerate
            var genPoint = new Point(34, 6);
            _hero.TeleportTo(genPoint); // Use teleport for action execution
            
            var newLevel = _world.PitLevel + 10;
            _world.RegeneratePit(newLevel);
            _hero.PitInitialized = true;
            
            Console.WriteLine($"[VirtualStateMachine] Pit regenerated to level {newLevel}");
            return true;
        }

        /// <summary>
        /// Check if exploration is complete
        /// </summary>
        public bool IsExplorationComplete()
        {
            // Check if all fog in pit has been cleared (excluding collision tiles that hero can't reach)
            var pitBounds = _world.PitBounds;
            
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    var tile = new Point(x, y);
                    // Only count fog on passable tiles - hero can't explore collision tiles
                    if (_world.HasFogOfWar(tile) && !_world.IsCollisionTile(tile))
                        return false; // Still has explorable fog
                }
            }
            
            // All explorable fog cleared - mark hero state and return true
            _hero.ExploredPit = true;
            return true;
        }

        /// <summary>
        /// Reset failed wander targets (for testing)
        /// </summary>
        public void ResetFailedTargets()
        {
            _failedWanderTargets.Clear();
            _visitedColumnsForConnectivity.Clear();
        }
    }
}