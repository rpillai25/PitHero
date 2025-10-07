using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

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
        private const float HOVER_OFFSET_Y = -16f; // Offset in pixels when hovering over a slot while another is selected
        private const float SWAP_TWEEN_DURATION = 0.2f; // Duration in seconds for swap animation

        private readonly FastList<InventorySlot> _slots;   // Row-major, may contain nulls for Null slots or capacity-disabled slots
        private readonly IItem[] _persistBuffer;           // Reusable buffer for bag ordering persistence (32 max bag capacity)
        private HeroComponent _heroComponent;
        private InventorySlot _highlightedSlot;
        private InventoryContextMenu _contextMenu;
        private Stage _stage; // Reference to stage for tooltip management
        
        // Swap animation entities (scene-space approach retained but unused currently)
        private Entity _swapEntity1;
        private Entity _swapEntity2;
        private SpriteRenderer _swapRenderer1;
        private SpriteRenderer _swapRenderer2;

        // UI-based swap animation state
        private bool _uiSwapActive;
        private float _uiSwapElapsed;
        private InventorySlot _uiSwapSlotA;
        private InventorySlot _uiSwapSlotB;
        private SpriteDrawable _uiSwapDrawableA;
        private SpriteDrawable _uiSwapDrawableB;
        private Vector2 _uiSwapStartA;
        private Vector2 _uiSwapEndA;
        private Vector2 _uiSwapStartB;
        private Vector2 _uiSwapEndB;

        // Public events for item card display
        public event System.Action<IItem> OnItemHovered;
        public event System.Action OnItemUnhovered;
        public event System.Action<IItem> OnItemSelected;
        public event System.Action OnItemDeselected;

        private int _nextAcquireIndex = 1; // monotonic acquisition counter
        private readonly Dictionary<IItem, int> _acquireIndexMap; // persistent mapping of items to acquire indices
        private readonly Dictionary<IItem, int> _itemStackMap;    // last known stack count per item instance

        // Sorting state
        private InventorySortOrder _currentSortOrder = InventorySortOrder.Time;
        private SortDirection _currentSortDirection = SortDirection.Descending;

        // Event for sort order changed
        public event System.Action<InventorySortOrder, SortDirection> OnSortOrderChanged;

        public InventoryGrid()
        {
            _slots = new FastList<InventorySlot>(CELL_COUNT);
            _persistBuffer = new IItem[CELL_COUNT];
            _acquireIndexMap = new Dictionary<IItem, int>(64);
            _itemStackMap = new Dictionary<IItem, int>(64);
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
                    slot.OnSlotDoubleClicked += HandleSlotDoubleClicked;
                    slot.OnSlotHovered += HandleSlotHovered;
                    slot.OnSlotUnhovered += HandleSlotUnhovered;
                    slot.OnSlotRightClicked += HandleSlotRightClicked;
                    _slots.Add(slot);
                    AddElement(slot);
                }
            }
        }

        /// <summary>Creates slot data for a given grid coordinate.</summary>
        private InventorySlotData CreateSlotData(int x, int y)
        {
            if (x == 3 && y == 0) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Hat };
            if (y == 1 && x == 1) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield1 };
            if (y == 1 && x == 3) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Armor };
            if (y == 1 && x == 5) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield2 };
            if (y == 2 && x == 2) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory1 };
            if (y == 2 && x == 4) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory2 };
            if (y == 3) return new InventorySlotData(x, y, InventorySlotType.Shortcut) { ShortcutKey = x + 1 };
            if (y >= 4) return new InventorySlotData(x, y, InventorySlotType.Inventory);
            return new InventorySlotData(x, y, InventorySlotType.Null);
        }

        /// <summary>Connects grid to hero and loads items.</summary>
        public void ConnectToHero(HeroComponent heroComponent)
        {
            // Only reset mappings if connecting to a different hero instance
            if (!object.ReferenceEquals(_heroComponent, heroComponent))
            {
                _acquireIndexMap.Clear();
                _itemStackMap.Clear();
                _nextAcquireIndex = 1;
            }

            _heroComponent = heroComponent;
            if (_heroComponent?.Bag != null)
            {
                UpdateBagCapacity(_heroComponent.Bag.Capacity);
                UpdateItemsFromBag();
            }
            InitializeSwapEntities();
        }
        
        /// <summary>Initializes the context menu for inventory interactions.</summary>
        public void InitializeContextMenu(Stage stage, Skin skin)
        {
            if (_contextMenu != null) return;
            
            _stage = stage;
            _contextMenu = new InventoryContextMenu();
            _contextMenu.Initialize(stage, skin);
            _contextMenu.OnUseItem += (item, bagIndex) => UseConsumable(item, bagIndex);
            _contextMenu.OnDiscardItem += (item, bagIndex) => DiscardItem(bagIndex);
            
            // Add placeholder tooltips to stage
            AddPlaceholderTooltipsToStage();
        }
        
        /// <summary>Adds placeholder tooltips for empty equipment slots to the stage.</summary>
        private void AddPlaceholderTooltipsToStage()
        {
            if (_stage == null) return;
            
            // Configure TooltipManager globally to avoid animations and delays
            var tm = TooltipManager.GetInstance();
            tm.Animations = false;
            tm.InitialTime = 0f;
            tm.SubsequentTime = 0f;
            
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                
                // Get the placeholder tooltip from the slot if it's an equipment slot
                var tooltip = slot.GetPlaceholderTooltip();
                if (tooltip != null)
                {
                    _stage.AddElement(tooltip);
                }
            }
        }
        
        /// <summary>Initializes scene entities for swap animation (unused currently).</summary>
        private void InitializeSwapEntities()
        {
            if (_swapEntity1 != null) return;
            var scene = GetStage()?.Entity?.Scene;
            if (scene == null) return;
            _swapEntity1 = scene.CreateEntity("SwapAnimEntity1");
            _swapEntity1.Position = new Vector2(-1000, -1000);
            _swapRenderer1 = _swapEntity1.AddComponent(new SpriteRenderer());
            _swapRenderer1.RenderLayer = GameConfig.RenderLayerUI - 1;
            _swapRenderer1.Enabled = false;
            _swapEntity2 = scene.CreateEntity("SwapAnimEntity2");
            _swapEntity2.Position = new Vector2(-1000, -1000);
            _swapRenderer2 = _swapEntity2.AddComponent(new SpriteRenderer());
            _swapRenderer2.RenderLayer = GameConfig.RenderLayerUI - 1;
            _swapRenderer2.Enabled = false;
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
            var heroEquipment = _heroComponent?.LinkedHero;
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
                    var newItem = bag.GetSlotItem(bagIndex);

                    // Assign item to slot
                    slot.SlotData.Item = newItem;

                    if (newItem != null)
                    {
                        // Ensure unique, monotonically increasing acquire index per item instance
                        int idx;
                        if (!_acquireIndexMap.TryGetValue(newItem, out idx))
                        {
                            idx = _nextAcquireIndex++;
                            _acquireIndexMap[newItem] = idx;
                        }

                        // If consumable, detect stack increase per item (not per slot)
                        if (newItem is Consumable consumable)
                        {
                            int lastKnown;
                            if (!_itemStackMap.TryGetValue(newItem, out lastKnown))
                            {
                                lastKnown = consumable.StackCount;
                                _itemStackMap[newItem] = lastKnown;
                            }
                            if (consumable.StackCount > lastKnown)
                            {
                                idx = _nextAcquireIndex++;
                                _acquireIndexMap[newItem] = idx;
                                _itemStackMap[newItem] = consumable.StackCount;
                            }
                            // Update slot-visible stack count always
                            slot.SlotData.StackCount = consumable.StackCount;
                        }
                        else
                        {
                            slot.SlotData.StackCount = 0;
                        }

                        // Non-null items must never have AcquireIndex 0
                        if (idx <= 0)
                        {
                            idx = _nextAcquireIndex++;
                            _acquireIndexMap[newItem] = idx;
                        }
                        slot.SlotData.AcquireIndex = idx;
                    }
                    else
                    {
                        // Empty slots get AcquireIndex 0
                        slot.SlotData.AcquireIndex = 0;
                        slot.SlotData.StackCount = 0;
                    }

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
                        
                        // Use the consumable if it's a consumable
                        if (data.Item is Consumable && data.BagIndex.HasValue)
                        {
                            UseConsumable(data.Item, data.BagIndex.Value);
                        }
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
                if (clickedSlot.SlotData.Item != null)
                    OnItemSelected?.Invoke(clickedSlot.SlotData.Item);
            }
            else if (_highlightedSlot == clickedSlot)
            {
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                OnItemDeselected?.Invoke();
            }
            else
            {
                var prev = _highlightedSlot;
                SwapSlotItems(_highlightedSlot, clickedSlot);
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                Debug.Log($"Swapped items between ({prev.SlotData.X},{prev.SlotData.Y}) and ({clickedSlot.SlotData.X},{clickedSlot.SlotData.Y})");
                OnItemDeselected?.Invoke();
            }
        }

        private void HandleSlotHovered(InventorySlot slot)
        {
            if (_highlightedSlot != null && _highlightedSlot != slot && slot.SlotData.Item != null)
                slot.SetItemSpriteOffsetY(HOVER_OFFSET_Y);
            if (slot.SlotData.Item != null)
                OnItemHovered?.Invoke(slot.SlotData.Item);
        }

        private void HandleSlotUnhovered(InventorySlot slot)
        {
            slot.SetItemSpriteOffsetY(0f);
            OnItemUnhovered?.Invoke();
        }

        /// <summary>Handles double-click to use consumables or equip/unequip gear.</summary>
        private void HandleSlotDoubleClicked(InventorySlot slot)
        {
            // Equipment slot: attempt to unequip to first empty bag slot
            if (slot.SlotData.SlotType == InventorySlotType.Equipment)
            {
                if (slot.SlotData.Item != null)
                {
                    var emptySlot = FindFirstEmptyBagSlot();
                    if (emptySlot != null)
                    {
                        SwapSlotItems(slot, emptySlot);
                    }
                }
                // always clear selection/highlight after a double-click
                ClearSelectionHighlight();
                return;
            }
            
            // Bag slots: use consumables or equip gear
            if (slot.SlotData.Item is Consumable && slot.SlotData.BagIndex.HasValue)
            {
                UseConsumable(slot.SlotData.Item, slot.SlotData.BagIndex.Value);
            }
            else if (slot.SlotData.Item is IGear gear)
            {
                var targetEquipmentSlot = FindTargetEquipmentSlot(gear);
                if (targetEquipmentSlot != null)
                {
                    SwapSlotItems(slot, targetEquipmentSlot);
                }
            }
            
            // always clear selection/highlight after a double-click
            ClearSelectionHighlight();
        }

        /// <summary>Clears current highlighted slot and notifies deselection.</summary>
        private void ClearSelectionHighlight()
        {
            if (_highlightedSlot != null)
            {
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                OnItemDeselected?.Invoke();
            }
        }

        /// <summary>Handles right-click to show context menu.</summary>
        private void HandleSlotRightClicked(InventorySlot slot, Vector2 mousePos)
        {
            // Only show context menu for non-equipment slots with items
            if (slot.SlotData.SlotType == InventorySlotType.Equipment)
                return;
            
            if (slot.SlotData.Item != null && _contextMenu != null && slot.SlotData.BagIndex.HasValue)
            {
                // Get the slot's top-left in stage coordinates
                var stageTopLeft = slot.LocalToStageCoordinates(Vector2.Zero);
                // Position menu to the right of the slot with small padding
                var desiredPos = new Vector2(stageTopLeft.X + slot.GetWidth() + 4f, stageTopLeft.Y);
                _contextMenu.Show(slot.SlotData.Item, slot.SlotData.BagIndex.Value, desiredPos);
            }
        }

        /// <summary>Uses a consumable item.</summary>
        private void UseConsumable(IItem item, int bagIndex)
        {
            if (item is not Consumable consumable)
                return;
            
            var hero = _heroComponent?.LinkedHero;
            if (hero == null)
            {
                Debug.Log($"Cannot use {item.Name}: No hero linked");
                return;
            }

            // Try to consume the item
            if (consumable.Consume(hero))
            {
                Debug.Log($"Used {item.Name}");
                
                // Decrement stack or remove item
                if (_heroComponent.Bag.ConsumeFromStack(bagIndex))
                {
                    // Refresh the UI to show updated stack counts
                    UpdateItemsFromBag();
                }
            }
            else
            {
                Debug.Log($"Failed to use {item.Name}");
            }
        }

        /// <summary>Discards an item from the bag.</summary>
        private void DiscardItem(int bagIndex)
        {
            if (_heroComponent?.Bag == null)
                return;
            
            var item = _heroComponent.Bag.GetSlotItem(bagIndex);
            if (item != null)
            {
                _heroComponent.Bag.SetSlotItem(bagIndex, null);
                Debug.Log($"Discarded {item.Name}");
                UpdateItemsFromBag();
            }
        }

        /// <summary>Swaps two slot items (if legal) and persists bag ordering.</summary>
        private void SwapSlotItems(InventorySlot a, InventorySlot b)
        {
            if (!CanPlaceItemInSlot(a.SlotData.Item, b.SlotData) || !CanPlaceItemInSlot(b.SlotData.Item, a.SlotData))
                return;
            AnimateSwap(a, b); // visual first (captures pre-swap sprites)
            var tmp = a.SlotData.Item;
            a.SlotData.Item = b.SlotData.Item;
            b.SlotData.Item = tmp;

            // Update hero equipment accurately depending on swap types
            var heroEquipment = _heroComponent?.LinkedHero;
            if (heroEquipment != null)
            {
                var aEquip = a.SlotData.SlotType == InventorySlotType.Equipment && a.SlotData.EquipmentSlot.HasValue;
                var bEquip = b.SlotData.SlotType == InventorySlotType.Equipment && b.SlotData.EquipmentSlot.HasValue;
                if (aEquip && bEquip)
                {
                    heroEquipment.ApplyEquipmentSwap(a.SlotData.EquipmentSlot.Value, a.SlotData.Item, b.SlotData.EquipmentSlot.Value, b.SlotData.Item);
                }
                else
                {
                    UpdateHeroDataFromSlot(a);
                    UpdateHeroDataFromSlot(b);
                }
            }

            PersistBagOrdering();
        }
        
        /// <summary>Animates swap by hiding originals and rendering tweened sprites in Draw.</summary>
        private void AnimateSwap(InventorySlot a, InventorySlot b)
        {
            if (_uiSwapActive)
            {
                if (_uiSwapSlotA != null) _uiSwapSlotA.SetItemSpriteHidden(false);
                if (_uiSwapSlotB != null) _uiSwapSlotB.SetItemSpriteHidden(false);
                _uiSwapActive = false;
            }
            var itemA = a.SlotData.Item;
            var itemB = b.SlotData.Item;
            if (itemA == null && itemB == null) return;
            if (Core.Content == null) return;
            Sprite spriteA = null;
            Sprite spriteB = null;
            try
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                if (itemA != null) spriteA = itemsAtlas.GetSprite(itemA.Name);
                if (itemB != null) spriteB = itemsAtlas.GetSprite(itemB.Name);
            }
            catch { return; }
            if (spriteA == null && spriteB == null) return;
            a.SetItemSpriteOffsetY(0f);
            b.SetItemSpriteOffsetY(0f);
            if (itemA != null) a.SetItemSpriteHidden(true);
            if (itemB != null) b.SetItemSpriteHidden(true);
            // Use local coordinates relative to InventoryGrid (matches how InventorySlot draws its item)
            var aPos = new Vector2(a.GetX(), a.GetY());
            var bPos = new Vector2(b.GetX(), b.GetY());
            _uiSwapSlotA = a;
            _uiSwapSlotB = b;
            _uiSwapDrawableA = spriteA != null ? new SpriteDrawable(spriteA) : null;
            _uiSwapDrawableB = spriteB != null ? new SpriteDrawable(spriteB) : null;
            _uiSwapStartA = aPos;
            _uiSwapEndA = bPos;
            _uiSwapStartB = bPos;
            _uiSwapEndB = aPos;
            _uiSwapElapsed = 0f;
            _uiSwapActive = true;
        }

        /// <summary>Draw override also advances swap animation so we do not rely on a non-existent Act override.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Draw children (slots) first so animated sprites render on top
            base.Draw(batcher, parentAlpha);
            if (!_uiSwapActive) return;
            _uiSwapElapsed += Time.DeltaTime;
            if (_uiSwapElapsed >= SWAP_TWEEN_DURATION)
            {
                if (_uiSwapSlotA != null) _uiSwapSlotA.SetItemSpriteHidden(false);
                if (_uiSwapSlotB != null) _uiSwapSlotB.SetItemSpriteHidden(false);
                _uiSwapActive = false;
                return;
            }
            float t = _uiSwapElapsed / SWAP_TWEEN_DURATION;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float ease = 1f - (1f - t) * (1f - t); // QuadOut
            if (_uiSwapDrawableA != null && _uiSwapSlotA != null)
            {
                var pos = Vector2.Lerp(_uiSwapStartA, _uiSwapEndA, ease);
                _uiSwapDrawableA.Draw(batcher, pos.X, pos.Y, SLOT_SIZE, SLOT_SIZE, Color.White);
            }
            if (_uiSwapDrawableB != null && _uiSwapSlotB != null)
            {
                var pos = Vector2.Lerp(_uiSwapStartB, _uiSwapEndB, ease);
                _uiSwapDrawableB.Draw(batcher, pos.X, pos.Y, SLOT_SIZE, SLOT_SIZE, Color.White);
            }
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
            var heroEquipment = _heroComponent?.LinkedHero;
            if (heroEquipment == null) return;
            var d = slot.SlotData;
            if (d.SlotType != InventorySlotType.Equipment || !d.EquipmentSlot.HasValue) return;
            heroEquipment.SetEquipmentSlot(d.EquipmentSlot.Value, d.Item);
        }

        /// <summary>Finds the first empty bag slot (shortcut or inventory).</summary>
        private InventorySlot FindFirstEmptyBagSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var data = slot.SlotData;
                if ((data.SlotType == InventorySlotType.Shortcut || data.SlotType == InventorySlotType.Inventory) && data.Item == null)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>Finds the target equipment slot for a given gear item.</summary>
        private InventorySlot FindTargetEquipmentSlot(IGear gear)
        {
            // Determine the target equipment slot based on item kind
            EquipmentSlot? targetSlot = null;
            
            if (gear.Kind == ItemKind.HatHelm || gear.Kind == ItemKind.HatHeadband || gear.Kind == ItemKind.HatWizard || gear.Kind == ItemKind.HatPriest)
            {
                targetSlot = EquipmentSlot.Hat;
            }
            else if (gear.Kind == ItemKind.ArmorMail || gear.Kind == ItemKind.ArmorRobe || gear.Kind == ItemKind.ArmorGi)
            {
                targetSlot = EquipmentSlot.Armor;
            }
            else if (gear.Kind == ItemKind.WeaponSword || gear.Kind == ItemKind.WeaponKnuckle || gear.Kind == ItemKind.WeaponStaff || gear.Kind == ItemKind.WeaponRod)
            {
                targetSlot = EquipmentSlot.WeaponShield1;
            }
            else if (gear.Kind == ItemKind.Shield)
            {
                targetSlot = EquipmentSlot.WeaponShield2;
            }
            else if (gear.Kind == ItemKind.Accessory)
            {
                // Find first empty accessory slot, or return null if both are occupied
                var accessory1Slot = FindEquipmentSlot(EquipmentSlot.Accessory1);
                var accessory2Slot = FindEquipmentSlot(EquipmentSlot.Accessory2);
                
                if (accessory1Slot != null && accessory1Slot.SlotData.Item == null)
                {
                    return accessory1Slot;
                }
                else if (accessory2Slot != null && accessory2Slot.SlotData.Item == null)
                {
                    return accessory2Slot;
                }
                else
                {
                    // Both accessory slots are occupied, cannot auto-select
                    return null;
                }
            }
            
            if (targetSlot.HasValue)
            {
                return FindEquipmentSlot(targetSlot.Value);
            }
            
            return null;
        }

        /// <summary>Finds the inventory slot for a specific equipment slot.</summary>
        private InventorySlot FindEquipmentSlot(EquipmentSlot equipmentSlot)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                if (slot.SlotData.SlotType == InventorySlotType.Equipment && slot.SlotData.EquipmentSlot == equipmentSlot)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>Updates active inventory slot availability based on capacity.</summary>
        public void UpdateBagCapacity(int capacity)
        {
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
                        data.SlotType = InventorySlotType.Null;
                        data.Item = null;
                        slot.Remove();
                    }
                }
            }
        }

        /// <summary>Finds next empty usable bag slot (shortcut first then inventory).</summary>
        public InventorySlotData FindNextAvailableSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i]; if (slot == null) continue;
                var d = slot.SlotData;
                if (d.SlotType == InventorySlotType.Shortcut && d.Item == null) return d;
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i]; if (slot == null) continue;
                var d = slot.SlotData;
                if (d.SlotType == InventorySlotType.Inventory && d.Item == null) return d;
            }
            return null;
        }

        /// <summary>Gets the current sort order.</summary>
        public InventorySortOrder GetCurrentSortOrder() => _currentSortOrder;

        /// <summary>Gets the current sort direction.</summary>
        public SortDirection GetCurrentSortDirection() => _currentSortDirection;

        /// <summary>Sorts inventory items by the specified order and direction.</summary>
        public void SortInventory(InventorySortOrder sortOrder, SortDirection sortDirection)
        {
            // Update current sort state first
            _currentSortOrder = sortOrder;
            _currentSortDirection = sortDirection;

            // Notify listeners
            OnSortOrderChanged?.Invoke(sortOrder, sortDirection);

            if (_heroComponent?.Bag == null) return;

            // Collect all bag slot items (shortcut + inventory)
            var bagSlots = new System.Collections.Generic.List<InventorySlot>();
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var type = slot.SlotData.SlotType;
                if (type == InventorySlotType.Shortcut || type == InventorySlotType.Inventory)
                {
                    bagSlots.Add(slot);
                }
            }

            // Sort the slots based on criteria
            bagSlots.Sort((a, b) =>
            {
                // Empty slots always go to the end
                if (a.SlotData.Item == null && b.SlotData.Item == null) return 0;
                if (a.SlotData.Item == null) return 1;
                if (b.SlotData.Item == null) return -1;

                int comparison = 0;
                switch (sortOrder)
                {
                    case InventorySortOrder.Time:
                        comparison = a.SlotData.AcquireIndex.CompareTo(b.SlotData.AcquireIndex);
                        break;
                    case InventorySortOrder.Type:
                        comparison = a.SlotData.Item.Kind.CompareTo(b.SlotData.Item.Kind);
                        break;
                    case InventorySortOrder.Name:
                        comparison = string.Compare(a.SlotData.Item.Name, b.SlotData.Item.Name, System.StringComparison.Ordinal);
                        break;
                }

                if (comparison == 0)
                {
                    // Deterministic tie-breakers to avoid non-deterministic order
                    int nameCmp = string.Compare(a.SlotData.Item.Name, b.SlotData.Item.Name, System.StringComparison.Ordinal);
                    if (nameCmp != 0) comparison = nameCmp;
                    else
                    {
                        int kindCmp = a.SlotData.Item.Kind.CompareTo(b.SlotData.Item.Kind);
                        if (kindCmp != 0) comparison = kindCmp;
                        else
                        {
                            // Final fallback: bag index (should exist for these slots)
                            var ai = a.SlotData.BagIndex.GetValueOrDefault(-1);
                            var bi = b.SlotData.BagIndex.GetValueOrDefault(-1);
                            comparison = ai.CompareTo(bi);
                        }
                    }
                }

                // Apply direction
                if (sortDirection == SortDirection.Descending)
                    comparison = -comparison;

                return comparison;
            });

            // Rebuild item array in sorted order
            for (int i = 0; i < bagSlots.Count; i++)
            {
                _persistBuffer[i] = bagSlots[i].SlotData.Item;
            }
            for (int i = bagSlots.Count; i < _persistBuffer.Length; i++)
            {
                _persistBuffer[i] = null;
            }

            // Persist to bag
            _heroComponent.Bag.SetItemsInOrder(_persistBuffer, bagSlots.Count);

            // Refresh UI
            UpdateItemsFromBag();
        }
    }
}