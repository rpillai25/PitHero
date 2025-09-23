using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts; // Added for BitmapFont
using PitHero.ECS.Scenes;
using PitHero.Services;

namespace PitHero.ECS.Components
{
	/// <summary>Bouncy combat digit renderer (world-space, constant screen size)</summary>
	internal class BouncyDigitComponent : RenderableComponent, IUpdatable
	{
		int[] _digitTable ={
				0,0,0,0,0,0,3,6,9,12,
				14,15,15,16,16,16,15,15,14,12,
				9,6,3,0,2,4,4,5,5,5,
				4,4,2,1,0,0,0,0,0,0,
				0,0,0,0,0,0,0,0,0,0,
				0,0,0,0,0,0,0,0,0,0,
				0,0,0,0};

		// up to thousands place (index 3 = leftmost, 0 = rightmost)
		string[] _digits = new string[4];

		private static readonly string[] DigitStrings = { "0","1","2","3","4","5","6","7","8","9" };

		uint _elapsedFrames;
		uint _startFrame;
		float _elapsedTime;

		uint _pauseStartDelta;
		uint _pauseFrames;
		float _pauseTime;
		bool _pausedLastFrame = false;

		public static Color HeroDigitColor = Color.Red;
		public static Color EnemyDigitColor = Color.White;
		private Color _critColor = new Color(255, 213, 16);
		private Color _initColor = Color.White;
		private Color _currentColor = Color.White;

		private PauseService _pauseService;

		// Digit spacing cached per HUD font instance (screen-space pixel width of one glyph)
		private float _digitSpacing = 6f;
		private BitmapFont _cachedFont; // last font used to compute spacing

		public override float Width => 32;
		public override float Height => 32;

		/// <summary>Initialize digits for value (0-9999)</summary>
		public void Init(int value, Color digitColor, bool critical = false)
		{
			_startFrame = Time.FrameCount;
			_elapsedFrames = 0;
			_elapsedTime = 0;

			if (value >= 1000)
			{
				_digits[3] = DigitStrings[(value / 1000) % 10];
				_digits[2] = DigitStrings[(value / 100) % 10];
				_digits[1] = DigitStrings[(value / 10) % 10];
				_digits[0] = DigitStrings[value % 10];
			}
			else if (value >= 100)
			{
				_digits[3] = DigitStrings[(value / 100) % 10];
				_digits[2] = DigitStrings[(value / 10) % 10];
				_digits[1] = DigitStrings[value % 10];
				_digits[0] = string.Empty;
			}
			else if (value >= 10)
			{
				_digits[3] = DigitStrings[(value / 10) % 10];
				_digits[2] = DigitStrings[value % 10];
				_digits[1] = string.Empty;
				_digits[0] = string.Empty;
			}
			else
			{
				_digits[3] = DigitStrings[value % 10];
				_digits[2] = string.Empty;
				_digits[1] = string.Empty;
				_digits[0] = string.Empty;
			}

			_currentColor = _initColor = digitColor;
			if (critical)
			{
				_currentColor = _critColor;
				_initColor = digitColor;
			}
		}

		/// <summary>Service fetch</summary>
		public override void OnAddedToEntity() => _pauseService = Core.Services.GetService<PauseService>();

		/// <summary>World-space render with cached spacing and inverse zoom scaling.</summary>
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
				var measure = hudFont.MeasureString("0");
				_digitSpacing = measure.X > 0 ? measure.X : 6f; // screen-space pixels between digits at default scale
				_cachedFont = hudFont;
			}

			// Inverse zoom for glyph scaling so on-screen size is constant
			float inverseZoom = 1f / camera.RawZoom;
			// Convert desired screen-space spacing into world-space so after camera zoom it stays constant
			float spacingWorld = _digitSpacing * inverseZoom; // because position is scaled by camera.RawZoom

			for (int i = 0; i < 4; i++)
			{
				if (string.IsNullOrEmpty(_digits[i]))
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

		/// <summary>FF4-style bounce (unused)</summary>
		private void BounceStyle1(int i, float x, float y, float scale, float spacingWorld, Batcher batcher)
		{
			PrintDigit(_digits[i], x + spacingWorld * 3f - i * spacingWorld,
				y - _digitTable[Mathf.Clamp((int)(5 + 3 * i + _elapsedFrames / 3), 0, _digitTable.Length - 1)],
				scale, batcher);
		}

		/// <summary>FF5-style bounce animation</summary>
		private void BounceStyle2(int i, float x, float y, float scale, float spacingWorld, Batcher batcher)
		{
			PrintDigit(_digits[i], x + spacingWorld * 3f - i * spacingWorld,
				y - _digitTable[Mathf.Clamp((int)(3 + 3 * i + _elapsedFrames / 3), 0, _digitTable.Length - 1)],
				scale, batcher);
		}

		/// <summary>Render single digit in world space</summary>
		private void PrintDigit(string digit, float x, float y, float scale, Batcher batcher)
		{
			var hudFont = _cachedFont; // already validated
			if (hudFont == null)
				return;
			hudFont.DrawInto(batcher, digit, new Vector2(x, y), _currentColor, 0, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0);
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
