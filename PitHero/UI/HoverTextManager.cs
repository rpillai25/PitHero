using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// Manages hover text display for UI elements
    /// </summary>
    public static class HoverTextManager
    {
        private static Stage _stage;
        private static Label _hoverLabel;
        private static bool _isVisible = false;

        /// <summary>
        /// Initialize the hover text manager with a stage
        /// </summary>
        public static void Initialize(Stage stage)
        {
            _stage = stage;
            
            // Create hover label with a custom style that accounts for window scaling
            CreateHoverLabel();
        }

        /// <summary>
        /// Create the hover label with proper scaling
        /// </summary>
        private static void CreateHoverLabel()
        {
            if (_stage == null) return;

            // Remove existing label if it exists
            if (_hoverLabel != null)
            {
                _hoverLabel.Remove();
                _hoverLabel = null;
            }

            // Create a new LabelStyle with proper font scaling
            float fontScale = GetFontScaleForCurrentWindowMode();
            var labelStyle = new LabelStyle
            {
                Font = Graphics.Instance.BitmapFont,
                FontColor = Color.White,
                FontScaleX = fontScale,
                FontScaleY = fontScale,
                Background = null // Transparent background
            };
            
            _hoverLabel = new Label("", labelStyle);
            _hoverLabel.SetVisible(false);
            
            // Add to stage
            _stage.AddElement(_hoverLabel);
        }

        /// <summary>
        /// Show hover text at the specified position with appropriate scaling
        /// </summary>
        public static void ShowHoverText(string text, float x, float y)
        {
            if (_hoverLabel == null || string.IsNullOrEmpty(text))
                return;

            // Update font scale in case window mode changed since initialization
            UpdateFontScale();

            _hoverLabel.SetText(text);
            _hoverLabel.SetPosition(x, y);
            _hoverLabel.SetVisible(true);
            _hoverLabel.ToFront();
            _isVisible = true;
        }

        /// <summary>
        /// Hide the hover text
        /// </summary>
        public static void HideHoverText()
        {
            if (_hoverLabel == null)
                return;

            _hoverLabel.SetVisible(false);
            _isVisible = false;
        }

        /// <summary>
        /// Update font scale based on current window mode
        /// </summary>
        private static void UpdateFontScale()
        {
            if (_hoverLabel == null)
                return;

            float newFontScale = GetFontScaleForCurrentWindowMode();
            var style = _hoverLabel.GetStyle();
            
            // Only update if scale actually changed to avoid unnecessary work
            if (System.Math.Abs(style.FontScaleX - newFontScale) > 0.001f)
            {
                style.FontScaleX = newFontScale;
                style.FontScaleY = newFontScale;
                _hoverLabel.InvalidateHierarchy(); // Force layout update
            }
        }

        /// <summary>
        /// Get the appropriate font scale for the current window mode to maintain readability
        /// </summary>
        private static float GetFontScaleForCurrentWindowMode()
        {
            try
            {
                if (WindowManager.IsQuarterHeightMode())
                {
                    return 4f; // Scale up by 4x to counteract quarter (0.25x) scaling
                }
                else if (WindowManager.IsHalfHeightMode())
                {
                    return 2f; // Scale up by 2x to counteract half (0.5x) scaling
                }
                else
                {
                    return 1f; // Normal size
                }
            }
            catch
            {
                // Fallback to normal scale if WindowManager calls fail
                return 1f;
            }
        }

        /// <summary>
        /// Check if hover text is currently visible
        /// </summary>
        public static bool IsVisible => _isVisible;
    }
}