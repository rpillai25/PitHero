using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// Static factory that renders 1x atlas sprites into 64x64 RenderTexture-backed 2x sprites on demand.
    /// Cache is keyed by sprite name; each RT is painted once and repainted on GraphicsDevice reset.
    /// </summary>
    public static class ButtonSprite2xFactory
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // Paired list of (rt, sourceSprite) for repaint on device reset.
        private static readonly List<RenderTexture> _rts = new List<RenderTexture>();
        private static readonly List<Sprite> _sources = new List<Sprite>();
        private static bool _resetSubscribed = false;

        /// <summary>
        /// Returns a 2x-scale sprite for the named atlas sprite, creating and caching a RenderTexture
        /// the first time. Returns the original 1x sprite when running headless (no graphics device).
        /// </summary>
        public static Sprite GetOrCreate2x(SpriteAtlas atlas, string spriteName)
        {
            // Headless guard — tests run without a graphics device.
            if (Core.GraphicsDevice == null || Graphics.Instance == null)
                return atlas.GetSprite(spriteName);

            if (_cache.TryGetValue(spriteName, out var cached))
                return cached;

            var src = atlas.GetSprite(spriteName);
            int w = src.SourceRect.Width * 2;
            int h = src.SourceRect.Height * 2;

            // ResizeBehavior.None prevents the RT from being resized when the scene RT changes.
            var rt = new RenderTexture(w, h, SurfaceFormat.Color, DepthFormat.None)
            {
                ResizeBehavior = RenderTexture.RenderTextureResizeBehavior.None
            };

            PaintInto(rt, src, w, h);

            var sprite = new Sprite(rt.RenderTarget);
            _cache[spriteName] = sprite;

            _rts.Add(rt);
            _sources.Add(src);

            if (!_resetSubscribed)
            {
                Core.Emitter.AddObserver(CoreEvents.GraphicsDeviceReset, OnDeviceReset);
                _resetSubscribed = true;
            }

            return sprite;
        }

        /// <summary>
        /// Builds a half-height ImageButtonStyle from Up=baseName, Down=baseName+"Inverse",
        /// Over=baseName+"Highlight", each code-gen scaled to 2x.
        /// </summary>
        public static ImageButtonStyle CreateHalfStyle(SpriteAtlas atlas, string baseName)
        {
            return new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(GetOrCreate2x(atlas, baseName)),
                ImageDown = new SpriteDrawable(GetOrCreate2x(atlas, baseName + "Inverse")),
                ImageOver = new SpriteDrawable(GetOrCreate2x(atlas, baseName + "Highlight"))
            };
        }

        private static void PaintInto(RenderTexture rt, Sprite src, int w, int h)
        {
            var prevRTs = Core.GraphicsDevice.GetRenderTargets();
            Core.GraphicsDevice.SetRenderTarget(rt);
            Core.GraphicsDevice.Clear(Color.Transparent);

            var batcher = Graphics.Instance.Batcher;
            batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, Matrix.Identity, false);

            // Draw from the underlying Texture2D using SourceRect directly so that the atlas sprite's
            // centered origin does not shift the art within the RT.
            batcher.Draw(
                src.Texture2D,
                destinationRectangle: new Rectangle(0, 0, w, h),
                sourceRectangle: src.SourceRect,
                color: Color.White);

            batcher.End();

            Core.GraphicsDevice.SetRenderTargets(prevRTs.Length > 0 ? prevRTs : null);
        }

        private static void OnDeviceReset()
        {
            for (int i = 0; i < _rts.Count; i++)
            {
                var rt  = _rts[i];
                var src = _sources[i];
                int w   = src.SourceRect.Width * 2;
                int h   = src.SourceRect.Height * 2;
                PaintInto(rt, src, w, h);
            }
        }
    }
}
