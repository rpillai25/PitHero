using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// Outside-click dismissal for non-modal popups (info cards, designation dialogs) and paused-game
    /// UI windows. Polls global mouse state instead of using a click-consuming overlay so the same
    /// click still reaches whatever was under it (top bar buttons, tabs, other windows). Callers
    /// stamp Time.FrameCount when showing the popup so the click that opened it can't also dismiss it.
    /// </summary>
    public static class OutsideClickDismissal
    {
        /// <summary>Returns true when a visible popup should hide: a click occurred this frame outside its bounds.</summary>
        public static bool ShouldDismiss(Element popup, Stage stage, uint shownFrame)
        {
            if (popup == null || stage == null || !popup.IsVisible()) return false;
            if (!Input.LeftMouseButtonPressed && !Input.RightMouseButtonPressed) return false;
            if (Time.FrameCount == shownFrame) return false;
            if (!ClickIsInGameWindow()) return false;

            return !IsInside(popup, stage.GetMousePosition());
        }

        /// <summary>
        /// Multi-window variant for UIs composed of several windows/cards (e.g. the shop's paired
        /// panels): returns true when a click this frame landed outside the bounding envelope of all
        /// visible elements in the list. Using the envelope (not the strict union) means clicks in
        /// the gap BETWEEN two windows of the same UI do not dismiss it — a mis-click between the
        /// shop panels mid-session must not close the shop. Null or hidden entries are skipped, so
        /// the list can safely contain lazily created popups.
        /// </summary>
        public static bool ShouldDismiss(List<Element> insideAny, Stage stage, uint shownFrame)
        {
            if (insideAny == null || stage == null) return false;
            if (!Input.LeftMouseButtonPressed && !Input.RightMouseButtonPressed) return false;
            if (Time.FrameCount == shownFrame) return false;
            if (!ClickIsInGameWindow()) return false;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool anyVisible = false;
            for (int i = 0; i < insideAny.Count; i++)
            {
                var element = insideAny[i];
                if (element == null || !element.IsVisible()) continue;
                anyVisible = true;
                if (element.GetX() < minX) minX = element.GetX();
                if (element.GetY() < minY) minY = element.GetY();
                if (element.GetX() + element.GetWidth() > maxX) maxX = element.GetX() + element.GetWidth();
                if (element.GetY() + element.GetHeight() > maxY) maxY = element.GetY() + element.GetHeight();
            }
            if (!anyVisible) return false;

            var mousePos = stage.GetMousePosition();
            return mousePos.X < minX || mousePos.X > maxX || mousePos.Y < minY || mousePos.Y > maxY;
        }

        /// <summary>
        /// True when the mouse is currently over the given visible element. Used to exempt a UI's
        /// own top-bar toggle button from outside-click dismissal: the dismissal poll runs before
        /// stage input, so without the exemption the poll closes the window and the button's toggle
        /// handler immediately reopens it — the window appears stuck open.
        /// </summary>
        public static bool IsMouseInside(Element element, Stage stage)
        {
            if (element == null || stage == null || !element.IsVisible()) return false;
            return IsInside(element, stage.GetMousePosition());
        }

        /// <summary>
        /// True only when the click actually landed in the game window. MonoGame's desktop mouse
        /// state reports button presses even when the window is unfocused, so without this guard a
        /// click on another app (browser, OS folders) while the game runs in the background
        /// dismisses open UI.
        /// </summary>
        private static bool ClickIsInGameWindow()
        {
            return Util.MouseUtils.IsMouseInsideWindow();
        }

        private static bool IsInside(Element element, Vector2 stagePos)
        {
            return stagePos.X >= element.GetX() && stagePos.X <= element.GetX() + element.GetWidth()
                && stagePos.Y >= element.GetY() && stagePos.Y <= element.GetY() + element.GetHeight();
        }
    }
}
