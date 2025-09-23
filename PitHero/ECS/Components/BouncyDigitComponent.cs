using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.ECS.Scenes;
using PitHero.Services;

namespace PitHero.ECS.Components
{
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

		// Pre-created digit strings to avoid ToString() allocations
		private static readonly string[] DigitStrings = {
			"0","1","2","3","4","5","6","7","8","9"
		};

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

		/// <summary>Initialize the bouncing digit set for a value (0-9999) without allocations.</summary>
		public void Init(int value, Color digitColor, bool critical = false)
		{
			_startFrame = Time.FrameCount;
			_elapsedFrames = 0;
			_elapsedTime = 0;

			// Preserve original positioning logic:
			// value >= 1000: a b c d => indices 3,2,1,0
			// 100-999:       a b c    => 3,2,1; 0 empty
			// 10-99:         a b      => 3,2; 1,0 empty
			// 0-9:           a        => 3; others empty
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

		public override void OnAddedToEntity()
		{
			_pauseService = Core.Services.GetService<PauseService>();
		}

		public override void Render(Batcher batcher, Camera camera)
		{
			for (int i = 0; i < 4; i++)
				BounceStyle2(i, Entity.Position.X, Entity.Position.Y, batcher);
		}

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

		/// <summary>FF4-style bounce animation</summary>
		private void BounceStyle1(int i, float x, float y, Batcher batcher)
		{
			PrintDigit(_digits[i], x + 18 - i * 6,
				y - _digitTable[Mathf.Clamp((int)(5 + 3 * i + _elapsedFrames / 3), 0, _digitTable.Length - 1)],
				batcher);
		}

		/// <summary>FF5-style bounce animation</summary>
		private void BounceStyle2(int i, float x, float y, Batcher batcher)
		{
			PrintDigit(_digits[i], x + 18 - i * 6,
				y - _digitTable[Mathf.Clamp((int)(3 + 3 * i + _elapsedFrames / 3), 0, _digitTable.Length - 1)],
				batcher);
		}

		/// <summary>Render a single digit</summary>
		private void PrintDigit(string digit, float x, float y, Batcher batcher)
		{
			var hudFont = ((MainGameScene)Entity.Scene).GetHudFontForCurrentMode();
			hudFont?.DrawInto(batcher, digit, new Vector2(x, y),
				_currentColor, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
		}

		public override bool IsVisibleFromCamera(Camera camera) => true;
	}
}
