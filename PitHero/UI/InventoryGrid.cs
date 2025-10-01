using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>Grid layout container for inventory slots with interaction logic (single linear buffer version).</summary>
    public class InventoryGrid : Group
    {
        private const int GRID_WIDTH = 8;
        private const int GRID_HEIGHT = 7;
        private const int CELL_COUNT = GRID_WIDTH * GRID_HEIGHT;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 1f;

        private readonly FastList<InventorySlot> _slots;   // Row-major, may contain nulls for Null slots or capacity-disabled slots
        private readonly IItem[] _persistBuffer;           // Reusable buffer for bag ordering persistence (32 max bag capacity)
        private HeroComponent _heroComponent;
        private InventorySlot _highlightedSlot;

        // Public events for item card display
        public event System.Action<IItem> OnItemHovered;
        public event System.Action OnItemUnhovered;
        public event System.Action<IItem> OnItemSelected;
        public event System.Action OnItemDeselected;

        public InventoryGrid()
        {
            _slots = new FastList<InventorySlot>(CELL_COUNT);
            _persistBuffer = new IItem[CELL_COUNT];
            BuildSlots();
            LayoutSlots();
        }

        /// <summary>Returns true if any slot is currently hovered.</summary>
        public bool HasAnyHoveredSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                if (slot.SlotData.IsHovered) return true;
            }
            return false;
        }

        /// <summary>Builds all slot components in row-major order (adds null placeholders for Null slots).</summary>
        private void BuildSlots()
        {
            for (int y = 0; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var data = CreateSlotData(x, y);
                    if (data.SlotType == InventorySlotType.Null)
                    {
                        _slots.Add(null); // placeholder to keep index mapping intact
                        continue;
                    }
                    var slot = new InventorySlot(data);
                    slot.OnSlotClicked += HandleSlotClicked;
                    slot.OnSlotHovered += HandleSlotHovered;
                    slot.OnSlotUnhovered += HandleSlotUnhovered;
                    _slots.Add(slot);
                    AddElement(slot);
                }
            }
        }

        /// <summary>Creates slot data for a given grid coordinate.</summary>
        private InventorySlotData CreateSlotData(int x, int y)
        {
            // Equipment layout
            if (x == 3 && y == 0) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Hat };
            if (y == 1 && x == 1) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield1 };
            if (y == 1 && x == 3) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Armor };
            if (y == 1 && x == 5) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield2 };
            if (y == 2 && x == 2) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory1 };
            if (y == 2 && x == 4) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory2 };
            // Shortcuts row
            if (y == 3) return new InventorySlotData(x, y, InventorySlotType.Shortcut) { ShortcutKey = x + 1 };
            // Inventory rows (4-6)
            if (y >= 4) return new InventorySlotData(x, y, InventorySlotType.Inventory);
            // Everything else is Null spacer
            return new InventorySlotData(x, y, InventorySlotType.Null);
        }

        /// <summary>Connects grid to hero and loads items.</summary>
        public void ConnectToHero(HeroComponent heroComponent)
        {
            _heroComponent = heroComponent;
            if (_heroComponent?.Bag != null)
            {
                UpdateBagCapacity(_heroComponent.Bag.Capacity);
                UpdateItemsFromBag();
            }
        }

        /// <summary>Refreshes items from hero state.</summary>
        public void UpdateItemsFromBag()
        {
            if (_heroComponent?.Bag == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot != null)
                    slot.SlotData.Item = null;
            }
            UpdateEquipmentSlots();
            UpdateBagSlots();
        }

        /// <summary>Updates equipment slot items from hero equipment.</summary>
        private void UpdateEquipmentSlots()
        {
            var heroEquipment = _heroComponent?.Entity.GetComponent<Hero>();
            if (heroEquipment == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                if (slot.SlotData.SlotType != InventorySlotType.Equipment) continue;
                switch (slot.SlotData.EquipmentSlot)
                {
                    case EquipmentSlot.WeaponShield1: slot.SlotData.Item = heroEquipment.WeaponShield1; break;
                    case EquipmentSlot.Armor: slot.SlotData.Item = heroEquipment.Armor; break;
                    case EquipmentSlot.Hat: slot.SlotData.Item = heroEquipment.Hat; break;
                    case EquipmentSlot.WeaponShield2: slot.SlotData.Item = heroEquipment.WeaponShield2; break;
                    case EquipmentSlot.Accessory1: slot.SlotData.Item = heroEquipment.Accessory1; break;
                    case EquipmentSlot.Accessory2: slot.SlotData.Item = heroEquipment.Accessory2; break;
                }
            }
        }

        /// <summary>Populates bag slots with items using 1:1 index -> bag index mapping.</summary>
        private void UpdateBagSlots()
        {
            var bag = _heroComponent.Bag;
            int bagIndex = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var type = slot.SlotData.SlotType;
                if (type == InventorySlotType.Shortcut || type == InventorySlotType.Inventory)
                {
                    slot.SlotData.BagIndex = bagIndex;
                    slot.SlotData.Item = bag.GetSlotItem(bagIndex);
                    bagIndex++;
                }
            }
        }

        /// <summary>Handles shortcut key presses (1-8).</summary>
        public void HandleKeyboardShortcuts()
        {
            for (int keyOffset = 0; keyOffset < 8; keyOffset++)
            {
                var key = (Keys)((int)Keys.D1 + keyOffset);
                if (!Input.IsKeyPressed(key)) continue;
                for (int i = 0; i < _slots.Length; i++)
                {
                    var slot = _slots.Buffer[i];
                    if (slot == null) continue;
                    var data = slot.SlotData;
                    if (data.SlotType == InventorySlotType.Shortcut && data.ShortcutKey == keyOffset + 1 && data.Item != null)
                    {
                        Debug.Log($"Activated shortcut slot {data.ShortcutKey} with item: {data.Item.Name}");
                        break;
                    }
                }
            }
        }

        /// <summary>Positions slot components based on grid coordinates.</summary>
        private void LayoutSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var data = slot.SlotData;
                slot.SetPosition(data.X * (SLOT_SIZE + SLOT_PADDING), data.Y * (SLOT_SIZE + SLOT_PADDING));
            }
        }

        /// <summary>Handles slot click highlighting and swapping.</summary>
        private void HandleSlotClicked(InventorySlot clickedSlot)
        {
            if (_highlightedSlot == null)
            {
                _highlightedSlot = clickedSlot;
                clickedSlot.SlotData.IsHighlighted = true;
                Debug.Log($"Highlighted slot at ({clickedSlot.SlotData.X},{clickedSlot.SlotData.Y})");
                
                // Notify that an item was selected
                if (clickedSlot.SlotData.Item != null)
                {
                    OnItemSelected?.Invoke(clickedSlot.SlotData.Item);
                }
            }
            else if (_highlightedSlot == clickedSlot)
            {
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                
                // Notify that the item was deselected
                OnItemDeselected?.Invoke();
            }
            else
            {
                var prev = _highlightedSlot;
                SwapSlotItems(_highlightedSlot, clickedSlot);
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                Debug.Log($"Swapped items between ({prev.SlotData.X},{prev.SlotData.Y}) and ({clickedSlot.SlotData.X},{clickedSlot.SlotData.Y})");
                
                // Notify that the selection was cleared
                OnItemDeselected?.Invoke();
            }
        }

        private void HandleSlotHovered(InventorySlot slot)
        {
            if (slot.SlotData.Item != null)
            {
                OnItemHovered?.Invoke(slot.SlotData.Item);
            }
        }

        private void HandleSlotUnhovered(InventorySlot slot)
        {
            OnItemUnhovered?.Invoke();
        }

        /// <summary>Swaps two slot items (if legal) and persists bag ordering.</summary>
        private void SwapSlotItems(InventorySlot a, InventorySlot b)
        {
            if (!CanPlaceItemInSlot(a.SlotData.Item, b.SlotData) || !CanPlaceItemInSlot(b.SlotData.Item, a.SlotData))
                return;
            var tmp = a.SlotData.Item;
            a.SlotData.Item = b.SlotData.Item;
            b.SlotData.Item = tmp;
            UpdateHeroDataFromSlot(a);
            UpdateHeroDataFromSlot(b);
            PersistBagOrdering();
        }

        /// <summary>Persists current bag ordering to ItemBag via raw buffer (no allocations).</summary>
        private void PersistBagOrdering()
        {
            if (_heroComponent?.Bag == null) return;
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var type = slot.SlotData.SlotType;
                if (type == InventorySlotType.Shortcut || type == InventorySlotType.Inventory)
                {
                    _persistBuffer[count++] = slot.SlotData.Item;
                }
            }
            // Clear remainder of buffer to avoid stale references (optional safety)
            for (int i = count; i < _persistBuffer.Length; i++)
                _persistBuffer[i] = null;
            _heroComponent.Bag.SetItemsInOrder(_persistBuffer, count);
        }

        /// <summary>Validates whether an item can be placed in target slot data.</summary>
        private bool CanPlaceItemInSlot(IItem item, InventorySlotData slotData)
        {
            if (item == null) return true;
            if (slotData.SlotType != InventorySlotType.Equipment) return true;
            switch (slotData.EquipmentSlot)
            {
                case EquipmentSlot.WeaponShield1:
                case EquipmentSlot.WeaponShield2: return IsWeaponOrShield(item);
                case EquipmentSlot.Armor: return item.Kind == ItemKind.ArmorMail || item.Kind == ItemKind.ArmorRobe || item.Kind == ItemKind.ArmorGi;
                case EquipmentSlot.Hat: return item.Kind == ItemKind.HatHelm || item.Kind == ItemKind.HatHeadband || item.Kind == ItemKind.HatWizard || item.Kind == ItemKind.HatPriest;
                case EquipmentSlot.Accessory1:
                case EquipmentSlot.Accessory2: return item.Kind == ItemKind.Accessory;
                default: return false;
            }
        }

        /// <summary>Checks if item is a weapon or shield.</summary>
        private bool IsWeaponOrShield(IItem item)
        {
            return item.Kind == ItemKind.WeaponSword || item.Kind == ItemKind.WeaponKnuckle || item.Kind == ItemKind.WeaponStaff || item.Kind == ItemKind.WeaponRod || item.Kind == ItemKind.Shield;
        }

        /// <summary>Updates hero equipment when equipment slot changed.</summary>
        private void UpdateHeroDataFromSlot(InventorySlot slot)
        {
            var heroEquipment = _heroComponent?.Entity.GetComponent<Hero>();
            if (heroEquipment == null) return;
            var d = slot.SlotData;
            if (d.SlotType != InventorySlotType.Equipment) return;
            if (d.Item != null) heroEquipment.TryEquip(d.Item); else if (d.EquipmentSlot.HasValue) heroEquipment.TryUnequip(d.EquipmentSlot.Value);
        }

        /// <summary>Updates active inventory slot availability based on capacity.</summary>
        public void UpdateBagCapacity(int capacity)
        {
            // Bag capacity includes first 8 shortcut slots + inventory slots.
            int allowedInventorySlots = capacity - 8;
            if (allowedInventorySlots < 0) allowedInventorySlots = 0;
            int inventorySeen = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var data = slot.SlotData;
                if (data.SlotType == InventorySlotType.Inventory)
                {
                    inventorySeen++;
                    if (inventorySeen > allowedInventorySlots)
                    {
                        // Disable this slot visually and logically
                        data.SlotType = InventorySlotType.Null;
                        data.Item = null;
                        slot.Remove(); // remove from stage so user cannot interact
                    }
                }
            }
        }

        /// <summary>Finds next empty usable bag slot (shortcut first then inventory).</summary>
        public InventorySlotData FindNextAvailableSlot()
        {
            // Shortcuts
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i]; if (slot == null) continue;
                var d = slot.SlotData;
                if (d.SlotType == InventorySlotType.Shortcut && d.Item == null) return d;
            }
            // Inventory
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i]; if (slot == null) continue;
                var d = slot.SlotData;
                if (d.SlotType == InventorySlotType.Inventory && d.Item == null) return d;
            }
            return null;
        }
    }
}