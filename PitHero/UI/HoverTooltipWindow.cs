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

        /// <summary>
        /// True when the stage cursor lies inside the element's bounds. Owners use this to clear a
        /// stale hover flag: Nez only clears its own on a mouse-exit event, which never fires for a
        /// widget hidden under the cursor (Settings closed with ESC) and re-shown later, so without
        /// this the tooltip comes back on the next draw and never goes away.
        /// </summary>
        public static bool MouseIsOver(Element element)
        {
            var stage = element?.GetStage();
            if (stage == null)
                return false;
            var mouse = stage.GetMousePosition();
            float x = element.GetX();
            float y = element.GetY();
            var p = element.GetParent();
            while (p != null)
            {
                x += p.GetX();
                y += p.GetY();
                p = p.GetParent();
            }
            return mouse.X >= x && mouse.X <= x + element.GetWidth()
                && mouse.Y >= y && mouse.Y <= y + element.GetHeight();
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
