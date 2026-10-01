using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.AI;

// Also covers WalkToPitEdgeAction.PartyMemberBlocksStep (anti-overlap yield rule)

namespace PitHero.Tests
{
    [TestClass]
    public class MercenaryPitEdgeTileTests
    {
        // Pit interior rows span PitRectY+1 .. PitRectY+PitRectHeight-2; a merc's jump lands at
        // (edgeX - 2, edgeY), so the rim tile must keep its row inside that span.
        private const int InteriorRowMin = GameConfig.PitRectY + 1;
        private const int InteriorRowMax = GameConfig.PitRectY + GameConfig.PitRectHeight - 2;

        [TestMethod]
        public void PitEdgeTile_IsTheSingleEntryTileAtCenterRow()
        {
            // The pit has exactly one entry point: the rim tile at (edgeX, PitCenterTileY). The
            // hero (HeroStateMachine.CalculatePitOutsideEdgeLocation) and every merc walk to and
            // jump from this same tile; mercs queue single-file via the yield rule instead of
            // spreading along the rim. Per-merc row offsets (rows 4/8) were a regression: the
            // party entered the pit at different places.
            foreach (var edgeX in new[] { 13, 15, 23, 33 })
            {
                var tile = WalkToPitEdgeAction.CalculatePitEdgeTile(edgeX);
                Assert.AreEqual(new Point(edgeX, GameConfig.PitCenterTileY), tile,
                    $"Pit edge tile for edge column {edgeX} must be the center-row rim tile");
            }
        }

        [TestMethod]
        public void PitEdgeTile_KeepsJumpLandingRowInsidePitInterior()
        {
            var tile = WalkToPitEdgeAction.CalculatePitEdgeTile(13);
            Assert.IsTrue(tile.Y >= InteriorRowMin && tile.Y <= InteriorRowMax,
                $"Edge row {tile.Y} must be within interior rows {InteriorRowMin}..{InteriorRowMax}");
        }

        [TestMethod]
        public void PitEdgeTile_AvoidsBlockedRimRows()
        {
            // The map's rim column pattern (PitHero.tmx Collision layer, stamped at every pit
            // width by PitWidthManager) has collision tiles at rows 5 and 7 — the center row 6
            // is open at every pit width.
            var tile = WalkToPitEdgeAction.CalculatePitEdgeTile(13);
            Assert.AreNotEqual(GameConfig.PitCenterTileY - 1, tile.Y);
            Assert.AreNotEqual(GameConfig.PitCenterTileY + 1, tile.Y);
        }

        // ── Anti-overlap yield rule ──────────────────────────────────────────────

        private static Vector2 TileCenter(int tileX, int tileY) => new Vector2(
            tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
            tileY * GameConfig.TileSize + GameConfig.TileSize / 2);

        [TestMethod]
        public void PartyMemberBlocksStep_OverlappingBoundingBoxes_Blocks()
        {
            // Same tile — fully overlapped
            Assert.IsTrue(WalkToPitEdgeAction.PartyMemberBlocksStep(
                TileCenter(50, 6), TileCenter(50, 6), new Point(49, 6)));

            // Partially overlapped (other is mid-move, half a tile ahead)
            var halfTile = TileCenter(50, 6) + new Vector2(GameConfig.TileSize / 2f, 0);
            Assert.IsTrue(WalkToPitEdgeAction.PartyMemberBlocksStep(
                halfTile, TileCenter(50, 6), new Point(49, 6)));
        }

        [TestMethod]
        public void PartyMemberBlocksStep_NextTileOccupied_Blocks()
        {
            // Other stands two tiles away but on the step destination
            Assert.IsTrue(WalkToPitEdgeAction.PartyMemberBlocksStep(
                TileCenter(48, 6), TileCenter(50, 6), new Point(48, 6)));
        }

        [TestMethod]
        public void PartyMemberBlocksStep_JumpLandingOccupied_Blocks()
        {
            // A merc on the rim tile (edgeX, 6) jumps to (edgeX - 2, 6); a party member ahead
            // still standing on that landing tile must hold the jump.
            Assert.IsTrue(WalkToPitEdgeAction.PartyMemberBlocksStep(
                TileCenter(11, 6), TileCenter(13, 6), new Point(11, 6)));
        }

        [TestMethod]
        public void PartyMemberBlocksStep_AdjacentGridAligned_AllowsSingleFile()
        {
            // Grid-aligned actors on adjacent tiles are exactly TileSize apart — trailing
            // single-file must not be blocked, or the party could never walk in a line.
            Assert.IsFalse(WalkToPitEdgeAction.PartyMemberBlocksStep(
                TileCenter(49, 6), TileCenter(50, 6), new Point(51, 6)));
        }

        [TestMethod]
        public void PartyMemberBlocksStep_FarAway_DoesNotBlock()
        {
            Assert.IsFalse(WalkToPitEdgeAction.PartyMemberBlocksStep(
                TileCenter(10, 4), TileCenter(50, 6), new Point(49, 6)));
        }
    }
}
