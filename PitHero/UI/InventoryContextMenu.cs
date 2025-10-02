using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>Context menu for inventory slots with Use/Discard options.</summary>
    public class InventoryContextMenu
    {
        private Window _contextMenuWindow;
        private Window _confirmDialog;
        private Stage _stage;
        private IItem _currentItem;
        private int _currentBagIndex;

        public event System.Action<IItem, int> OnUseItem;
        public event System.Action<IItem, int> OnDiscardItem;

        public void Initialize(Stage stage, Skin skin)
        {
            _stage = stage;
            CreateContextMenu(skin);
            CreateConfirmDialog(skin);
        }

        private void CreateContextMenu(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            _contextMenuWindow = new Window("", windowStyle);
            _contextMenuWindow.SetSize(120, 90);

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

            _contextMenuWindow.Add(table);
            _contextMenuWindow.SetVisible(false);
            _stage.AddElement(_contextMenuWindow);
        }

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

        public void Show(IItem item, int bagIndex, Vector2 position)
        {
            _currentItem = item;
            _currentBagIndex = bagIndex;

            // Position menu near the click position
            float menuX = position.X;
            float menuY = position.Y;

            // Keep menu on screen
            if (menuX + _contextMenuWindow.GetWidth() > _stage.GetWidth())
                menuX = _stage.GetWidth() - _contextMenuWindow.GetWidth();
            if (menuY + _contextMenuWindow.GetHeight() > _stage.GetHeight())
                menuY = _stage.GetHeight() - _contextMenuWindow.GetHeight();

            _contextMenuWindow.SetPosition(menuX, menuY);
            _contextMenuWindow.SetVisible(true);
            _contextMenuWindow.ToFront();
        }

        public void Hide()
        {
            _contextMenuWindow.SetVisible(false);
        }

        private void ShowDiscardConfirmation()
        {
            // Center the dialog on screen
            float centerX = (_stage.GetWidth() - _confirmDialog.GetWidth()) / 2;
            float centerY = (_stage.GetHeight() - _confirmDialog.GetHeight()) / 2;

            _confirmDialog.SetPosition(centerX, centerY);
            _confirmDialog.SetVisible(true);
            _confirmDialog.ToFront();
        }

        private void HideDiscardConfirmation()
        {
            _confirmDialog.SetVisible(false);
        }

        public bool IsVisible()
        {
            return _contextMenuWindow.IsVisible() || _confirmDialog.IsVisible();
        }
    }
}
