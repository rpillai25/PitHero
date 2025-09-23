using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using PitHero.ECS.Scenes;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>Bouncy text renderer for battle messages like "Miss" (world-space, constant screen size)</summary>
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

        private string _text = string.Empty;

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
        private BitmapFont _cachedFont;

        public override float Width => 32;
        public override float Height => 32;

        /// <summary>Initialize text for display</summary>
        public void Init(string text, Color textColor)
        {
            _text = text ?? "Miss";
            _startFrame = Time.FrameCount;
            _elapsedFrames = 0;
            _elapsedTime = 0;

            _currentColor = _initColor = textColor;
        }

        /// <summary>Service fetch</summary>
        public override void OnAddedToEntity() => _pauseService = Core.Services.GetService<PauseService>();

        /// <summary>World-space render with bounce animation</summary>
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

            _cachedFont = hudFont;

            // Inverse zoom for text scaling so on-screen size is constant
            float inverseZoom = 1f / camera.RawZoom;

            // Apply bounce animation
            float bounceOffset = _bounceTable[Mathf.Clamp((int)(_elapsedFrames / 3), 0, _bounceTable.Length - 1)];

            // Render the text
            hudFont.DrawInto(batcher, _text, 
                new Vector2(worldPos.X, worldPos.Y - bounceOffset), 
                _currentColor, 0, Vector2.Zero, 
                new Vector2(inverseZoom, inverseZoom), 
                SpriteEffects.None, 0);
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