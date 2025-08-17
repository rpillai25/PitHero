using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;
using Nez;

namespace PitHero
{
    /// <summary>
    /// Cross-backend window manager for MonoGame using Win32 APIs.
    /// Supports setting window position and always-on-top.
    /// </summary>
    public static class WindowManager
    {
        #region Win32 API Imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Constants for SetWindowPos
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        // Constants for GetWindowLong/SetWindowLong
        private const int GWL_STYLE = -16;
        private const int WS_BORDER = 0x00800000;
        private const int WS_DLGFRAME = 0x00400000;
        private const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
        #endregion

        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen.
        /// </summary>
        public static void ConfigureHorizontalStrip(Game game, bool alwaysOnTop = true)
        {
            var window = game.Window;
            IntPtr windowHandle = window.Handle;
            if (windowHandle == IntPtr.Zero)
            {
                Debug.Error("Could not get window handle.");
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

            // Make borderless by removing border styles
            int style = GetWindowLong(windowHandle, GWL_STYLE);
            style &= ~(WS_CAPTION);
            SetWindowLong(windowHandle, GWL_STYLE, style);

            // Set window position and size
            uint flags = SWP_FRAMECHANGED;
            SetWindowPos(windowHandle, IntPtr.Zero, x, y, windowWidth, windowHeight, flags);

            // Set always-on-top if requested
            if (alwaysOnTop)
            {
                SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            }

            Debug.Log($"Window configured as horizontal strip at ({x},{y}) - Always on top: {alwaysOnTop}");
        }

        /// <summary>
        /// Sets the window position (clamped to >= 0).
        /// </summary>
        public static void SetPosition(Game game, int x, int y)
        {
            IntPtr windowHandle = game.Window.Handle;
            if (windowHandle == IntPtr.Zero)
                return;

            SetWindowPos(windowHandle, IntPtr.Zero, Math.Max(0, x), Math.Max(0, y), 0, 0, SWP_NOSIZE | SWP_NOZORDER);
        }

        /// <summary>
        /// Sets/unsets always-on-top.
        /// </summary>
        public static void SetAlwaysOnTop(Game game, bool alwaysOnTop)
        {
            IntPtr windowHandle = game.Window.Handle;
            if (windowHandle == IntPtr.Zero)
                return;

            IntPtr insertAfter = alwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(windowHandle, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }
    }
}