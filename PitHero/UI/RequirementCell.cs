using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// One unlock requirement: slot background, sprite and a "have/required" badge (green once
    /// met). Shared by the crop (issue #413) and dish (issue #417) requirement cards. The badge
    /// text is built once so drawing never allocates.
    /// </summary>
    public class RequirementCell : Element
    {
        private static readonly Color MetColor = new Color(120, 255, 140);
        private static readonly Color SlotBgColor = new Color(255, 255, 255, 100);

        private readonly SpriteDrawable _draw;
        private readonly SpriteDrawable _background;
        private readonly string _badge;
        private readonly Color _badgeColor;
        private readonly Color _spriteColor;

        /// <summary>
        /// Builds a cell. A locked requirement draws its sprite at the locked tint for
        /// <paramref name="lockedProgress"/> (0-1 toward its own unlock): near-black at 0, warming
        /// toward full colour as it nears unlocking. Pass 0 where no progress measure exists.
        /// The tint is a snapshot — these cells live in a dialog that is rebuilt each time it opens.
        /// </summary>
        public RequirementCell(Sprite sprite, int have, int required, bool locked, float lockedProgress)
        {
            _draw = sprite != null ? new SpriteDrawable(sprite) : null;
            _badge = have + "/" + required;
            _badgeColor = have >= required ? MetColor : Color.White;
            _spriteColor = locked ? GameConfig.GetLockedCropTint(lockedProgress) : Color.White;
            SetTouchable(Touchable.Disabled);
            SetSize(GameConfig.CropUnlockRequirementCellSize, GameConfig.CropUnlockRequirementCellSize);

            var itemsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/Items.atlas");
            var bgSprite = itemsAtlas?.GetSprite("Inventory");
            if (bgSprite != null)
                _background = new SpriteDrawable(bgSprite);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            _background?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), SlotBgColor);
            _draw?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), _spriteColor);

            var font = Nez.Graphics.Instance?.BitmapFont;
            if (font == null)
                return;
            float tw = font.MeasureString(_badge).X;
            StackCountText.Draw(batcher, font, _badge,
                new Vector2(GetX() + GetWidth() - tw - 2f, GetY() + GetHeight() - font.LineHeight - 1f),
                _badgeColor);
        }
    }
}
