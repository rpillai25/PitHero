using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using Nez.UI;
using Nez.BitmapFonts;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>UI component representing a single inventory slot.</summary>
    public class InventorySlot : Element, IInputListener
    {
        private readonly InventorySlotData _slotData;
        private readonly Sprite _backgroundSprite;
        private Sprite _selectBoxSprite;
        private Sprite _highlightBoxSprite;
        
        private SpriteDrawable _backgroundDrawable;
        private SpriteDrawable _selectBoxDrawable;
        private SpriteDrawable _highlightBoxDrawable;
        private BitmapFont _font;
        
        private float _itemSpriteOffsetY = 0f;
        private bool _hideItemSprite = false;
        
        // Placeholder sprites for empty equipment slots
        private SpriteDrawable _placeholderDrawable;
        private Tooltip _placeholderTooltip;
        
        // Double-click detection
        private float _lastClickTime = -1f;
        
        public event System.Action<InventorySlot> OnSlotClicked;
        public event System.Action<InventorySlot> OnSlotDoubleClicked;
        public event System.Action<InventorySlot> OnSlotHovered;
        public event System.Action<InventorySlot> OnSlotUnhovered;
        public event System.Action<InventorySlot, Vector2> OnSlotRightClicked;

        public InventorySlotData SlotData => _slotData;

        public float Scale { get; set; } = 1f;

        public InventorySlot(InventorySlotData slotData)
        {
            _slotData = slotData;
            
            // Only load content if Core.Content is available (not in test environment)
            if (Core.Content != null)
            {
                // Load atlases
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                
                // Set up background sprite based on slot type
                var spriteKey = _slotData.SlotType switch
                {
                    InventorySlotType.Inventory => "Inventory",
                    InventorySlotType.Shortcut => "Shortcut",
                    InventorySlotType.Equipment => "Equipment",
                    _ => null
                };

                if (spriteKey != null)
                {
                    _backgroundSprite = itemsAtlas.GetSprite(spriteKey);
                    _backgroundDrawable = new SpriteDrawable(_backgroundSprite);
                }
                
                // Pre-load select and highlight sprites
                _selectBoxSprite = uiAtlas.GetSprite("SelectBox");
                _selectBoxDrawable = new SpriteDrawable(_selectBoxSprite);
                
                _highlightBoxSprite = uiAtlas.GetSprite("HighlightBox");
                _highlightBoxDrawable = new SpriteDrawable(_highlightBoxSprite);
                
                // Load placeholder sprites for equipment slots
                if (_slotData.SlotType == InventorySlotType.Equipment && _slotData.EquipmentSlot.HasValue)
                {
                    string placeholderSpriteName = GetPlaceholderSpriteName(_slotData.EquipmentSlot.Value);
                    string placeholderTooltipText = GetPlaceholderTooltipText(_slotData.EquipmentSlot.Value);
                    
                    if (placeholderSpriteName != null)
                    {
                        var placeholderSprite = itemsAtlas.GetSprite(placeholderSpriteName);
                        if (placeholderSprite != null)
                        {
                            _placeholderDrawable = new SpriteDrawable(placeholderSprite);
                        }
                    }
                    
                    // Create tooltip for placeholder if we have tooltip text
                    if (placeholderTooltipText != null && Graphics.Instance != null)
                    {
                        var skin = new Skin();
                        skin.Add("default", Graphics.Instance.BitmapFont);
                        var tooltipStyle = new TextTooltipStyle
                        {
                            LabelStyle = new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White }
                        };
                        _placeholderTooltip = new TextTooltip(placeholderTooltipText, this, tooltipStyle);
                        _placeholderTooltip.SetInstant(true);
                        _placeholderTooltip.SetAlways(true);
                    }
                }
                
                // Load font for stack count display
                try
                {
                    _font = Core.Content.LoadBitmapFont("Content/Fonts/HudSmall.fnt");
                }
                catch
                {
                    // If font doesn't load, use default
                    _font = Graphics.Instance.BitmapFont;
                }
            }
            
            // Set size to 32x32 pixels
            SetSize(32f, 32f);
            SetTouchable(Touchable.Enabled); // ensure we always receive hover events
        }
        
        /// <summary>Gets the placeholder sprite name for an equipment slot.</summary>
        private string GetPlaceholderSpriteName(EquipmentSlot equipmentSlot)
        {
            return equipmentSlot switch
            {
                EquipmentSlot.WeaponShield1 => "WeaponShield",
                EquipmentSlot.WeaponShield2 => "WeaponShield",
                EquipmentSlot.Armor => "Armor",
                EquipmentSlot.Hat => "Helm",
                EquipmentSlot.Accessory1 => "Accessory",
                EquipmentSlot.Accessory2 => "Accessory",
                _ => null
            };
        }
        
        /// <summary>Gets the placeholder tooltip text for an equipment slot.</summary>
        private string GetPlaceholderTooltipText(EquipmentSlot equipmentSlot)
        {
            return equipmentSlot switch
            {
                EquipmentSlot.WeaponShield1 => "Weapon/Shield",
                EquipmentSlot.WeaponShield2 => "Weapon/Shield",
                EquipmentSlot.Armor => "Armor",
                EquipmentSlot.Hat => "Helm",
                EquipmentSlot.Accessory1 => "Accessory",
                EquipmentSlot.Accessory2 => "Accessory",
                _ => null
            };
        }
        
        /// <summary>Sets the item sprite Y offset for visual effects (like hover).</summary>
        public void SetItemSpriteOffsetY(float offsetY)
        {
            _itemSpriteOffsetY = offsetY;
        }
        
        /// <summary>Sets whether the item sprite should be hidden (for swap animation).</summary>
        public void SetItemSpriteHidden(bool hidden)
        {
            _hideItemSprite = hidden;
        }
        
        /// <summary>Gets the placeholder tooltip for this slot if it has one.</summary>
        public Tooltip GetPlaceholderTooltip()
        {
            return _placeholderTooltip;
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Only draw if not a null slot
            if (_slotData.SlotType == InventorySlotType.Null)
                return;

            // Draw background (only if sprite is loaded)
            if (_backgroundDrawable != null)
            {
                _backgroundDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), new Color(255, 255, 255, 100));
            }

            // Draw item sprite if slot has an item, or placeholder if equipment slot is empty
            if (_slotData.Item != null && Core.Content != null && !_hideItemSprite)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var itemSprite = itemsAtlas.GetSprite(_slotData.Item.Name);
                    if (itemSprite != null)
                    {
                        var itemDrawable = new SpriteDrawable(itemSprite);
                        // Apply the item sprite offset to the Y position only
                        itemDrawable.Draw(batcher, GetX(), GetY() + _itemSpriteOffsetY, GetWidth(), GetHeight(), Color.White);
                    }
                }
                catch
                {
                    // If item sprite doesn't exist, silently continue
                }
                
                // Draw stack count if it's a stackable consumable with more than 1 item
                if (_slotData.Item is Consumable consumable && consumable.StackCount > 1)
                {
                    if (_font != null)
                    {
                        var stackText = consumable.StackCount.ToString();
                        // Apply offset to stack count as well
                        var textPosition = new Vector2(GetX() + 2f * Scale, GetY() + _itemSpriteOffsetY + GetHeight() - _font.LineHeight * Scale + 2f * Scale);
                        batcher.DrawString(_font, stackText, textPosition, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
                    }
                }
            }
            else if (_slotData.Item == null && _slotData.SlotType == InventorySlotType.Equipment && _placeholderDrawable != null)
            {
                // Draw placeholder sprite for empty equipment slot
                _placeholderDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // Draw select box if hovered (only if sprite is loaded)
            if (_slotData.IsHovered && _selectBoxDrawable != null)
            {
                _selectBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // Draw highlight box if highlighted (only if sprite is loaded)
            if (_slotData.IsHighlighted && _highlightBoxDrawable != null)
            {
                _highlightBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }
            
            // Draw shortcut key number below slot for Shortcut type slots
            if (_slotData.SlotType == InventorySlotType.Shortcut && _slotData.ShortcutKey.HasValue && _font != null)
            {
                var keyText = _slotData.ShortcutKey.Value.ToString();
                var textSize = _font.MeasureString(keyText) * Scale;
                // Center the number horizontally below the slot
                var textX = GetX() + (GetWidth() - textSize.X) / 2f;
                var textY = GetY() + GetHeight() + 2f * Scale; // 2 pixels below the slot
                batcher.DrawString(_font, keyText, new Vector2(textX, textY), Color.Goldenrod, 0f, Vector2.Zero, Scale, SpriteEffects.None, 0f);
            }
            
            base.Draw(batcher, parentAlpha);
        }

        #region IInputListener Implementation

        void IInputListener.OnMouseEnter()
        {
            _slotData.IsHovered = true;
            // Manually drive tooltip enter when hovering an empty equipment slot
            if (_slotData.SlotType == InventorySlotType.Equipment && _slotData.Item == null && _placeholderTooltip != null)
            {
                _placeholderTooltip.Hit(Input.MousePosition);
            }
            OnSlotHovered?.Invoke(this);
        }

        void IInputListener.OnMouseExit()
        {
            _slotData.IsHovered = false;
            // Force tooltip to register exit so internal state resets properly
            if (_placeholderTooltip != null)
            {
                _placeholderTooltip.Hit(new Vector2(-10000f, -10000f));
            }
            OnSlotUnhovered?.Invoke(this);
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            // Keep placeholder tooltip following the cursor while hovering
            if (_slotData.SlotType == InventorySlotType.Equipment && _slotData.Item == null && _placeholderTooltip != null)
            {
                _placeholderTooltip.Hit(Input.MousePosition);
            }
        }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            // Check for double-click
            float currentTime = Time.TotalTime;
            if (_lastClickTime >= 0 && (currentTime - _lastClickTime) <= GameConfig.DoubleClickThresholdSeconds)
            {
                // Double-click detected
                OnSlotDoubleClicked?.Invoke(this);
                _lastClickTime = -1f; // Reset to prevent triple-click
            }
            else
            {
                // Single click
                OnSlotClicked?.Invoke(this);
                _lastClickTime = currentTime;
            }
            return true;
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos)
        {
            OnSlotRightClicked?.Invoke(this, mousePos);
            return true;
        }

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
        }

        void IInputListener.OnRightMouseUp(Vector2 mousePos)
        {
        }

        bool IInputListener.OnMouseScrolled(int mouseWheelDelta)
        {
            return false;
        }

        #endregion
    }
}