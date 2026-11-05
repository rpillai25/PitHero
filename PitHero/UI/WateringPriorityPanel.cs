using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Services.Replay;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// "Watering Priority" card docked to the right of the Seeds window (issue #450). Lists the
    /// unlocked crops in the order farm workers water them, each as its crop sprite (hover for the
    /// name) with Up/Down buttons.
    ///
    /// Pure view: it reads GameStateService to render and dispatches SwapWateringPriority to change
    /// the order, never writing the service directly — the sim must not depend on this window ever
    /// being opened.
    /// </summary>
    public class WateringPriorityPanel : Window
    {
        private const float ContentPadding = 6f;
        private const float ScrollBarAllowance = 16f;

        /// <summary>One listed crop, carrying its ordinal so the order never round-trips through display text.</summary>
        private sealed class CropPriorityItem
        {
            public readonly CropType Crop;
            public readonly string Name;

            public CropPriorityItem(CropType crop, string name)
            {
                Crop = crop;
                Name = name;
            }

            public override string ToString() => Name;
        }

        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;
        private readonly CropPriorityItem[] _byCrop = new CropPriorityItem[CropTypeInfo.Count];
        private readonly List<CropPriorityItem> _items = new List<CropPriorityItem>(CropTypeInfo.Count);
        private readonly int[] _order = new int[CropTypeInfo.Count];
        private readonly ReorderableTableList<CropPriorityItem> _list;
        private readonly Table _content;
        private readonly Cell _scrollCell;

        // What the list was last built from; a change in either (a swap applied, a crop unlocked
        // while the window is open) triggers a rebuild.
        private int _shownVersion = -1;
        private int _shownUnlockedMask = -1;

        private TextService _textService;

        /// <summary>Creates the (hidden) panel; call <see cref="ShowBeside"/> to dock and show it.</summary>
        public WateringPriorityPanel(Skin skin, Stage stage, SpriteAtlas cropsAtlas) : base("", skin, "ph-default")
        {
            _stage = stage;
            _cropsAtlas = cropsAtlas;
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);

            GetTitleLabel().SetText(GetText(UITextKey.WindowWateringPriority));

            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                var crop = (CropType)i;
                _byCrop[i] = new CropPriorityItem(crop, GetText(CropConfig.GetDisplayNameKey(crop)));
            }

            // ReorderableTableList mutates _items in place, so this same instance is reused for the
            // life of the panel — Sync refills it rather than replacing it.
            _list = new ReorderableTableList<CropPriorityItem>(skin, _items, OnReordered, CreateCropIcon);

            var scroll = new ScrollPane(_list, skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);

            _content = new Table();
            _content.Top().Left();
            _scrollCell = _content.Add(scroll).Left().Top();

            Add(_content).Expand().Fill().Pad(ContentPadding);
            SetVisible(false);
        }

        /// <summary>
        /// Rebuilds from the simulation, sizes the list to <paramref name="height"/> (the Seeds
        /// window's), docks the card at the given stage position and shows it.
        /// </summary>
        public void ShowBeside(float x, float y, float height)
        {
            Sync();
            SetVisible(true);
            Layout(x, y, height);
        }

        /// <summary>Hides the card and any crop-name hover text it raised.</summary>
        public void HidePanel()
        {
            if (!IsVisible())
                return;
            SetVisible(false);
            CropIcon.HideOwnedHoverText();
        }

        /// <summary>Rebuilds the list when the watering order or the unlocked crops changed. Call once per presentation frame while visible.</summary>
        public void RefreshIfChanged()
        {
            if (!IsVisible())
                return;
            var gameState = GameState;
            int version = gameState != null ? gameState.WateringPriorityVersion : 0;
            int unlocked = GetUnlockedMask(gameState);
            if (version == _shownVersion && unlocked == _shownUnlockedMask)
                return;
            float x = GetX(), y = GetY(), height = GetHeight();
            Sync();
            Layout(x, y, height);
        }

        private void Layout(float x, float y, float height)
        {
            // Width from the list itself (widest row + scroll bar); height pinned to the Seeds
            // window's so the two read as one block, the list scrolling when it is longer.
            _list.Pack();
            _scrollCell.Width(_list.PreferredWidth + ScrollBarAllowance);
            _scrollCell.Height(1f);
            _content.InvalidateHierarchy();
            Pack();
            float chrome = GetHeight() - 1f;
            float cellHeight = height - chrome;
            if (cellHeight < 1f)
                cellHeight = 1f;
            _scrollCell.Height(cellHeight);
            _content.InvalidateHierarchy();
            Pack();
            SetPosition(x, y);
        }

        /// <summary>Mirrors the simulation's order into the list, unlocked crops only.</summary>
        private void Sync()
        {
            var gameState = GameState;
            if (gameState != null)
                gameState.CopyWateringPriorityOrder(_order);
            else
                System.Array.Copy(GameStateService.DefaultWateringPriorityOrder(), _order, _order.Length);

            _items.Clear();
            for (int i = 0; i < _order.Length; i++)
            {
                var crop = (CropType)_order[i];
                if (CropUnlockTracker.IsUnlocked(crop))
                    _items.Add(_byCrop[_order[i]]);
            }

            _shownVersion = gameState != null ? gameState.WateringPriorityVersion : 0;
            _shownUnlockedMask = GetUnlockedMask(gameState);
            CropIcon.HideOwnedHoverText(); // the icons are about to be replaced
            _list.Rebuild();
        }

        private void OnReordered(int oldIndex, int newIndex, CropPriorityItem item)
        {
            // _items already holds the result: the crop the player moved swapped places with its
            // visible neighbour, which now sits at oldIndex. Name both crops, never positions.
            var other = _items[oldIndex];
            PlayerCommandService.Dispatch(new PlayerCommand(PlayerCommandType.SwapWateringPriority,
                (int)item.Crop, (int)other.Crop));
            CropIcon.HideOwnedHoverText();
        }

        private Element CreateCropIcon(CropPriorityItem item, bool grayed)
        {
            var sprite = _cropsAtlas?.GetSprite(CropConfig.GetFullyGrownSpriteName(item.Crop));
            return new CropIcon(sprite, item.Name);
        }

        private static GameStateService GameState
            => Core.Instance != null ? Core.Services.GetService<GameStateService>() : null;

        private static int GetUnlockedMask(GameStateService gameState)
            => gameState != null ? CropUnlockConfig.GetUnlockedMask(gameState.CropHarvestedTotals) : -1;

        private string GetText(string key)
        {
            if (_textService == null && Core.Instance != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>
        /// A crop sprite fitted into a square cell that shows the crop's name while hovered. Tracks
        /// which icon raised the shared hover text so an exit never hides the text a neighbour's
        /// enter just raised (Nez dispatches enter before exit).
        /// </summary>
        private sealed class CropIcon : Element, IInputListener
        {
            private static CropIcon _hoverOwner;

            private readonly Sprite _sprite;
            private readonly string _name;

            public CropIcon(Sprite sprite, string name)
            {
                _sprite = sprite;
                _name = name;
                SetTouchable(Touchable.Enabled);
                SetSize(GameConfig.WateringPriorityIconSize, GameConfig.WateringPriorityIconSize);
            }

            public override float PreferredWidth => GameConfig.WateringPriorityIconSize;
            public override float PreferredHeight => GameConfig.WateringPriorityIconSize;

            /// <summary>Hides the hover text if one of these icons raised it.</summary>
            public static void HideOwnedHoverText()
            {
                if (_hoverOwner == null)
                    return;
                _hoverOwner = null;
                HoverTextManager.HideHoverText();
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                if (_sprite == null)
                    return;
                // Fit the (possibly tall) crop sprite into the square cell, bottom-centred.
                float w = _sprite.SourceRect.Width;
                float h = _sprite.SourceRect.Height;
                float scale = System.Math.Min(GetWidth() / w, GetHeight() / h);
                float dw = w * scale, dh = h * scale;
                float dx = GetX() + (GetWidth() - dw) * 0.5f;
                float dy = GetY() + GetHeight() - dh;
                batcher.Draw(_sprite, new RectangleF(dx, dy, dw, dh), _sprite.SourceRect,
                    Color.White * parentAlpha);
            }

            void IInputListener.OnMouseEnter()
            {
                var stage = GetStage();
                if (stage == null || string.IsNullOrEmpty(_name))
                    return;
                var mp = stage.GetMousePosition();
                HoverTextManager.ShowHoverText(_name, mp.X + 12f, mp.Y - 4f);
                _hoverOwner = this;
            }

            void IInputListener.OnMouseExit()
            {
                if (_hoverOwner != this)
                    return;
                _hoverOwner = null;
                HoverTextManager.HideHoverText();
            }

            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
