using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Particles;
using Nez.Sprites;
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
        private static readonly HashSet<Type> _warnedTypes = new HashSet<Type>();

        /// <summary>Emits the ops for a renderable that is enabled this tick (zero ops = not drawn).</summary>
        public static void Capture(RenderableComponent rc, ref FrameWriter w, FrameCaptureContext ctx)
        {
            if (rc is ILiveOnlyRenderable)
                return;
            // The pause dim is a live overlay: the viewer never dims a replay
            if (rc.RenderLayer == GameConfig.TransparentPauseOverlay)
                return;
            if (rc is ICompositeLayer layer && layer.OwnedByComposite)
                return;
            if (rc is IFrameCapturable capturable)
            {
                capturable.CaptureFrame(ref w, ctx);
                return;
            }
            if (rc is MultiSpriteAnimator multi)
            {
                CaptureMultiSprite(multi, ref w, ctx);
                return;
            }
            if (rc is StaticSpriteCompositor compositor)
            {
                CaptureStaticCompositor(compositor, ref w, ctx);
                return;
            }
            if (rc is PrototypeSpriteRenderer prototype)
            {
                CapturePrototype(prototype, ref w);
                return;
            }
            if (rc is SpriteRenderer sprite)
            {
                CaptureSprite(sprite, ref w, ctx);
                return;
            }
            if (rc is TiledMapRenderer || rc is ParticleEmitter || rc is UICanvas)
                return;
            WarnOnce(rc);
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
            var sprite = sr.Sprite;
            if (sprite == null)
                return;
            ushort id = ctx.SpriteId(sprite);
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

        /// <summary>Composite op for a paperdoll: one layer per composite layer with a sprite.</summary>
        public static void CaptureMultiSprite(MultiSpriteAnimator m, ref FrameWriter w, FrameCaptureContext ctx)
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
                w.WriteCompositeLayer(ctx.SpriteId(layer.Sprite), offset.X, offset.Y, MultiplyColor(layer.LayerColor.PackedValue, tint),
                    layer.FlipX ? FrameOpFlags.FlipX : FrameOpFlags.None);
            }
        }

        /// <summary>Composite op for a static compositor: one layer per child renderer with a sprite.</summary>
        public static void CaptureStaticCompositor(StaticSpriteCompositor c, ref FrameWriter w, FrameCaptureContext ctx)
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
                w.WriteCompositeLayer(ctx.SpriteId(layer.Sprite), offset.X, offset.Y, MultiplyColor(layer.Color.PackedValue, tint),
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
