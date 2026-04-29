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
        private readonly List<Point> _tempFogWalls = new List<Point>(64);

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
        /// Calculate a path from start to target, treating fog-of-war covered tiles as additional walls.
        /// Temporary fog walls are injected before the A* search and always removed in a finally block.
        /// Falls back to regular CalculatePath when TiledMapService is unavailable.
        /// </summary>
        public virtual List<Point> CalculateFogAwarePath(Point start, Point target)
        {
            if (!_isPathfindingInitialized)
            {
                Debug.Warn($"[PathfindingActor] Cannot calculate fog-aware path for {Entity.Name} - pathfinding not initialized");
                return null;
            }

            var tms = Core.Services.GetService<TiledMapService>();
            if (tms == null)
                return CalculatePath(start, target);

            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int pitLeft   = GameConfig.PitRectX;
            int pitTop    = GameConfig.PitRectY;
            int pitRight  = pitLeft + (pitWidthManager?.CurrentPitRectWidthTiles ?? GameConfig.PitRectWidth) - 1;
            int pitBottom = pitTop + GameConfig.PitRectHeight - 1;

            _tempFogWalls.Clear();

            try
            {
                for (int x = pitLeft; x <= pitRight; x++)
                {
                    for (int y = pitTop; y <= pitBottom; y++)
                    {
                        if ((x == start.X && y == start.Y) || (x == target.X && y == target.Y))
                            continue;

                        if (tms.IsFogOfWarTile(x, y))
                        {
                            var fogTile = new Point(x, y);
                            if (!_astarGraph.Walls.Contains(fogTile))
                            {
                                _astarGraph.Walls.Add(fogTile);
                                _tempFogWalls.Add(fogTile);
                            }
                        }
                    }
                }

                return CalculatePath(start, target);
            }
            finally
            {
                for (int i = 0; i < _tempFogWalls.Count; i++)
                    _astarGraph.Walls.Remove(_tempFogWalls[i]);
                _tempFogWalls.Clear();
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