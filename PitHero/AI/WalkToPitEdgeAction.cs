using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that makes the mercenary walk to the pit edge using pathfinding.
    /// </summary>
    public class WalkToPitEdgeAction : MercenaryActionBase
    {
        private Point _pitEdgeTile;
        private bool _pathCalculated = false;

        public WalkToPitEdgeAction() : base(GoapConstants.WalkToPitEdgeAction, 1)
        {
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.PitInitialized, true);
            SetPrecondition(GoapConstants.MercenaryInsidePit, false);
            SetPrecondition(GoapConstants.TargetInsidePit, true);

            SetPostcondition(GoapConstants.MercenaryAtPitEdge, true);
        }

        public override bool Execute(MercenaryComponent mercenary)
        {
            if (!_pathCalculated)
            {
                _pitEdgeTile = FindNearestPitEdge();
                _pathCalculated = true;
            }

            var currentTile = GetCurrentTile(mercenary);

            if (currentTile == _pitEdgeTile)
            {
                Debug.Log($"[WalkToPitEdge] {mercenary.Entity.Name} reached pit edge at ({_pitEdgeTile.X},{_pitEdgeTile.Y})");
                _pathCalculated = false;
                return true;
            }

            var tileMover = mercenary.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null && tileMover.IsMoving)
            {
                return false;
            }

            var pathfinding = mercenary.Entity.GetComponent<PathfindingActorComponent>();
            if (pathfinding == null || !pathfinding.IsPathfindingInitialized)
            {
                return false;
            }

            var path = pathfinding.CalculatePath(currentTile, _pitEdgeTile);
            if (path == null || path.Count == 0)
            {
                Debug.Warn($"[WalkToPitEdge] {mercenary.Entity.Name} cannot find path to pit edge");
                return true;
            }

            if (path.Count > 0)
            {
                var nextTile = path[0];
                var direction = GetDirectionToTile(currentTile, nextTile);
                if (direction.HasValue && tileMover != null)
                {
                    tileMover.StartMoving(direction.Value);
                }
            }

            return false;
        }

        private Point GetCurrentTile(MercenaryComponent mercenary)
        {
            var pos = mercenary.Entity.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        private Point FindNearestPitEdge()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return Point.Zero;

            var pitLeft = GameConfig.PitRectX;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitRight = pitLeft + pitWidth - 1;
            var pitEdgeX = pitRight;
            var pitEdgeY = GameConfig.PitCenterTileY;

            return new Point(pitEdgeX, pitEdgeY);
        }

        private Direction? GetDirectionToTile(Point current, Point target)
        {
            var dx = target.X - current.X;
            var dy = target.Y - current.Y;

            if (dx > 0 && dy == 0) return Direction.Right;
            if (dx < 0 && dy == 0) return Direction.Left;
            if (dy > 0 && dx == 0) return Direction.Down;
            if (dy < 0 && dx == 0) return Direction.Up;

            return null;
        }
    }
}


