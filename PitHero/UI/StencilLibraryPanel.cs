using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>UI panel showing all discovered stencils in a scrollable grid.</summary>
    public class StencilLibraryPanel : Window
    {
        private const int GRID_COLUMNS = 10;
        private const int GRID_ROWS = 7;
        private const int TOTAL_SLOTS = GRID_COLUMNS * GRID_ROWS;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 2f;
        
        private GameStateService _gameStateService;
        private List<SynergyPattern> _allPatterns;
        private Table _gridTable;
        private StencilSlot[] _slots;
        private Label _detailsLabel;
        private TextButton _activateButton;
        private TextButton _closeButton;
        private SynergyPattern _selectedPattern;
        
        public event System.Action<SynergyPattern> OnStencilActivated;
        
        public StencilLibraryPanel(Skin skin) : base("Stencil Library", skin)
        {
            _slots = new StencilSlot[TOTAL_SLOTS];
            _allPatterns = new List<SynergyPattern>();
            
            SetSize(450f, 500f); // Increased size to accommodate scroll pane and details
            SetMovable(true);
            SetResizable(false);
            
            BuildUI(skin);
        }
        
        private void BuildUI(Skin skin)
        {
            var mainTable = new Table();
            mainTable.SetFillParent(true);
            mainTable.Pad(5f); // Reduced padding for less empty space
            
            // Create content table to hold grid and details
            var contentTable = new Table();
            contentTable.Pad(0f); // Remove content table padding

            // Add title label at the top (centered)
            var titleSkin = Skin.CreateDefaultSkin();
            var titleLabel = new Label("Synergy Stencils", titleSkin);
            titleLabel.SetFontScale(2f); // Make it slightly larger
            titleLabel.SetAlignment(Nez.UI.Align.Center);
            contentTable.Add(titleLabel).Pad(0f, 0f, 5f, 0f).Top().Center();
            contentTable.Row();
            
            // Create grid table
            _gridTable = new Table();
            _gridTable.Pad(5f);
            
            // Create slots in row-major order
            for (int i = 0; i < TOTAL_SLOTS; i++)
            {
                var slot = new StencilSlot(i, skin);
                slot.OnSlotClicked += HandleSlotClicked;
                _slots[i] = slot;
                
                _gridTable.Add(slot).Size(SLOT_SIZE, SLOT_SIZE).Pad(SLOT_PADDING);
                
                // New row after each column count
                if ((i + 1) % GRID_COLUMNS == 0)
                    _gridTable.Row();
            }
            
            // Create details panel
            var detailsTable = new Table();
            detailsTable.Pad(10f);
            detailsTable.SetBackground(skin.Get<WindowStyle>().Background); // Add background for visibility
            
            _detailsLabel = new Label("Select a stencil to view details", skin);
            _detailsLabel.SetWrap(true);
            detailsTable.Add(_detailsLabel).Pad(20f, 0, 0, 0).Width(380f).Top().Left();
            detailsTable.Row();
            
            _activateButton = new TextButton("Activate Stencil", skin);
            _activateButton.SetTouchable(Touchable.Disabled);
            _activateButton.OnClicked += HandleActivateClicked;
            _closeButton = new TextButton("Close", skin);
            _closeButton.OnClicked += HandleCloseClicked;
            
            var buttonsTable = new Table();
            buttonsTable.Add(_activateButton).Pad(10f, 10f, 0, 0).Width(150f).Height(30f);
            buttonsTable.Add(_closeButton).Pad(10f, 24f, 0, 0).Width(150f).Height(30f);
            detailsTable.Add(buttonsTable).Top().Left();
            
            // Add grid and details to content table
            contentTable.Add(_gridTable).Width(420f).Top().Left();
            contentTable.Row();
            contentTable.Add(detailsTable).Height(64f).Width(420f).Top().Left();
            
            // Wrap content in scroll pane
            var scrollPane = new ScrollPane(contentTable, skin);
            scrollPane.SetScrollingDisabled(true, false); // Disable horizontal, enable vertical scrolling
            scrollPane.SetFadeScrollBars(false); // Always show scroll bar
            
            // Add scroll pane to main table
            mainTable.Add(scrollPane).Height(350f).Width(420f).Top().Left();
            
            Add(mainTable);
        }
        
        /// <summary>Updates the panel with current game state and patterns.</summary>
        public void UpdateWithGameState(GameStateService gameStateService, List<SynergyPattern> allPatterns)
        {
            _gameStateService = gameStateService;
            _allPatterns = allPatterns;
            
            RefreshSlots();
        }
        
        private void RefreshSlots()
        {
            if (_gameStateService == null || _allPatterns == null)
                return;
            
            // Map patterns to slots (up to TOTAL_SLOTS)
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i < _allPatterns.Count)
                {
                    var pattern = _allPatterns[i];
                    bool discovered = _gameStateService.IsStencilDiscovered(pattern.Id);
                    _slots[i].UpdateSlot(pattern, discovered);
                }
                else
                {
                    _slots[i].UpdateSlot(null, false);
                }
            }
        }
        
        private void HandleSlotClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _allPatterns.Count)
                return;
            
            var pattern = _allPatterns[slotIndex];
            if (!_gameStateService.IsStencilDiscovered(pattern.Id))
                return;
            
            _selectedPattern = pattern;
            
            // Update selection state for all slots
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].IsSelected = (_slots[i].Pattern == _selectedPattern);
            }
            
            UpdateDetailsPanel();
        }
        
        private void UpdateDetailsPanel()
        {
            if (_selectedPattern == null)
            {
                _detailsLabel.SetText("Select a stencil to view details");
                _activateButton.SetTouchable(Touchable.Disabled);
                return;
            }

            // Build details text
            var details = $"{_selectedPattern.Name}\n\n{_selectedPattern.Description}";
            
            _detailsLabel.SetText(details);
            _activateButton.SetTouchable(Touchable.Enabled);
        }
        
        private void HandleActivateClicked(Button button)
        {
            if (_selectedPattern != null)
            {
                OnStencilActivated?.Invoke(_selectedPattern);
                _selectedPattern = null;
                
                // Clear selection state
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i].IsSelected = false;
                }
                
                SetVisible(false);
            }
        }
        
        private void HandleCloseClicked(Button button)
        {
            _selectedPattern = null;
            
            // Clear selection state
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].IsSelected = false;
            }
            
            SetVisible(false);
        }
        
        /// <summary>Individual slot in the stencil library grid.</summary>
        private class StencilSlot : Element, IInputListener
        {
            private readonly int _index;
            private readonly Sprite _backgroundSprite;
            private readonly SpriteDrawable _backgroundDrawable;
            private Sprite _selectBoxSprite;
            private Sprite _highlightBoxSprite;
            private SpriteDrawable _selectBoxDrawable;
            private SpriteDrawable _highlightBoxDrawable;
            private SynergyPattern _pattern;
            private bool _isDiscovered;
            private bool _isHovered;
            private Tooltip _tooltip;
            private Skin _skin;
            
            public event System.Action<int> OnSlotClicked;
            
            public SynergyPattern Pattern { get; private set; }
            public bool IsSelected { get; set; }
            
            public StencilSlot(int index, Skin skin)
            {
                _index = index;
                _skin = skin;
                IsSelected = false;
                
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                _backgroundSprite = itemsAtlas.GetSprite("Inventory");
                _backgroundDrawable = new SpriteDrawable(_backgroundSprite);
                
                // Load UI atlas for select and highlight sprites
                if (Core.Content != null)
                {
                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    _selectBoxSprite = uiAtlas.GetSprite("SelectBox");
                    _selectBoxDrawable = new SpriteDrawable(_selectBoxSprite);
                    _highlightBoxSprite = uiAtlas.GetSprite("HighlightBox");
                    _highlightBoxDrawable = new SpriteDrawable(_highlightBoxSprite);
                }
                
                SetSize(SLOT_SIZE, SLOT_SIZE);
                SetTouchable(Touchable.Enabled);
            }
            
            public void UpdateSlot(SynergyPattern pattern, bool discovered)
            {
                _pattern = pattern;
                Pattern = pattern;
                _isDiscovered = discovered;
                
                // Update tooltip
                if (_tooltip != null && _skin != null)
                {
                    _tooltip = null;
                }
                
                if (_isDiscovered && _pattern != null)
                {
                    var tooltipStyle = new TextTooltipStyle
                    {
                        LabelStyle = new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = Color.White }
                    };
                    _tooltip = new TextTooltip(_pattern.Name, this, tooltipStyle);
                }
            }
            
            public override void Draw(Batcher batcher, float parentAlpha)
            {
                // Draw background
                _backgroundDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), new Color(255, 255, 255, 100));
                
                // Draw stencil preview if discovered
                if (_isDiscovered && _pattern != null && Core.Content != null)
                {
                    DrawStencilPreview(batcher);
                }
                
                // Draw select box if hovered
                if (_isHovered && _selectBoxDrawable != null)
                {
                    _selectBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                }
                
                // Draw highlight box if selected
                if (IsSelected && _highlightBoxDrawable != null)
                {
                    _highlightBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                }
                
                base.Draw(batcher, parentAlpha);
            }
            
            private void DrawStencilPreview(Batcher batcher)
            {
                // Draw a simplified preview of the stencil pattern
                // For now, just render the first required item icon as a preview
                if (_pattern.RequiredKinds.Count > 0)
                {
                    try
                    {
                        var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                        var itemKind = _pattern.RequiredKinds[0];
                        var spriteName = GetStencilSpriteName(itemKind);
                        var sprite = itemsAtlas.GetSprite(spriteName);
                        
                        if (sprite != null)
                        {
                            var drawable = new SpriteDrawable(sprite);
                            drawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
                        }
                    }
                    catch
                    {
                        // Silently fail if sprite not found
                    }
                }
            }
            
            private string GetStencilSpriteName(RolePlayingFramework.Equipment.ItemKind kind)
            {
                return $"Stencil{kind}";
            }
            
            void IInputListener.OnMouseEnter()
            {
                _isHovered = true;
                if (_tooltip != null && _isDiscovered)
                {
                    _tooltip.Hit(Nez.Input.MousePosition);
                }
            }
            
            void IInputListener.OnMouseExit()
            {
                _isHovered = false;
                if (_tooltip != null)
                {
                    _tooltip.Hit(new Vector2(-10000f, -10000f));
                }
            }
            
            void IInputListener.OnMouseMoved(Vector2 mousePos)
            {
                if (_tooltip != null && _isDiscovered)
                {
                    _tooltip.Hit(Nez.Input.MousePosition);
                }
            }
            
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
            {
                if (_isDiscovered)
                {
                    OnSlotClicked?.Invoke(_index);
                }
                return true;
            }
            
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
