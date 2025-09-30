using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using PitHero.ECS.Components;
using RolePlayingFramework.Inventory;
// FastList is in Nez namespace

namespace PitHero.UI
{
    /// <summary>Grid layout container for inventory slots with interaction logic.</summary>
    public class InventoryGrid : Group
    {
        private const int GRID_WIDTH = 8;
        private const int GRID_HEIGHT = 7;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 1f;
        
        private readonly InventorySlotData[,] _slotGrid;        // Data definitions
        private readonly InventorySlot[,] _slotComponentGrid;    // Direct lookup for components
        private readonly FastList<InventorySlot> _slotComponents; // All created slot components
        private readonly FastList<InventorySlot> _orderedBagSlots; // Ordered bag slots (shortcut + inventory)
        private InventorySlot _highlightedSlot;
        private HeroComponent _heroComponent;
        private readonly FastList<IItem> _bagRebuildBuffer;      // Buffer for persisting bag ordering
        
        public InventoryGrid()
        {
            _slotGrid = new InventorySlotData[GRID_WIDTH, GRID_HEIGHT];
            _slotComponentGrid = new InventorySlot[GRID_WIDTH, GRID_HEIGHT];
            _slotComponents = new FastList<InventorySlot>(GRID_WIDTH * GRID_HEIGHT);
            _orderedBagSlots = new FastList<InventorySlot>(GRID_WIDTH * GRID_HEIGHT);
            _bagRebuildBuffer = new FastList<IItem>(GRID_WIDTH * GRID_HEIGHT);
            InitializeSlotGrid();
            CreateSlotComponents();
            LayoutSlots();
        }

        /// <summary>Connects grid to hero.</summary>
        public void ConnectToHero(HeroComponent heroComponent)
        {
            _heroComponent = heroComponent;
            if (_heroComponent?.Bag != null)
            {
                UpdateBagCapacity(_heroComponent.Bag.Capacity);
                UpdateItemsFromBag();
            }
        }

        /// <summary>Refresh items from hero bag.</summary>
        public void UpdateItemsFromBag()
        {
            if (_heroComponent?.Bag == null) return;
            for (int i = 0; i < _slotComponents.Length; i++)
                _slotComponents.Buffer[i].SlotData.Item = null;
            UpdateEquipmentSlots();
            UpdateBagSlots();
        }

        /// <summary>Populate equipment slots.</summary>
        private void UpdateEquipmentSlots()
        {
            var heroEquipment = _heroComponent.Entity.GetComponent<Hero>();
            if (heroEquipment == null) return;
            for (int i = 0; i < _slotComponents.Length; i++)
            {
                var slot = _slotComponents.Buffer[i];
                if (slot.SlotData.SlotType == InventorySlotType.Equipment)
                {
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
        }

        /// <summary>Populate bag slots (shortcut + inventory) row-major.</summary>
        private void UpdateBagSlots()
        {
            var bag = _heroComponent.Bag;
            _orderedBagSlots.Reset();
            for (int y = 0; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var comp = _slotComponentGrid[x, y];
                    if (comp == null) continue;
                    var type = comp.SlotData.SlotType;
                    if (type == InventorySlotType.Shortcut || type == InventorySlotType.Inventory)
                        _orderedBagSlots.Add(comp);
                }
            }
            for (int i = 0; i < _orderedBagSlots.Length; i++)
            {
                var slot = _orderedBagSlots.Buffer[i];
                slot.SlotData.BagIndex = i;
                slot.SlotData.Item = bag.GetSlotItem(i);
            }
        }

        /// <summary>Keyboard shortcuts.</summary>
        public void HandleKeyboardShortcuts()
        {
            for (int i = 0; i < 8; i++)
            {
                var key = (Keys)((int)Keys.D1 + i);
                if (Input.IsKeyPressed(key))
                {
                    InventorySlot shortcutSlot = null;
                    for (int s = 0; s < _slotComponents.Length; s++)
                    {
                        var sc = _slotComponents.Buffer[s];
                        if (sc.SlotData.SlotType == InventorySlotType.Shortcut && sc.SlotData.ShortcutKey == i + 1)
                        { shortcutSlot = sc; break; }
                    }
                    if (shortcutSlot != null && shortcutSlot.SlotData.Item != null)
                        ActivateShortcutSlot(shortcutSlot, i + 1);
                }
            }
        }

        /// <summary>Activate shortcut (placeholder).</summary>
        private void ActivateShortcutSlot(InventorySlot slot, int keyNumber)
        { Debug.Log($"Activated shortcut slot {keyNumber} with item: {slot.SlotData.Item?.Name ?? "None"}"); }

        /// <summary>Initialize logical slot grid.</summary>
        private void InitializeSlotGrid()
        {
            for (int x = 0; x < GRID_WIDTH; x++)
                for (int y = 0; y < GRID_HEIGHT; y++)
                    _slotGrid[x, y] = new InventorySlotData(x, y, InventorySlotType.Null);

            _slotGrid[3, 0] = new InventorySlotData(3, 0, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Hat };
            _slotGrid[1, 1] = new InventorySlotData(1, 1, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield1 };
            _slotGrid[3, 1] = new InventorySlotData(3, 1, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Armor };
            _slotGrid[5, 1] = new InventorySlotData(5, 1, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield2 };
            _slotGrid[2, 2] = new InventorySlotData(2, 2, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory1 };
            _slotGrid[4, 2] = new InventorySlotData(4, 2, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory2 };
            for (int x = 0; x < GRID_WIDTH; x++)
                _slotGrid[x, 3] = new InventorySlotData(x, 3, InventorySlotType.Shortcut) { ShortcutKey = x + 1 };
            for (int y = 4; y < GRID_HEIGHT; y++)
                for (int x = 0; x < GRID_WIDTH; x++)
                    _slotGrid[x, y] = new InventorySlotData(x, y, InventorySlotType.Inventory);
        }

        /// <summary>Create slot components.</summary>
        private void CreateSlotComponents()
        {
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    var slotData = _slotGrid[x, y];
                    if (slotData.SlotType == InventorySlotType.Null) continue;
                    var slotComponent = new InventorySlot(slotData);
                    slotComponent.OnSlotClicked += HandleSlotClicked;
                    slotComponent.OnSlotHovered += HandleSlotHovered;
                    slotComponent.OnSlotUnhovered += HandleSlotUnhovered;
                    _slotComponents.Add(slotComponent);
                    _slotComponentGrid[x, y] = slotComponent;
                    AddElement(slotComponent);
                }
            }
        }

        /// <summary>Layout slot components.</summary>
        private void LayoutSlots()
        {
            for (int i = 0; i < _slotComponents.Length; i++)
            {
                var slot = _slotComponents.Buffer[i];
                slot.SetPosition(slot.SlotData.X * (SLOT_SIZE + SLOT_PADDING), slot.SlotData.Y * (SLOT_SIZE + SLOT_PADDING));
            }
        }

        /// <summary>Handle slot click highlight/swap.</summary>
        private void HandleSlotClicked(InventorySlot clickedSlot)
        {
            if (_highlightedSlot == null)
            { _highlightedSlot = clickedSlot; clickedSlot.SlotData.IsHighlighted = true; Debug.Log($"Highlighted slot at ({clickedSlot.SlotData.X}, {clickedSlot.SlotData.Y})"); }
            else if (_highlightedSlot == clickedSlot)
            { _highlightedSlot.SlotData.IsHighlighted = false; _highlightedSlot = null; }
            else
            { var prev = _highlightedSlot; SwapSlotItems(_highlightedSlot, clickedSlot); _highlightedSlot.SlotData.IsHighlighted = false; _highlightedSlot = null; Debug.Log($"Swapped items between ({prev.SlotData.X},{prev.SlotData.Y}) and ({clickedSlot.SlotData.X},{clickedSlot.SlotData.Y})"); }
        }

        private void HandleSlotHovered(InventorySlot slot) { }
        private void HandleSlotUnhovered(InventorySlot slot) { }

        /// <summary>Swap two items and persist.</summary>
        private void SwapSlotItems(InventorySlot slot1, InventorySlot slot2)
        {
            if (!CanPlaceItemInSlot(slot1.SlotData.Item, slot2.SlotData) || !CanPlaceItemInSlot(slot2.SlotData.Item, slot1.SlotData)) return;
            var item1 = slot1.SlotData.Item; var item2 = slot2.SlotData.Item; slot1.SlotData.Item = item2; slot2.SlotData.Item = item1; UpdateHeroDataFromSlot(slot1); UpdateHeroDataFromSlot(slot2); PersistBagOrdering();
        }

        /// <summary>Persist bag ordering to ItemBag.</summary>
        private void PersistBagOrdering()
        {
            if (_heroComponent?.Bag == null) return;
            _bagRebuildBuffer.Reset();
            for (int y = 0; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var comp = _slotComponentGrid[x, y];
                    if (comp == null) continue;
                    var type = comp.SlotData.SlotType;
                    if (type == InventorySlotType.Shortcut || type == InventorySlotType.Inventory)
                        _bagRebuildBuffer.Add(comp.SlotData.Item);
                }
            }
            // zero-allocation bag update
            _heroComponent.Bag.SetItemsInOrder(_bagRebuildBuffer.Buffer, _bagRebuildBuffer.Length);
        }

        /// <summary>Validate placing an item in a slot.</summary>
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

        /// <summary>Check weapon/shield type.</summary>
        private bool IsWeaponOrShield(IItem item)
        { return item.Kind == ItemKind.WeaponSword || item.Kind == ItemKind.WeaponKnuckle || item.Kind == ItemKind.WeaponStaff || item.Kind == ItemKind.WeaponRod || item.Kind == ItemKind.Shield; }

        /// <summary>Update hero data for equipment slots.</summary>
        private void UpdateHeroDataFromSlot(InventorySlot slot)
        {
            var heroEquipment = _heroComponent?.Entity.GetComponent<Hero>(); if (heroEquipment == null) return;
            var sd = slot.SlotData; if (sd.SlotType == InventorySlotType.Equipment) UpdateHeroEquipment(heroEquipment, sd);
        }

        /// <summary>Equip or unequip item.</summary>
        private void UpdateHeroEquipment(Hero heroEquipment, InventorySlotData slotData)
        { var item = slotData.Item; if (item != null) heroEquipment.TryEquip(item); else if (slotData.EquipmentSlot.HasValue) heroEquipment.TryUnequip(slotData.EquipmentSlot.Value); }

        /// <summary>Adjust slot visibility based on capacity.</summary>
        public void UpdateBagCapacity(int capacity)
        {
            int inventorySlotCount = capacity - 8; int currentInventorySlot = 0;
            for (int y = 4; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var slotData = _slotGrid[x, y]; if (slotData.SlotType != InventorySlotType.Inventory) continue;
                    currentInventorySlot++; if (currentInventorySlot > inventorySlotCount)
                    { slotData.SlotType = InventorySlotType.Null; var comp = _slotComponentGrid[x, y]; if (comp != null)
                        { for (int i = 0; i < _slotComponents.Length; i++) { if (_slotComponents.Buffer[i] == comp) { _slotComponents.RemoveAt(i); break; } } RemoveElement(comp); _slotComponentGrid[x, y] = null; } }
                }
            }
        }

        /// <summary>Find next empty slot.</summary>
        public InventorySlotData FindNextAvailableSlot()
        {
            for (int x = 0; x < GRID_WIDTH; x++) { var slot = _slotGrid[x, 3]; if (slot.SlotType == InventorySlotType.Shortcut && slot.Item == null) return slot; }
            for (int y = 4; y < GRID_HEIGHT; y++) for (int x = 0; x < GRID_WIDTH; x++) { var slot = _slotGrid[x, y]; if (slot.SlotType == InventorySlotType.Inventory && slot.Item == null) return slot; }
            return null;
        }
    }
}