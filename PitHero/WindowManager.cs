using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;

namespace PitHero
{
    /// <summary>
    /// Cross-backend window manager for FNA/Nez using SDL3.
    /// Supports setting window position and always-on-top.
    /// </summary>
    public static class WindowManager
    {
        private const int SDL_TRUE = 1;
        private const int SDL_FALSE = 0;

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowAlwaysOnTop(IntPtr window, int on);

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_GetCurrentDisplayMode(int displayIndex, out SDL_DisplayMode mode);

        [StructLayout(LayoutKind.Sequential)]
        private struct SDL_DisplayMode
        {
            public uint format;
            public int w;
            public int h;
            public int refresh_rate;
            public IntPtr driverdata;
        }

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

            //// Get display size
            //SDL_DisplayMode displayMode;
            //SDL_GetCurrentDisplayMode(0, out displayMode);

            int windowWidth = GameConfig.VirtualWidth;
            int windowHeight = GameConfig.VirtualHeight;

            // Clamp window size to display size
            windowWidth = Math.Min(windowWidth, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width);
            windowHeight = Math.Min(windowHeight, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height);

            int x = Math.Max(0, (GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width - windowWidth) / 2);
            int y = Math.Max(0, GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height - windowHeight); // Clamp to bottom

            // Ensure window is not offscreen at the bottom
            if (y + windowHeight > GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height)
                y = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height - windowHeight;
            if (y < 0)
                y = 0;

            // Set borderless via FNA/Nez API
            if (window is Microsoft.Xna.Framework.GameWindow gw)
            {
                gw.IsBorderlessEXT = true;
            }

            SDL_SetWindowPosition(sdlWindow, x, y);
            SDL_SetWindowAlwaysOnTop(sdlWindow, alwaysOnTop ? SDL_TRUE : SDL_FALSE);

            Console.WriteLine($"Window configured as horizontal strip at ({x},{y}) - Always on top: {alwaysOnTop}");
        }

        /// <summary>
        /// Sets the window position.
        /// </summary>
        public static void SetPosition(Game game, int x, int y)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;
            SDL_SetWindowPosition(sdlWindow, Math.Max(0, x), Math.Max(0, y));
        }

        /// <summary>
        /// Sets the window always-on-top state.
        /// </summary>
        public static void SetAlwaysOnTop(Game game, bool alwaysOnTop)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;
            SDL_SetWindowAlwaysOnTop(sdlWindow, alwaysOnTop ? SDL_TRUE : SDL_FALSE);
        }
    }
}