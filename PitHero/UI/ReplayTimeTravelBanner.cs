using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// "Time Travelling..." banner shown in the middle of the UI stage while a Time Travel rebuild
    /// runs behind the frozen recorded frame (issue #432). The rebuild re-simulates the session and
    /// can take minutes on a long one; the frozen frame does not move and the scrubber's percentage
    /// changes slowly, so without this the screen reads as a hang. The label waves on the wall clock
    /// (<see cref="SineWaveLabel"/>), which keeps moving because the presentation pass runs every
    /// rendered frame even while the seek monopolises the simulation. Presentation-only: it reads
    /// playback state and never touches the simulation.
    /// </summary>
    public class ReplayTimeTravelBanner : Table
    {
        private readonly SineWaveLabel _label;

        public ReplayTimeTravelBanner(Skin skin)
        {
            var baseStyle = skin.Get<LabelStyle>("ph-default");
            // Its own style: a shared style's scale must not change under every other label
            var style = new LabelStyle(baseStyle.Font, baseStyle.FontColor)
            {
                FontScaleX = GameConfig.ReplayTimeTravelBannerFontScale,
                FontScaleY = GameConfig.ReplayTimeTravelBannerFontScale
            };
            var text = Core.Services?.GetService<TextService>()?.DisplayText(TextType.UI, UITextKey.ReplayTimeTravelling)
                       ?? UITextKey.ReplayTimeTravelling;
            _label = new SineWaveLabel(text, style);

            SetBackground(skin.Get<WindowStyle>("ph-default").Background);
            // The wave bobs a few pixels either way: pad so the glyphs never leave the backdrop
            Pad(GameConfig.ReplayTimeTravelBannerPad);
            Add(_label);
            Pack();
            SetTouchable(Touchable.Disabled);
            SetVisible(false);
        }

        /// <summary>Per-frame presentation update: shows the banner centred on the stage while a Time Travel rebuild runs.</summary>
        public void Update(bool timeTravelling)
        {
            if (IsVisible() != timeTravelling)
            {
                SetVisible(timeTravelling);
                if (timeTravelling)
                    ToFront();
            }
            if (!timeTravelling)
                return;
            Reposition();
        }

        /// <summary>Centres the banner on its stage (cheap enough to run every frame, so a resize needs no hook).</summary>
        public void Reposition()
        {
            var stage = GetStage();
            if (stage == null)
                return;
            SetPosition((stage.GetWidth() - GetWidth()) / 2f, (stage.GetHeight() - GetHeight()) / 2f);
        }
    }
}
