using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// The replay scrubber's slider: an EnhancedSlider over the recorded session, tick 0 to the session
    /// end. Deferred commit so a drag previews the target time without seeking every frame.
    /// </summary>
    public class ReplayTimelineSlider : EnhancedSlider
    {
        /// <summary>Creates the slider with the skin's default style and deferred commit.</summary>
        public ReplayTimelineSlider(Skin skin, bool useDeferredCommit)
            : base(0f, 1f, 1f, false, skin, null, useDeferredCommit)
        {
        }
    }
}
