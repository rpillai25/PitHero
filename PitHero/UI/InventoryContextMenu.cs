using Microsoft.Xna.Framework;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>Context menu for inventory slots with Use/Discard/Cancel options.</summary>
    public class InventoryContextMenu
    {
        private Window _contextMenuWindow;
        private Window _confirmDialog;
        private Stage _stage;
        private Skin _skin;
        private IItem _currentItem;
        private int _currentBagIndex;
        private DismissLayer _dismissLayer;
        private ResettableTextButton _useButton;
        private ResettableTextButton _discardButton;
        private ResettableTextButton _cancelButton;

        public event System.Action<IItem, int> OnUseItem;
        public event System.Action<IItem, int> OnDiscardItem;

        /// <summary>Initializes context menu windows.</summary>
        public void Initialize(Stage stage, Skin skin)
        {
            _stage = stage;
            _skin = skin;
            CreateContextMenu();
            CreateConfirmDialog();
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

            _discardButton = new ResettableTextButton("Discard", _skin);
            _discardButton.OnClicked += (btn) =>
            {
                Hide();
                ShowDiscardConfirmation();
            };
            table.Add(_discardButton).Width(100).Height(25).SetPadTop(5);
            table.Row();

            _cancelButton = new ResettableTextButton("Cancel", _skin);
            _cancelButton.OnClicked += (btn) => Hide();
            table.Add(_cancelButton).Width(100).Height(25).SetPadTop(5);

            _contextMenuWindow.Add(table);
            _contextMenuWindow.SetVisible(false);
            _stage.AddElement(_contextMenuWindow);
        }

        /// <summary>Creates discard confirmation dialog once.</summary>
        private void CreateConfirmDialog()
        {
            var windowStyle = _skin.Get<WindowStyle>();
            _confirmDialog = new Window("Really discard?", windowStyle);
            _confirmDialog.SetSize(250, 120);
            var table = new Table();
            table.Pad(10);
            table.Add(new Label("Discard this item?", _skin)).SetPadBottom(10);
            table.Row();
            var buttonTable = new Table();
            var yesButton = new TextButton("Yes", _skin);
            yesButton.OnClicked += (btn) =>
            {
                HideDiscardConfirmation();
                OnDiscardItem?.Invoke(_currentItem, _currentBagIndex);
            };
            buttonTable.Add(yesButton).Width(60);
            var noButton = new TextButton("No", _skin);
            noButton.OnClicked += (btn) => HideDiscardConfirmation();
            buttonTable.Add(noButton).Width(60).SetPadLeft(10);
            table.Add(buttonTable);
            _confirmDialog.Add(table);
            _confirmDialog.SetVisible(false);
            _stage.AddElement(_confirmDialog);
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
        }

        /// <summary>Shows discard confirmation.</summary>
        private void ShowDiscardConfirmation()
        {
            float centerX = (_stage.GetWidth() - _confirmDialog.GetWidth()) / 2f;
            float centerY = (_stage.GetHeight() - _confirmDialog.GetHeight()) / 2f;
            _confirmDialog.SetPosition(centerX, centerY);
            _confirmDialog.SetVisible(true);
            _confirmDialog.ToFront();
        }

        /// <summary>Hides discard confirmation.</summary>
        private void HideDiscardConfirmation()
        {
            _confirmDialog.SetVisible(false);
            if (_dismissLayer != null)
                _dismissLayer.SetVisible(false);
        }

        /// <summary>True if any menu window visible.</summary>
        public bool IsVisible()
        {
            return (_contextMenuWindow != null && _contextMenuWindow.IsVisible()) || (_confirmDialog != null && _confirmDialog.IsVisible());
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
