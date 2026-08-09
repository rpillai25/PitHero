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
        private float _yieldTimer;

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
                _pitEdgeTile = FindNearestPitEdge(mercenary);
                _pathCalculated = true;
            }

            var currentTile = GetCurrentTile(mercenary);

            if (currentTile == _pitEdgeTile)
            {
                Debug.Log($"[WalkToPitEdge] {mercenary.Entity.Name} reached pit edge at ({_pitEdgeTile.X},{_pitEdgeTile.Y})");
                _pathCalculated = false;
                _yieldTimer = 0f;
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
                // The per-merc offset tile may be unreachable (map decor, runtime obstacles) —
                // fall back to the shared center rim tile rather than dead-ending the plan.
                // Overlapping at the edge beats a merc that never leaves for the pit at all.
                var centerEdgeTile = new Point(_pitEdgeTile.X, GameConfig.PitCenterTileY);
                if (_pitEdgeTile != centerEdgeTile)
                {
                    var fallbackPath = pathfinding.CalculatePath(currentTile, centerEdgeTile);
                    if (fallbackPath != null && fallbackPath.Count > 0)
                    {
                        Debug.Warn($"[WalkToPitEdge] {mercenary.Entity.Name} cannot reach offset edge tile ({_pitEdgeTile.X},{_pitEdgeTile.Y}), falling back to center ({centerEdgeTile.X},{centerEdgeTile.Y})");
                        _pitEdgeTile = centerEdgeTile;
                        path = fallbackPath;
                    }
                }
            }

            if (path == null || path.Count == 0)
            {
                // Never cache the edge tile across attempts — the pit can widen between them
                _pathCalculated = false;

                // A merc standing on the pit's interior floor without ever having jumped (the pit
                // widened underneath it mid-walk) can never reach the rim on foot. Adopt the
                // position as truth so the next plan follows the target inside instead of walking
                // to the edge forever.
                var pitWidthManager = Core.Services.GetService<PitWidthManager>();
                if (pitWidthManager != null && pitWidthManager.IsTileInsidePitInterior(currentTile))
                {
                    Debug.Warn($"[WalkToPitEdge] {mercenary.Entity.Name} is stranded on the pit floor at ({currentTile.X},{currentTile.Y}) — marking InsidePit and refreshing pathfinding");
                    mercenary.InsidePit = true;
                    pathfinding.RefreshPathfindingWithObstacles();
                    return true;
                }

                Debug.Warn($"[WalkToPitEdge] {mercenary.Entity.Name} cannot find path to pit edge");
                return true;
            }

            if (path.Count > 0)
            {
                var nextTile = path[0];

                // Anti-overlap: while walking independently, never walk inside a party member
                // ahead in priority (hero > merc 1 > merc 2) — wait for them to move clear so the
                // party doesn't render as one person. Priority ordering makes the yield one-way,
                // so two mercs can never deadlock waiting on each other; the timer is a safety
                // valve in case the member ahead parks on this merc's only route.
                if (ShouldYieldToPartyAhead(mercenary, nextTile))
                {
                    _yieldTimer += Time.DeltaTime;
                    if (_yieldTimer < GameConfig.MovementStuckTimeoutSeconds)
                        return false;
                }
                _yieldTimer = 0f;

                var direction = GetDirectionToTile(currentTile, nextTile);
                if (direction.HasValue && tileMover != null)
                {
                    tileMover.StartMoving(direction.Value);
                }
            }

            return false;
        }

        /// <summary>
        /// True when a higher-priority party member (the hero, or a merc hired earlier) is
        /// currently overlapping this merc's bounding box or standing on the tile it would step
        /// onto next.
        /// </summary>
        private bool ShouldYieldToPartyAhead(MercenaryComponent mercenary, Point nextTile)
        {
            var selfEntity = mercenary.Entity;
            var selfPos = selfEntity.Transform.Position;

            var heroEntity = selfEntity.Scene?.FindEntity("hero");
            if (heroEntity != null && PartyMemberBlocksStep(heroEntity.Transform.Position, selfPos, nextTile))
                return true;

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            var hired = mercenaryManager?.GetHiredMercenaries();
            if (hired == null)
                return false;

            var myIndex = hired.IndexOf(selfEntity);
            for (int i = 0; i < myIndex; i++)
            {
                if (PartyMemberBlocksStep(hired[i].Transform.Position, selfPos, nextTile))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A step is blocked when the other member's tile-sized bounding box overlaps this one's,
        /// or when the other member currently occupies the step's destination tile. Grid-aligned
        /// actors on adjacent tiles are exactly TileSize apart, so single-file trailing is allowed.
        /// </summary>
        public static bool PartyMemberBlocksStep(Vector2 otherPos, Vector2 selfPos, Point nextTile)
        {
            if (System.Math.Abs(otherPos.X - selfPos.X) < GameConfig.TileSize &&
                System.Math.Abs(otherPos.Y - selfPos.Y) < GameConfig.TileSize)
                return true;

            var otherTile = new Point(
                (int)(otherPos.X / GameConfig.TileSize),
                (int)(otherPos.Y / GameConfig.TileSize)
            );
            return otherTile == nextTile;
        }

        private Point GetCurrentTile(MercenaryComponent mercenary)
        {
            var pos = mercenary.Entity.Transform.Position;
            return new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );
        }

        private Point FindNearestPitEdge(MercenaryComponent mercenary)
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return Point.Zero;

            var pitLeft = GameConfig.PitRectX;
            var pitWidth = pitWidthManager.CurrentPitRectWidthTiles;
            var pitRight = pitLeft + pitWidth - 1;

            int mercIndex = 0;
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                var index = mercenaryManager.GetHiredMercenaries().IndexOf(mercenary.Entity);
                if (index >= 0)
                    mercIndex = index;
            }

            return CalculatePitEdgeTileForPartyIndex(pitRight, mercIndex);
        }

        /// <summary>
        /// The hero targets (edgeX, PitCenterTileY); mercs offset vertically by hire order so the
        /// party never converges on one tile — and their subsequent jump landings stay distinct too.
        /// The rim rows directly adjacent to center (5 and 7) are collision tiles in the map's rim
        /// column pattern, so the offsets must be ±2 to land on the open rows (4 and 8).
        /// </summary>
        public static Point CalculatePitEdgeTileForPartyIndex(int pitEdgeX, int mercIndex)
        {
            int yOffset = mercIndex == 0 ? -2 : 2;
            return new Point(pitEdgeX, GameConfig.PitCenterTileY + yOffset);
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


