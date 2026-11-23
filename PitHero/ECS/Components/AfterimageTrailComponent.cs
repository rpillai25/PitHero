using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using PitHero.Services.Replay.Frames;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A fading, tinted afterimage trail behind a character during a choreographed move (Iaido Slash, Heavy
    /// Strike). Nez's SpriteTrail copies a single SpriteRenderer; characters are layered composites
    /// (<see cref="MultiSpriteAnimator"/>), so this snapshots every layer's sprite, offset and flip each time
    /// the body moves <see cref="GameConfig.AfterimageSpacing"/> px and draws the copies fading out, tinted
    /// <see cref="GameConfig.AfterimageColor"/>, just behind the character (a small negative Y-sort offset).
    /// Render-only; it runs on the fixed step, holds while paused and removes itself once the last copy fades.
    /// Each copy is recorded as a Composite frame op, so replays show the trail.
    /// </summary>
    public class AfterimageTrailComponent : RenderableComponent, IUpdatable, IFrameCapturable, IYSortOffset
    {
        private const int MaxGhosts = 16;
        private const int MaxLayers = 8;

        private struct Ghost
        {
            public Vector2 Position;
            public float Age;
            public int LayerCount;
        }

        private readonly Ghost[] _ghosts = new Ghost[MaxGhosts];
        private readonly Sprite[] _sprites = new Sprite[MaxGhosts * MaxLayers];
        private readonly Vector2[] _offsets = new Vector2[MaxGhosts * MaxLayers];
        private readonly bool[] _flips = new bool[MaxGhosts * MaxLayers];
        private int _next;
        private MultiSpriteAnimator _body;
        private bool _emitting;
        private Vector2 _lastSnapshot;
        private bool _hasSnapshot;

        /// <summary>Starts (or restarts) the trail on <paramref name="entity"/>'s body.</summary>
        public static void Begin(Entity entity)
        {
            if (entity == null || entity.IsDestroyed || Core.CosmeticUpdatesSuspended)
                return;
            var body = entity.GetComponent<MultiSpriteAnimator>();
            if (body == null)
                return;
            var trail = entity.GetComponent<AfterimageTrailComponent>() ?? entity.AddComponent(new AfterimageTrailComponent());
            trail._body = body;
            trail.SetRenderLayer(body.RenderLayer);
            trail._emitting = true;
            trail._hasSnapshot = false;
            trail.Snapshot();
        }

        /// <summary>Stops adding copies; the ones already left fade out, then the component removes itself.</summary>
        public static void End(Entity entity)
        {
            var trail = entity?.GetComponent<AfterimageTrailComponent>();
            if (trail != null)
                trail._emitting = false;
        }

        /// <inheritdoc/>
        public float YSortOffset => -0.5f; // just behind the character on its own Y-sorted layer

        /// <summary>Where the body is drawn now (the compositor rounds the entity position).</summary>
        private Vector2 BodyPosition
        {
            get
            {
                var p = Entity.Transform.Position;
                return new Vector2(System.MathF.Round(p.X), System.MathF.Round(p.Y)) + _body.LocalOffset;
            }
        }

        /// <inheritdoc/>
        public void Update()
        {
            if (_body == null || _body.Entity == null)
            {
                Remove();
                return;
            }
            var pause = Core.Instance != null ? Core.Services.GetService<PitHero.Services.PauseService>() : null;
            if (pause?.IsPaused == true)
                return;

            float dt = Time.DeltaTime;
            int alive = 0;
            for (int i = 0; i < MaxGhosts; i++)
            {
                if (_ghosts[i].LayerCount == 0)
                    continue;
                _ghosts[i].Age += dt;
                if (_ghosts[i].Age >= GameConfig.AfterimageFadeDuration)
                    _ghosts[i].LayerCount = 0;
                else
                    alive++;
            }

            if (_emitting && Vector2.DistanceSquared(BodyPosition, _lastSnapshot)
                >= GameConfig.AfterimageSpacing * GameConfig.AfterimageSpacing)
                Snapshot();

            if (!_emitting && alive == 0)
                Remove();
        }

        private void Remove()
        {
            _body = null;
            if (Entity != null && !Entity.IsDestroyed)
                Entity.RemoveComponent(this);
        }

        /// <summary>Copies the body's layers as a new afterimage (overwriting the oldest when full).</summary>
        private void Snapshot()
        {
            var pos = BodyPosition;
            if (_hasSnapshot && pos == _lastSnapshot)
                return;
            _lastSnapshot = pos;
            _hasSnapshot = true;

            int slot = _next;
            _next = (_next + 1) % MaxGhosts;
            int n = 0;
            for (int i = 0; i < _body.LayerCount && n < MaxLayers; i++)
            {
                var layer = _body.GetLayer(i);
                if (layer?.Sprite == null)
                    continue;
                int k = slot * MaxLayers + n;
                _sprites[k] = layer.Sprite;
                _offsets[k] = layer.LocalOffset;
                _flips[k] = layer.FlipX;
                n++;
            }
            _ghosts[slot].Position = pos;
            _ghosts[slot].Age = 0f;
            _ghosts[slot].LayerCount = n;
        }

        /// <summary>A copy's colour: the trail tint, fading from <see cref="GameConfig.AfterimageStartAlpha"/> to clear.</summary>
        private static Color GhostColor(float age)
        {
            float a = GameConfig.AfterimageStartAlpha * (1f - age / GameConfig.AfterimageFadeDuration);
            if (a < 0f) a = 0f;
            var c = GameConfig.AfterimageColor;
            // Premultiplied alpha (AlphaBlend)
            return new Color((byte)(c.R * a), (byte)(c.G * a), (byte)(c.B * a), (byte)(255 * a));
        }

        /// <inheritdoc/>
        public override RectangleF Bounds
        {
            get
            {
                var p = Entity.Transform.Position;
                float half = GameConfig.TileSize * 2f;
                return new RectangleF(p.X - half, p.Y - half, half * 2f, half * 2f);
            }
        }

        /// <inheritdoc/>
        public override float Width => GameConfig.TileSize * 4f;

        /// <inheritdoc/>
        public override float Height => GameConfig.TileSize * 4f;

        /// <inheritdoc/>
        public override void Render(Batcher batcher, Camera camera)
        {
            for (int g = 0; g < MaxGhosts; g++)
            {
                var ghost = _ghosts[g];
                if (ghost.LayerCount == 0)
                    continue;
                var color = GhostColor(ghost.Age);
                for (int l = 0; l < ghost.LayerCount; l++)
                {
                    int k = g * MaxLayers + l;
                    var sprite = _sprites[k];
                    if (sprite == null)
                        continue;
                    batcher.Draw(sprite, ghost.Position + _offsets[k], color, 0f, sprite.Origin, Vector2.One,
                        _flips[k] ? SpriteEffects.FlipHorizontally : SpriteEffects.None, LayerDepth);
                }
            }
        }

        /// <inheritdoc/>
        public void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx)
        {
            for (int g = 0; g < MaxGhosts; g++)
            {
                var ghost = _ghosts[g];
                if (ghost.LayerCount == 0)
                    continue;
                int count = 0;
                for (int l = 0; l < ghost.LayerCount; l++)
                {
                    var sprite = _sprites[g * MaxLayers + l];
                    if (sprite != null && ctx.SpriteId(sprite) != SpriteKeyRegistry.None)
                        count++;
                }
                if (count == 0)
                    continue;
                uint color = GhostColor(ghost.Age).PackedValue;
                w.WriteCompositeHeader(ghost.Position.X, ghost.Position.Y, LayerDepth, RenderLayer, (byte)count);
                for (int l = 0; l < ghost.LayerCount; l++)
                {
                    int k = g * MaxLayers + l;
                    var sprite = _sprites[k];
                    if (sprite == null)
                        continue;
                    ushort id = ctx.SpriteId(sprite);
                    if (id == SpriteKeyRegistry.None)
                        continue;
                    w.WriteCompositeLayer(id, _offsets[k].X, _offsets[k].Y, color,
                        _flips[k] ? FrameOpFlags.FlipX : FrameOpFlags.None);
                }
            }
        }
    }
}
