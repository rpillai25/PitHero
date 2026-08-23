using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// The graphical close button (issue #399). Anchors flush against the outside of a UI window's left
    /// edge, vertically centered on it, and gives the player a visible way out of any window. It is purely
    /// additive - the top-bar toggle, ESC and outside-click paths are unchanged, and clicking this
    /// button routes into the owning UI's normal close method so the pause/window-size bookkeeping in
    /// UIWindowManager still runs.
    /// </summary>
    public class WindowCloseButton : HoverableImageButton
    {
        private readonly Element _target;

        private WindowCloseButton(ImageButtonStyle style, string hoverText, Element target)
            : base(style, hoverText)
        {
            _target = target;
        }

        /// <summary>
        /// Builds a close button for the given window. The Up/Down/Over triple follows the atlas
        /// naming convention used by every other icon button (base / Inverse / Highlight), and the
        /// size is read off the art so swapping in a differently sized icon needs no code change.
        /// Only the 1x sprite is needed: UIWindowManager.OnUIWindowOpening restores the OS window to
        /// Normal size before any of these windows shows, so the half-height 2x art never applies.
        /// </summary>
        public static WindowCloseButton Create(Element target, string hoverText, System.Action onClose)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var sprite    = uiAtlas.GetSprite("UIClose");
            var highlight = uiAtlas.GetSprite("UICloseHighlight");
            var inverse   = uiAtlas.GetSprite("UICloseInverse");

            var style = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };

            var button = new WindowCloseButton(style, hoverText, target);
            button.ClickSoundCategory = ButtonClickCategory.Cancel;
            button.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            // The owning UI's close path is expected to end in HideAndDetach, which is what dismisses
            // the hover text the cursor left showing.
            button.OnClicked += (_) => onClose?.Invoke();
            return button;
        }

        /// <summary>
        /// Attaches the button to the stage (if it is not already there), shows it and anchors it.
        /// Deliberately does NOT call ToFront: a freshly added element is already on top of everything
        /// currently on the stage, and re-raising an already-attached button would lift it above
        /// overlays that were raised after it (the free-move blocker, confirmation dialogs).
        /// </summary>
        public void ShowOn(Stage stage)
        {
            if (stage == null) return;
            // Parent, not GetStage(): Group.RemoveElement clears the parent but leaves the element's
            // cached stage set, so GetStage() stays non-null forever after the first add and would
            // skip every re-attach after the first close.
            if (GetParent() == null)
                stage.AddElement(this);
            SetVisible(true);
            SyncPosition(stage);
        }

        /// <summary>
        /// Hides and detaches the button. Dismisses the hover text first: closing pulls the button out
        /// from under the cursor, so Draw never runs the hover-exit path and "Close" would be stranded
        /// over the game world. This covers every close path, not just a click on the button itself.
        /// </summary>
        public void HideAndDetach()
        {
            DismissHoverText();

            // Closing pulls the button out from under the cursor, so OnMouseExit never fires and Nez's
            // Button keeps _mouseOver set. Without this reset the button comes back drawn in its Over
            // (highlight) state the next time the window opens.
            _mouseOver = false;
            _mouseDown = false;

            SetVisible(false);
            Remove();
        }

        /// <summary>Re-anchors to the target window's current bounds. Cheap; safe to call every frame.</summary>
        public void SyncPosition(Stage stage)
        {
            if (_target == null || stage == null) return;
            var pos = AnchorLeftOf(_target.GetX(), _target.GetY(), _target.GetHeight(),
                GetWidth(), GetHeight(), stage.GetHeight());
            SetPosition(pos.X, pos.Y);
        }

        /// <summary>
        /// Pure anchor math: fully outside the target's left edge and flush against it (the button's
        /// right edge touches the window's left border), vertically centered on the target, clamped so
        /// the button never leaves the stage.
        /// </summary>
        public static Vector2 AnchorLeftOf(float targetX, float targetY, float targetH,
            float btnW, float btnH, float stageH)
        {
            float x = targetX - btnW;
            if (x < 0f) x = 0f;
            float y = UILayout.ClampY(targetY + (targetH - btnH) * 0.5f, btnH, stageH);
            return new Vector2(x, y);
        }
    }
}
