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
        private enum ShrinkMode { Normal = 0, Half = 1 }
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
        public static bool IsHalfHeightMode() => _currentShrinkMode == ShrinkMode.Half;

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

            // capture current size + position for horizontal adjustment
            SDL.SDL_GetWindowSize(sdlWindow, out int prevW, out int prevH);
            SDL.SDL_GetWindowPosition(sdlWindow, out int prevX, out int prevY);

            ShrinkMode targetMode = _currentShrinkMode == ShrinkMode.Normal ? ShrinkMode.Half : ShrinkMode.Normal;

            float factor = targetMode == ShrinkMode.Half ? 0.5f : 1f;

            int newHeight = (int)System.Math.Max(1, _originalWindowHeight * factor);
            int newWidth = (int)System.Math.Max(1, _originalWindowWidth * factor); // proportional to keep aspect ratio

            // Default horizontal behavior: center relative to previous
            int newX = prevX + (prevW - newWidth) / 2;

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
                    break;
            }

            // Clamp inside display bounds (display-origin aware for secondary monitors)
            ClampRectToBounds(ref newX, ref newY, newWidth, newHeight,
                _currentDisplayBounds.x, _currentDisplayBounds.y, _currentDisplayBounds.w, _currentDisplayBounds.h);

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

            // Clamp inside display bounds (a full-display-width window pins X to the display edge,
            // so restoring from an off-center half-size window can't hang past the right edge)
            ClampRectToBounds(ref newX, ref newY, _originalWindowWidth, _originalWindowHeight,
                _currentDisplayBounds.x, _currentDisplayBounds.y, _currentDisplayBounds.w, _currentDisplayBounds.h);

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
        /// Physical window height for the strip on a monitor of the given height. The design height
        /// (GameConfig.VirtualHeight) is 1:1 at GameConfig.ReferenceDisplayHeight and scales from there,
        /// so the FixedHeight render target always maps to whole pixels (1080 -> 1x, 2160 -> 2x).
        /// </summary>
        public static int GetStripHeight(int displayHeight)
        {
            return displayHeight * GameConfig.VirtualHeight / GameConfig.ReferenceDisplayHeight;
        }

        /// <summary>
        /// Configures the game window as a horizontal strip docked at the bottom of the screen,
        /// sized by GetStripHeight (the design height scaled to the monitor).
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

            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            int displayWidth = displayMode.Width;
            int displayHeight = displayMode.Height;

            int windowWidth = displayWidth;
            int windowHeight = GetStripHeight(displayHeight);

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
        /// Clears dock tracking so later shrink/restore anchor to the window's current position
        /// instead of snapping back to a dock (used by free-move mode).
        /// </summary>
        public static void ClearDockMode()
        {
            _currentDockMode = DockMode.None;
            _currentDockYOffset = 0;
        }

        /// <summary>
        /// Gets the window's position and size in global desktop coordinates. False if the SDL handle is unavailable.
        /// </summary>
        public static bool TryGetWindowRect(Game game, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return false;

            SDL.SDL_GetWindowPosition(sdlWindow, out x, out y);
            SDL.SDL_GetWindowSize(sdlWindow, out w, out h);
            return true;
        }

        /// <summary>
        /// Reads the global desktop mouse position. Returns true while the left button is held.
        /// </summary>
        public static bool GetGlobalMouseLeftDown(out float x, out float y)
        {
            return (SDL.SDL_GetGlobalMouseState(out x, out y) & SDL.SDL_MouseButtonFlags.SDL_BUTTON_LMASK) != 0;
        }

        /// <summary>
        /// Moves the window to (x, y) clamped inside the current display's bounds. Supports negative desktop coordinates.
        /// </summary>
        public static void MoveWindowClampedToCurrentDisplay(Game game, int x, int y)
        {
            IntPtr sdlWindow = game.Window.Handle;
            if (sdlWindow == IntPtr.Zero)
                return;
            EnsureCurrentDisplay(sdlWindow);

            SDL.SDL_GetWindowSize(sdlWindow, out int winW, out int winH);
            ClampRectToBounds(ref x, ref y, winW, winH,
                _currentDisplayBounds.x, _currentDisplayBounds.y, _currentDisplayBounds.w, _currentDisplayBounds.h);
            SDL.SDL_SetWindowPosition(sdlWindow, x, y);
        }

        /// <summary>
        /// Clamps a window rect into display bounds. A window as wide/tall as the bounds pins to the
        /// bounds origin on that axis, so full-display-width windows can only move vertically.
        /// </summary>
        public static void ClampRectToBounds(ref int x, ref int y, int winW, int winH, int bx, int by, int bw, int bh)
        {
            int maxX = bx + bw - winW;
            if (maxX < bx) maxX = bx;
            int maxY = by + bh - winH;
            if (maxY < by) maxY = by;

            if (x < bx) x = bx;
            else if (x > maxX) x = maxX;
            if (y < by) y = by;
            else if (y > maxY) y = maxY;
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
            int windowHeight = GetStripHeight(_currentDisplayBounds.h);

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
            int windowHeight = GetStripHeight(_currentDisplayBounds.h);

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
            int windowHeight = GetStripHeight(_currentDisplayBounds.h);

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
            int targetHeight = GetStripHeight(nextBounds.h);
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

            // Move first, then resize: a resize issued while the window is still on the old monitor
            // gets processed in that monitor's context (DPI/resolution) and comes out wrong after
            // the move. Re-assert the position afterwards since resizing can shift the window, and
            // sync between operations (SDL3 window ops are asynchronous).
            SDL.SDL_SetWindowPosition(sdlWindow, targetX, targetY);
            SDL.SDL_SyncWindow(sdlWindow);
            SDL.SDL_SetWindowSize(sdlWindow, targetWidth, targetHeight);
            SDL.SDL_SyncWindow(sdlWindow);
            SDL.SDL_SetWindowPosition(sdlWindow, targetX, targetY);
            SDL.SDL_SyncWindow(sdlWindow);

            // The Normal-size strip now belongs to the new monitor; keep shrink/restore consistent
            if (_storedOriginalSize)
            {
                _originalWindowWidth = targetWidth;
                _originalWindowHeight = targetHeight;
            }

            SetCurrentDisplay(nextDisplayID, nextBounds);

            SDL.SDL_GetWindowPosition(sdlWindow, out int finalX, out int finalY);
            SDL.SDL_GetWindowSize(sdlWindow, out int finalW, out int finalH);

            Debug.Log($"SwapToNextMonitor: moved to displayID={nextDisplayID} bounds=({nextBounds.x},{nextBounds.y},{nextBounds.w},{nextBounds.h}) final=({finalX},{finalY}) size={finalW}x{finalH} Dock={_currentDockMode}");
        }
    }
}