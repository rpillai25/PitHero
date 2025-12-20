using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.ECS.Scenes;
using PitHero.Services;
using PitHero.Util;
using System.Collections;


namespace PitHero.ECS.Components
{
    public class RisingTextComponent : RenderableComponent, IUpdatable
    {
        float _elapsedTime;
        float _pauseTime;
        bool _pausedLastFrame = false;
        Vector2 _textPosition;
        bool _trackEntity = false;
        float _yOffset;
        string _text;
        Color _currentColor;
        ICoroutine _fader;

        private PauseService _pauseService;

        //Color guide:
        //Pink = exp
        //Orchid = poison

        public RisingTextComponent(string text, Color color, bool trackEntity = false)
        {
            _text = text;
            _currentColor = color;
            _trackEntity = trackEntity;
        }

        public override void OnAddedToEntity()
        {
            _pauseService = Core.Services.GetService<PauseService>();
            _textPosition = Entity.Transform.Position;
            _fader = Core.StartCoroutine(Fader());
        }


        public override bool IsVisibleFromCamera(Camera camera)
        {
            return true;
        }

        public override void DebugRender(Batcher batcher)
        {
            //Do nothing
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            PrintText(_text, _textPosition.X * 2, _textPosition.Y * 2 + _yOffset, _currentColor, batcher);
        }

        public void Update()
        {
            _elapsedTime += Time.DeltaTime;

            if (_pauseService?.IsPaused == true)
            {
                _pausedLastFrame = true;
                _pauseTime += Time.DeltaTime;
                return;
            }

            if (_pausedLastFrame)
            {
                _pausedLastFrame = false;
                _elapsedTime -= _pauseTime;
                _pauseTime = 0;
            }

            if (_trackEntity)
            {
                _textPosition = Entity.Transform.Position;
            }

            _yOffset -= 5 * _elapsedTime;  //Rise!

            if (_elapsedTime > 1.5f)
            {
                Kill();
            }
        }

        private void PrintText(string text, float x, float y, Color color, Batcher batcher)
        {
            var hudFont = ((MainGameScene)Entity.Scene).GetHudFontForCurrentMode();
            hudFont?.DrawInto(batcher, text, new Vector2(x, y),
                color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        }

        private IEnumerator Fader()
        {
            for (int i = 0; i < 10; i++)
            {
                while (_pauseService?.IsPaused == true)
                {
                    yield return 1;
                }
                yield return Coroutine.WaitForSeconds(0.1f);
                ColorUtil.SubtractRed(ref _currentColor, (byte)(i << 3));
                ColorUtil.SubtractGreen(ref _currentColor, (byte)(i << 3));
                ColorUtil.SubtractBlue(ref _currentColor, (byte)(i << 3));
                ColorUtil.SubtractAlpha(ref _currentColor, (byte)(i << 3));
            }
        }

        private void Kill()
        {
            _fader.Stop();
            this.Enabled = false;
            Entity.RemoveAllComponents();
            Entity.Destroy();
        }
    }
}
