using PitHero.UI;

namespace PitHero.Services
{
    /// <summary>
    /// Service that provides access to the shortcut bar UI element
    /// </summary>
    public class ShortcutBarService
    {
        private ShortcutBar _shortcutBar;

        /// <summary>
        /// Gets the current shortcut bar instance
        /// </summary>
        public ShortcutBar ShortcutBar => _shortcutBar;

        /// <summary>
        /// Sets the shortcut bar instance (called during scene initialization)
        /// </summary>
        public void SetShortcutBar(ShortcutBar shortcutBar)
        {
            _shortcutBar = shortcutBar;
        }
    }
}
