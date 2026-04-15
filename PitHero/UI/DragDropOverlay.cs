using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>Stage-level overlay that renders the dragged item sprite at the cursor position.</summary>
    public class DragDropOverlay : Element
    {
        private SpriteDrawable _dragDrawable;
        private bool _active;
        private Vector2 _currentStagePos;
        private Color _dragTint = Color.White;
        private const float DragAlpha = 0.7f;
        private const float SlotSize = 32f;

        /// <summary>Begins rendering the dragged item sprite following the cursor.</summary>
        public void BeginDrag(SpriteDrawable drawable)
        {
            BeginDrag(drawable, Color.White);
        }

        /// <summary>Begins rendering the dragged item sprite following the cursor with a tint color.</summary>
        public void BeginDrag(SpriteDrawable drawable, Color tint)
        {
            _dragDrawable = drawable;
            _dragTint = tint;
            _active = true;
            SetVisible(true);
        }

        /// <summary>Updates the position where the dragged item is rendered.</summary>
        public void UpdatePosition(Vector2 stagePos)
        {
            _currentStagePos = stagePos;
        }

        /// <summary>Stops rendering the dragged item and hides the overlay.</summary>
        public void EndDrag()
        {
            _active = false;
            _dragDrawable = null;
            _dragTint = Color.White;
            SetVisible(false);
        }

        /// <summary>Draws the dragged item sprite centered on the current cursor position.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            if (!_active || _dragDrawable == null)
                return;
            var drawPos = new Vector2(_currentStagePos.X - SlotSize * 0.5f, _currentStagePos.Y - SlotSize * 0.5f);
            _dragDrawable.Draw(batcher, drawPos.X, drawPos.Y, SlotSize, SlotSize,
                new Color(_dragTint.R / 255f, _dragTint.G / 255f, _dragTint.B / 255f, DragAlpha));
        }
    }
}
