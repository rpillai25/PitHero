using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using SDL3;
using Nez;

namespace PitHero
{
    /// <summary>
    /// Cross-backend window manager for FNA/Nez using SDL3.
    /// Supports setting window position and always-on-top.
    /// </summary>
    public static class WindowManager
    {
        private static int _currentAdapterIndex = 0;
        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen.
        /// </summary>
        public static void ConfigureHorizontalStrip(Game game, bool alwaysOnTop = true)
        {
            var window = game.Window;
            IntPtr sdlWindow = window.Handle;
            if (sdlWindow == IntPtr.Zero)
            {
                Debug.Log("Could not get SDL window handle.");
                return;
            }

            int windowWidth = GameConfig.VirtualWidth;
            int windowHeight = GameConfig.VirtualHeight;

            // Clamp to current display
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            windowWidth = Math.Min(windowWidth, displayMode.Width);
            windowHeight = Math.Min(windowHeight, displayMode.Height);

            int x = Math.Max(0, (displayMode.Width - windowWidth) / 2);
            int y = Math.Max(0, displayMode.Height - windowHeight);

            if (y + windowHeight > displayMode.Height)
                y = displayMode.Height - windowHeight;
            if (y < 0)
                y = 0;

            // Borderless
            if (window is Microsoft.Xna.Framework.GameWindow gw)
                gw.IsBorderlessEXT = true;

            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
            SDL.SDL_SetWindowAlwaysOnTop(sdlWindow, alwaysOnTop ? true : false);

            Debug.Log($"Window configured as horizontal strip at ({x},{y}) - Always on top: {alwaysOnTop}");
        }

        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen,
        /// taking up 1/3 of the screen height.
        /// </summary>
        public static void ConfigureHorizontalStripOneThird(Game game, bool alwaysOnTop = true)
        {
            var window = game.Window;
            IntPtr sdlWindow = window.Handle;
            if (sdlWindow == IntPtr.Zero)
            {
                Debug.Log("Could not get SDL window handle.");
                return;
            }

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int displayWidth = displayMode.Width;
            int displayHeight = displayMode.Height;

            int windowWidth = displayWidth;
            int windowHeight = (int)(displayHeight / 3);

            int x = 0;
            int y = displayHeight - windowHeight;

            if (window is Microsoft.Xna.Framework.GameWindow gw)
                gw.IsBorderlessEXT = true;

            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);
            SDL.SDL_SetWindowAlwaysOnTop(sdlWindow, alwaysOnTop);

            Debug.Log($"Window configured as bottom docked strip {windowWidth}x{windowHeight} at ({x},{y}) - Always on top: {alwaysOnTop}");
        }

        /// <summary>
        /// Sets the window position (clamped to >= 0).
        /// </summary>
        public static void SetPosition(Game game, int x, int y)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            SDL.SDL_SetWindowPosition(sdlWindow, Math.Max(0, x), Math.Max(0, y));
        }

        /// <summary>
        /// Sets/unsets always-on-top.
        /// </summary>
        public static void SetAlwaysOnTop(Game game, bool alwaysOnTop)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            SDL.SDL_SetWindowAlwaysOnTop(sdlWindow, alwaysOnTop ? true : false);
        }

        /// <summary>
        /// Docks the window to the top of the screen with optional Y offset for fine-tuning.
        /// </summary>
        public static void DockTop(Game game, int yOffset = 0)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int windowWidth = displayMode.Width;
            int windowHeight = (int)(displayMode.Height / 3);

            int x = 0;
            // Clamp Y to ensure window stays onscreen (minimum 0, maximum leaves some window visible)
            int y = Math.Max(0, Math.Min(yOffset, displayMode.Height - 100)); // Keep at least 100px visible

            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);

            Debug.Log($"Window docked to top at ({x},{y}) with size {windowWidth}x{windowHeight}");
        }

        /// <summary>
        /// Docks the window to the bottom of the screen with optional Y offset for fine-tuning.
        /// </summary>
        public static void DockBottom(Game game, int yOffset = 0)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int windowWidth = displayMode.Width;
            int windowHeight = (int)(displayMode.Height / 3);

            int x = 0;
            // Clamp Y to ensure window stays onscreen 
            int baseY = displayMode.Height - windowHeight;
            int y = Math.Max(100, Math.Min(baseY + yOffset, displayMode.Height - 100)); // Keep at least 100px visible

            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);

            Debug.Log($"Window docked to bottom at ({x},{y}) with size {windowWidth}x{windowHeight}");
        }

        /// <summary>
        /// Centers the window on the screen with optional Y offset for fine-tuning.
        /// </summary>
        public static void DockCenter(Game game, int yOffset = 0)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int windowWidth = displayMode.Width;
            int windowHeight = (int)(displayMode.Height / 3);

            int x = 0; // Keep full width
            // Center the window vertically with optional offset
            int centerY = (displayMode.Height - windowHeight) / 2;
            int y = Math.Max(100, Math.Min(centerY + yOffset, displayMode.Height - 100)); // Keep at least 100px visible

            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);

            Debug.Log($"Window centered at ({x},{y}) with size {windowWidth}x{windowHeight}");
        }

        /// <summary>
        /// Swaps the window to the next available monitor/display adapter.
        /// Maintains the same docking position relative to the new monitor.
        /// </summary>
        public static void SwapToNextMonitor(Game game)
        {
            var adapters = GraphicsAdapter.Adapters;
            if (adapters.Count <= 1)
            {
                Debug.Log("Only one display adapter available, cannot swap monitors");
                return;
            }

            // Move to next adapter
            _currentAdapterIndex = (_currentAdapterIndex + 1) % adapters.Count;
            var targetAdapter = adapters[_currentAdapterIndex];
            
            Debug.Log($"Swapping to monitor {_currentAdapterIndex + 1} of {adapters.Count}");

            // Get current window position to determine docking mode
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            // Note: We can't easily get current window position from SDL3 in FNA,
            // so we'll use the current adapter's display to re-dock in the same position
            var currentDisplayMode = targetAdapter.CurrentDisplayMode;
            int windowWidth = currentDisplayMode.Width;
            int windowHeight = (int)(currentDisplayMode.Height / 3);

            // For simplicity, dock to bottom of new monitor (same as default behavior)
            int x = 0;
            int y = currentDisplayMode.Height - windowHeight;

            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);

            Debug.Log($"Window moved to monitor {_currentAdapterIndex + 1} at ({x},{y}) with size {windowWidth}x{windowHeight}");
        }
    }
}