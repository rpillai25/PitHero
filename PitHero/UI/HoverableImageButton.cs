using Microsoft.Xna.Framework;
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
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Call base draw first
            base.Draw(batcher, parentAlpha);

            // Check if mouse over state changed
            bool isMouseOver = _mouseOver; // Access the protected field from Button
            
            if (isMouseOver && !_wasMouseOver)
            {
                // Mouse entered
                if (!string.IsNullOrEmpty(_hoverText))
                {
                    // Calculate position with better centering that accounts for text scaling
                    float textScale = GetCurrentTextScale();
                    float estimatedTextWidth = _hoverText.Length * 6f * textScale; // Rough character width estimation with scaling
                    
                    float hoverX = GetX() + (GetWidth() * 0.5f) - (estimatedTextWidth * 0.5f); // Center text properly
                    float hoverY = GetY() + GetHeight() + 5f; // Below button with padding
                    
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
        /// Get the current text scale factor based on window mode
        /// </summary>
        private float GetCurrentTextScale()
        {
            if (WindowManager.IsQuarterHeightMode())
            {
                return 4f; // Text scaled up 4x for quarter window
            }
            else if (WindowManager.IsHalfHeightMode())
            {
                return 2f; // Text scaled up 2x for half window
            }
            else
            {
                return 1f; // Normal size
            }
        }

        /// <summary>
        /// Update the hover text
        /// </summary>
        public void SetHoverText(string hoverText)
        {
            _hoverText = hoverText;
        }
    }
}