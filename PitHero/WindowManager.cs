using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PitHero
{
    /// <summary>
    /// Cross-backend window manager for FNA/Nez using SDL3.
    /// Supports setting window position, always-on-top, and selective click-through regions.
    /// </summary>
    public static class WindowManager
    {
        private const int SDL_TRUE = 1;
        private const int SDL_FALSE = 0;

        // SDL3 HitTest constants
        private const int SDL_HITTEST_NORMAL = 0;
        private const int SDL_HITTEST_DRAGGABLE = 1;
        private const int SDL_HITTEST_RESIZE_TOPLEFT = 2;
        private const int SDL_HITTEST_RESIZE_TOP = 3;
        private const int SDL_HITTEST_RESIZE_TOPRIGHT = 4;
        private const int SDL_HITTEST_RESIZE_RIGHT = 5;
        private const int SDL_HITTEST_RESIZE_BOTTOMRIGHT = 6;
        private const int SDL_HITTEST_RESIZE_BOTTOM = 7;
        private const int SDL_HITTEST_RESIZE_BOTTOMLEFT = 8;
        private const int SDL_HITTEST_RESIZE_LEFT = 9;

        // HitTest callback delegate
        private delegate int SDL_HitTestCallback(IntPtr win, ref SDL_Point area, IntPtr data);

        // Static list of clickable regions
        private static readonly List<Rectangle> _clickableRegions = new List<Rectangle>();
        private static SDL_HitTestCallback _hitTestCallback;

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_SetWindowAlwaysOnTop(IntPtr window, int on);

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_GetCurrentDisplayMode(int displayIndex, out SDL_DisplayMode mode);

        [DllImport("SDL3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int SDL_SetWindowHitTest(IntPtr window, SDL_HitTestCallback callback, IntPtr callbackData);

        [StructLayout(LayoutKind.Sequential)]
        private struct SDL_Point
        {
            public int x;
            public int y;
        }

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
        /// Hit test callback that determines if a point is clickable or click-through
        /// </summary>
        private static int HitTestCallback(IntPtr win, ref SDL_Point area, IntPtr data)
        {
            // Check if point is within any clickable region
            foreach (var region in _clickableRegions)
            {
                if (area.x >= region.X && area.x < region.X + region.Width &&
                    area.y >= region.Y && area.y < region.Y + region.Height)
                {
                    return SDL_HITTEST_NORMAL; // Clickable
                }
            }
            
            // If not in any clickable region, it's click-through
            return -1; // Click-through (transparent to mouse events)
        }

        /// <summary>
        /// Adds a clickable region to the window
        /// </summary>
        public static void AddClickableRegion(Rectangle region)
        {
            if (!_clickableRegions.Contains(region))
            {
                _clickableRegions.Add(region);
                Console.WriteLine($"Added clickable region: {region}");
            }
        }

        /// <summary>
        /// Removes a clickable region from the window
        /// </summary>
        public static void RemoveClickableRegion(Rectangle region)
        {
            if (_clickableRegions.Remove(region))
            {
                Console.WriteLine($"Removed clickable region: {region}");
            }
        }

        /// <summary>
        /// Clears all clickable regions
        /// </summary>
        public static void ClearClickableRegions()
        {
            _clickableRegions.Clear();
            Console.WriteLine("Cleared all clickable regions");
        }

        /// <summary>
        /// Sets up the default clickable region (bottom playable area)
        /// </summary>
        public static void SetupDefaultClickableRegions()
        {
            ClearClickableRegions();
            AddClickableRegion(new Rectangle(
                GameConfig.PlayableAreaX,
                GameConfig.PlayableAreaY,
                GameConfig.PlayableAreaWidth,
                GameConfig.PlayableAreaHeight));
        }

        /// <summary>
        /// Configures the game window as a full-height overlay with selective click-through regions.
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

            // Set up click-through regions
            SetupDefaultClickableRegions();
            SetupHitTest(game);

            Console.WriteLine($"Window configured as full-height overlay at ({x},{y}) with selective click-through - Always on top: {alwaysOnTop}");
        }

        /// <summary>
        /// Sets up the SDL3 hit test for click-through functionality
        /// </summary>
        public static void SetupHitTest(Game game)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
            {
                Console.WriteLine("Could not get SDL window handle for hit test setup.");
                return;
            }

            // Create and store the callback to prevent garbage collection
            _hitTestCallback = HitTestCallback;
            
            int result = SDL_SetWindowHitTest(sdlWindow, _hitTestCallback, IntPtr.Zero);
            if (result != 0)
            {
                Console.WriteLine($"Failed to set up hit test callback. SDL Error code: {result}");
            }
            else
            {
                Console.WriteLine("Hit test callback set up successfully for click-through regions.");
            }
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