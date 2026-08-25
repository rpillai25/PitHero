using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Context menu for inventory slots with Use/Discard/Cancel options.
    ///
    /// Discard sells the item to the Second Chance vault — the same action as dragging it onto the
    /// shop window — so it confirms through the shared <see cref="ItemSellPrompt"/> and both routes
    /// show the identical dialog. Those prompts register themselves with
    /// <see cref="UIPromptRegistry"/>, so drag suppression, hover-card suppression, outside-click
    /// dismissal, and Escape all see them. The context menu itself is NOT a prompt — it has its own
    /// dismiss layer and must stay click-through-dismissable.
    /// </summary>
    public class InventoryContextMenu
    {
        private Window _contextMenuWindow;
        private Stage _stage;
        private Skin _skin;
        private IItem _currentItem;
        private int _currentBagIndex;
        private DismissLayer _dismissLayer;
        private ResettableTextButton _useButton;
        private ResettableTextButton _discardButton;
        private ResettableTextButton _cancelButton;
        private TextService _textService;

        public event System.Action<IItem, int> OnUseItem;

        /// <summary>Fired with (item, bagIndex, quantity) once the sell is confirmed. Quantity is
        /// int.MaxValue for a whole-stack sale.</summary>
        public event System.Action<IItem, int, int> OnDiscardItem;
        public event System.Action OnHidden;

        /// <summary>Initializes context menu windows.</summary>
        public void Initialize(Stage stage, Skin skin)
        {
            _stage = stage;
            _skin = skin;
            _textService = Core.Services.GetService<TextService>();
            CreateContextMenu();
        }

        /// <summary>Ensures dismiss layer exists.</summary>
        private void EnsureDismissLayer()
        {
            if (_dismissLayer != null) return;
            _dismissLayer = new DismissLayer(this);
            _dismissLayer.SetSize(_stage.GetWidth(), _stage.GetHeight());
            _stage.AddElement(_dismissLayer);
            _dismissLayer.SetVisible(false);
        }

        /// <summary>Creates main context menu once.</summary>
        private void CreateContextMenu()
        {
            var windowStyle = _skin.Get<WindowStyle>();
            _contextMenuWindow = new Window("", windowStyle);
            _contextMenuWindow.SetSize(120, 120);
            var table = new Table();
            table.Pad(10);

            _useButton = new ResettableTextButton("Use", _skin);
            _useButton.OnClicked += (btn) =>
            {
                Hide();
                if (_currentItem is Consumable)
                    OnUseItem?.Invoke(_currentItem, _currentBagIndex);
            };
            table.Add(_useButton).Width(100).Height(25);
            table.Row();

            _discardButton = new ResettableTextButton(_textService.DisplayText(TextType.UI, UITextKey.ButtonDiscard), _skin);
            _discardButton.OnClicked += (btn) =>
            {
                Hide();
                ShowDiscardConfirmation();
            };
            table.Add(_discardButton).Width(100).Height(25).SetPadTop(5);
            table.Row();

            _cancelButton = new ResettableTextButton(_textService.DisplayText(TextType.UI, UITextKey.ButtonCancel), _skin);
            _cancelButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            _cancelButton.OnClicked += (btn) => Hide();
            table.Add(_cancelButton).Width(100).Height(25).SetPadTop(5);

            _contextMenuWindow.Add(table);
            _contextMenuWindow.SetVisible(false);
            _stage.AddElement(_contextMenuWindow);
        }

        /// <summary>Shows the menu at stage position.</summary>
        public void Show(IItem item, int bagIndex, Vector2 position)
        {
            _currentItem = item;
            _currentBagIndex = bagIndex;
            EnsureDismissLayer();
            if (_dismissLayer != null)
            {
                _dismissLayer.SetSize(_stage.GetWidth(), _stage.GetHeight());
                _dismissLayer.SetVisible(true);
                _dismissLayer.ToFront();
            }
            ResetButtons();
            float menuX = position.X;
            float menuY = position.Y;
            if (menuX + _contextMenuWindow.GetWidth() > _stage.GetWidth())
                menuX = _stage.GetWidth() - _contextMenuWindow.GetWidth();
            if (menuY + _contextMenuWindow.GetHeight() > _stage.GetHeight())
                menuY = _stage.GetHeight() - _contextMenuWindow.GetHeight();
            _contextMenuWindow.SetPosition(menuX, menuY);
            _contextMenuWindow.SetVisible(true);
            _contextMenuWindow.ToFront();
        }

        /// <summary>Resets transient button state manually.</summary>
        private void ResetButtons()
        {
            _useButton.ResetVisualState();
            _discardButton.ResetVisualState();
            _cancelButton.ResetVisualState();
        }

        /// <summary>Hides context menu.</summary>
        public void Hide()
        {
            if (_contextMenuWindow != null)
                _contextMenuWindow.SetVisible(false);
            if (_dismissLayer != null)
                _dismissLayer.SetVisible(false);
            OnHidden?.Invoke();
        }

        /// <summary>
        /// Confirms the sell through the shared prompt, so this route and dragging the item onto the
        /// shop window show the identical dialog. Item and bag index are captured here rather than
        /// read at confirm time: the menu can be reused for another slot while the prompt is open.
        /// </summary>
        private void ShowDiscardConfirmation()
        {
            if (_currentItem == null)
                return;

            var item = _currentItem;
            var bagIndex = _currentBagIndex;

            ItemSellPrompt.Show(_stage, _skin, item,
                onSell: (qty) =>
                {
                    HideDismissLayer();
                    OnDiscardItem?.Invoke(item, bagIndex, qty);
                },
                onCancelled: HideDismissLayer);
        }

        /// <summary>Drops the outside-click overlay once the sell confirmation resolves.</summary>
        private void HideDismissLayer()
        {
            if (_dismissLayer != null)
                _dismissLayer.SetVisible(false);
        }

        /// <summary>
        /// True if the context menu itself is visible. The sell confirmation it spawns is owned by
        /// <see cref="UIPromptRegistry"/> — ask <c>ConfirmationDialog.AnyVisible</c> about that.
        /// </summary>
        public bool IsVisible()
        {
            return _contextMenuWindow != null && _contextMenuWindow.IsVisible();
        }

        /// <summary>Overlay to detect outside clicks.</summary>
        private class DismissLayer : Element, IInputListener
        {
            private readonly InventoryContextMenu _owner;
            public DismissLayer(InventoryContextMenu owner) { _owner = owner; SetTouchable(Touchable.Enabled); }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) { _owner.Hide(); return true; }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) { _owner.Hide(); return true; }
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            void IInputListener.OnMouseEnter() { }
            void IInputListener.OnMouseExit() { }
            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) { return false; }
        }
    }
}
