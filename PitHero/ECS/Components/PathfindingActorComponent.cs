using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Base component for actors that can use A* pathfinding
    /// Contains AStarGridGraph and related pathfinding functionality
    /// </summary>
    public class PathfindingActorComponent : ActorComponent
    {
        private AstarGridGraph _astarGraph;
        private bool _isPathfindingInitialized;

        /// <summary>
        /// Gets the A* pathfinding graph for this actor
        /// </summary>
        public AstarGridGraph PathfindingGraph => _astarGraph;

        /// <summary>
        /// Returns true if pathfinding has been initialized for this actor
        /// </summary>
        public bool IsPathfindingInitialized => _isPathfindingInitialized;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            InitializePathfinding();
        }

        /// <summary>
        /// Initialize the A* pathfinding graph for this actor
        /// </summary>
        public virtual void InitializePathfinding()
        {
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService?.CurrentMap == null)
            {
                Debug.Warn($"[PathfindingActor] Cannot initialize pathfinding for {Entity.Name} - no tilemap available");
                return;
            }

            var collisionLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Collision");
            if (collisionLayer == null)
            {
                Debug.Warn($"[PathfindingActor] Cannot initialize pathfinding for {Entity.Name} - no collision layer found");
                return;
            }

            // Create A* graph from collision layer
            _astarGraph = new AstarGridGraph(collisionLayer);
            _isPathfindingInitialized = true;

            Debug.Log($"[PathfindingActor] Initialized pathfinding for {Entity.Name} with {_astarGraph.Walls.Count} walls from collision layer");
        }

        /// <summary>
        /// Refresh the pathfinding graph from the current collision layer state
        /// This completely recreates the graph to pick up new tilemap dimensions and obstacles
        /// </summary>
        public virtual void RefreshPathfinding()
        {
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService?.CurrentMap == null)
            {
                Debug.Warn($"[PathfindingActor] Cannot refresh pathfinding for {Entity.Name} - no tilemap available");
                return;
            }

            var collisionLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Collision");
            if (collisionLayer == null)
            {
                Debug.Warn($"[PathfindingActor] Cannot refresh pathfinding for {Entity.Name} - no collision layer found");
                return;
            }

            // Completely recreate the A* graph to pick up new dimensions
            _astarGraph = new AstarGridGraph(collisionLayer);
            _isPathfindingInitialized = true;

            Debug.Log($"[PathfindingActor] Refreshed pathfinding for {Entity.Name} with {_astarGraph.Walls.Count} walls from collision layer");
        }

        /// <summary>
        /// Add a wall obstacle to the pathfinding graph
        /// </summary>
        public virtual void AddWall(Point tilePosition)
        {
            if (!_isPathfindingInitialized)
            {
                Debug.Warn($"[PathfindingActor] Cannot add wall for {Entity.Name} - pathfinding not initialized");
                return;
            }

            _astarGraph.Walls.Add(tilePosition);
        }

        /// <summary>
        /// Remove a wall obstacle from the pathfinding graph
        /// </summary>
        public virtual void RemoveWall(Point tilePosition)
        {
            if (!_isPathfindingInitialized)
            {
                Debug.Warn($"[PathfindingActor] Cannot remove wall for {Entity.Name} - pathfinding not initialized");
                return;
            }

            _astarGraph.Walls.Remove(tilePosition);
        }

        /// <summary>
        /// Calculate a path from start to target using A* pathfinding
        /// </summary>
        public virtual List<Point> CalculatePath(Point start, Point target)
        {
            if (!_isPathfindingInitialized)
            {
                Debug.Warn($"[PathfindingActor] Cannot calculate path for {Entity.Name} - pathfinding not initialized");
                return null;
            }

            try
            {
                // Early out if target is not passable
                if (_astarGraph.Walls.Contains(target))
                {
                    Debug.Warn($"[PathfindingActor] Target ({target.X},{target.Y}) is not passable for {Entity.Name}");
                    return null;
                }

                var path = _astarGraph.Search(start, target);

                if (path != null && path.Count > 0)
                {
                    // Remove the first point if it's the current position
                    if (path.Count > 0 && path[0].X == start.X && path[0].Y == start.Y)
                    {
                        path.RemoveAt(0);
                    }
                }
                else
                {
                    Debug.Warn($"[PathfindingActor] No path found for {Entity.Name} from ({start.X},{start.Y}) to ({target.X},{target.Y})");
                }

                return path;
            }
            catch (System.Exception ex)
            {
                Debug.Error($"[PathfindingActor] Error calculating path for {Entity.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a tile position is passable (not a wall)
        /// </summary>
        public virtual bool IsPassable(Point tilePosition)
        {
            if (!_isPathfindingInitialized)
            {
                return false;
            }

            return !_astarGraph.Walls.Contains(tilePosition);
        }

        /// <summary>
        /// Set world state based on this actor's current state
        /// </summary>
        public virtual void SetWorldState(ref WorldState worldState)
        {
            // Default implementation does nothing. Override in derived classes to set specific world state values.
        }

        /// <summary>
        /// Set goal state based on this actor's desired state
        /// </summary>
        public virtual void SetGoalState(ref WorldState goalState)
        {
            // Default implementation does nothing. Override in derived classes to set specific goal state values.
        }
    }
}