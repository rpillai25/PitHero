using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// A reorderable table list using buttons for up/down movement
    /// </summary>
    public sealed class ReorderableTableList<T> : Table where T : class
    {
        private readonly List<T> _items;
        private readonly Skin _skin;

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

        private void Build()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var row = MakeRow(i, _items[i]);
                Add(row).SetFillX().SetPadBottom(2f);
                Row();
            }
            Pack();
        }

        private Table MakeRow(int index, T item)
        {
            var row = new Table();
            row.SetTouchable(Touchable.Enabled);
            
            // Priority number label
            var num = new Label((index + 1).ToString(), _skin.Get<LabelStyle>());
            
            // Item text
            var txt = new Label(item?.ToString() ?? string.Empty, _skin.Get<LabelStyle>());
            
            // Up button
            var upButton = new TextButton("↑", _skin);
            upButton.SetDisabled(index == 0); // Disable if first item
            upButton.OnClicked += (btn) => MoveItemUp(index);
            
            // Down button  
            var downButton = new TextButton("↓", _skin);
            downButton.SetDisabled(index == _items.Count - 1); // Disable if last item
            downButton.OnClicked += (btn) => MoveItemDown(index);
            
            row.Add(num).SetMinWidth(30f).SetPadRight(5f);
            row.Add(txt).SetExpandX().Left().SetPadRight(5f);
            row.Add(upButton).SetMinWidth(30f).SetPadRight(2f);
            row.Add(downButton).SetMinWidth(30f);
            
            return row;
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