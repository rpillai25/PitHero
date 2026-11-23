using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Services.Replay.Frames;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Draws one physical-attack effect (issue #458): a SkillsStencils.atlas animation, rotated to point from the
    /// attacker toward the target and placed at the entity by its sprite origin (the anchor stored in the
    /// atlas, so cropped art lands where it should). The art is drawn pointing right; when the
    /// effect points left it is flipped vertically so a slash still sweeps top to bottom.
    ///
    /// The driving coroutine (<see cref="Util.AttackEffects"/>) owns the timing and sets
    /// <see cref="Progress"/> each fixed step, so the live draw and the recorded RotSprite op agree at
    /// every tick. Stock sprite ops cannot carry a rotation, hence <see cref="IFrameCapturable"/>.
    /// </summary>
    public class AttackEffectRenderer : RenderableComponent, IFrameCapturable, IUpdatable
    {
        private SpriteAnimation _animation;
        private float _rotation;
        private SpriteEffects _effects;
        private float _autoDuration;
        private float _autoElapsed;

        /// <summary>Animation progress 0..1; the frame shown is the matching slice of the animation.</summary>
        public float Progress;

        /// <summary>
        /// When set, the effect stays anchored to this entity (a heal or buff on a character who keeps
        /// walking) until it ends; a destroyed target leaves the effect where it last was.
        /// </summary>
        public Entity Follow;

        /// <summary>Where the effect is drawn: the followed entity while it lives, else this entity.</summary>
        private Vector2 AnchorPosition
        {
            get
            {
                if (Follow != null && !Follow.IsDestroyed)
                    return Follow.Transform.Position;
                return Entity.Transform.Position;
            }
        }

        /// <summary>
        /// Plays the animation by itself over <paramref name="duration"/> seconds of simulation time (held
        /// while paused), then destroys the entity. For effects nothing waits on (heals, buffs, Provoke).
        /// </summary>
        public void AutoPlay(float duration)
        {
            _autoDuration = duration > 0f ? duration : 0.01f;
            _autoElapsed = 0f;
        }

        /// <summary>Advances an <see cref="AutoPlay"/> effect; driven effects (duration 0) are left alone.</summary>
        public void Update()
        {
            if (Follow != null)
            {
                if (Follow.IsDestroyed)
                    Follow = null;
                else
                    Entity.SetPosition(Follow.Transform.Position);
            }
            if (_autoDuration <= 0f)
                return;
            var pause = Core.Instance != null ? Core.Services.GetService<PitHero.Services.PauseService>() : null;
            if (pause?.IsPaused != true)
                _autoElapsed += Time.DeltaTime;
            Progress = _autoElapsed / _autoDuration;
            if (Progress >= 1f)
            {
                _autoDuration = 0f;
                Entity.Destroy();
            }
        }

        /// <summary>Sets the animation and its direction (radians, 0 = pointing right).</summary>
        public void Setup(SpriteAnimation animation, float rotation)
        {
            _animation = animation;
            _rotation = rotation;
            // Pointing left (cos < 0) would draw the art upside down; flip it back
            _effects = System.MathF.Cos(rotation) < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Progress = 0f;
        }

        /// <summary>
        /// Sets an upright animation (no rotation), mirrored left-right when <paramref name="mirror"/> is set
        /// (a rod held toward a target on the left). The mirror turns about the sprite's anchor, so the anchor
        /// stays on the entity.
        /// </summary>
        public void SetupMirrored(SpriteAnimation animation, bool mirror)
        {
            _animation = animation;
            _rotation = 0f;
            _effects = mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Progress = 0f;
        }

        /// <summary>
        /// The origin to draw <paramref name="sprite"/> with: its anchor, mirrored when the sprite is flipped
        /// left-right (Nez flips the texture inside the quad but not the origin). Shared with the replay renderer.
        /// </summary>
        public static Vector2 DrawOrigin(Sprite sprite, SpriteEffects effects)
        {
            var origin = sprite.Origin;
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
                origin.X = sprite.SourceRect.Width - origin.X;
            return origin;
        }

        /// <summary>Frames in the animation (0 without one).</summary>
        public int FrameCount => _animation?.Sprites != null ? _animation.Sprites.Length : 0;

        /// <summary>The sprite for the current progress, or null when there is no animation.</summary>
        private Sprite CurrentSprite
        {
            get
            {
                if (_animation == null || _animation.Sprites == null || _animation.Sprites.Length == 0)
                    return null;
                int count = _animation.Sprites.Length;
                int index = (int)(Progress * count);
                if (index < 0) index = 0;
                if (index >= count) index = count - 1;
                return _animation.Sprites[index];
            }
        }

        /// <summary>A generous square around the entity so a rotated sprite is never culled early.</summary>
        public override RectangleF Bounds
        {
            get
            {
                var p = AnchorPosition + LocalOffset;
                float half = GameConfig.TileSize * 1.5f;
                return new RectangleF(p.X - half, p.Y - half, half * 2f, half * 2f);
            }
        }

        /// <summary>Width of <see cref="Bounds"/>.</summary>
        public override float Width => GameConfig.TileSize * 3f;

        /// <summary>Height of <see cref="Bounds"/>.</summary>
        public override float Height => GameConfig.TileSize * 3f;

        /// <summary>Draws the current frame rotated about its origin.</summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            var sprite = CurrentSprite;
            if (sprite == null)
                return;
            batcher.Draw(sprite, AnchorPosition + LocalOffset, Color, _rotation, DrawOrigin(sprite, _effects),
                Vector2.One, _effects, LayerDepth);
        }

        /// <inheritdoc/>
        public void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx)
        {
            var sprite = CurrentSprite;
            if (sprite == null)
                return;
            ushort id = ctx.SpriteId(sprite);
            if (id == SpriteKeyRegistry.None)
                return;
            var pos = AnchorPosition + LocalOffset;
            byte flags = FrameOpFlags.None;
            if ((_effects & SpriteEffects.FlipVertically) != 0) flags |= FrameOpFlags.FlipY;
            if ((_effects & SpriteEffects.FlipHorizontally) != 0) flags |= FrameOpFlags.FlipX;
            w.WriteRotSprite(id, pos.X, pos.Y, _rotation, LayerDepth, RenderLayer, Color.PackedValue, flags);
        }
    }
}
