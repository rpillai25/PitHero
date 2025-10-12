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
                    // Calculate position with better centering using estimated text width
                    float estimatedTextWidth = EstimateTextWidth(_hoverText);
                    
                    float hoverX = GetX() + (GetWidth() * 0.5f) - (estimatedTextWidth * 0.5f); // Center text properly
                    float hoverY = GetY() + GetHeight() + GetYPadding(); // Below button with proper padding
                    
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
    }
}