using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Particles;
using Nez.Sprites;
using Nez.Textures;
using Nez.Tiled;
using PitHero.ECS.Components;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Turns one live renderable into draw ops by type check (no reflection): stock
    /// <see cref="SpriteRenderer"/> and every animator subclass become a Sprite op,
    /// <see cref="PrototypeSpriteRenderer"/> a filled Rect, <see cref="MultiSpriteAnimator"/> and
    /// <see cref="StaticSpriteCompositor"/> a Composite op from their layers (never their render
    /// texture), <see cref="IFrameCapturable"/> components capture themselves and
    /// <see cref="ILiveOnlyRenderable"/> ones are skipped. Tile maps (recorded as tile events), the UI
    /// canvas (live) and particle emitters (v1) are skipped silently; anything else is skipped with a
    /// one-time warning per type.
    /// </summary>
    public static class FrameCaptureAdapters
    {
        /// <summary>How a renderable is captured, decided once per renderable by <see cref="Classify"/>.</summary>
        public enum Kind : byte
        {
            /// <summary>Never captured (live-only, tile map, particles, UI canvas, unknown type).</summary>
            Skip = 0,
            /// <summary>Implements IFrameCapturable.</summary>
            Capturable,
            /// <summary>A composite layer: skipped while OwnedByComposite, else a stock sprite.</summary>
            CompositeLayer,
            MultiSprite,
            StaticCompositor,
            Prototype,
            Sprite,
        }

        /// <summary>Per-composite cache of each layer's last sprite and its id (composites re-emit every layer every tick).</summary>
        public sealed class CompositeSpriteCache
        {
            public const int MaxLayers = 16;
            public readonly Sprite[] Sprites = new Sprite[MaxLayers];
            public readonly ushort[] Ids = new ushort[MaxLayers];
        }

        private static readonly HashSet<Type> _warnedTypes = new HashSet<Type>();

        /// <summary>Emits the ops for a renderable that is enabled this tick (zero ops = not drawn).</summary>
        public static void Capture(RenderableComponent rc, ref FrameWriter w, FrameCaptureContext ctx)
        {
            Sprite cachedSprite = null;
            ushort cachedSpriteId = 0;
            Capture(rc, Classify(rc), ref w, ctx, ref cachedSprite, ref cachedSpriteId, null);
        }

        /// <summary>Decides once how a renderable is captured (the recorder caches the answer per list position).</summary>
        public static Kind Classify(RenderableComponent rc)
        {
            if (rc is ILiveOnlyRenderable)
                return Kind.Skip;
            if (rc is ICompositeLayer)
                return Kind.CompositeLayer;
            if (rc is IFrameCapturable)
                return Kind.Capturable;
            if (rc is MultiSpriteAnimator)
                return Kind.MultiSprite;
            if (rc is StaticSpriteCompositor)
                return Kind.StaticCompositor;
            if (rc is PrototypeSpriteRenderer)
                return Kind.Prototype;
            if (rc is SpriteRenderer)
                return Kind.Sprite;
            if (!(rc is TiledMapRenderer || rc is ParticleEmitter || rc is UICanvas))
                WarnOnce(rc);
            return Kind.Skip;
        }

        /// <summary>
        /// Emits the ops for a renderable of a known kind. <paramref name="cachedSprite"/> /
        /// <paramref name="cachedSpriteId"/> remember the last sprite seen on this renderable so an
        /// unchanged sprite costs no lookup.
        /// </summary>
        public static void Capture(RenderableComponent rc, Kind kind, ref FrameWriter w, FrameCaptureContext ctx, ref Sprite cachedSprite, ref ushort cachedSpriteId, CompositeSpriteCache layers)
        {
            // The pause dim is a live overlay: the viewer never dims a replay
            if (rc.RenderLayer == GameConfig.TransparentPauseOverlay)
                return;
            switch (kind)
            {
                case Kind.Capturable:
                    ((IFrameCapturable)rc).CaptureFrame(ref w, ctx);
                    return;
                case Kind.CompositeLayer:
                    if (((ICompositeLayer)rc).OwnedByComposite)
                        return;
                    if (rc is SpriteRenderer layerSprite)
                        CaptureSprite(layerSprite, ref w, ctx, ref cachedSprite, ref cachedSpriteId);
                    return;
                case Kind.MultiSprite:
                    CaptureMultiSprite((MultiSpriteAnimator)rc, ref w, ctx, layers);
                    return;
                case Kind.StaticCompositor:
                    CaptureStaticCompositor((StaticSpriteCompositor)rc, ref w, ctx, layers);
                    return;
                case Kind.Prototype:
                    CapturePrototype((PrototypeSpriteRenderer)rc, ref w);
                    return;
                case Kind.Sprite:
                    CaptureSprite((SpriteRenderer)rc, ref w, ctx, ref cachedSprite, ref cachedSpriteId);
                    return;
                default:
                    return;
            }
        }

        private static void WarnOnce(RenderableComponent rc)
        {
            var type = rc.GetType();
            if (_warnedTypes.Add(type))
                Debug.Warn("[FrameCapture] " + type.Name + " is neither stock, IFrameCapturable nor ILiveOnlyRenderable; it will not appear in replays");
        }

        /// <summary>Sprite op for a stock renderer from its live values.</summary>
        public static void CaptureSprite(SpriteRenderer sr, ref FrameWriter w, FrameCaptureContext ctx)
        {
            Sprite cachedSprite = null;
            ushort cachedSpriteId = 0;
            CaptureSprite(sr, ref w, ctx, ref cachedSprite, ref cachedSpriteId);
        }

        /// <summary>Sprite op for a stock renderer, resolving the sprite id through a per-renderable cache.</summary>
        public static void CaptureSprite(SpriteRenderer sr, ref FrameWriter w, FrameCaptureContext ctx, ref Sprite cachedSprite, ref ushort cachedSpriteId)
        {
            var sprite = sr.Sprite;
            if (sprite == null)
                return;
            ushort id;
            if (ReferenceEquals(sprite, cachedSprite))
            {
                id = cachedSpriteId;
            }
            else
            {
                id = ctx.SpriteId(sprite);
                cachedSprite = sprite;
                cachedSpriteId = id;
            }
            if (id == SpriteKeyRegistry.None)
                return;
            var pos = sr.Entity.Transform.Position + sr.LocalOffset;
            w.WriteSprite(id, pos.X, pos.Y, sr.LayerDepth, sr.RenderLayer, sr.Color.PackedValue,
                FrameCaptureContext.SpriteFlags(sr.SpriteEffects, sr.RenderLayer));
        }

        /// <summary>A prototype (pixel) renderer is a filled rectangle of its size and color.</summary>
        public static void CapturePrototype(PrototypeSpriteRenderer p, ref FrameWriter w)
        {
            var scale = p.Entity.Transform.Scale;
            var origin = p.Origin;
            var pos = p.Entity.Transform.Position - new Vector2(origin.X * scale.X, origin.Y * scale.Y) + p.LocalOffset;
            byte flags = FrameCaptureContext.IsScreenSpaceLayer(p.RenderLayer) ? FrameOpFlags.ScreenSpace : FrameOpFlags.None;
            w.WriteRect(pos.X, pos.Y, p.Width * scale.X, p.Height * scale.Y, p.Color.PackedValue, flags);
        }

        /// <summary>A layer's sprite id through the per-composite cache (or the context when there is none).</summary>
        private static ushort LayerSpriteId(Sprite sprite, int layerIndex, FrameCaptureContext ctx, CompositeSpriteCache cache)
        {
            if (cache == null || layerIndex >= CompositeSpriteCache.MaxLayers)
                return ctx.SpriteId(sprite);
            if (ReferenceEquals(cache.Sprites[layerIndex], sprite))
                return cache.Ids[layerIndex];
            ushort id = ctx.SpriteId(sprite);
            cache.Sprites[layerIndex] = sprite;
            cache.Ids[layerIndex] = id;
            return id;
        }

        /// <summary>Composite op for a paperdoll: one layer per composite layer with a sprite.</summary>
        public static void CaptureMultiSprite(MultiSpriteAnimator m, ref FrameWriter w, FrameCaptureContext ctx)
            => CaptureMultiSprite(m, ref w, ctx, null);

        /// <summary>Composite op for a paperdoll, resolving layer sprite ids through a per-composite cache.</summary>
        public static void CaptureMultiSprite(MultiSpriteAnimator m, ref FrameWriter w, FrameCaptureContext ctx, CompositeSpriteCache cache)
        {
            int count = 0;
            for (int i = 0; i < m.LayerCount; i++)
            {
                var layer = m.GetLayer(i);
                if (layer != null && layer.Sprite != null)
                    count++;
            }
            if (count == 0 || count > byte.MaxValue)
                return;
            var pos = m.Entity.Transform.Position + m.LocalOffset;
            uint tint = m.Color.PackedValue;
            w.WriteCompositeHeader(pos.X, pos.Y, m.LayerDepth, m.RenderLayer, (byte)count);
            for (int i = 0; i < m.LayerCount; i++)
            {
                var layer = m.GetLayer(i);
                if (layer == null || layer.Sprite == null)
                    continue;
                var offset = layer.LocalOffset;
                w.WriteCompositeLayer(LayerSpriteId(layer.Sprite, i, ctx, cache), offset.X, offset.Y, MultiplyColor(layer.LayerColor.PackedValue, tint),
                    layer.FlipX ? FrameOpFlags.FlipX : FrameOpFlags.None);
            }
        }

        /// <summary>Composite op for a static compositor: one layer per child renderer with a sprite.</summary>
        public static void CaptureStaticCompositor(StaticSpriteCompositor c, ref FrameWriter w, FrameCaptureContext ctx)
            => CaptureStaticCompositor(c, ref w, ctx, null);

        /// <summary>Composite op for a static compositor, resolving layer sprite ids through a per-composite cache.</summary>
        public static void CaptureStaticCompositor(StaticSpriteCompositor c, ref FrameWriter w, FrameCaptureContext ctx, CompositeSpriteCache cache)
        {
            int count = 0;
            for (int i = 0; i < c.LayerCount; i++)
            {
                var layer = c.GetLayer(i);
                if (layer != null && layer.Sprite != null)
                    count++;
            }
            if (count == 0 || count > byte.MaxValue)
                return;
            var pos = c.Entity.Transform.Position + c.LocalOffset;
            uint tint = c.Color.PackedValue;
            w.WriteCompositeHeader(pos.X, pos.Y, c.LayerDepth, c.RenderLayer, (byte)count);
            for (int i = 0; i < c.LayerCount; i++)
            {
                var layer = c.GetLayer(i);
                if (layer == null || layer.Sprite == null)
                    continue;
                var offset = layer.LocalOffset;
                w.WriteCompositeLayer(LayerSpriteId(layer.Sprite, i, ctx, cache), offset.X, offset.Y, MultiplyColor(layer.Color.PackedValue, tint),
                    FrameCaptureContext.SpriteFlags(layer.SpriteEffects, 0));
            }
        }

        /// <summary>Component-wise product of two packed RGBA colors (a layer color under its composite's tint).</summary>
        public static uint MultiplyColor(uint a, uint b)
        {
            if (b == 0xFFFFFFFFu)
                return a;
            if (a == 0xFFFFFFFFu)
                return b;
            uint r = ((a & 0xFF) * (b & 0xFF) + 127) / 255;
            uint g = (((a >> 8) & 0xFF) * ((b >> 8) & 0xFF) + 127) / 255;
            uint bl = (((a >> 16) & 0xFF) * ((b >> 16) & 0xFF) + 127) / 255;
            uint al = (((a >> 24) & 0xFF) * ((b >> 24) & 0xFF) + 127) / 255;
            return r | (g << 8) | (bl << 16) | (al << 24);
        }
    }
}
