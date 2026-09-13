using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// The cursor tooltip window every Hoverable* widget puts on the stage. It lives at the stage
    /// root, not inside its owner, so it must police itself: the owner's Draw (where the hover
    /// state is tracked) never runs once any ancestor is hidden — Settings closed with ESC, a tab
    /// switched away — which used to leave the tooltip stranded on screen. This window checks its
    /// owner's whole parent chain on its own Draw and hides the moment the owner is not showing.
    /// </summary>
    public class HoverTooltipWindow : Window
    {
        private readonly Element _owner;

        /// <summary>Builds the transparent tooltip window with one padded label; hidden until shown by the owner.</summary>
        public HoverTooltipWindow(Element owner, Skin skin, string text, Color fontColor)
            : base("", skin)
        {
            _owner = owner;
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);
            SetColor(GameConfig.TransparentMenu);

            var label = new Label(text, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = fontColor });
            Add(label).Pad(6f);
            Pack();
            SetVisible(false);
        }

        /// <summary>True when the element and every ancestor are visible and it sits on a stage.</summary>
        public static bool IsShowingInHierarchy(Element element)
        {
            if (element == null || element.GetStage() == null)
                return false;
            var e = element;
            while (e != null)
            {
                if (!e.IsVisible())
                    return false;
                e = e.GetParent();
            }
            return true;
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            if (!IsShowingInHierarchy(_owner))
            {
                SetVisible(false);
                return;
            }
            base.Draw(batcher, parentAlpha);
        }
    }
}
