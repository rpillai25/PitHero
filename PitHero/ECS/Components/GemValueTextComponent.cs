using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.ECS.Scenes;
using PitHero.Services.Replay.Frames;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// The "Agate (+1)" name-and-gold label above a gem rising out of the gem dig site (issue #452). Drawn in screen
    /// space (<see cref="GameConfig.RenderLayerSpeechBubble"/>) so the HUD font stays crisp at any
    /// zoom: the world anchor (the gem's top edge) is converted with the world camera every frame.
    /// <see cref="GemDigEffectComponent"/> drives its alpha.
    /// </summary>
    public class GemValueTextComponent : RenderableComponent, IFrameCapturable
    {
        private readonly string _text;
        private readonly Color _baseColor;
        private readonly float _anchorOffsetY;
        private float _alpha = 1f;

        /// <summary>Creates the text; anchorOffsetY is the world distance from the entity to the anchor (negative = above).</summary>
        public GemValueTextComponent(string text, Color color, float anchorOffsetY)
        {
            _text = text;
            _baseColor = color;
            _anchorOffsetY = anchorOffsetY;
        }

        /// <summary>Screen pixels; Bounds is not used for culling (see IsVisibleFromCamera).</summary>
        public override float Width => 64f;

        /// <summary>Screen pixels; Bounds is not used for culling (see IsVisibleFromCamera).</summary>
        public override float Height => 16f;

        /// <summary>Opacity 0..1, set by the rise animation.</summary>
        public void SetAlpha(float alpha)
        {
            _alpha = alpha < 0f ? 0f : (alpha > 1f ? 1f : alpha);
        }

        /// <summary>
        /// Called by the ScreenSpaceRenderer with its own static camera — ignored. Visibility follows
        /// the world camera, padded by a tile so the text does not pop at the edges.
        /// </summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            var sceneCamera = Entity.Scene?.Camera;
            if (sceneCamera == null)
                return false;
            var p = Entity.Position;
            var cam = sceneCamera.Bounds;
            const float margin = GameConfig.TileSize;
            return p.X >= cam.X - margin && p.X <= cam.Right + margin
                && p.Y >= cam.Y - margin && p.Y <= cam.Bottom + margin;
        }

        /// <summary>Draws the text centered above the anchor in screen pixels.</summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            var scene = Entity.Scene as MainGameScene;
            var font = scene?.GetHudFontForCurrentMode();
            if (font == null)
                return;
            // The half-window font is a pre-scaled 2x font; the gap scales with it
            float s = ReferenceEquals(font, scene.HudFrameFont) ? 1f : 2f;
            var anchor = scene.Camera.WorldToScreenPoint(new Vector2(Entity.Position.X, Entity.Position.Y + _anchorOffsetY));
            var size = font.MeasureString(_text);
            var pos = new Vector2(
                Mathf.Round(anchor.X - size.X * 0.5f),
                Mathf.Round(anchor.Y - font.LineHeight - GameConfig.GemValueTextGapY * s));
            font.DrawInto(batcher, _text, pos, _baseColor * _alpha, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Replay frame: the text at the world anchor with a screen-pixel offset, centered, always in
        /// the Normal (1x) HUD font like every other recorded text.
        /// </summary>
        public void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx)
        {
            var frameFont = (Entity.Scene as MainGameScene)?.HudFrameFont;
            if (frameFont == null || _alpha <= 0f)
                return;
            float dy = -(frameFont.LineHeight + GameConfig.GemValueTextGapY);
            w.WriteText(ctx.StringId(_text), Entity.Position.X, Entity.Position.Y + _anchorOffsetY, 0f, dy,
                (_baseColor * _alpha).PackedValue, 1f, FrameFontId.Hud,
                (byte)(FrameOpFlags.ConstantScreenSize | FrameOpFlags.Centered), 0, FrameOpCode.AllChars);
        }
    }
}
