using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Base component for actors that can perform pathfinding
    /// Each pathfinding actor has its own AstarGridGraph instance
    /// </summary>
    public abstract class PathfindingActorComponent : ActorComponent
    {
        /// <summary>
        /// The AstarGridGraph instance for this pathfinding actor
        /// </summary>
        public AstarGridGraph AstarGraph { get; private set; }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            InitializePathfinding();
        }

        /// <summary>
        /// Initialize the pathfinding graph for this actor
        /// </summary>
        private void InitializePathfinding()
        {
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService?.CurrentMap == null)
            {
                Debug.Warn("[PathfindingActor] Cannot initialize pathfinding without tilemap service");
                return;
            }

            // Get the collision layer for pathfinding
            var collisionLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Collision");
            if (collisionLayer == null)
            {
                Debug.Warn("[PathfindingActor] No 'Collision' layer found in tilemap for pathfinding");
                return;
            }

            // Build graph from the entire Collision layer: any present tile is a wall
            AstarGraph = new AstarGridGraph(collisionLayer);
            Debug.Log($"[PathfindingActor] AstarGridGraph initialized for {Entity.Name} with {AstarGraph.Walls.Count} walls from Collision layer");
        }

        /// <summary>
        /// Refresh the pathfinding graph by rebuilding it from the collision layer
        /// </summary>
        public void RefreshPathfindingGraph()
        {
            if (AstarGraph == null)
            {
                Debug.Warn("[PathfindingActor] Cannot refresh pathfinding graph - not initialized");
                return;
            }

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService?.CurrentMap == null)
            {
                Debug.Warn("[PathfindingActor] No tilemap service found for pathfinding graph refresh");
                return;
            }

            var collisionLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Collision");
            if (collisionLayer == null)
            {
                Debug.Warn("[PathfindingActor] No 'Collision' layer found for pathfinding graph refresh");
                return;
            }

            // Completely rebuild pathfinding graph from scratch
            AstarGraph.Walls.Clear();
            
            // Add all collision layer tiles
            for (int x = 0; x < collisionLayer.Width; x++)
            {
                for (int y = 0; y < collisionLayer.Height; y++)
                {
                    var tile = collisionLayer.GetTile(x, y);
                    if (tile != null && tile.Gid != 0)
                    {
                        AstarGraph.Walls.Add(new Point(x, y));
                    }
                }
            }

            Debug.Log($"[PathfindingActor] Pathfinding graph refreshed for {Entity.Name} with {AstarGraph.Walls.Count} walls from collision layer");
        }

        /// <summary>
        /// Add a wall tile to the pathfinding graph
        /// </summary>
        public void AddWallToPathfinding(Point tilePosition)
        {
            if (AstarGraph != null)
            {
                AstarGraph.Walls.Add(tilePosition);
                Debug.Log($"[PathfindingActor] Added wall tile to pathfinding graph at ({tilePosition.X},{tilePosition.Y}) for {Entity.Name}");
            }
        }

        /// <summary>
        /// Remove a wall tile from the pathfinding graph
        /// </summary>
        public void RemoveWallFromPathfinding(Point tilePosition)
        {
            if (AstarGraph != null)
            {
                AstarGraph.Walls.Remove(tilePosition);
                Debug.Log($"[PathfindingActor] Removed wall tile from pathfinding graph at ({tilePosition.X},{tilePosition.Y}) for {Entity.Name}");
            }
        }
    }
}