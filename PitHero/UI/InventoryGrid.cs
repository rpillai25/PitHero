using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>Grid layout container for inventory slots with interaction logic (single linear buffer version).</summary>
    public class InventoryGrid : Group
    {
        private const int GRID_WIDTH = 20;
        private const int GRID_HEIGHT = 8;  // 2 rows for equipment area + 6 rows for inventory
        private const int CELL_COUNT = GRID_WIDTH * GRID_HEIGHT;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 1f;
        private const float HOVER_OFFSET_Y = -16f; // Offset in pixels when hovering over a slot while another is selected
        private const float SWAP_TWEEN_DURATION = 0.2f; // Duration in seconds for swap animation

        private readonly FastList<InventorySlot> _slots;   // Row-major, may contain nulls for Null slots or capacity-disabled slots
        private readonly IItem[] _persistBuffer;           // Reusable buffer for bag ordering persistence (120 slot capacity)
        private HeroComponent _heroComponent;
        private InventorySlot _highlightedSlot;
        private InventoryContextMenu _contextMenu;
        private Stage _stage; // Reference to stage for tooltip management
        
        // Swap animation entities (scene-space approach retained but unused currently)
        private Entity _swapEntity1;
        private Entity _swapEntity2;
        private SpriteRenderer _swapRenderer1;
        private SpriteRenderer _swapRenderer2;

        // UI-based swap animation state (legacy, kept disabled when using overlay)
        private bool _uiSwapActive;
        private float _uiSwapElapsed;
        private InventorySlot _uiSwapSlotA;
        private InventorySlot _uiSwapSlotB;
        
        // Public events for item card display
        public event System.Action<IItem> OnItemHovered;
        public event System.Action OnItemUnhovered;
        public event System.Action<IItem> OnItemSelected;
        public event System.Action OnItemDeselected;

        private int _nextAcquireIndex = 1; // monotonic acquisition counter
        private readonly Dictionary<IItem, int> _acquireIndexMap; // persistent mapping of items to acquire indices
        private readonly Dictionary<IItem, int> _itemStackMap;    // last known stack count per item instance
        
        // Stencil system
        private readonly ActiveStencilManager _stencilManager;
        private bool _moveStencilsMode;
        private PlacedStencil _selectedStencil;
        private Point? _stencilDragOffset; // Offset from stencil anchor to click position
        private List<RolePlayingFramework.Synergies.ActiveSynergy> _activeSynergies;
        private readonly Dictionary<Point, Nez.Tweens.ITween<Color>> _slotGlowTweens; // Track glow tweens per slot position
        
        // Synergy detection
        private readonly SynergyDetector _synergyDetector;
        private IItem?[,] _synergyDetectionGrid; // Reusable grid for synergy detection

        public InventoryGrid()
        {
            _slots = new FastList<InventorySlot>(CELL_COUNT);
            _persistBuffer = new IItem[CELL_COUNT];
            _acquireIndexMap = new Dictionary<IItem, int>(64);
            _itemStackMap = new Dictionary<IItem, int>(64);
            _stencilManager = new ActiveStencilManager();
            _moveStencilsMode = false;
            _activeSynergies = new List<RolePlayingFramework.Synergies.ActiveSynergy>();
            _slotGlowTweens = new Dictionary<Point, Nez.Tweens.ITween<Color>>();
            
            // Initialize synergy detection
            _synergyDetector = new SynergyDetector();
            _synergyDetectionGrid = new IItem?[GRID_WIDTH, GRID_HEIGHT];
            RegisterDefaultSynergyPatterns();
            
            BuildSlots();
            LayoutSlots();
        }
        
        /// <summary>Registers default synergy patterns for detection.</summary>
        private void RegisterDefaultSynergyPatterns()
        {
            ExampleSynergyPatterns.RegisterAllExamplePatterns(_synergyDetector);
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
            // Top 2 rows (y=0,1) are ONLY for equipment display - everything else is NULL
            if (y < 2)
            {
                // Equipment slots in top-right area (columns 8-10, 3 adjacent columns)
                if (y == 0 && x == 8) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield1 };
                if (y == 0 && x == 9) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Hat };
                if (y == 0 && x == 10) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.WeaponShield2 };
                if (y == 1 && x == 8) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory1 };
                if (y == 1 && x == 9) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Armor };
                if (y == 1 && x == 10) return new InventorySlotData(x, y, InventorySlotType.Equipment) { EquipmentSlot = EquipmentSlot.Accessory2 };
                
                // All other slots in top 2 rows are NULL (not displayed)
                return new InventorySlotData(x, y, InventorySlotType.Null);
            }
            
            // Rows 2-7: Pure inventory (6 rows � 20 columns = 120 inventory slots)
            return new InventorySlotData(x, y, InventorySlotType.Inventory);
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
            
            // Subscribe to cross-component inventory changes
            InventorySelectionManager.OnInventoryChanged += UpdateItemsFromBag;
            
            // Subscribe to selection cleared event
            InventorySelectionManager.OnSelectionCleared += ClearLocalSelectionState;
            
            // Subscribe to cross-component swap animation
            InventorySelectionManager.OnCrossComponentSwapAnimate += HandleCrossComponentSwapAnimate;
        }
        
        /// <summary>Clears only the local highlighted slot without invoking events.</summary>
        private void ClearLocalSelectionState()
        {
            if (_highlightedSlot != null)
            {
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
            }
        }
        
        /// <summary>Handles cross-component swap animation by triggering local animation for this component's slot.</summary>
        private void HandleCrossComponentSwapAnimate(InventorySlot slotA, InventorySlot slotB)
        {
            // Find which slot belongs to this component
            InventorySlot mySlot = null;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == slotA || slot == slotB)
                {
                    mySlot = slot;
                    break;
                }
            }
            
            if (mySlot != null)
            {
                // Trigger a brief "pop" animation by hiding and showing the sprite
                AnimateCrossComponentSwap(mySlot);
            }
        }
        
        /// <summary>Animates a single slot for cross-component swap (simple hide/show effect).</summary>
        private void AnimateCrossComponentSwap(InventorySlot slot)
        {
            // For cross-component swaps, we can't tween between different coordinate spaces,
            // so we just hide the sprite briefly and let the refresh show the new item
            if (slot.SlotData.Item != null)
            {
                slot.SetItemSpriteHidden(true);
                // The sprite will be shown again when UpdateItemsFromBag is called after the swap
            }
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
                {
                    slot.SlotData.Item = null;
                    slot.SetItemSpriteHidden(false); // Ensure sprites are visible after refresh
                }
            }
            UpdateEquipmentSlots();
            UpdateBagSlots();
            
            // Detect synergies after inventory refresh
            DetectAndApplySynergies();
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
                // Only process Inventory type slots now (shortcuts are separate)
                if (type == InventorySlotType.Inventory)
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
            // Handle stencil mode interactions
            if (_moveStencilsMode)
            {
                HandleStencilModeClick(clickedSlot);
                return;
            }
            
            // Ignore clicks from shortcut bar selections - shortcuts reference inventory slots, not swap with them
            if (InventorySelectionManager.HasSelection() && InventorySelectionManager.IsSelectionFromShortcutBar())
            {
                // Clear the shortcut selection - we don't support moving shortcuts back to inventory
                InventorySelectionManager.ClearSelection();
                return;
            }
            
            if (_highlightedSlot == null)
            {
                _highlightedSlot = clickedSlot;
                clickedSlot.SlotData.IsHighlighted = true;
                InventorySelectionManager.SetSelectedFromInventory(clickedSlot, _heroComponent);
                Debug.Log($"Highlighted slot at ({clickedSlot.SlotData.X},{clickedSlot.SlotData.Y})");
                if (clickedSlot.SlotData.Item != null)
                    OnItemSelected?.Invoke(clickedSlot.SlotData.Item);
            }
            else if (_highlightedSlot == clickedSlot)
            {
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                InventorySelectionManager.ClearSelection();
                OnItemDeselected?.Invoke();
            }
            else
            {
                var prev = _highlightedSlot;
                SwapSlotItems(_highlightedSlot, clickedSlot);
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                InventorySelectionManager.ClearSelection();
                Debug.Log($"Swapped items between ({prev.SlotData.X},{clickedSlot.SlotData.Y}) and ({clickedSlot.SlotData.X},{clickedSlot.SlotData.Y})");
                OnItemDeselected?.Invoke();
            }
        }
        
        /// <summary>Handles clicks when in move stencils mode.</summary>
        private void HandleStencilModeClick(InventorySlot clickedSlot)
        {
            var gridPos = new Point(clickedSlot.SlotData.X, clickedSlot.SlotData.Y);
            
            if (_selectedStencil == null)
            {
                // Select a stencil if clicked on one
                _selectedStencil = _stencilManager.FindStencilAtPosition(gridPos);
                if (_selectedStencil != null)
                {
                    // Calculate offset from anchor to click position
                    _stencilDragOffset = new Point(gridPos.X - _selectedStencil.Anchor.X, gridPos.Y - _selectedStencil.Anchor.Y);
                    Debug.Log($"Selected stencil: {_selectedStencil.Pattern.Name}");
                }
            }
            else
            {
                // Move the selected stencil
                if (_stencilDragOffset.HasValue)
                {
                    var newAnchor = new Point(gridPos.X - _stencilDragOffset.Value.X, gridPos.Y - _stencilDragOffset.Value.Y);
                    _stencilManager.MoveStencil(_selectedStencil, newAnchor, GRID_WIDTH, GRID_HEIGHT);
                    Debug.Log($"Moved stencil to anchor: ({newAnchor.X}, {newAnchor.Y})");
                }
                
                // Deselect stencil after move
                _selectedStencil = null;
                _stencilDragOffset = null;
            }
        }

        private void HandleSlotHovered(InventorySlot slot)
        {
            // Check for cross-component hover (from ShortcutBar) or local hover
            if ((InventorySelectionManager.HasSelection() && slot.SlotData.Item != null) || 
                (_highlightedSlot != null && _highlightedSlot != slot && slot.SlotData.Item != null))
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

        /// <summary>Public method to clear selection state (called when closing inventory UI).</summary>
        public void ClearSelection()
        {
            // Just call the manager's clear - it will notify this component via callback
            InventorySelectionManager.ClearSelection();
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

            // Try unified stack absorption first (source=a -> target=b)
            if (InventorySelectionManager.CanAbsorbStacks(a, b, out var toAbsorb))
            {
                // Animate transfer then absorb and persist
                AnimateSwap(a, b);
                InventorySelectionManager.PerformAbsorbStacks(a, b, toAbsorb);
                PersistBagOrdering();
                
                // Detect synergies after inventory change
                DetectAndApplySynergies();
                
                // Notify inventory changed for shortcut bar to refresh
                InventorySelectionManager.OnInventoryChanged?.Invoke();
                return;
            }

            // Visual animation first (captures pre-swap sprites) via unified manager
            AnimateSwap(a, b);

            // Perform logical swap immediately; hidden sprites prevent flicker until overlay completes
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
            
            // Detect synergies after inventory change
            DetectAndApplySynergies();
            
            // Notify inventory changed for shortcut bar to refresh
            InventorySelectionManager.OnInventoryChanged?.Invoke();
        }
        
        /// <summary>Detects synergies in current inventory state and applies them to hero.</summary>
        private void DetectAndApplySynergies()
        {
            var hero = _heroComponent?.LinkedHero;
            if (hero == null) return;
            
            // Build synergy detection grid from current slot state
            BuildSynergyDetectionGrid();
            
            // Detect synergies
            var detectedSynergies = _synergyDetector.DetectSynergies(_synergyDetectionGrid, GRID_WIDTH, GRID_HEIGHT);
            
            // Get GameStateService for stencil discovery
            var gameStateService = Core.Services?.GetService<PitHero.Services.GameStateService>();
            
            // Apply synergies to hero (this also handles stencil discovery)
            hero.UpdateActiveSynergies(detectedSynergies, gameStateService);
            
            // Update local tracking for glow effects
            UpdateActiveSynergies(detectedSynergies);
            
            // Log detected synergies
            if (detectedSynergies.Count > 0)
            {
                Debug.Log($"[InventoryGrid] Detected {detectedSynergies.Count} active synergies:");
                for (int i = 0; i < detectedSynergies.Count; i++)
                {
                    var synergy = detectedSynergies[i];
                    Debug.Log($"  - {synergy.Pattern.Name} at anchor ({synergy.AnchorSlot.X},{synergy.AnchorSlot.Y})");
                }
            }
        }
        
        /// <summary>Builds the synergy detection grid from current slot state.</summary>
        private void BuildSynergyDetectionGrid()
        {
            // Clear grid
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    _synergyDetectionGrid[x, y] = null;
                }
            }
            
            // Populate grid from slots
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                
                var data = slot.SlotData;
                if (data.X >= 0 && data.X < GRID_WIDTH && data.Y >= 0 && data.Y < GRID_HEIGHT)
                {
                    _synergyDetectionGrid[data.X, data.Y] = data.Item;
                }
            }
        }
        
        /// <summary>Animates swap using unified InventorySelectionManager overlay in stage space.</summary>
        private void AnimateSwap(InventorySlot a, InventorySlot b)
        {
            // Cancel any legacy UI tween
            if (_uiSwapActive)
            {
                if (_uiSwapSlotA != null) _uiSwapSlotA.SetItemSpriteHidden(false);
                if (_uiSwapSlotB != null) _uiSwapSlotB.SetItemSpriteHidden(false);
                _uiSwapActive = false;
            }

            // Delegate to manager. It will hide sprites, animate, then unhide on completion.
            InventorySelectionManager.TryAnimateSwap(a, b, SWAP_TWEEN_DURATION);
        }

        /// <summary>Draw override also advances swap animation so we do not rely on a non-existent Act override.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Draw children (slots) first so animated sprites render on top
            base.Draw(batcher, parentAlpha);
            
            // Draw stencil overlays
            DrawStencilOverlays(batcher);

            // Legacy UI tween disabled when using manager overlay
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
        }
        
        /// <summary>Draws stencil overlays on the inventory grid.</summary>
        private void DrawStencilOverlays(Batcher batcher)
        {
            if (Core.Content == null) return;
            
            // Draw stencil overlays first
            var placedStencils = _stencilManager.PlacedStencils;
            for (int i = 0; i < placedStencils.Count; i++)
            {
                DrawStencilOverlay(batcher, placedStencils[i]);
            }
            
            // Draw glow effects for completed synergies (independent of stencils)
            DrawSynergyGlowEffects(batcher);
        }
        
        /// <summary>Draws glow effects for completed synergy patterns.</summary>
        private void DrawSynergyGlowEffects(Batcher batcher)
        {
            // Use oscillating alpha for glow effect (0 to 100, transparent to semi-transparent)
            var time = Time.TotalTime;
            var glowIntensity = (System.Math.Sin(time * 3f) * 0.5f + 0.5f) * 0.5f; // 0.0 to 0.5

            foreach (var slotPos in _slotGlowTweens.Keys)
            {
                var slot = FindSlotAtGrid(slotPos.X, slotPos.Y);
                if (slot == null) continue;

                // Draw green glow overlay
                var glowColor = new Color(0, 255, 0, 255) * (float)glowIntensity;
                var slotX = slot.GetX();
                var slotY = slot.GetY();
                var slotW = slot.GetWidth();
                var slotH = slot.GetHeight();
                
                batcher.DrawRect(slotX, slotY, slotW, slotH, glowColor);
            }
        }


        /// <summary>Draws a single stencil overlay.</summary>
        private void DrawStencilOverlay(Batcher batcher, PlacedStencil stencil)
        {
            var pattern = stencil.Pattern;
            var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
            
            var offsets = pattern.GridOffsets;
            var requiredKinds = pattern.RequiredKinds;
            
            for (int i = 0; i < offsets.Count; i++)
            {
                var targetX = stencil.Anchor.X + offsets[i].X;
                var targetY = stencil.Anchor.Y + offsets[i].Y;
                
                // Find the slot at this position
                var slot = FindSlotAtGrid(targetX, targetY);
                if (slot == null) continue;
                
                var slotItem = slot.SlotData.Item;
                var requiredKind = requiredKinds[i];
                
                // Determine overlay color based on item match
                Color overlayColor;
                Nez.Textures.Sprite overlaySprite = null;
                
                if (slotItem == null)
                {
                    // Empty slot - show stencil icon
                    var stencilSpriteName = $"Stencil{requiredKind}";
                    try
                    {
                        overlaySprite = itemsAtlas.GetSprite(stencilSpriteName);
                        overlayColor = new Color(255, 255, 255, 150); // Semi-transparent white
                    }
                    catch
                    {
                        // Sprite not found, skip
                        continue;
                    }
                }
                else if (slotItem.Kind == requiredKind)
                {
                    // Matching item - more transparent green highlight
                    overlayColor = new Color(0, 255, 0, 50); // 20% opacity green
                }
                else
                {
                    // Non-matching item - more transparent red highlight
                    overlayColor = new Color(255, 0, 0, 50); // 20% opacity red
                }
                
                // Draw overlay
                var slotX = slot.GetX();
                var slotY = slot.GetY();
                var slotW = slot.GetWidth();
                var slotH = slot.GetHeight();
                
                if (overlaySprite != null)
                {
                    // Draw stencil icon sprite
                    var drawable = new Nez.UI.SpriteDrawable(overlaySprite);
                    drawable.Draw(batcher, slotX, slotY, slotW, slotH, overlayColor);
                }
                else
                {
                    // Draw color rectangle overlay
                    batcher.DrawRect(slotX, slotY, slotW, slotH, overlayColor);
                }
            }
        }
        
        /// <summary>Finds a slot at the given grid coordinates.</summary>
        private InventorySlot FindSlotAtGrid(int gridX, int gridY)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                var data = slot.SlotData;
                if (data.X == gridX && data.Y == gridY)
                {
                    return slot;
                }
            }
            return null;
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
                // Only persist Inventory type slots (shortcuts are separate)
                if (type == InventorySlotType.Inventory)
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

        /// <summary>finds the first empty bag slot (shortcut or inventory).</summary>
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
            // Fixed capacity - no longer used for limiting slots
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
        
        /// <summary>Finds the inventory slot containing a specific item instance.</summary>
        public InventorySlot FindSlotContainingItem(IItem item)
        {
            if (item == null) return null;
            
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                if (slot.SlotData.SlotType == InventorySlotType.Inventory && slot.SlotData.Item == item)
                {
                    return slot;
                }
            }
            return null;
        }
        
        // Stencil system public methods
        
        /// <summary>Places a stencil on the inventory grid.</summary>
        public void PlaceStencil(RolePlayingFramework.Synergies.SynergyPattern pattern, Point anchor)
        {
            _stencilManager.PlaceStencil(pattern, anchor);
        }
        
        /// <summary>Removes a stencil from the grid.</summary>
        public void RemoveStencil(PlacedStencil stencil)
        {
            _stencilManager.RemoveStencil(stencil);
        }
        
        /// <summary>Toggles move stencils mode.</summary>
        public void SetMoveStencilsMode(bool enabled)
        {
            _moveStencilsMode = enabled;
            if (!enabled)
            {
                _selectedStencil = null;
                _stencilDragOffset = null;
            }
        }
        
        /// <summary>Gets whether move stencils mode is active.</summary>
        public bool IsMoveStencilsModeActive() => _moveStencilsMode;
        
        /// <summary>Gets all placed stencils.</summary>
        public IReadOnlyList<PlacedStencil> GetPlacedStencils() => _stencilManager.PlacedStencils;
        
        /// <summary>Updates active synergies for glow effect rendering.</summary>
        public void UpdateActiveSynergies(List<RolePlayingFramework.Synergies.ActiveSynergy> synergies)
        {
            _activeSynergies = synergies ?? new List<RolePlayingFramework.Synergies.ActiveSynergy>();
            UpdateSynergyGlowEffects();
        }
        
        /// <summary>Updates glow effects for completed synergy patterns.</summary>
        private void UpdateSynergyGlowEffects()
        {
            // Stop all existing glow tweens
            foreach (var tween in _slotGlowTweens.Values)
            {
                tween?.Stop();
            }
            _slotGlowTweens.Clear();
            
            // Start new glow tweens for active synergies
            for (int i = 0; i < _activeSynergies.Count; i++)
            {
                var synergy = _activeSynergies[i];
                var slots = synergy.AffectedSlots;
                
                for (int j = 0; j < slots.Count; j++)
                {
                    var slotPos = slots[j];
                    var slot = FindSlotAtGrid(slotPos.X, slotPos.Y);
                    
                    if (slot != null && !_slotGlowTweens.ContainsKey(slotPos))
                    {
                        // Mark slot for glow rendering (actual glow drawn in DrawSynergyGlowEffects)
                        _slotGlowTweens[slotPos] = null;
                    }
                }
            }
        }
    }
}