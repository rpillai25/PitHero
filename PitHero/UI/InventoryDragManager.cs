using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Manages global drag-and-drop state for inventory, equip, and shortcut slots.
    /// Works alongside InventorySelectionManager for cross-component coordination.
    /// </summary>
    public static class InventoryDragManager
    {
        private static bool _isDragging;
        private static InventorySlot _sourceSlot;
        private static IItem _dragItem;
        private static DragDropOverlay _overlay;
        private static Stage _stage;

        /// <summary>Gets whether a drag operation is currently in progress.</summary>
        public static bool IsDragging => _isDragging;

        /// <summary>Gets the inventory slot that the drag originated from.</summary>
        public static InventorySlot SourceSlot => _sourceSlot;

        /// <summary>Gets the item being dragged.</summary>
        public static IItem DragItem => _dragItem;

        /// <summary>
        /// Fired when the source component found no local drop target.
        /// ShortcutBar subscribes to handle inventory-to-shortcut drops.
        /// InventoryGrid subscribes to handle shortcut-to-inventory drops.
        /// </summary>
        public static System.Action<InventorySlot, Vector2> OnDropRequested;

        /// <summary>Begins a drag from an inventory slot.</summary>
        public static void BeginDrag(InventorySlot source, Stage stage)
        {
            if (_isDragging) return;

            _sourceSlot = source;
            _dragItem = source?.SlotData?.Item;
            _stage = stage;
            _isDragging = true;

            if (_overlay == null)
            {
                _overlay = new DragDropOverlay();
                stage.AddElement(_overlay);
                _overlay.SetVisible(false);
            }
            else if (_overlay.GetStage() != stage)
            {
                _overlay.Remove();
                stage.AddElement(_overlay);
                _overlay.SetVisible(false);
            }
            _overlay.ToFront();

            if (_dragItem != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var sprite = itemsAtlas.GetSprite(_dragItem.SpriteName);
                    if (sprite != null)
                        _overlay.BeginDrag(new SpriteDrawable(sprite));
                    else
                        _overlay.BeginDrag(null);
                }
                catch
                {
                    _overlay.BeginDrag(null);
                }
            }
        }

        /// <summary>Updates the overlay position to follow the cursor.</summary>
        public static void UpdateDrag(Vector2 stagePos)
        {
            _overlay?.UpdatePosition(stagePos);
        }

        /// <summary>Ends the drag without cancelling — used after a successful drop.</summary>
        public static void EndDrag()
        {
            _isDragging = false;
            _sourceSlot = null;
            _dragItem = null;
            _overlay?.EndDrag();
        }

        /// <summary>Cancels the drag and makes the item reappear in its source slot.</summary>
        public static void CancelDrag()
        {
            if (_sourceSlot != null)
                _sourceSlot.SetItemSpriteHidden(false);
            _isDragging = false;
            _sourceSlot = null;
            _dragItem = null;
            _overlay?.EndDrag();
        }

        /// <summary>Called by a component when it could not handle a drop locally.</summary>
        public static void NotifyDropRequested(InventorySlot source, Vector2 stagePos)
        {
            OnDropRequested?.Invoke(source, stagePos);
        }
    }
}
