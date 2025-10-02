using Microsoft.Xna.Framework;
using Nez;
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
        private Skin _skin; // store skin so we can rebuild menu to reset button state
        private IItem _currentItem;
        private int _currentBagIndex;
        private DismissLayer _dismissLayer; // overlay to detect outside clicks

        public event System.Action<IItem, int> OnUseItem;
        public event System.Action<IItem, int> OnDiscardItem;

        /// <summary>Initializes context menu windows.</summary>
        public void Initialize(Stage stage, Skin skin)
        {
            _stage = stage;
            _skin = skin;
            CreateContextMenu(_skin);
            CreateConfirmDialog(_skin);
        }

        /// <summary>Creates dismiss layer if needed.</summary>
        private void EnsureDismissLayer()
        {
            if (_dismissLayer != null) return;
            _dismissLayer = new DismissLayer(this);
            _dismissLayer.SetSize(_stage.GetWidth(), _stage.GetHeight());
            _stage.AddElement(_dismissLayer);
            _dismissLayer.SetVisible(false);
        }

        /// <summary>Creates the main context menu window.</summary>
        private void CreateContextMenu(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            _contextMenuWindow = new Window("", windowStyle);
            _contextMenuWindow.SetSize(120, 120); // increased height for Cancel button

            var table = new Table();
            table.Pad(10);

            var useButton = new TextButton("Use", skin);
            useButton.OnClicked += (btn) =>
            {
                Hide();
                if (_currentItem is Consumable)
                {
                    OnUseItem?.Invoke(_currentItem, _currentBagIndex);
                }
            };
            table.Add(useButton).Width(100).Height(25);
            table.Row();

            var discardButton = new TextButton("Discard", skin);
            discardButton.OnClicked += (btn) =>
            {
                Hide();
                ShowDiscardConfirmation();
            };
            table.Add(discardButton).Width(100).Height(25).SetPadTop(5);
            table.Row();

            var cancelButton = new TextButton("Cancel", skin);
            cancelButton.OnClicked += (btn) =>
            {
                Hide();
            };
            table.Add(cancelButton).Width(100).Height(25).SetPadTop(5);

            _contextMenuWindow.Add(table);
            _contextMenuWindow.SetVisible(false);
            _stage.AddElement(_contextMenuWindow);
        }

        /// <summary>Creates the discard confirmation dialog.</summary>
        private void CreateConfirmDialog(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            _confirmDialog = new Window("Really discard?", windowStyle);
            _confirmDialog.SetSize(250, 120);

            var table = new Table();
            table.Pad(10);

            table.Add(new Label("Discard this item?", skin)).SetPadBottom(10);
            table.Row();

            var buttonTable = new Table();

            var yesButton = new TextButton("Yes", skin);
            yesButton.OnClicked += (btn) =>
            {
                HideDiscardConfirmation();
                OnDiscardItem?.Invoke(_currentItem, _currentBagIndex);
            };
            buttonTable.Add(yesButton).Width(60);

            var noButton = new TextButton("No", skin);
            noButton.OnClicked += (btn) => HideDiscardConfirmation();
            buttonTable.Add(noButton).Width(60).SetPadLeft(10);

            table.Add(buttonTable);

            _confirmDialog.Add(table);
            _confirmDialog.SetVisible(false);
            _stage.AddElement(_confirmDialog);
        }

        /// <summary>Shows the context menu at a position.</summary>
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

            // Rebuild context menu window each show to reset button pressed/hover state
            if (_contextMenuWindow != null)
            {
                _contextMenuWindow.Remove();
            }
            CreateContextMenu(_skin);

            float menuX = position.X;
            float menuY = position.Y;

            if (menuX + _contextMenuWindow.GetWidth() > _stage.GetWidth())
                menuX = _stage.GetWidth() - _contextMenuWindow.GetWidth();
            if (menuY + _contextMenuWindow.GetHeight() > _stage.GetHeight())
                menuY = _stage.GetHeight() - _contextMenuWindow.GetHeight();

            _contextMenuWindow.SetPosition(menuX, menuY);
            _contextMenuWindow.SetVisible(true);
            _contextMenuWindow.ToFront();
            // layering: dismiss layer was added earlier; bringing menu ToFront keeps it above
        }

        /// <summary>Hides the context menu.</summary>
        public void Hide()
        {
            if (_contextMenuWindow != null)
                _contextMenuWindow.SetVisible(false);
            if (_dismissLayer != null)
                _dismissLayer.SetVisible(false);
        }

        /// <summary>Shows confirmation dialog centered.</summary>
        private void ShowDiscardConfirmation()
        {
            float centerX = (_stage.GetWidth() - _confirmDialog.GetWidth()) / 2;
            float centerY = (_stage.GetHeight() - _confirmDialog.GetHeight()) / 2;

            _confirmDialog.SetPosition(centerX, centerY);
            _confirmDialog.SetVisible(true);
            _confirmDialog.ToFront();
        }

        /// <summary>Hides discard confirmation dialog.</summary>
        private void HideDiscardConfirmation()
        {
            _confirmDialog.SetVisible(false);
            if (_dismissLayer != null)
                _dismissLayer.SetVisible(false);
        }

        /// <summary>Returns true if any menu window is visible.</summary>
        public bool IsVisible()
        {
            return (_contextMenuWindow != null && _contextMenuWindow.IsVisible()) || (_confirmDialog != null && _confirmDialog.IsVisible());
        }

        /// <summary>Overlay element to detect outside clicks.</summary>
        private class DismissLayer : Element, IInputListener
        {
            private readonly InventoryContextMenu _owner;
            public DismissLayer(InventoryContextMenu owner)
            {
                _owner = owner;
                SetTouchable(Touchable.Enabled);
            }

            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
            {
                _owner.Hide();
                return true;
            }

            bool IInputListener.OnRightMousePressed(Vector2 mousePos)
            {
                _owner.Hide();
                return true;
            }

            void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            void IInputListener.OnMouseEnter() { }
            void IInputListener.OnMouseExit() { }
            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) { return false; }
        }
    }
}
