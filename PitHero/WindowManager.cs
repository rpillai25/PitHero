using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;

namespace PitHero
{
    /// <summary>
    /// Cross-platform window manager for MonoGame/Nez using Win32 APIs.
    /// Supports setting window position and always-on-top.
    /// </summary>
    public static class WindowManager
    {
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen.
        /// </summary>
        public static void ConfigureHorizontalStrip(Game game, bool alwaysOnTop = true)
        {
            var window = game.Window;
            IntPtr windowHandle = window.Handle;
            if (windowHandle == IntPtr.Zero)
            {
                Nez.Debug.Log("Could not get window handle.");
                return;
            }

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

            // Set borderless via MonoGame API
            if (window is Microsoft.Xna.Framework.GameWindow gw)
            {
                gw.IsBorderless = true;
            }

            // Move window to position
            MoveWindow(windowHandle, x, y, windowWidth, windowHeight, true);
            
            // Set always on top
            SetWindowPos(windowHandle, alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

            Nez.Debug.Log($"Window configured as horizontal strip at ({x},{y}) - Always on top: {alwaysOnTop}");
        }

        /// <summary>
        /// Sets the window position.
        /// </summary>
        public static void SetPosition(Game game, int x, int y)
        {
            IntPtr windowHandle = game.Window.Handle;
            if (windowHandle == IntPtr.Zero)
                return;
                
            MoveWindow(windowHandle, Math.Max(0, x), Math.Max(0, y), GameConfig.VirtualWidth, GameConfig.VirtualHeight, true);
        }

        /// <summary>
        /// Sets the window always-on-top state.
        /// </summary>
        public static void SetAlwaysOnTop(Game game, bool alwaysOnTop)
        {
            IntPtr windowHandle = game.Window.Handle;
            if (windowHandle == IntPtr.Zero)
                return;
                
            SetWindowPos(windowHandle, alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
    }
}