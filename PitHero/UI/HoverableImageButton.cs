using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// An ImageButton that supports hover text display by overriding Draw to track mouse state
    /// </summary>
    public class HoverableImageButton : ImageButton
    {
        private string _hoverText;
        private bool _wasMouseOver = false;

        public HoverableImageButton(ImageButtonStyle style, string hoverText) : base(style)
        {
            _hoverText = hoverText;
        }

        /// <summary>
        /// Override Draw to track mouse hover state and show/hide text accordingly
        /// </summary>
        /// <summary>
        /// Returns the button's X position in stage (absolute) coordinates by walking the parent chain.
        /// Necessary because GetX()/GetY() return local coordinates relative to the immediate parent.
        /// </summary>
        private Microsoft.Xna.Framework.Vector2 GetStagePosition()
        {
            float absX = GetX();
            float absY = GetY();
            var p = GetParent();
            while (p != null)
            {
                absX += p.GetX();
                absY += p.GetY();
                p = p.GetParent();
            }
            return new Microsoft.Xna.Framework.Vector2(absX, absY);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Correct the hover flag before base.Draw picks a style from it. Nez only clears
            // _mouseOver from OnMouseExit, which fires solely on a mouse-move where the hit-tested
            // element changed. Any way the cursor stops being "on" the button without such a move -
            // a window opening over it, the top bar becoming untouchable, the button sliding away
            // under a stationary cursor, the element being detached and re-added - strands the flag
            // and the button comes back stuck in its Over (highlight) sprite.
            if (_mouseOver && !MouseIsInsideBounds())
                _mouseOver = _mouseDown = false;

            // Call base draw first
            base.Draw(batcher, parentAlpha);

            // Check if mouse over state changed
            bool isMouseOver = _mouseOver; // Access the protected field from Button

            if (isMouseOver && !_wasMouseOver)
            {
                // Mouse entered
                if (!string.IsNullOrEmpty(_hoverText))
                {
                    float estimatedTextWidth = EstimateTextWidth(_hoverText);
                    var stagePos = GetStagePosition();
                    float hoverX = stagePos.X + (GetWidth() * 0.5f) - (estimatedTextWidth * 0.5f);
                    float hoverY = stagePos.Y + GetHeight() + GetYPadding();
                    HoverTextManager.ShowHoverText(_hoverText, hoverX, hoverY);
                }
            }
            else if (!isMouseOver && _wasMouseOver)
            {
                // Mouse exited
                HoverTextManager.HideHoverText();
            }

            _wasMouseOver = isMouseOver;
        }

        /// <summary>
        /// True when the cursor is genuinely within this button's current stage bounds. Used to
        /// self-heal a stranded hover flag; returns true when there is no stage to measure against so
        /// the flag is left alone rather than cleared on a guess.
        /// </summary>
        private bool MouseIsInsideBounds()
        {
            var stage = GetStage();
            if (stage == null)
                return true;

            var mouse = stage.GetMousePosition();
            var pos = GetStagePosition();
            return mouse.X >= pos.X && mouse.X <= pos.X + GetWidth()
                && mouse.Y >= pos.Y && mouse.Y <= pos.Y + GetHeight();
        }

        /// <summary>
        /// Get appropriate Y padding based on current window mode
        /// </summary>
        private float GetYPadding()
        {
            try
            {
                // Use different padding amounts based on window mode to maintain consistent visual spacing
                if (WindowManager.IsHalfHeightMode())
                {
                    return 18f; // Medium padding for half mode
                }
                else
                {
                    return 10f; // Standard padding for normal mode
                }
            }
            catch
            {
                // Fallback to standard padding
                return 10f;
            }
        }

        /// <summary>
        /// Estimate text width based on current window mode and font sizes
        /// </summary>
        private float EstimateTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            try
            {
                // Use different character width estimates based on window mode
                // These are rough estimates based on the actual font sizes
                float charWidth;
                if (WindowManager.IsHalfHeightMode())
                {
                    charWidth = 8f; // Hud2x.fnt has medium characters
                }
                else
                {
                    charWidth = 4f; // HUD.fnt has normal size characters
                }

                return text.Length * charWidth;
            }
            catch
            {
                // Fallback to normal size estimation
                return text.Length * 4f;
            }
        }

        /// <summary>
        /// Update the hover text
        /// </summary>
        public void SetHoverText(string hoverText)
        {
            _hoverText = hoverText;
        }

        /// <summary>
        /// Immediately dismisses this button's hover text — useful right after a click opens a
        /// full-screen UI over the button, where the mouse never "leaves" so the normal hover-exit
        /// path in Draw never fires. Leaves <c>_wasMouseOver</c> true so the text does not instantly
        /// reappear; it shows again only after the mouse actually leaves and re-enters the button.
        /// </summary>
        public void DismissHoverText()
        {
            if (_wasMouseOver)
                HoverTextManager.HideHoverText();
        }
    }
}