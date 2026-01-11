using Microsoft.Xna.Framework;
using Nez;
using System;

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
            Half
        }

        // Track the persistent window size preference (separate from temporary UI state)
        private static WindowSizeMode _persistentWindowSize = WindowSizeMode.Normal;
        private static bool _isInitialized = false;
        private static Game _game;

        // Track how many UI windows are currently open to prevent external change detection interference
        private static int _openUIWindowCount = 0;

        // Track auto-scroll to hero setting
        private static bool _autoScrollToHeroEnabled = GameConfig.CameraAutoScrollToHeroDefault;

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
        /// Gets whether auto-scroll to hero is enabled
        /// </summary>
        public static bool AutoScrollToHeroEnabled => _autoScrollToHeroEnabled;

        /// <summary>
        /// Sets the auto-scroll to hero setting
        /// </summary>
        public static void SetAutoScrollToHero(bool enabled)
        {
            _autoScrollToHeroEnabled = enabled;
            Debug.Log($"[UIWindowManager] Auto-scroll to hero: {(_autoScrollToHeroEnabled ? "Enabled" : "Disabled")}");
        }

        /// <summary>
        /// Gets debug information about the current state
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"Initialized: {_isInitialized}, Persistent: {_persistentWindowSize}, Current: {GetCurrentWindowSize()}";
        }

        /// <summary>
        /// Updates the persistent window size based on current window manager state
        /// /// </summary>
        public static void UpdatePersistentWindowSize()
        {
            _persistentWindowSize = GetCurrentWindowSize();
            Debug.Log($"[UIWindowManager] Updated persistent window size: {_persistentWindowSize}");
        }

        /// <summary>
        /// Updates persistent window size if it changed externally (e.g., via Shift+Mouse Wheel)
        /// Only updates when no UI windows are open to prevent interference with temporary sizing
        /// </summary>
        public static void UpdatePersistentWindowSizeIfChanged()
        {
            if (!_isInitialized)
            {
                // Attempt fallback initialization
                if (Core.Instance != null)
                {
                    Initialize(Core.Instance);
                }
                else
                {
                    return; // Can't initialize, skip update
                }
            }

            // Don't update persistent size while any UI windows are open
            // This prevents temporary window restores from overwriting the persistent preference
            if (_openUIWindowCount > 0)
            {
                return;
            }

            WindowSizeMode currentActualSize = GetCurrentWindowSize();

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
                Debug.Warn("[UIWindowManager] Not initialized, attempting to initialize with Core.Game");
                if (Core.Instance != null)
                {
                    Initialize(Core.Instance);
                }
                else
                {
                    Debug.Error("[UIWindowManager] Cannot initialize - Core.Instance is null");
                    return;
                }
            }

            // Increment open window counter
            _openUIWindowCount++;
            Debug.Log($"[UIWindowManager] UI window opening (count: {_openUIWindowCount})");

            // Always store the current window state as persistent BEFORE any changes
            var currentActualSize = GetCurrentWindowSize();
            _persistentWindowSize = currentActualSize;
            Debug.Log($"[UIWindowManager] Stored persistent size: {_persistentWindowSize}");

            // Temporarily restore to normal size for UI viewing
            if (WindowManager.IsHalfHeightMode())
            {
                WindowManager.RestoreOriginalSize(_game);
                Debug.Log("[UIWindowManager] Temporarily restored to normal size for UI viewing");
            }
            else
            {
                Debug.Log("[UIWindowManager] Window already at normal size, no temporary restore needed");
            }
        }

        /// <summary>
        /// Gets the current window size mode
        /// </summary>
        private static WindowSizeMode GetCurrentWindowSize()
        {
            if (WindowManager.IsHalfHeightMode())
            {
                return WindowSizeMode.Half;
            }
            else
            {
                return WindowSizeMode.Normal;
            }
        }

        /// <summary>
        /// Called when closing a UI window - applies the persistent window size
        /// </summary>
        public static void OnUIWindowClosing()
        {
            if (!_isInitialized)
            {
                Debug.Warn("[UIWindowManager] Not initialized, attempting to initialize with Core.Game");
                if (Core.Instance != null)
                {
                    Initialize(Core.Instance);
                }
                else
                {
                    Debug.Error("[UIWindowManager] Cannot initialize - Core.Instance is null");
                    return;
                }
            }

            // Decrement open window counter
            _openUIWindowCount = Math.Max(0, _openUIWindowCount - 1);
            Debug.Log($"[UIWindowManager] UI window closing (count: {_openUIWindowCount})");
            Debug.Log($"[UIWindowManager] Applying persistent size: {_persistentWindowSize}");

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

            Debug.Log($"[UIWindowManager] Applying persistent window size: {_persistentWindowSize}");
            Debug.Log($"[UIWindowManager] Current window state - Half: {WindowManager.IsHalfHeightMode()}");

            switch (_persistentWindowSize)
            {
                case WindowSizeMode.Normal:
                    Debug.Log("[UIWindowManager] Applying Normal size");
                    // Restore to original size if currently shrunk
                    if (WindowManager.IsHalfHeightMode())
                    {
                        WindowManager.RestoreOriginalSize(_game);
                        Debug.Log("[UIWindowManager] Restored to original size");
                    }
                    else
                    {
                        Debug.Log("[UIWindowManager] Already at normal size");
                    }
                    break;

                case WindowSizeMode.Half:
                    Debug.Log("[UIWindowManager] Applying Half size");
                    if (!WindowManager.IsHalfHeightMode())
                    {
                        WindowManager.ShrinkToNextLevel(_game); // Normal -> Half
                        Debug.Log("[UIWindowManager] Shrunk from Normal to Half");
                    }
                    else
                    {
                        Debug.Log("[UIWindowManager] Already at Half size");
                    }
                    break;
            }

            Debug.Log($"[UIWindowManager] Applied persistent window size: {_persistentWindowSize}");
            Debug.Log($"[UIWindowManager] Final window state - Half: {WindowManager.IsHalfHeightMode()}");
        }
    }
}