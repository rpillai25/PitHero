using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.Config
{
    /// <summary>
    /// Maps every tavern seat tile to its table tile, the direction the seated entity faces,
    /// and the world-space center position where a dish plate is placed on that table.
    /// Plate offsets are relative to the table tile's top-left pixel corner (tile * 32).
    /// </summary>
    public static class TavernSeatConfig
    {
        private struct SeatInfo
        {
            public Point TableTile;
            public Direction Facing;
            public Vector2 PlateOffset; // relative to table tile top-left corner, dish centered here
        }

        // Offset constants (spec §2): seat position relative to table → plate center offset from table top-left
        private static readonly Vector2 OffsetSeatLeft  = new Vector2(0f,  5f);   // patron left  of table, facing Right
        private static readonly Vector2 OffsetSeatBelow = new Vector2(14f, 16f);  // patron below table,    facing Up
        private static readonly Vector2 OffsetSeatRight = new Vector2(20f, 5f);   // patron right of table, facing Left
        private static readonly Vector2 OffsetSeatAbove = new Vector2(14f, 4f);   // patron above table,    facing Down

        // Tables in the tavern (derived from the Base TMX layer; tile 80 = table top decoration)
        // Left upper table at (93,3), right upper table at (97,3)
        // Left lower/party table at (93,7), right lower table at (97,7)

        private static readonly Dictionary<Point, SeatInfo> _seats;

        static TavernSeatConfig()
        {
            _seats = new Dictionary<Point, SeatInfo>();

            // ── Party seats (WalkToTavernForStopAction facings) ──────────────────
            // Hero at (93,6), facing Down → table (93,7), above the table
            Register(new Point(93, 6), new Point(93, 7), Direction.Down,  OffsetSeatAbove);
            // Merc1 at (92,7), facing Right → table (93,7), left of the table
            Register(new Point(92, 7), new Point(93, 7), Direction.Right, OffsetSeatLeft);
            // Merc2 at (94,7), facing Left  → table (93,7), right of the table
            Register(new Point(94, 7), new Point(93, 7), Direction.Left,  OffsetSeatRight);

            // ── Right lower table (97,7) — 3 patron seats ───────────────────────
            // (97,6) above the table, facing Down
            Register(new Point(97, 6), new Point(97, 7), Direction.Down,  OffsetSeatAbove);
            // (96,7) left of the table, facing Right
            Register(new Point(96, 7), new Point(97, 7), Direction.Right, OffsetSeatLeft);
            // (98,7) right of the table, facing Left
            Register(new Point(98, 7), new Point(97, 7), Direction.Left,  OffsetSeatRight);

            // ── Left upper table (93,3) — 3 patron seats ────────────────────────
            // (93,4) below the table, facing Up
            Register(new Point(93, 4), new Point(93, 3), Direction.Up,    OffsetSeatBelow);
            // (92,3) left of the table, facing Right
            Register(new Point(92, 3), new Point(93, 3), Direction.Right, OffsetSeatLeft);
            // (94,3) right of the table, facing Left
            Register(new Point(94, 3), new Point(93, 3), Direction.Left,  OffsetSeatRight);

            // ── Right upper table (97,3) — 3 patron seats ───────────────────────
            // (97,4) below the table, facing Up
            Register(new Point(97, 4), new Point(97, 3), Direction.Up,    OffsetSeatBelow);
            // (96,3) left of the table, facing Right
            Register(new Point(96, 3), new Point(97, 3), Direction.Right, OffsetSeatLeft);
            // (98,3) right of the table, facing Left
            Register(new Point(98, 3), new Point(97, 3), Direction.Left,  OffsetSeatRight);
        }

        private static void Register(Point seat, Point table, Direction facing, Vector2 plateOffset)
        {
            _seats[seat] = new SeatInfo { TableTile = table, Facing = facing, PlateOffset = plateOffset };
        }

        /// <summary>True when the given tile is a registered seat.</summary>
        public static bool IsSeatTile(Point seatTile) => _seats.ContainsKey(seatTile);

        /// <summary>
        /// Returns the direction the seated entity faces at the given seat tile.
        /// Returns Direction.Down if the seat is not registered.
        /// </summary>
        public static Direction GetFacing(Point seatTile)
        {
            if (_seats.TryGetValue(seatTile, out var info))
                return info.Facing;
            return Direction.Down;
        }

        /// <summary>
        /// Computes the world-space center position where a plate/dish is placed on the table
        /// for the given seat. Returns false when <paramref name="seatTile"/> is not a known seat.
        /// worldPos is the CENTER of the dish sprite (use SpawnDishAtWorldPos).
        /// </summary>
        public static bool TryGetPlateWorldPosition(Point seatTile, out Vector2 worldPos)
        {
            if (!_seats.TryGetValue(seatTile, out var info))
            {
                worldPos = Vector2.Zero;
                return false;
            }
            worldPos = new Vector2(
                info.TableTile.X * GameConfig.TileSize + info.PlateOffset.X,
                info.TableTile.Y * GameConfig.TileSize + info.PlateOffset.Y);
            return true;
        }

        /// <summary>Returns the table tile for the given seat, or Point.Zero if unknown.</summary>
        public static Point GetTableTile(Point seatTile)
        {
            if (_seats.TryGetValue(seatTile, out var info))
                return info.TableTile;
            return Point.Zero;
        }
    }
}
