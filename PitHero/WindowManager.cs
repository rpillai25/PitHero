using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using SDL3;

namespace PitHero
{
    /// <summary>
    /// Cross-backend window manager for FNA/Nez using SDL3.
    /// Supports setting window position and always-on-top.
    /// </summary>
    public static class WindowManager
    {
        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen.
        /// </summary>
        public static void ConfigureHorizontalStrip(Game game, bool alwaysOnTop = true)
        {
            var window = game.Window;
            IntPtr sdlWindow = window.Handle;
            if (sdlWindow == IntPtr.Zero)
            {
                Console.WriteLine("Could not get SDL window handle.");
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

            Console.WriteLine($"Window configured as horizontal strip at ({x},{y}) - Always on top: {alwaysOnTop}");
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
    }
}