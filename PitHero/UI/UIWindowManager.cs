using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.UI
{
    /// <summary>
    /// Centralized manager for UI window behavior, including temporary window sizing
    /// </summary>
    public static class UIWindowManager
    {
        // Window size modes
        public enum WindowSizeMode
        {
            Normal,
            Half,
            Quarter
        }
        
        // Track the persistent window size preference (separate from temporary UI state)
        private static WindowSizeMode _persistentWindowSize = WindowSizeMode.Normal;
        private static bool _isInitialized = false;
        private static Game _game;

        /// <summary>
        /// Initialize the UI window manager with the game instance
        /// </summary>
        public static void Initialize(Game game)
        {
            _game = game;
            UpdatePersistentWindowSize();
            _isInitialized = true;
        }

        /// <summary>
        /// Gets the current persistent window size
        /// </summary>
        public static WindowSizeMode PersistentWindowSize => _persistentWindowSize;

        /// <summary>
        /// Updates the persistent window size based on current window manager state
        /// </summary>
        public static void UpdatePersistentWindowSize()
        {
            if (WindowManager.IsQuarterHeightMode())
            {
                _persistentWindowSize = WindowSizeMode.Quarter;
            }
            else if (WindowManager.IsHalfHeightMode())
            {
                _persistentWindowSize = WindowSizeMode.Half;
            }
            else
            {
                _persistentWindowSize = WindowSizeMode.Normal;
            }
            
            Debug.Log($"[UIWindowManager] Updated persistent window size: {_persistentWindowSize}");
        }

        /// <summary>
        /// Updates persistent window size if it changed externally (e.g., via Shift+Mouse Wheel)
        /// </summary>
        public static void UpdatePersistentWindowSizeIfChanged()
        {
            if (!_isInitialized) return;

            WindowSizeMode currentActualSize;
            if (WindowManager.IsQuarterHeightMode())
            {
                currentActualSize = WindowSizeMode.Quarter;
            }
            else if (WindowManager.IsHalfHeightMode())
            {
                currentActualSize = WindowSizeMode.Half;
            }
            else
            {
                currentActualSize = WindowSizeMode.Normal;
            }

            if (currentActualSize != _persistentWindowSize)
            {
                _persistentWindowSize = currentActualSize;
                Debug.Log($"[UIWindowManager] Updated persistent window size due to external change: {_persistentWindowSize}");
            }
        }

        /// <summary>
        /// Sets the persistent window size preference
        /// </summary>
        public static void SetPersistentWindowSize(WindowSizeMode size)
        {
            _persistentWindowSize = size;
            Debug.Log($"[UIWindowManager] Set persistent window size: {_persistentWindowSize}");
        }

        /// <summary>
        /// Called when opening a UI window - stores current state and temporarily restores to normal size
        /// </summary>
        public static void OnUIWindowOpening()
        {
            if (!_isInitialized)
            {
                Debug.Warn("[UIWindowManager] Not initialized, cannot handle window opening");
                return;
            }

            // Store the current window state as persistent before temporarily changing it
            UpdatePersistentWindowSize();
            
            // Temporarily restore to normal size for UI viewing
            if (WindowManager.IsHalfHeightMode() || WindowManager.IsQuarterHeightMode())
            {
                WindowManager.RestoreOriginalSize(_game);
                Debug.Log("[UIWindowManager] Temporarily restored to normal size for UI viewing");
            }
        }

        /// <summary>
        /// Called when closing a UI window - applies the persistent window size
        /// </summary>
        public static void OnUIWindowClosing()
        {
            if (!_isInitialized)
            {
                Debug.Warn("[UIWindowManager] Not initialized, cannot handle window closing");
                return;
            }

            // Apply persistent window size when closing UI
            ApplyPersistentWindowSize();
        }

        /// <summary>
        /// Apply the persistent window size
        /// </summary>
        public static void ApplyPersistentWindowSize()
        {
            if (!_isInitialized)
            {
                Debug.Warn("[UIWindowManager] Not initialized, cannot apply persistent window size");
                return;
            }

            switch (_persistentWindowSize)
            {
                case WindowSizeMode.Normal:
                    // Restore to original size if currently shrunk
                    if (WindowManager.IsHalfHeightMode() || WindowManager.IsQuarterHeightMode())
                    {
                        WindowManager.RestoreOriginalSize(_game);
                    }
                    break;
                    
                case WindowSizeMode.Half:
                    // First restore to normal if at quarter, then shrink to half
                    if (WindowManager.IsQuarterHeightMode())
                    {
                        WindowManager.RestoreOriginalSize(_game);
                    }
                    if (!WindowManager.IsHalfHeightMode())
                    {
                        WindowManager.ShrinkToNextLevel(_game); // Normal -> Half
                    }
                    break;
                    
                case WindowSizeMode.Quarter:
                    // Shrink to quarter (this handles all transitions)
                    if (!WindowManager.IsQuarterHeightMode())
                    {
                        if (!WindowManager.IsHalfHeightMode())
                        {
                            WindowManager.ShrinkToNextLevel(_game); // Normal -> Half
                        }
                        WindowManager.ShrinkToNextLevel(_game); // Half -> Quarter
                    }
                    break;
            }
            
            Debug.Log($"[UIWindowManager] Applied persistent window size: {_persistentWindowSize}");
        }
    }
}