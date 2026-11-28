using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.Config;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Grades the pit's tiles with the biome tint (issue #473): the Underworld reuses the Cave tiles,
    /// multiplied purple. Draws one multiply-blended rectangle over the pit's Base tiles — the stamped
    /// 14x11 block plus the extension columns out to the pit's right edge — on a layer between the
    /// Base (100) and Detail (90) tile layers, so actors, walls and fog stay above it.
    ///
    /// Live-only: while the replay frame viewer shows a recorded tick the tint follows the HUD record's
    /// pit level (and the right edge that level produces), never the live pit, which sits elsewhere on
    /// the timeline.
    /// </summary>
    public class PitBiomeTintComponent : RenderableComponent, Services.Replay.Frames.ILiveOnlyRenderable
    {
        private static readonly BlendState MultiplyBlend = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor,
            ColorDestinationBlend = Blend.Zero,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add
        };

        private static readonly RectangleF HugeBounds = new RectangleF(-100000f, -100000f, 200000f, 200000f);

        public PitBiomeTintComponent()
        {
            Material = new Material(MultiplyBlend);
        }

        public override RectangleF Bounds => HugeBounds;

        public override bool IsVisibleFromCamera(Camera camera) => true;

        public override void Render(Batcher batcher, Camera camera)
        {
            int pitLevel, rightEdge;
            var replay = Services.Replay.ReplayPlaybackService.Current;
            if (replay != null && replay.IsActive && replay.Viewer != null)
            {
                var frame = replay.Viewer.CurrentFrame;
                if (frame == null)
                    return;
                pitLevel = frame.Hud.PitLevel;
                rightEdge = PitWidthManager.CalculateRightEdgeForLevel(pitLevel);
            }
            else
            {
                var pit = Core.Services.GetService<PitWidthManager>();
                if (pit == null)
                    return;
                pitLevel = pit.CurrentPitLevel;
                rightEdge = pit.CurrentPitRightEdge;
            }

            var tint = PitBiomeTileSet.GetTint(BiomeProgressionConfig.GetBiome(pitLevel));
            if (tint == Color.White)
                return;

            int ts = GameConfig.TileSize;
            int left = PitBiomeTileSet.StampTileX;
            int right = rightEdge > left + PitBiomeTileSet.BlockWidth - 1 ? rightEdge : left + PitBiomeTileSet.BlockWidth - 1;
            var rect = new Rectangle(left * ts, PitBiomeTileSet.StampTileY * ts,
                (right - left + 1) * ts, PitBiomeTileSet.BlockHeight * ts);
            batcher.DrawRect(rect, tint);
        }
    }
}
