using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// The Nez-facing side of interning for one capture session: resolves a live <see cref="Sprite"/>
    /// to its stream id once per reference (the key is <c>Texture2D.Name</c> + source rectangle, see
    /// <see cref="SpriteKeyRegistry"/>), and forwards string and nine-patch names. Sprites on an
    /// unnamed texture (composite render textures, a loader that forgot to name its atlas) get id 0
    /// and are not drawn; each such texture is reported once.
    /// </summary>
    public sealed class FrameCaptureContext
    {
        private readonly SpriteKeyRegistry _registry;
        private readonly Dictionary<Sprite, ushort> _spriteIds = new Dictionary<Sprite, ushort>(2048, ReferenceEqualityComparer.Instance);
        private readonly HashSet<Texture2D> _warnedTextures = new HashSet<Texture2D>(ReferenceEqualityComparer.Instance);

        public FrameCaptureContext(SpriteKeyRegistry registry)
        {
            _registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
        }

        /// <summary>The table the ids point into.</summary>
        public SpriteKeyRegistry Registry => _registry;
        /// <summary>
        /// The scene's day-night colour-grading material (null headless). A renderable drawing through
        /// it gets <see cref="FrameOpFlags.Graded"/> so the viewer applies the same material.
        /// </summary>
        public Nez.Material GradedMaterial;
        /// <summary>Distinct sprite references resolved so far.</summary>
        public int CachedSpriteCount => _spriteIds.Count;

        /// <summary>The stream id of a sprite, interning its key on first sight; 0 for null or an unnamed texture.</summary>
        public ushort SpriteId(Sprite sprite)
        {
            if (sprite == null)
                return SpriteKeyRegistry.None;
            if (_spriteIds.TryGetValue(sprite, out ushort id))
                return id;
            var texture = sprite.Texture2D;
            string name = texture?.Name;
            if (string.IsNullOrEmpty(name))
            {
                if (texture != null && _warnedTextures.Add(texture))
                    Debug.Warn("[FrameCapture] Sprite on an unnamed texture (" + texture.Width + "x" + texture.Height + ") cannot be recorded; name it in the loader");
                id = SpriteKeyRegistry.None;
            }
            else
            {
                var rect = sprite.SourceRect;
                id = _registry.InternSprite(new SpriteKey(name, rect.X, rect.Y, rect.Width, rect.Height));
            }
            _spriteIds[sprite] = id;
            return id;
        }

        /// <summary>The stream id of a string (0 for null/empty).</summary>
        public ushort StringId(string s) => _registry.InternString(s);

        /// <summary>The stream id of a nine-patch by name (0 for null/empty).</summary>
        public ushort NinePatchId(string name) => _registry.InternNinePatch(name);

        /// <summary>Forgets the sprite reference cache (a new scene may hold new Sprite objects for the same keys).</summary>
        public void ClearSpriteCache()
        {
            _spriteIds.Clear();
        }

        /// <summary>True for render layers the scene's ScreenSpaceRenderer draws (positions are screen pixels).</summary>
        public static bool IsScreenSpaceLayer(int renderLayer)
        {
            return renderLayer == GameConfig.RenderLayerSpeechBubble
                || renderLayer == GameConfig.TransparentPauseOverlay
                || renderLayer == GameConfig.RenderLayerUI
                || renderLayer == GameConfig.RenderLayerGraphicalHUD
                || renderLayer == GameConfig.RenderLayerActionQueue;
        }

        /// <summary>Sprite op flags from a renderer's effects and layer.</summary>
        public static byte SpriteFlags(SpriteEffects effects, int renderLayer)
        {
            byte flags = FrameOpFlags.None;
            if ((effects & SpriteEffects.FlipHorizontally) != 0) flags |= FrameOpFlags.FlipX;
            if ((effects & SpriteEffects.FlipVertically) != 0) flags |= FrameOpFlags.FlipY;
            if (IsScreenSpaceLayer(renderLayer)) flags |= FrameOpFlags.ScreenSpace;
            return flags;
        }

        /// <summary><see cref="FrameOpFlags.Graded"/> when the renderable draws through the grading material.</summary>
        public byte GradedFlag(RenderableComponent rc)
        {
            var graded = GradedMaterial;
            return graded != null && ReferenceEquals(rc.Material, graded) ? FrameOpFlags.Graded : FrameOpFlags.None;
        }
    }
}
