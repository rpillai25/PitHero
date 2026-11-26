using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// A full-stage translucent layer placed directly under a dialog: it dims everything behind the dialog and
    /// swallows clicks, hover and scrolling meant for it, so the dialog reads as the only thing to interact with.
    /// Follows the stage size every frame. (Scene entities drawn after the UI, such as the hero creation
    /// preview, are not dimmed.)
    /// </summary>
    public class DimBackdrop : Element, IInputListener
    {
        /// <summary>The pause dim used elsewhere (free-move mode).</summary>
        public static readonly Color DefaultColor = new Color(0, 0, 0, 100);

        private readonly Color _color;

        public DimBackdrop() : this(DefaultColor) { }

        public DimBackdrop(Color color)
        {
            _color = color;
            SetTouchable(Touchable.Enabled);
        }

        /// <summary>Adds the backdrop to the stage, covering it; add the dialog afterwards so it draws on top.</summary>
        public void Show(Stage stage)
        {
            SetPosition(0f, 0f);
            SetSize(stage.GetWidth(), stage.GetHeight());
            stage.AddElement(this);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            var stage = GetStage();
            if (stage != null && (GetWidth() != stage.GetWidth() || GetHeight() != stage.GetHeight()))
                SetSize(stage.GetWidth(), stage.GetHeight());
            batcher.DrawRect(GetX(), GetY(), GetWidth(), GetHeight(), _color);
        }

        void IInputListener.OnMouseEnter() { }
        void IInputListener.OnMouseExit() { }
        void IInputListener.OnMouseMoved(Vector2 mousePos) { }
        bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;
        void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
        bool IInputListener.OnRightMousePressed(Vector2 mousePos) => true;
        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => true;
    }
}
