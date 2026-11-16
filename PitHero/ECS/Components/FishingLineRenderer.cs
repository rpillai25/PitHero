using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Services.Replay.Frames;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Draws a fishing monster's pole and line with Nez line drawing: a brown pole from the fisher's
    /// hands up to the tip, a light line sagging from the tip to a red-and-white bobber in the water.
    /// While a catch is reeled in the pole lifts and the line follows the catch out of the water
    /// (<see cref="FishingCatchPath"/>), then hangs slack once the catch flies free.
    ///
    /// The <see cref="FishingMonsterStateMachine"/> sets the state each fixed step; this component only
    /// reads it. The bobber's bob is keyed on the simulation tick, so the live draw and the recorded
    /// Line ops agree at every tick. Every point is snapped to whole pixels before drawing.
    /// </summary>
    public class FishingLineRenderer : RenderableComponent, IFrameCapturable
    {
        private static readonly Color PoleColor = new Color(0x8f, 0x56, 0x3b);
        private static readonly Color PoleTipColor = new Color(0x66, 0x39, 0x31);
        private static readonly Color LineColor = new Color(0xcb, 0xdb, 0xfc);
        private static readonly Color BobberTopColor = new Color(0xac, 0x32, 0x32);
        private static readonly Color BobberBottomColor = new Color(0xff, 0xff, 0xff);

        private const int MaxSegments = 8;
        private readonly Vector2[] _from = new Vector2[MaxSegments];
        private readonly Vector2[] _to = new Vector2[MaxSegments];
        private readonly Color[] _color = new Color[MaxSegments];
        private readonly float[] _thickness = new float[MaxSegments];
        private int _segmentCount;

        /// <summary>True while the fisher is seated and fishing (pole out).</summary>
        public bool Active;

        /// <summary>True when the fisher faces right (the pole points that way).</summary>
        public bool FacingRight = true;

        /// <summary>The bobber's resting point in the water (world).</summary>
        public Vector2 Bobber;

        /// <summary>Reel progress 0..1 while a catch is being reeled in, or a negative value when not reeling.</summary>
        public float ReelProgress = -1f;

        /// <summary>Covers the fisher and the bobber so the camera culls the line with them.</summary>
        public override RectangleF Bounds
        {
            get
            {
                var p = Entity.Transform.Position;
                float minX = MathHelper.Min(p.X, Bobber.X) - GameConfig.TileSize;
                float minY = MathHelper.Min(p.Y, Bobber.Y) - GameConfig.TileSize;
                float maxX = MathHelper.Max(p.X, Bobber.X) + GameConfig.TileSize;
                float maxY = MathHelper.Max(p.Y, Bobber.Y) + GameConfig.TileSize;
                return new RectangleF(minX, minY, maxX - minX, maxY - minY);
            }
        }

        /// <summary>Width of <see cref="Bounds"/>.</summary>
        public override float Width => Bounds.Width;

        /// <summary>Height of <see cref="Bounds"/>.</summary>
        public override float Height => Bounds.Height;

        /// <summary>Where a reeled-in catch flies to: the fisher's hands.</summary>
        public static Vector2 CatchTarget(Vector2 fisher, bool facingRight)
            => new Vector2(fisher.X + (facingRight ? 6f : -6f), fisher.Y - 4f);

        /// <summary>Rebuilds the pole/line segments for the current state, in whole pixels.</summary>
        private void BuildSegments()
        {
            _segmentCount = 0;
            if (!Active)
                return;

            var fisher = Entity.Transform.Position;
            float s = FacingRight ? 1f : -1f;
            bool reeling = ReelProgress >= 0f;

            var hand = Snap(new Vector2(fisher.X + s * 5f, fisher.Y + 3f));
            var tip = Snap(reeling
                ? new Vector2(fisher.X + s * 10f, fisher.Y - 20f)
                : new Vector2(fisher.X + s * 15f, fisher.Y - 13f));
            var mid = Snap(Vector2.Lerp(hand, tip, 0.75f));
            Add(hand, mid, PoleColor, 2f);
            Add(mid, tip, PoleTipColor, 1f);

            Vector2 end;
            float sag;
            bool showBobber = false;
            if (!reeling)
            {
                // The bobber bobs one pixel every half second, keyed on the simulation tick
                int bob = (int)(SimulationClock.CurrentTick / 30L % 2L);
                end = Snap(new Vector2(Bobber.X, Bobber.Y + bob));
                sag = 5f;
                showBobber = true;
            }
            else if (FishingCatchPath.IsLifting(ReelProgress))
            {
                end = Snap(FishingCatchPath.Position(Bobber, CatchTarget(fisher, FacingRight), ReelProgress));
                sag = 1f;
            }
            else
            {
                // The catch has flown free: the hook hangs slack under the tip
                end = Snap(new Vector2(tip.X, tip.Y + 8f));
                sag = 0f;
            }

            var lineMid = Snap(new Vector2((tip.X + end.X) / 2f, (tip.Y + end.Y) / 2f + sag));
            Add(tip, lineMid, LineColor, 1f);
            Add(lineMid, end, LineColor, 1f);

            if (showBobber)
            {
                Add(new Vector2(end.X - 1f, end.Y), new Vector2(end.X + 1f, end.Y), BobberTopColor, 1f);
                Add(new Vector2(end.X - 1f, end.Y + 1f), new Vector2(end.X + 1f, end.Y + 1f), BobberBottomColor, 1f);
            }
        }

        private void Add(Vector2 from, Vector2 to, Color color, float thickness)
        {
            if (_segmentCount >= MaxSegments)
                return;
            _from[_segmentCount] = from;
            _to[_segmentCount] = to;
            _color[_segmentCount] = color;
            _thickness[_segmentCount] = thickness;
            _segmentCount++;
        }

        private static Vector2 Snap(Vector2 v) => new Vector2(FrameWriter.ToPixel(v.X), FrameWriter.ToPixel(v.Y));

        /// <summary>Draws the pole, line and bobber.</summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            BuildSegments();
            for (int i = 0; i < _segmentCount; i++)
                batcher.DrawLine(_from[i], _to[i], _color[i], _thickness[i]);
        }

        /// <summary>Replay frame: one world-space Line op per segment.</summary>
        public void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx)
        {
            BuildSegments();
            for (int i = 0; i < _segmentCount; i++)
                w.WriteLine(_from[i].X, _from[i].Y, _to[i].X, _to[i].Y, _thickness[i], _color[i].PackedValue, LayerDepth, RenderLayer);
        }
    }
}
