using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using SDL3;
using System;

namespace PitHero
{
    public static class WindowManager
    {
        private static uint _currentDisplayID;
        private static SDL.SDL_Rect _currentDisplayBounds;
        private static bool _haveBounds;

        private static int _originalWindowWidth;
        private static int _originalWindowHeight;
        private static bool _storedOriginalSize;

        // track shrink levels
        private enum ShrinkMode { Normal = 0, Half = 1, Quarter = 2 }
        private static ShrinkMode _currentShrinkMode = ShrinkMode.Normal;

        // track docking mode so shrink/restore can honor it
        private enum DockMode { None, Top, Bottom, Center }
        private static DockMode _currentDockMode = DockMode.None;
        private static int _currentDockYOffset = 0;

        private static void EnsureCurrentDisplay(IntPtr sdlWindow)
        {
            if (sdlWindow == IntPtr.Zero)
                return;

            if (_currentDisplayID == 0)
            {
                _currentDisplayID = SDL.SDL_GetDisplayForWindow(sdlWindow);
            }

            if (!_haveBounds || _currentDisplayBounds.w == 0 || _currentDisplayBounds.h == 0)
            {
                if (!SDL.SDL_GetDisplayBounds(_currentDisplayID, out _currentDisplayBounds))
                {
                    // Fallback: fabricate bounds from default adapter
                    var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    _currentDisplayBounds = new SDL.SDL_Rect { x = 0, y = 0, w = dm.Width, h = dm.Height };
                }
                _haveBounds = true;
            }
        }

        private static void SetCurrentDisplay(uint displayID, SDL.SDL_Rect bounds)
        {
            _currentDisplayID = displayID;
            _currentDisplayBounds = bounds;
            _haveBounds = true;
        }

        /// <summary>Returns true if window is at least half shrink</summary>
        public static bool IsHalfHeightMode() => _currentShrinkMode == ShrinkMode.Half || _currentShrinkMode == ShrinkMode.Quarter;
        /// <summary>Returns true if window is in quarter shrink mode</summary>
        public static bool IsQuarterHeightMode() => _currentShrinkMode == ShrinkMode.Quarter;

        /// <summary>Shrinks window to half (if normal) or quarter (if already half). Does nothing past quarter. Keeps aspect ratio to avoid squish. Honors docking mode.</summary>
        public static void ShrinkToNextLevel(Game game)
        {
            var sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            EnsureCurrentDisplay(sdlWindow);

            if (!_storedOriginalSize)
            {
                SDL.SDL_GetWindowSize(sdlWindow, out _originalWindowWidth, out _originalWindowHeight);
                _storedOriginalSize = true;
                Debug.Log($"Stored original window size Width={_originalWindowWidth} Height={_originalWindowHeight}");
            }

            if (_currentShrinkMode == ShrinkMode.Quarter)
                return; // already at smallest

            // capture current size + position for horizontal adjustment
            SDL.SDL_GetWindowSize(sdlWindow, out int prevW, out int prevH);
            SDL.SDL_GetWindowPosition(sdlWindow, out int prevX, out int prevY);

            ShrinkMode targetMode = _currentShrinkMode + 1; // Normal->Half, Half->Quarter

            float factor = targetMode switch
            {
                ShrinkMode.Half => 0.5f,
                ShrinkMode.Quarter => 0.25f,
                _ => 1f
            };

            int newHeight = (int)System.Math.Max(1, _originalWindowHeight * factor);
            int newWidth = (int)System.Math.Max(1, _originalWindowWidth * factor); // proportional to keep aspect ratio

            // Default horizontal behavior: center relative to previous
            int newX = prevX + (prevW - newWidth) / 2;
            if (newX < 0) newX = 0;

            // Determine Y based on docking mode (fix: keep top-docked windows at top when shrinking)
            int newY;
            switch (_currentDockMode)
            {
                case DockMode.Top:
                    newY = _currentDisplayBounds.y + _currentDockYOffset;
                    if (newY < _currentDisplayBounds.y) newY = _currentDisplayBounds.y;
                    break;
                case DockMode.Center:
                    {
                        int centerY = _currentDisplayBounds.y + (_currentDisplayBounds.h - newHeight) / 2 + _currentDockYOffset;
                        newY = centerY;
                        if (newY < _currentDisplayBounds.y) newY = _currentDisplayBounds.y;
                        if (newY + newHeight > _currentDisplayBounds.y + _currentDisplayBounds.h)
                            newY = _currentDisplayBounds.y + _currentDisplayBounds.h - newHeight;
                        break;
                    }
                case DockMode.Bottom:
                    {
                        int baseBottomY = _currentDisplayBounds.y + _currentDisplayBounds.h - newHeight;
                        newY = baseBottomY + _currentDockYOffset; // offset expected negative/zero for bottom
                        if (newY < _currentDisplayBounds.y) newY = _currentDisplayBounds.y; // clamp top
                        if (newY + newHeight > _currentDisplayBounds.y + _currentDisplayBounds.h)
                            newY = _currentDisplayBounds.y + _currentDisplayBounds.h - newHeight;
                        break;
                    }
                case DockMode.None:
                default:
                    // legacy behavior: anchor bottom edge as before
                    int bottomY = prevY + prevH; // previous bottom pixel
                    newY = bottomY - newHeight;
                    if (newY < 0) newY = 0;
                    break;
            }

            SDL.SDL_SetWindowSize(sdlWindow, newWidth, newHeight);
            SDL.SDL_SetWindowPosition(sdlWindow, newX, newY);

            _currentShrinkMode = targetMode;
            Debug.Log($"ShrinkToNextLevel -> Mode={_currentShrinkMode} Dock={_currentDockMode} NewSize={newWidth}x{newHeight} Pos=({newX},{newY})");
        }

        /// <summary>Restores window to original size from any shrink level honoring docking mode.</summary>
        public static void RestoreOriginalSize(Game game)
        {
            if (!_storedOriginalSize || _currentShrinkMode == ShrinkMode.Normal)
                return;

            var sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;

            EnsureCurrentDisplay(sdlWindow);

            SDL.SDL_GetWindowSize(sdlWindow, out int prevW, out int prevH);
            SDL.SDL_GetWindowPosition(sdlWindow, out int prevX, out int prevY);

            int newX = prevX - (_originalWindowWidth - prevW) / 2;
            if (newX < 0) newX = 0;

            int newY;
            switch (_currentDockMode)
            {
                case DockMode.Top:
                    newY = _currentDisplayBounds.y + _currentDockYOffset;
                    break;
                case DockMode.Center:
                    newY = _currentDisplayBounds.y + (_currentDisplayBounds.h - _originalWindowHeight) / 2 + _currentDockYOffset;
                    break;
                case DockMode.Bottom:
                    newY = _currentDisplayBounds.y + _currentDisplayBounds.h - _originalWindowHeight + _currentDockYOffset;
                    break;
                case DockMode.None:
                default:
                    // legacy: maintain bottom anchoring relative to current bottom
                    int bottomY = prevY + prevH;
                    newY = bottomY - _originalWindowHeight;
                    break;
            }

            // Clamp inside display bounds
            if (newY < _currentDisplayBounds.y) newY = _currentDisplayBounds.y;
            if (newY + _originalWindowHeight > _currentDisplayBounds.y + _currentDisplayBounds.h)
                newY = _currentDisplayBounds.y + _currentDisplayBounds.h - _originalWindowHeight;

            SDL.SDL_SetWindowSize(sdlWindow, _originalWindowWidth, _originalWindowHeight);
            SDL.SDL_SetWindowPosition(sdlWindow, newX, newY);

            Debug.Log($"RestoreOriginalSize -> Dock={_currentDockMode} Size={_originalWindowWidth}x{_originalWindowHeight} Pos=({newX},{newY})");
            _currentShrinkMode = ShrinkMode.Normal;
        }

        /// <summary>Legacy compatibility: shrink to half if not already shrunk.</summary>
        public static void ShrinkHeightToHalf(Game game)
        {
            if (!IsHalfHeightMode())
                ShrinkToNextLevel(game);
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

            _currentDockMode = DockMode.Bottom; // treat initial configuration as bottom dock
            _currentDockYOffset = 0;

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

            _currentDockMode = DockMode.Bottom;
            _currentDockYOffset = 0;

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
            var sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero) return;
            EnsureCurrentDisplay(sdlWindow);

            int windowWidth = _currentDisplayBounds.w;
            int windowHeight = _currentDisplayBounds.h / 3;

            int x = _currentDisplayBounds.x;
            int y = _currentDisplayBounds.y + Math.Max(0, Math.Min(yOffset, _currentDisplayBounds.h - 100));

            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);
            SDL.SDL_SetWindowPosition(sdlWindow, x, y);

            _currentDockMode = DockMode.Top;
            _currentDockYOffset = yOffset;

            Debug.Log($"DockTop -> displayID={_currentDisplayID} bounds=({_currentDisplayBounds.x},{_currentDisplayBounds.y},{_currentDisplayBounds.w},{_currentDisplayBounds.h}) pos=({x},{y}) size={windowWidth}x{windowHeight}");
        }

        /// <summary>
        /// Docks the window to the bottom of the screen with optional Y offset for fine-tuning.
        /// </summary>
        public static void DockBottom(Game game, int yOffset = 0)
        {
            var sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero) return;
            EnsureCurrentDisplay(sdlWindow);

            int windowWidth = _currentDisplayBounds.w;
            int windowHeight = _currentDisplayBounds.h / 3;

            int baseY = _currentDisplayBounds.y + _currentDisplayBounds.h - windowHeight;
            int y = Math.Max(_currentDisplayBounds.y + 100, Math.Min(baseY + yOffset, _currentDisplayBounds.y + _currentDisplayBounds.h - 100));
            int x = _currentDisplayBounds.x;

            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);
            SDL.SDL_SetWindowPosition(sdlWindow, x, y);

            _currentDockMode = DockMode.Bottom;
            _currentDockYOffset = yOffset;

            Debug.Log($"DockBottom -> displayID={_currentDisplayID} bounds=({_currentDisplayBounds.x},{_currentDisplayBounds.y},{_currentDisplayBounds.w},{_currentDisplayBounds.h}) pos=({x},{y}) size={windowWidth}x{windowHeight}");
        }

        /// <summary>
        /// Docks the window to the center of the screen with optional Y offset for fine-tuning.
        /// </summary>
        public static void DockCenter(Game game, int yOffset = 0)
        {
            var sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero) return;
            EnsureCurrentDisplay(sdlWindow);

            int windowWidth = _currentDisplayBounds.w;
            int windowHeight = _currentDisplayBounds.h / 3;

            int centerY = _currentDisplayBounds.y + (_currentDisplayBounds.h - windowHeight) / 2;
            int y = Math.Max(_currentDisplayBounds.y + 100, Math.Min(centerY + yOffset, _currentDisplayBounds.y + _currentDisplayBounds.h - 100));
            int x = _currentDisplayBounds.x;

            SDL.SDL_SetWindowSize(sdlWindow, windowWidth, windowHeight);
            SDL.SDL_SetWindowPosition(sdlWindow, x, y);

            _currentDockMode = DockMode.Center;
            _currentDockYOffset = yOffset;

            Debug.Log($"DockCenter -> displayID={_currentDisplayID} bounds=({_currentDisplayBounds.x},{_currentDisplayBounds.y},{_currentDisplayBounds.w},{_currentDisplayBounds.h}) pos=({x},{y}) size={windowWidth}x{windowHeight}");
        }

        /// <summary>
        /// Swaps the window to the next physical monitor.
        /// Uses global desktop coordinates from SDL display bounds so the window really moves.
        /// Attempts to preserve bottom docking (1/3 height) behavior.
        /// </summary>
        public static void SwapToNextMonitor(Game game)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
            {
                Debug.Log("SwapToNextMonitor: SDL window handle invalid.");
                return;
            }

            IntPtr displaysPtr = SDL.SDL_GetDisplays(out int displayCount);
            if (displayCount <= 1 || displaysPtr == IntPtr.Zero)
            {
                Debug.Log("SwapToNextMonitor: Only one display detected or failed to get displays.");
                return;
            }

            uint currentDisplayID = SDL.SDL_GetDisplayForWindow(sdlWindow);

            int currentIndex = -1;
            uint nextDisplayID = 0;

            unsafe
            {
                uint* displays = (uint*)displaysPtr;
                for (int i = 0; i < displayCount; i++)
                {
                    if (displays[i] == currentDisplayID)
                    {
                        currentIndex = i;
                        break;
                    }
                }
                if (currentIndex == -1)
                    currentIndex = 0;

                int nextIndex = (currentIndex + 1) % displayCount;
                nextDisplayID = displays[nextIndex];
            }

            if (!SDL.SDL_GetDisplayBounds(nextDisplayID, out var nextBounds))
            {
                Debug.Log($"SwapToNextMonitor: SDL_GetDisplayBounds failed: {SDL.SDL_GetError()}");
                return;
            }

            // Use docking mode to decide target position/size
            int targetWidth = nextBounds.w;
            int targetHeight = nextBounds.h / 3;
            int targetX = nextBounds.x;
            int targetY;
            switch (_currentDockMode)
            {
                case DockMode.Top:
                    targetY = nextBounds.y + _currentDockYOffset;
                    break;
                case DockMode.Center:
                    targetY = nextBounds.y + (nextBounds.h - targetHeight) / 2 + _currentDockYOffset;
                    break;
                case DockMode.Bottom:
                case DockMode.None:
                default:
                    targetY = nextBounds.y + nextBounds.h - targetHeight + _currentDockYOffset;
                    break;
            }

            SDL.SDL_SetWindowSize(sdlWindow, targetWidth, targetHeight);
            SDL.SDL_SetWindowPosition(sdlWindow, targetX, targetY);

            SetCurrentDisplay(nextDisplayID, nextBounds);

            SDL.SDL_GetWindowPosition(sdlWindow, out int finalX, out int finalY);
            SDL.SDL_GetWindowSize(sdlWindow, out int finalW, out int finalH);

            Debug.Log($"SwapToNextMonitor: moved to displayID={nextDisplayID} bounds=({nextBounds.x},{nextBounds.y},{nextBounds.w},{nextBounds.h}) final=({finalX},{finalY}) size={finalW}x{finalH} Dock={_currentDockMode}");
        }
    }
}