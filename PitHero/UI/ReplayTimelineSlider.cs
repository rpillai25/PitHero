using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// The replay scrubber's slider: an EnhancedSlider whose track is tinted past
    /// <see cref="FutureStartValue"/>, marking the stretch of the timeline that lies beyond the recorded
    /// session end (future simulation). The knob is redrawn on top so it never reads as tinted.
    /// </summary>
    public class ReplayTimelineSlider : EnhancedSlider
    {
        /// <summary>Slider value where the future region begins; at or above Max there is no future region.</summary>
        public float FutureStartValue = float.MaxValue;

        /// <summary>Creates the slider with the skin's default style and deferred commit.</summary>
        public ReplayTimelineSlider(Skin skin, bool useDeferredCommit)
            : base(0f, 1f, 1f, false, skin, null, useDeferredCommit)
        {
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            base.Draw(batcher, parentAlpha);

            if (FutureStartValue >= Max || Max <= Min)
                return;

            var style = GetStyle();
            var bg = Disabled && style.DisabledBackground != null ? style.DisabledBackground : style.Background;
            if (bg == null)
                return;

            var knob = GetKnobDrawable();
            float knobWidth = knob != null ? knob.MinWidth : 0f;
            float knobHeight = knob != null ? knob.MinHeight : 0f;

            // Mirror ProgressBar's horizontal geometry: the knob travels width - knobWidth pixels and
            // its centre marks the value, so the future region starts at the centre for FutureStartValue
            float fraction = (FutureStartValue - Min) / (Max - Min);
            if (fraction < 0f) fraction = 0f;
            float travel = width - bg.LeftWidth - bg.RightWidth - knobWidth;
            float startX = x + bg.LeftWidth + knobWidth * 0.5f + travel * fraction;
            float endX = x + width - bg.RightWidth;
            if (endX <= startX)
                return;

            var tint = GameConfig.ReplayFutureTrackColor;
            var color = ColorExt.Create(tint, (int)(tint.A * parentAlpha));
            batcher.DrawRect(startX, y + (int)((height - bg.MinHeight) * 0.5f), endX - startX, bg.MinHeight, color);

            if (knob != null)
            {
                var knobColor = ColorExt.Create(this.color, (int)(this.color.A * parentAlpha));
                knob.Draw(batcher, (int)(x + position), (int)(y + (height - knobHeight) * 0.5f), knobWidth, knobHeight, knobColor);
            }
        }
    }
}
