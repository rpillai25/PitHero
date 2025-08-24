using Microsoft.Xna.Framework;
using Nez;
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
            Debug.Log($"[PathfindingActor] Map dimensions: {collisionLayer.Width}x{collisionLayer.Height}");
            
            // Log some sample wall positions for debugging
            int wallCount = 0;
            foreach (var wall in _astarGraph.Walls)
            {
                if (wallCount < 10) // Log first 10 walls
                {
                    Debug.Log($"[PathfindingActor] Wall at ({wall.X},{wall.Y})");
                }
                wallCount++;
                if (wallCount >= 10) break;
            }
        }

        /// <summary>
        /// Refresh the pathfinding graph from the current collision layer state
        /// This removes dynamically added obstacles and rebuilds from the base tilemap
        /// </summary>
        public virtual void RefreshPathfinding()
        {
            if (!_isPathfindingInitialized)
            {
                InitializePathfinding();
                return;
            }

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

            // Clear existing walls and rebuild from collision layer
            _astarGraph.Walls.Clear();

            // Add all collision layer tiles
            for (int x = 0; x < collisionLayer.Width; x++)
            {
                for (int y = 0; y < collisionLayer.Height; y++)
                {
                    var tile = collisionLayer.GetTile(x, y);
                    if (tile != null && tile.Gid != 0)
                    {
                        _astarGraph.Walls.Add(new Point(x, y));
                    }
                }
            }

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
            Debug.Log($"[PathfindingActor] Added wall at ({tilePosition.X},{tilePosition.Y}) to {Entity.Name} pathfinding graph");
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
            Debug.Log($"[PathfindingActor] Removed wall at ({tilePosition.X},{tilePosition.Y}) from {Entity.Name} pathfinding graph");
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

                Debug.Log($"[PathfindingActor] Calculating path for {Entity.Name} from ({start.X},{start.Y}) to ({target.X},{target.Y})");
                var path = _astarGraph.Search(start, target);

                if (path != null && path.Count > 0)
                {
                    Debug.Log($"[PathfindingActor] Found path for {Entity.Name} with {path.Count} steps");
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
    }
}