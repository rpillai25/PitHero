using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
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

        // HUD fonts for different window sizes
        private static BitmapFont _hudFontNormal;
        private static BitmapFont _hudFontHalf;
        private static BitmapFont _hudFontQuarter;

        // Label styles for different window sizes
        private static LabelStyle _styleNormal;
        private static LabelStyle _styleHalf;
        private static LabelStyle _styleQuarter;

        private enum HoverMode { Normal, Half, Quarter }
        private static HoverMode _currentHoverMode = HoverMode.Normal;

        /// <summary>
        /// Initialize the hover text manager with a stage
        /// </summary>
        public static void Initialize(Stage stage)
        {
            _stage = stage;
            
            LoadFonts();
            CreateLabelStyles();
            CreateHoverLabel();
        }

        /// <summary>
        /// Load the appropriately sized fonts for each window mode
        /// </summary>
        private static void LoadFonts()
        {
            _hudFontNormal = Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt");
            _hudFontHalf = Core.Content.LoadBitmapFont("Content/Fonts/Hud2x.fnt");
            _hudFontQuarter = Core.Content.LoadBitmapFont("Content/Fonts/Hud4x.fnt");
        }

        /// <summary>
        /// Create label styles for each font size
        /// </summary>
        private static void CreateLabelStyles()
        {
            _styleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _styleHalf = new LabelStyle(_hudFontHalf, Color.White);
            _styleQuarter = new LabelStyle(_hudFontQuarter, Color.White);
        }

        /// <summary>
        /// Create the hover label with proper font
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

            // Create label with appropriate style for current window mode
            var currentStyle = GetCurrentLabelStyle();
            _hoverLabel = new Label("", currentStyle);
            _hoverLabel.SetVisible(false);
            
            // Add to stage
            _stage.AddElement(_hoverLabel);
        }

        /// <summary>
        /// Get the appropriate label style for the current window mode
        /// </summary>
        private static LabelStyle GetCurrentLabelStyle()
        {
            try
            {
                if (WindowManager.IsQuarterHeightMode())
                {
                    return _styleQuarter;
                }
                else if (WindowManager.IsHalfHeightMode())
                {
                    return _styleHalf;
                }
                else
                {
                    return _styleNormal;
                }
            }
            catch
            {
                // Fallback to normal style if WindowManager calls fail
                return _styleNormal ?? new LabelStyle(_hudFontNormal ?? Graphics.Instance.BitmapFont, Color.White);
            }
        }

        /// <summary>
        /// Get the current hover mode based on window state
        /// </summary>
        private static HoverMode GetCurrentHoverMode()
        {
            try
            {
                if (WindowManager.IsQuarterHeightMode())
                {
                    return HoverMode.Quarter;
                }
                else if (WindowManager.IsHalfHeightMode())
                {
                    return HoverMode.Half;
                }
                else
                {
                    return HoverMode.Normal;
                }
            }
            catch
            {
                return HoverMode.Normal;
            }
        }

        /// <summary>
        /// Show hover text at the specified position with appropriate font
        /// </summary>
        public static void ShowHoverText(string text, float x, float y)
        {
            if (_hoverLabel == null || string.IsNullOrEmpty(text))
                return;

            // Update label style if window mode changed
            UpdateLabelStyleIfNeeded();

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
        /// Update label style based on current window mode if it changed
        /// </summary>
        private static void UpdateLabelStyleIfNeeded()
        {
            if (_hoverLabel == null)
                return;

            var newHoverMode = GetCurrentHoverMode();
            if (newHoverMode != _currentHoverMode)
            {
                var newStyle = GetCurrentLabelStyle();
                _hoverLabel.SetStyle(newStyle);
                _hoverLabel.InvalidateHierarchy(); // Force layout update
                _currentHoverMode = newHoverMode;
            }
        }

        /// <summary>
        /// Check if hover text is currently visible
        /// </summary>
        public static bool IsVisible => _isVisible;
    }
}