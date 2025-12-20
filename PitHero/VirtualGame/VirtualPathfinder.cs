using Microsoft.Xna.Framework;
using PitHero.AI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual pathfinder that implements IPathfinder without Nez dependencies
    /// Uses A* algorithm for pathfinding in virtual world
    /// </summary>
    public class VirtualPathfinder : IPathfinder
    {
        private readonly IWorldState _worldState;

        public VirtualPathfinder(IWorldState worldState)
        {
            _worldState = worldState;
        }

        public bool IsInitialized => true; // Virtual pathfinder is always ready

        public List<Point> CalculatePath(Point start, Point end)
        {
            try
            {
                return AStar(start, end);
            }
            catch (System.Exception)
            {
                return null; // Path not found
            }
        }

        public bool IsPassable(Point tilePosition)
        {
            return _worldState.IsPassable(tilePosition);
        }

        /// <summary>
        /// Simple A* pathfinding implementation
        /// </summary>
        private List<Point> AStar(Point start, Point goal)
        {
            if (!IsPassable(goal))
                return null;

            var openSet = new HashSet<Point> { start };
            var cameFrom = new Dictionary<Point, Point>();
            var gScore = new Dictionary<Point, float> { [start] = 0 };
            var fScore = new Dictionary<Point, float> { [start] = Heuristic(start, goal) };

            while (openSet.Count > 0)
            {
                // Find node with lowest fScore
                var current = openSet.OrderBy(p => fScore.GetValueOrDefault(p, float.MaxValue)).First();

                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (!IsPassable(neighbor))
                        continue;

                    var tentativeGScore = gScore.GetValueOrDefault(current, float.MaxValue) + 1;

                    if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);

                        if (!openSet.Contains(neighbor))
                            openSet.Add(neighbor);
                    }
                }
            }

            return null; // No path found
        }

        private float Heuristic(Point a, Point b)
        {
            // Manhattan distance
            return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
        }

        private List<Point> GetNeighbors(Point point)
        {
            return new List<Point>
            {
                new Point(point.X - 1, point.Y),
                new Point(point.X + 1, point.Y),
                new Point(point.X, point.Y - 1),
                new Point(point.X, point.Y + 1)
            };
        }

        private List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current)
        {
            var path = new List<Point> { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }

            // Remove the starting position from the path
            if (path.Count > 0)
                path.RemoveAt(0);

            return path;
        }
    }
}