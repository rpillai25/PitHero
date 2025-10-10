using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>Lightweight overlay element that animates two sprites between start/end positions in stage space.</summary>
    public class SwapAnimationOverlay : Element
    {
        private bool _active;
        private float _elapsed;
        private float _duration;

        private SpriteDrawable _drawableA;
        private SpriteDrawable _drawableB;
        private Vector2 _startA;
        private Vector2 _endA;
        private Vector2 _startB;
        private Vector2 _endB;
        private System.Action _onCompleted;

        /// <summary>Begins a new animation tween for up to two sprites.</summary>
        public void Begin(SpriteDrawable drawableA, Vector2 startA, Vector2 endA,
                          SpriteDrawable drawableB, Vector2 startB, Vector2 endB,
                          float durationSeconds, System.Action onCompleted)
        {
            _drawableA = drawableA;
            _drawableB = drawableB;
            _startA = startA;
            _endA = endA;
            _startB = startB;
            _endB = endB;
            _duration = durationSeconds <= 0f ? 0.001f : durationSeconds;
            _elapsed = 0f;
            _onCompleted = onCompleted;
            _active = true;
            SetVisible(true);
            Invalidate();
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            base.Draw(batcher, parentAlpha);
            if (!_active)
                return;

            _elapsed += Time.DeltaTime;
            float t = _elapsed / _duration;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;

            // simple quad out easing
            float ease = 1f - (1f - t) * (1f - t);

            if (_drawableA != null)
            {
                var posA = Vector2.Lerp(_startA, _endA, ease);
                _drawableA.Draw(batcher, posA.X, posA.Y, 32f, 32f, Color.White);
            }

            if (_drawableB != null)
            {
                var posB = Vector2.Lerp(_startB, _endB, ease);
                _drawableB.Draw(batcher, posB.X, posB.Y, 32f, 32f, Color.White);
            }

            if (_elapsed >= _duration)
            {
                _active = false;
                SetVisible(false);
                var cb = _onCompleted; _onCompleted = null;
                cb?.Invoke();
            }
        }
    }
}
