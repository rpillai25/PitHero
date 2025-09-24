using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using PitHero.ECS.Scenes;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>Bouncy text renderer for battle messages like "Miss" (world-space, constant screen size, per-character bounce)</summary>
    internal class BouncyTextComponent : RenderableComponent, IUpdatable
    {
        int[] _bounceTable = {
            0,0,0,0,0,0,3,6,9,12,
            14,15,15,16,16,16,15,15,14,12,
            9,6,3,0,2,4,4,5,5,5,
            4,4,2,1,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,0,
            0,0,0,0};

        // up to 4 characters (index 3 = leftmost, 0 = rightmost) to mirror BouncyDigitComponent layout
        string[] _chars = new string[4];

        uint _elapsedFrames;
        uint _startFrame;
        float _elapsedTime;

        uint _pauseStartDelta;
        uint _pauseFrames;
        float _pauseTime;
        bool _pausedLastFrame = false;

        public static Color HeroMissColor = Color.Red;
        public static Color EnemyMissColor = Color.White;
        private Color _initColor = Color.White;
        private Color _currentColor = Color.White;

        private PauseService _pauseService;

        // Character spacing cached per HUD font instance (screen-space pixel width)
        private float _charSpacing = 6f;
        private BitmapFont _cachedFont; // last font used to compute spacing

        public override float Width => 32;
        public override float Height => 32;

        /// <summary>Initialize text for display</summary>
        public void Init(string text, Color textColor)
        {
            // reset timing
            _startFrame = Time.FrameCount;
            _elapsedFrames = 0;
            _elapsedTime = 0f;

            // default and clamp to 4 characters
            if (string.IsNullOrEmpty(text))
                text = "Miss";

            // clear array
            for (int i = 0; i < 4; i++)
                _chars[i] = string.Empty;

            // Fill characters such that leftmost goes into index 3, next into 2, etc.
            // This mirrors the rendering layout used by BouncyDigitComponent.
            int len = text.Length;
            if (len > 4) len = 4;
            for (int i = 0; i < len; i++)
            {
                // i = 0 is leftmost char of provided text
                _chars[3 - i] = text[i].ToString();
            }

            _currentColor = _initColor = textColor;
        }

        /// <summary>Service fetch</summary>
        public override void OnAddedToEntity() => _pauseService = Core.Services.GetService<PauseService>();

        /// <summary>World-space render with per-character bounce animation and inverse zoom scaling</summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            var camBounds = camera.Bounds;
            var worldPos = Entity.Position;
            const int margin = 48;
            if (worldPos.X < camBounds.X - margin || worldPos.X > camBounds.Right + margin ||
                worldPos.Y < camBounds.Y - margin || worldPos.Y > camBounds.Bottom + margin)
                return;

            var scene = (MainGameScene)Entity.Scene;
            var hudFont = scene.GetHudFontForCurrentMode();
            if (hudFont == null)
                return;

            // Recalculate spacing only if font instance changed
            if (!ReferenceEquals(hudFont, _cachedFont))
            {
                var measure = hudFont.MeasureString("M"); // representative glyph width
                _charSpacing = measure.X > 0 ? measure.X : 6f; // screen-space pixels between characters at default scale
                _cachedFont = hudFont;
            }

            // Inverse zoom for text scaling so on-screen size is constant
            float inverseZoom = 1f / camera.RawZoom;
            // Convert desired screen-space spacing into world-space so after camera zoom it stays constant
            float spacingWorld = _charSpacing * inverseZoom;

            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(_chars[i]))
                    continue;
                BounceStyle2(i, worldPos.X, worldPos.Y, inverseZoom, spacingWorld, batcher);
            }
        }

        /// <summary>Advance timers</summary>
        public void Update()
        {
            _elapsedTime += Time.DeltaTime;

            if (_pauseService?.IsPaused == true)
            {
                if (!_pausedLastFrame)
                {
                    _pauseStartDelta = Time.FrameCount - _startFrame;
                    _pausedLastFrame = true;
                }
                _pauseFrames = Time.FrameCount;
                _pauseTime += Time.DeltaTime;
                return;
            }

            if (_pausedLastFrame)
            {
                _pausedLastFrame = false;
                _startFrame = _pauseFrames - _pauseStartDelta;
                _elapsedTime -= _pauseTime;
                _pauseFrames = 0;
                _pauseTime = 0;
            }

            _elapsedFrames = (Time.FrameCount - _startFrame) * 2;
            if (_elapsedTime > 1.0f)
            {
                _currentColor = _initColor;
                Enabled = false;
            }
        }

        /// <summary>FF5-style bounce animation</summary>
        private void BounceStyle2(int i, float x, float y, float scale, float spacingWorld, Batcher batcher)
        {
            PrintChar(_chars[i], x + spacingWorld * 3f - i * spacingWorld,
                y - _bounceTable[Mathf.Clamp((int)(3 + 3 * i + _elapsedFrames / 3), 0, _bounceTable.Length - 1)] * (2 * scale),
                scale, batcher);
        }

        /// <summary>Render single character in world space</summary>
        private void PrintChar(string ch, float x, float y, float scale, Batcher batcher)
        {
            var hudFont = _cachedFont; // already validated in Render
            if (hudFont == null || string.IsNullOrEmpty(ch))
                return;
            hudFont.DrawInto(batcher, ch, new Vector2(x, y), _currentColor, 0, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0);
        }

        /// <summary>Visibility check</summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            var camBounds = camera.Bounds;
            var worldPos = Entity.Position;
            const int margin = 48;
            return !(worldPos.X < camBounds.X - margin || worldPos.X > camBounds.Right + margin ||
                     worldPos.Y < camBounds.Y - margin || worldPos.Y > camBounds.Bottom + margin);
        }
    }
}