using Nez.UI;
using System;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// A reorderable table list using buttons for up/down movement
    /// </summary>
    public sealed class ReorderableTableList<T> : Table where T : class
    {
        private readonly List<T> _items;
        private readonly Skin _skin;
        private bool _grayed;

        public Action<int, int, T> OnReordered;

        public ReorderableTableList(Skin skin, List<T> items, Action<int, int, T> onReordered = null)
        {
            _skin = skin;
            _items = items;
            OnReordered = onReordered;
            Top().Left();
            Build();
        }

        public void Rebuild()
        {
            ClearChildren();
            Build();
        }

        /// <summary>
        /// Draws the list faded out with the reorder buttons deactivated. Used when the owning
        /// feature is switched off but the list stays on screen.
        /// </summary>
        public void SetGrayed(bool grayed)
        {
            if (_grayed == grayed)
                return;

            _grayed = grayed;
            Rebuild();
        }

        private void Build()
        {
            // All rows share this table's columns so the Up/Down buttons align across rows
            // regardless of each item's text width.
            for (int i = 0; i < _items.Count; i++)
            {
                AddRowCells(i, _items[i]);
                Row();
            }
            Pack();
        }

        private void AddRowCells(int index, T item)
        {
            var styleName = _grayed ? "ph-grayed" : "ph-default";

            // Priority number label
            var num = new Label((index + 1).ToString(), _skin, styleName);

            // Item text
            var txt = new Label(item?.ToString() ?? string.Empty, _skin, styleName);

            // Up button
            var upButton = new TextButton("Up", _skin, styleName);
            upButton.SetDisabled(_grayed || index == 0); // Disable if first item
            upButton.OnClicked += (btn) => MoveItemUp(index);

            // Down button
            var downButton = new TextButton("Down", _skin, styleName);
            downButton.SetDisabled(_grayed || index == _items.Count - 1); // Disable if last item
            downButton.OnClicked += (btn) => MoveItemDown(index);

            Add(num).SetMinWidth(30f).SetPadRight(5f).SetPadBottom(2f);
            Add(txt).Left().SetPadRight(5f).SetPadBottom(2f);
            Add(upButton).SetMinWidth(30f).SetMinHeight(16f).SetPadRight(2f).SetPadBottom(2f);
            Add(downButton).SetMinWidth(30f).SetMinHeight(16f).SetPadBottom(2f);
            Add().SetExpandX(); // spacer soaks leftover width so the buttons stay next to the text
        }

        private void MoveItemUp(int index)
        {
            if (index <= 0) return;

            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(index - 1, item);

            Rebuild();
            OnReordered?.Invoke(index, index - 1, item);
        }

        private void MoveItemDown(int index)
        {
            if (index >= _items.Count - 1) return;

            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(index + 1, item);

            Rebuild();
            OnReordered?.Invoke(index, index + 1, item);
        }
    }
}