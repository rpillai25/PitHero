using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez.AI.Pathfinding;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Farming
{
    /// <summary>
    /// Shared A* grid over the surface map for farming monsters. The bottom two footprint rows of
    /// each placed building are solid; everything else is walkable. Searches return 4-directional
    /// paths which are then string-pulled into straight/diagonal segments for smooth movement.
    /// </summary>
    public class FarmPathfinder
    {
        private readonly AstarGridGraph _graph;
        private readonly List<Point> _smoothed = new List<Point>(32);
        private readonly int _width;
        private readonly int _height;

        /// <summary>Map width in tiles.</summary>
        public int Width => _width;

        /// <summary>Map height in tiles.</summary>
        public int Height => _height;

        public FarmPathfinder(int mapWidthTiles, int mapHeightTiles)
        {
            _width = mapWidthTiles;
            _height = mapHeightTiles;
            _graph = new AstarGridGraph(mapWidthTiles, mapHeightTiles);
        }

        /// <summary>Rebuilds the wall set from the bottom two footprint rows of every placed building.</summary>
        public void RebuildWalls(BuildingService buildings)
        {
            _graph.Walls.Clear();
            var all = buildings.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                var fp = BuildingConfig.GetFootprint(b.Type);
                int maxDy = int.MinValue;
                for (int j = 0; j < fp.Length; j++)
                    if (fp[j].dy > maxDy)
                        maxDy = fp[j].dy;
                for (int j = 0; j < fp.Length; j++)
                    if (fp[j].dy >= maxDy - 1)
                        _graph.Walls.Add(new Point(b.TileX + fp[j].dx, b.TileY + fp[j].dy));
            }
        }

        public bool IsPassable(Point tile)
        {
            if (tile.X < 0 || tile.Y < 0 || tile.X >= _width || tile.Y >= _height)
                return false;
            return !_graph.Walls.Contains(tile);
        }

        /// <summary>Returns a 4-directional tile path from start to goal, or null when unreachable.</summary>
        public List<Point> Search(Point start, Point goal) => _graph.Search(start, goal);

        /// <summary>
        /// Greedy string-pull: keeps only the waypoints needed to maintain line-of-sight, turning a
        /// 4-directional A* path into straight/diagonal segments. The returned list is reused across
        /// calls — consume it before searching again.
        /// </summary>
        public List<Point> SmoothPath(Point start, List<Point> path)
        {
            _smoothed.Clear();
            var anchor = start;
            int i = 0;
            while (i < path.Count)
            {
                int next = i;
                for (int j = path.Count - 1; j > i; j--)
                {
                    if (HasLineOfSight(anchor, path[j]))
                    {
                        next = j;
                        break;
                    }
                }
                _smoothed.Add(path[next]);
                anchor = path[next];
                i = next + 1;
            }
            return _smoothed;
        }

        // Samples the segment between tile centers at quarter-tile steps, requiring the monster's
        // ~one-tile body to stay clear of walls at every sample.
        private bool HasLineOfSight(Point a, Point b)
        {
            float ax = a.X + 0.5f, ay = a.Y + 0.5f;
            float dx = (b.X + 0.5f) - ax, dy = (b.Y + 0.5f) - ay;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            int steps = (int)(dist * 4f) + 1;
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                if (!BodyClear(ax + dx * t, ay + dy * t))
                    return false;
            }
            return true;
        }

        private bool BodyClear(float cx, float cy)
        {
            const float h = 0.45f;
            return IsPassable(new Point((int)(cx - h), (int)(cy - h)))
                && IsPassable(new Point((int)(cx + h), (int)(cy - h)))
                && IsPassable(new Point((int)(cx - h), (int)(cy + h)))
                && IsPassable(new Point((int)(cx + h), (int)(cy + h)));
        }
    }
}
