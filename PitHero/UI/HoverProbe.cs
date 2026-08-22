using Microsoft.Xna.Framework;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// Validity checks for the periodic geometric hover polls that back our tooltip safety nets.
    ///
    /// Those polls test raw slot bounds against the cursor, which is only meaningful while the slot
    /// is actually on screen. TabPane.SetActiveTab clears the tab table, so an inactive tab's content
    /// is detached from the stage but keeps both its last laid-out coordinates and its stale _stage
    /// reference (Group.RemoveElement nulls parent, not _stage). A raw bounds test therefore keeps
    /// reporting hits for slots the player cannot see — and because a detached element never receives
    /// OnMouseExit, whatever the poll shows can never be dismissed by the matching unhover.
    ///
    /// Use IsLive before trusting a bounds test, and IsTopmostAt when something (a stage-centered
    /// dialog, another window) may be covering the element.
    /// </summary>
    public static class HoverProbe
    {
        /// <summary>
        /// True when the element is still attached to the stage's root chain and visible the whole way
        /// up — i.e. its laid-out coordinates describe something the player can actually point at.
        /// </summary>
        public static bool IsLive(Element element, Stage stage)
        {
            if (element == null || stage == null) return false;

            var root = stage.GetRoot();
            if (root == null) return false;

            for (var current = element; current != null; current = current.GetParent())
            {
                if (!current.IsVisible()) return false;
                if (current == root) return true;
            }

            // Ran out of parents without reaching the root: detached (inactive tab, removed window).
            return false;
        }

        /// <summary>
        /// True when the element is live and is the top-most hit-testable thing under the given stage
        /// position. Hit-testing walks the real draw order, so this also rejects an element buried
        /// under a dialog or another window.
        /// </summary>
        public static bool IsTopmostAt(Element element, Stage stage, Vector2 stagePosition)
        {
            if (!IsLive(element, stage)) return false;

            var hit = stage.Hit(stagePosition);
            for (var current = hit; current != null; current = current.GetParent())
            {
                if (current == element) return true;
            }

            return false;
        }

        /// <summary>The cursor in stage space, matching what Stage.Update feeds its own hit tests.</summary>
        public static Vector2 GetStageMousePosition(Stage stage)
        {
            return stage.ScreenToStageCoordinates(stage.GetMousePosition());
        }
    }
}
