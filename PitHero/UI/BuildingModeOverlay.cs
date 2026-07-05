using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// Manages the building placement flow: inventory grid UI, description panel, world-space ghost
    /// entity, and confirmed placement with fall animation. Mirrors the TillModeOverlay pattern.
    /// </summary>
    public class BuildingModeOverlay
    {
        private enum PlacementState { Choosing, Describing, Placing }

        // ── World / scene references ──────────────────────────────────────────────
        private readonly Scene _scene;
        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;

        // ── State ─────────────────────────────────────────────────────────────────
        private PlacementState _state = PlacementState.Choosing;
        private BuildingType _selectedType;
        private bool _savedAutoScroll;

        // ── Move state (relocating an already-placed building) ─────────────────────
        private bool _moveActive;
        private PlacedBuilding _movingBuilding;
        private bool _savedMoveAutoScroll;
        private bool _moveJustEnded;

        // ── Ghost entity ──────────────────────────────────────────────────────────
        private Entity _ghostEntity;
        private SpriteRenderer _ghostRenderer;

        // Red-tinted copy shown at a moving building's original spot until the move is confirmed/cancelled.
        private Entity _originGhostEntity;

        // ── Inventory window ──────────────────────────────────────────────────────
        private Window _inventoryWindow;

        // ── Description panel ─────────────────────────────────────────────────────
        private Window _descWindow;
        private Label _descNameLabel;
        private Label _descDescLabel;
        private Label _descCostLabel;
        private TextButton _buildButton;
        private int _pendingCost;

        // ── Constants ─────────────────────────────────────────────────────────────
        private const int TillMinTileX = 120;
        private const int TillMinTileY = 0;
        private const float FallStartOffset = -600f;
        private const float SlotSize = 100f;
        private const float WinPad = 16f;

        private TextService _textService;

        /// <summary>Resolves a localized UI string, falling back to the key if the service is unavailable.</summary>
        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        public bool IsInPlacingState => _state == PlacementState.Placing;

        /// <summary>True while an already-placed building is being relocated via the context menu.</summary>
        public bool IsMoving => _moveActive;

        /// <summary>
        /// Returns true exactly once on the frame a move finished (confirm or cancel), then resets.
        /// Lets the click handler ignore the same left-click that confirmed a move.
        /// </summary>
        public bool ConsumeMoveJustEnded()
        {
            if (_moveJustEnded)
            {
                _moveJustEnded = false;
                return true;
            }
            return false;
        }

        /// <summary>Fired when the Cancel button is clicked; caller should invoke FarmUI.ExitBuildingMode().</summary>
        public event System.Action RequestExitBuildingMode;

        // ─────────────────────────────────────────────────────────────────────────
        public BuildingModeOverlay(Scene scene, Stage stage)
        {
            _scene     = scene;
            _stage     = stage;
            _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

            CreateInventoryWindow();
            CreateDescriptionWindow();
        }

        // ── Public lifecycle ──────────────────────────────────────────────────────

        public void OnEnterBuildingMode()
        {
            _savedAutoScroll = UIWindowManager.AutoScrollToHeroEnabled;
            UIWindowManager.SetAutoScrollToHero(false);
            _state = PlacementState.Choosing;
            ShowInventoryWindow();
        }

        public void OnExitBuildingMode()
        {
            UIWindowManager.SetAutoScrollToHero(_savedAutoScroll);
            DestroyGhost();
            _inventoryWindow?.SetVisible(false);
            _descWindow?.SetVisible(false);
            _state = PlacementState.Choosing;
        }

        public void Update()
        {
            if (_moveActive)
            {
                UpdateMoveState();
                return;
            }
            if (_state == PlacementState.Placing)
                UpdatePlacingState();
        }

        // ── Move flow ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Enters relocation mode for an already-placed building. Reuses the placement ghost/preview;
        /// the original building is hidden until the move is confirmed or cancelled.
        /// </summary>
        public void BeginMove(PlacedBuilding building)
        {
            if (building == null)
                return;

            _movingBuilding = building;
            _selectedType   = building.Type;
            _moveActive     = true;

            _savedMoveAutoScroll = UIWindowManager.AutoScrollToHeroEnabled;
            UIWindowManager.SetAutoScrollToHero(false);

            // Hide the real (day/night-graded) building and show a red-tinted copy in its place so the
            // original spot stays visible until the player commits to (or cancels) the new location.
            building.WorldEntity?.SetEnabled(false);
            var startPos = BuildingConfig.GetWorldPos(building.TileX, building.TileY, building.Type);
            CreateOriginGhost(building.Type, startPos);

            CreateGhost(building.Type);

            // Seed the moving ghost at the building's current spot so it doesn't flash at the world
            // origin before the first UpdateMoveState() positions it under the cursor.
            _ghostEntity?.SetPosition(startPos.X, startPos.Y);
        }

        private void UpdateMoveState()
        {
            // Cancel via Escape or right-click, restoring the building at its original location.
            if (Input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape) || Input.RightMouseButtonPressed)
            {
                CancelMove();
                return;
            }

            if (_stage.Hit(_stage.GetMousePosition()) != null)
                return;

            var worldPos = _scene.Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);

            var ghostPos = BuildingConfig.GetWorldPos(tileX, tileY, _selectedType);
            bool valid   = IsValidPlacement(tileX, tileY, _selectedType, _movingBuilding);

            if (_ghostEntity != null)
            {
                _ghostEntity.SetPosition(ghostPos.X, ghostPos.Y);
                if (_ghostRenderer != null)
                    _ghostRenderer.Color = valid
                        ? new Color(0, 255, 0, 180)
                        : new Color(255, 0, 0, 180);
            }

            if (Input.LeftMouseButtonPressed && valid)
                ConfirmMove(tileX, tileY, ghostPos);
        }

        private void ConfirmMove(int tileX, int tileY, Vector2 finalPos)
        {
            var moved = _movingBuilding;
            if (moved != null)
            {
                moved.TileX = tileX;
                moved.TileY = tileY;
                if (moved.WorldEntity != null)
                {
                    moved.WorldEntity.SetPosition(finalPos.X, finalPos.Y);
                    moved.WorldEntity.SetEnabled(true);
                }

                // Notify in-flight workers (e.g. a monster carrying a crop here) to retarget.
                Core.Services.GetService<BuildingService>()?.NotifyBuildingMoved(moved);
            }

            EndMove();
        }

        private void CancelMove()
        {
            _movingBuilding?.WorldEntity?.SetEnabled(true);
            EndMove();
        }

        private void EndMove()
        {
            DestroyGhost();
            DestroyOriginGhost();
            _moveActive = false;
            _movingBuilding = null;
            _moveJustEnded = true;
            UIWindowManager.SetAutoScrollToHero(_savedMoveAutoScroll);
        }

        // ── Inventory window ──────────────────────────────────────────────────────

        private void CreateInventoryWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _inventoryWindow = new Window(GetText(UITextKey.WindowBuildings), skin, "ph-default");
            _inventoryWindow.SetMovable(false);
            _inventoryWindow.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            // Building slots row
            var slotA = new BuildingSlotButton(
                _cropsAtlas.GetSprite(BuildingConfig.GetSpriteName(BuildingType.MonsterHouse)),
                GetText(BuildingConfig.GetDisplayNameKey(BuildingType.MonsterHouse)));
            slotA.OnClicked += () => OnBuildingSlotClicked(BuildingType.MonsterHouse);
            content.Add(slotA).Size(SlotSize, SlotSize).Pad(4f);

            var slotB = new BuildingSlotButton(
                _cropsAtlas.GetSprite(BuildingConfig.GetSpriteName(BuildingType.CropStorage)),
                GetText(BuildingConfig.GetDisplayNameKey(BuildingType.CropStorage)));
            slotB.OnClicked += () => OnBuildingSlotClicked(BuildingType.CropStorage);
            content.Add(slotB).Size(SlotSize, SlotSize).Pad(4f);

            // Cancel button below slots
            content.Row();
            var cancelButton = new TextButton(GetText(UITextKey.ButtonCancel), skin, "ph-default");
            cancelButton.OnClicked += (_) => RequestExitBuildingMode?.Invoke();
            content.Add(cancelButton).SetColspan(2).Width(100f).SetPadTop(8f);

            _inventoryWindow.Add(content).Expand().Fill();
            _inventoryWindow.Pack();
            _inventoryWindow.SetVisible(false);
            _stage.AddElement(_inventoryWindow);
        }

        private void ShowInventoryWindow()
        {
            _inventoryWindow.Pack();
            float w = _inventoryWindow.GetWidth();
            float h = _inventoryWindow.GetHeight();
            _inventoryWindow.SetPosition(
                (_stage.GetWidth()  - w) / 2f,
                (_stage.GetHeight() - h) / 2f - 30f);
            _inventoryWindow.SetVisible(true);
        }

        // ── Description window ────────────────────────────────────────────────────

        private void CreateDescriptionWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _descWindow = new Window("", skin, "ph-default");
            _descWindow.SetMovable(false);
            _descWindow.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            _descNameLabel = new Label("", skin, "ph-default");
            content.Add(_descNameLabel).SetPadBottom(6f);
            content.Row();

            _descDescLabel = new Label("", skin, "ph-default");
            _descDescLabel.SetWrap(true);
            content.Add(_descDescLabel).Width(180f).SetPadBottom(6f);
            content.Row();

            _descCostLabel = new Label("", skin, "ph-default");
            content.Add(_descCostLabel).SetPadBottom(10f);
            content.Row();

            _buildButton = new TextButton(GetText(UITextKey.ButtonBuild), skin, "ph-default");
            _buildButton.OnClicked += (_) => { if (!_buildButton.GetDisabled()) OnBuildClicked(); };
            content.Add(_buildButton).Width(80f);

            _descWindow.Add(content).Expand().Fill();
            _descWindow.Pack();
            _descWindow.SetVisible(false);
            _stage.AddElement(_descWindow);
        }

        private void PopulateAndShowDescriptionWindow(BuildingType type, int funds)
        {
            _pendingCost = BuildingConfig.GetCost(type);

            _descNameLabel.SetText(GetText(BuildingConfig.GetDisplayNameKey(type)));
            _descDescLabel.SetText(GetText(BuildingConfig.GetDescriptionKey(type)));
            _descCostLabel.SetText(string.Format(GetText(UITextKey.BuildingCostFormat), _pendingCost));
            _buildButton.SetDisabled(funds < _pendingCost);

            _descWindow.Pack();

            float invX = _inventoryWindow.GetX();
            float invY = _inventoryWindow.GetY();
            float invH = _inventoryWindow.GetHeight();
            _descWindow.SetPosition(invX, invY + invH + 8f);
            _descWindow.SetVisible(true);
        }

        // ── State transitions ─────────────────────────────────────────────────────

        private void OnBuildingSlotClicked(BuildingType type)
        {
            _selectedType = type;
            int funds = Core.Services.GetService<GameStateService>()?.Funds ?? 0;
            PopulateAndShowDescriptionWindow(type, funds);
            _state = PlacementState.Describing;
        }

        private void OnBuildClicked()
        {
            _descWindow.SetVisible(false);
            _inventoryWindow.SetVisible(false);
            CreateGhost(_selectedType);
            _state = PlacementState.Placing;
        }

        // ── Placing state update ──────────────────────────────────────────────────

        private void UpdatePlacingState()
        {
            if (_stage.Hit(_stage.GetMousePosition()) != null)
                return;

            var worldPos = _scene.Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);

            var ghostPos = BuildingConfig.GetWorldPos(tileX, tileY, _selectedType);
            bool valid   = IsValidPlacement(tileX, tileY, _selectedType);

            if (_ghostEntity != null)
            {
                _ghostEntity.SetPosition(ghostPos.X, ghostPos.Y);
                if (_ghostRenderer != null)
                    _ghostRenderer.Color = valid
                        ? new Color(0, 255, 0, 180)
                        : new Color(255, 0, 0, 180);
            }

            if (Input.LeftMouseButtonPressed && valid)
                ConfirmPlacement(tileX, tileY, _selectedType, ghostPos);
        }

        private bool IsValidPlacement(int tx, int ty, BuildingType type) => IsValidPlacement(tx, ty, type, null);

        private bool IsValidPlacement(int tx, int ty, BuildingType type, PlacedBuilding ignore)
        {
            var tileService     = Core.Services.GetService<TileStateService>();
            var buildingService = Core.Services.GetService<BuildingService>();
            var footprint       = BuildingConfig.GetFootprint(type);

            for (int i = 0; i < footprint.Length; i++)
            {
                int fx = tx + footprint[i].dx;
                int fy = ty + footprint[i].dy;

                if (fx < TillMinTileX || fy < TillMinTileY)
                    return false;

                if (tileService != null)
                {
                    var tile = new Point(fx, fy);
                    if (tileService.HasFlag(tile, TileStateFlag.ReadyToTill)) return false;
                    if (tileService.HasFlag(tile, TileStateFlag.Tilled))      return false;
                }

                if (buildingService != null && buildingService.IsTileOccupied(fx, fy, ignore))
                    return false;
            }
            return true;
        }

        // ── Load restore ─────────────────────────────────────────────────────────

        /// <summary>
        /// Spawns a previously-saved building at its final world position with no fall animation.
        /// Called by MainGameScene.ApplyPendingLoadData() for each building in the save file.
        /// </summary>
        public void SpawnRestoredBuilding(BuildingType type, int tileX, int tileY, int uniqueId)
        {
            var finalPos = BuildingConfig.GetWorldPos(tileX, tileY, type);
            var sprite   = _cropsAtlas.GetSprite(BuildingConfig.GetSpriteName(type));
            var entity   = _scene.CreateEntity(
                "building-" + type.ToString().ToLower() + "-" + tileX + "-" + tileY);
            entity.SetPosition(finalPos.X, finalPos.Y);

            var renderer = entity.AddComponent(new SpriteRenderer(sprite));
            renderer.SetRenderLayer(GameConfig.RenderLayerBuilding);
            ApplyDayNightGrading(renderer);

            Core.Services.GetService<BuildingService>()?.AddBuilding(new PlacedBuilding
            {
                Type        = type,
                TileX       = tileX,
                TileY       = tileY,
                UniqueId    = uniqueId,
                WorldEntity = entity
            });
        }

        /// <summary>
        /// Attaches the shared day/night grading material (if present) so placed buildings
        /// tint with the terrain. Ghost/preview sprites are intentionally left ungraded.
        /// </summary>
        private void ApplyDayNightGrading(SpriteRenderer renderer)
        {
            var colorGrading = Core.Services.GetService<Rendering.ColorGradingController>();
            if (colorGrading?.Material != null)
                renderer.SetMaterial(colorGrading.Material);
        }

        // ── Ghost management ──────────────────────────────────────────────────────

        private void CreateGhost(BuildingType type)
        {
            DestroyGhost();
            var sprite = _cropsAtlas.GetSprite(BuildingConfig.GetSpriteName(type));
            _ghostEntity   = _scene.CreateEntity("building-ghost");
            _ghostRenderer = _ghostEntity.AddComponent(new SpriteRenderer(sprite));
            _ghostRenderer.SetRenderLayer(GameConfig.RenderLayerTop);
            _ghostRenderer.Color = new Color(0, 255, 0, 180);
        }

        private void DestroyGhost()
        {
            _ghostEntity?.Destroy();
            _ghostEntity   = null;
            _ghostRenderer = null;
        }

        /// <summary>Creates the stationary red-tinted marker at a moving building's original location.</summary>
        private void CreateOriginGhost(BuildingType type, Vector2 worldPos)
        {
            DestroyOriginGhost();
            var sprite = _cropsAtlas.GetSprite(BuildingConfig.GetSpriteName(type));
            _originGhostEntity = _scene.CreateEntity("building-origin-ghost");
            _originGhostEntity.SetPosition(worldPos.X, worldPos.Y);
            var renderer = _originGhostEntity.AddComponent(new SpriteRenderer(sprite));
            renderer.SetRenderLayer(GameConfig.RenderLayerTop);
            renderer.Color = new Color(255, 0, 0, 180);
        }

        private void DestroyOriginGhost()
        {
            _originGhostEntity?.Destroy();
            _originGhostEntity = null;
        }

        // ── Placement confirmation ────────────────────────────────────────────────

        private void ConfirmPlacement(int tileX, int tileY, BuildingType type, Vector2 finalPos)
        {
            // Deduct gold
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState != null)
                gameState.Funds -= _pendingCost;

            DestroyGhost();

            // Create permanent entity above screen; BuildingFallAnimator moves it into position
            var sprite = _cropsAtlas.GetSprite(BuildingConfig.GetSpriteName(type));
            var entity = _scene.CreateEntity(
                "building-" + type.ToString().ToLower() + "-" + tileX + "-" + tileY);
            entity.SetPosition(finalPos.X, finalPos.Y + FallStartOffset);

            var renderer = entity.AddComponent(new SpriteRenderer(sprite));
            renderer.SetRenderLayer(GameConfig.RenderLayerBuilding);
            ApplyDayNightGrading(renderer);

            entity.AddComponent(new BuildingFallAnimator(finalPos.Y));

            var buildingSvc = Core.Services.GetService<BuildingService>();
            int newId = buildingSvc?.AllocateId() ?? 0;
            buildingSvc?.AddBuilding(new PlacedBuilding
            {
                Type        = type,
                TileX       = tileX,
                TileY       = tileY,
                UniqueId    = newId,
                WorldEntity = entity
            });

            // Return to Choosing so the player can place more
            ShowInventoryWindow();
            _state = PlacementState.Choosing;
        }

        // ── BuildingSlotButton ────────────────────────────────────────────────────

        private class BuildingSlotButton : Element, IInputListener
        {
            private readonly Sprite     _sprite;
            private readonly string     _tooltipText;
            private readonly SpriteDrawable _draw;
            private Sprite _selectBox;
            private bool   _hovered;

            public event System.Action OnClicked;

            public BuildingSlotButton(Sprite sprite, string tooltipText)
            {
                _sprite      = sprite;
                _tooltipText = tooltipText;
                _draw        = new SpriteDrawable(sprite);
                SetTouchable(Touchable.Enabled);
                SetSize(100f, 100f);

                if (Core.Content != null)
                {
                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    _selectBox  = uiAtlas.GetSprite("SelectBox");
                }
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                _draw.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                if (_hovered && _selectBox != null)
                    new SpriteDrawable(_selectBox).Draw(
                        batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            void IInputListener.OnMouseEnter()
            {
                _hovered = true;
                if (!string.IsNullOrEmpty(_tooltipText))
                    HoverTextManager.ShowHoverText(_tooltipText,
                        GetX(), GetY() + GetHeight() + 4f);
            }

            void IInputListener.OnMouseExit()
            {
                _hovered = false;
                HoverTextManager.HideHoverText();
            }

            void IInputListener.OnMouseMoved(Vector2 mousePos) { }

            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;

            void IInputListener.OnLeftMouseUp(Vector2 mousePos)   => OnClicked?.Invoke();

            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;

            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }

            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
