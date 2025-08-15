using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace PitHero
{
    /// <summary>
    /// Handles window positioning and styling for the horizontal strip game
    /// </summary>
    public static class WindowManager
    {
        #region Win32 API Constants
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int SWP_NOACTIVATE = 0x0010;
        
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        
        private const uint WS_BORDER = 0x00800000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_MAXIMIZEBOX = 0x00010000;
        
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        
        // System metrics constants
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        #endregion

        #region Win32 API Imports
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        #endregion

        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen
        /// </summary>
        public static void ConfigureHorizontalStrip(Game game, bool alwaysOnTop = true, bool clickThrough = false)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Window management is only supported on Windows. Running in normal window mode.");
                return;
            }

            try
            {
                // Get the window handle
                var window = game.Window;
                IntPtr hwnd = window.Handle;

                if (hwnd == IntPtr.Zero)
                {
                    Console.WriteLine("Could not get window handle. Window management features disabled.");
                    return;
                }

                // Make the window borderless
                MakeBorderless(hwnd);

                // Position at bottom of screen
                PositionAtBottom(hwnd);

                // Set always on top if requested
                if (alwaysOnTop)
                {
                    SetAlwaysOnTop(hwnd, true);
                }

                // Set click-through if requested
                if (clickThrough)
                {
                    SetClickThrough(hwnd, true);
                }

                Console.WriteLine($"Window configured as horizontal strip - Always on top: {alwaysOnTop}, Click-through: {clickThrough}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to configure window: {ex.Message}");
                Console.WriteLine("Running in normal window mode.");
            }
        }

        /// <summary>
        /// Removes window border and title bar
        /// </summary>
        private static void MakeBorderless(IntPtr hwnd)
        {
            var style = (uint)GetWindowLong(hwnd, GWL_STYLE);
            var originalStyle = style;
            
            // Remove all window decorations
            style &= ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            
            var result = SetWindowLong(hwnd, GWL_STYLE, style);
            Console.WriteLine($"Window style changed from 0x{originalStyle:X} to 0x{style:X}, result: {result}");
            
            // Force window to redraw with new style
            SetWindowPos(hwnd, 0, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Positions the window at the bottom of the screen
        /// </summary>
        private static void PositionAtBottom(IntPtr hwnd)
        {
            // Get full screen dimensions using GetSystemMetrics
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Position window at bottom center
            int windowWidth = GameConfig.VirtualWidth;
            int windowHeight = GameConfig.VirtualHeight;
            int x = (screenWidth - windowWidth) / 2; // Center horizontally
            int y = screenHeight - windowHeight; // Bottom of screen

            Console.WriteLine($"Screen: {screenWidth}x{screenHeight}, Window: {windowWidth}x{windowHeight}, Position: ({x}, {y})");
            
            SetWindowPos(hwnd, 0, x, y, windowWidth, windowHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Sets the window to always stay on top
        /// </summary>
        public static void SetAlwaysOnTop(IntPtr hwnd, bool onTop)
        {
            int insertAfter = onTop ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Makes the window click-through (transparent to mouse events)
        /// </summary>
        public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
        {
            var exStyle = (uint)GetWindowLong(hwnd, GWL_EXSTYLE);
            if (clickThrough)
            {
                exStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        /// <summary>
        /// Updates window position when screen resolution changes
        /// </summary>
        public static void UpdatePosition(Game game)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            try
            {
                IntPtr hwnd = game.Window.Handle;
                if (hwnd != IntPtr.Zero)
                {
                    PositionAtBottom(hwnd);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update window position: {ex.Message}");
            }
        }
    }
}