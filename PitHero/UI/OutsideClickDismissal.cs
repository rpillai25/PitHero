using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// Outside-click dismissal for non-modal popups (info cards, designation dialogs). Polls global
    /// mouse state instead of using a click-consuming overlay so the same click still reaches
    /// whatever was under it (top bar buttons, tabs, other windows). Callers stamp Time.FrameCount
    /// when showing the popup so the click that opened it can't also dismiss it.
    /// </summary>
    public static class OutsideClickDismissal
    {
        /// <summary>Returns true when a visible popup should hide: a click occurred this frame outside its bounds.</summary>
        public static bool ShouldDismiss(Element popup, Stage stage, uint shownFrame)
        {
            if (popup == null || stage == null || !popup.IsVisible()) return false;
            if (!Input.LeftMouseButtonPressed && !Input.RightMouseButtonPressed) return false;
            if (Time.FrameCount == shownFrame) return false;

            var mousePos = stage.GetMousePosition();
            bool inside = mousePos.X >= popup.GetX() && mousePos.X <= popup.GetX() + popup.GetWidth()
                && mousePos.Y >= popup.GetY() && mousePos.Y <= popup.GetY() + popup.GetHeight();
            return !inside;
        }
    }
}
