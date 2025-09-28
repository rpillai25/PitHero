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
            
            // Create hover label with default skin
            var skin = Skin.CreateDefaultSkin();
            _hoverLabel = new Label("", skin);
            _hoverLabel.SetVisible(false);
            
            // Add to stage
            _stage.AddElement(_hoverLabel);
        }

        /// <summary>
        /// Show hover text at the specified position
        /// </summary>
        public static void ShowHoverText(string text, float x, float y)
        {
            if (_hoverLabel == null || string.IsNullOrEmpty(text))
                return;

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
        /// Check if hover text is currently visible
        /// </summary>
        public static bool IsVisible => _isVisible;
    }
}