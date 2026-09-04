using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that handles camera zoom and pan controls via mouse input, with automatic hero following
    /// </summary>
    public class CameraControllerComponent : Component, IUpdatable, IPausableComponent
    {
        private Camera _camera;
        private Vector2 _lastMousePosition;
        private bool _isPanning;
        private Vector2 _defaultCameraPosition;
        private Rectangle _tileMapBounds;
        private float _currentMinimumZoom = GameConfig.CameraMinimumZoom;
        private float _currentMaximumZoom = GameConfig.CameraMaximumZoom;
        private const float PixelPerfectZoomStep = 0.125f; // 1/8 increments keeps scaling clean (32 * 0.125 = 4)

        private Point _lastRenderTargetSize; // re-clamps the camera when the render target is resized

        private bool _isFollowingHero = true; // camera auto-follows hero by default
        private float _manualControlTimer = 0f; // tracks time since last manual control input
        private float _keyboardPanHeldTime = 0f; // continuous seconds arrow/WASD pan keys have been held
        private Vector2 _keyboardPanRemainder; // banked sub-pixel movement released in whole-pixel steps
        private Entity _heroEntity; // cached reference to hero entity
        private bool _hasPendingCenter; // CenterOnWorldPosition called before OnAddedToEntity initialised the camera
        private Vector2 _pendingCenter;

        /// <summary>
        /// While true every input path (zoom, pan, quadrant/keyboard pan, hero following) is skipped;
        /// the camera stays wherever it was put. Used by the new-game intro (issue #396).
        /// </summary>
        public bool InputSuspended { get; set; }

        /// <summary>
        /// Optional hook set by the scene; returns true when the pointer is over a UI element.
        /// Gates the modifier-less wheel zoom so scrolling a UI list doesn't also zoom the camera.
        /// </summary>
        public System.Func<bool> IsPointerOverUI;

        /// <summary>
        /// Optional hook set by the scene; returns true when a UI text field has keyboard focus.
        /// Gates the quadrant jump keys so typing digits into a text field doesn't move the camera.
        /// </summary>
        public System.Func<bool> HasKeyboardFocus;

        /// <summary>
        /// Gets whether this component should respect the manual pause state.
        /// We shouldn't modify camera size or zoom while paused from menu.
        /// The farm-mode pause gate is deliberately ignored so the player can pan/zoom the map
        /// while planning crops over a wide area.
        /// </summary>
        public bool ShouldPause => true;

        public override void OnAddedToEntity()
        {
            _camera = Entity.GetComponent<Camera>() ?? Entity.Scene.Camera;
            InitializeTileMapBounds();
            if (_camera != null)
            {
                _camera.SetMinimumZoom(_currentMinimumZoom);
                _camera.SetMaximumZoom(_currentMaximumZoom);
                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                // Vertically centre on the pit's centre row (statue / rim-walk row) rather than on
                // half the design height, so the whole pit is framed at any GameConfig.VirtualHeight
                _defaultCameraPosition = ConstrainCameraPosition(new Vector2(
                    _tileMapBounds.X + _tileMapBounds.Width / 2f,
                    (GameConfig.MapCenterTileY + 0.5f) * GameConfig.TileSize));
                // A centre requested during scene setup (before this deferred init) wins over the default
                _camera.Position = _hasPendingCenter ? ConstrainCameraPosition(_pendingCenter) : _defaultCameraPosition;
                _hasPendingCenter = false;
                QuantizeCameraPosition();

                // A full view restore requested during scene setup (replay rebuild) wins over both
                if (_hasPendingView)
                {
                    _hasPendingView = false;
                    ApplyView(_pendingView);
                }
            }
        }

        private bool _hasPendingView;
        private CameraViewState _pendingView;

        /// <summary>Captures position, zoom and follow state so a rebuilt scene can show the same view.</summary>
        public CameraViewState CaptureView()
        {
            return new CameraViewState
            {
                Position = _camera != null ? _camera.Position : _pendingCenter,
                RawZoom = _camera != null ? _camera.RawZoom : GameConfig.CameraDefaultZoom,
                IsFollowingHero = _isFollowingHero,
                ManualControlTimer = _manualControlTimer,
            };
        }

        /// <summary>Restores a captured view. Safe before the camera is attached: applied in OnAddedToEntity.</summary>
        public void RestoreView(in CameraViewState view)
        {
            if (_camera == null)
            {
                _pendingView = view;
                _hasPendingView = true;
                return;
            }
            ApplyView(view);
        }

        private void ApplyView(in CameraViewState view)
        {
            _camera.RawZoom = view.RawZoom;
            _camera.Position = ConstrainCameraPosition(view.Position);
            QuantizeCameraPosition();
            _isFollowingHero = view.IsFollowingHero;
            _manualControlTimer = view.ManualControlTimer;
        }

        /// <summary>
        /// No-op: the camera is view-only and polls input, so it runs from
        /// <see cref="PresentationUpdate"/> once per rendered frame instead of inside a simulation step.
        /// </summary>
        public void Update()
        {
        }

        /// <summary>Per-rendered-frame camera control (zoom, pan, quadrant jumps, hero following). Called by the scene's presentation pass.</summary>
        public void PresentationUpdate()
        {
            if (_camera == null)
                return;

            // Nothing re-clamps after a graphics reset (window shrink/restore, dock, monitor swap),
            // and Camera.OnSceneRenderTargetSizeChanged can leave the camera out of bounds.
            var renderTargetSize = GetSceneRenderTargetSize();
            if (renderTargetSize != _lastRenderTargetSize)
            {
                _lastRenderTargetSize = renderTargetSize;
                _camera.Position = ConstrainCameraPosition(_camera.Position);
                QuantizeCameraPosition();
            }

            // Scripted sequences (new-game intro) own the camera: no input, no hero following
            if (InputSuspended)
                return;

            // Only the manual (menu) pause freezes the camera; the farm-mode pause keeps camera
            // controls live so the player can right-mouse pan while planning crops.
            // A replay re-applies the recorded pauses to the simulation; the viewer keeps the camera.
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsManuallyPaused == true && ShouldPause && !Services.Replay.ReplayPlaybackService.IsPlaybackActive)
                return;

            // Cache hero entity reference if not already cached, or clear if hero is destroyed/dead
            if (_heroEntity == null)
            {
                var heroEntities = Entity.Scene.FindEntitiesWithTag(GameConfig.TAG_HERO);
                if (heroEntities.Count > 0)
                {
                    _heroEntity = heroEntities[0];
                }
            }
            else
            {
                // Check if cached hero is still valid (not destroyed and alive)
                if (_heroEntity.IsDestroyed)
                {
                    _heroEntity = null;
                }
                else
                {
                    // Check if hero is dead or dying
                    var heroComponent = _heroEntity.GetComponent<HeroComponent>();
                    if (heroComponent?.LinkedHero != null && heroComponent.LinkedHero.CurrentHP <= 0)
                    {
                        _heroEntity = null;
                    }
                }
            }

            HandleZoomInput();
            HandlePanInput();
            HandleQuadrantJumpInput();
            HandleKeyboardPanInput();
            HandleHeroFollowing();
        }

        private void HandleZoomInput()
        {
            bool shiftDown = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            bool ctrlDown = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);

            // SHIFT + Right-Click: Reset window size (if shrunk) and reset zoom + recenter
            if (shiftDown && Input.RightMouseButtonPressed)
            {
                if (WindowManager.IsHalfHeightMode())
                    WindowManager.RestoreOriginalSize(Core.Instance);

                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                // Vertically centre on the pit's centre row (statue / rim-walk row) rather than on
                // half the design height, so the whole pit is framed at any GameConfig.VirtualHeight
                _defaultCameraPosition = ConstrainCameraPosition(new Vector2(
                    _tileMapBounds.X + _tileMapBounds.Width / 2f,
                    (GameConfig.MapCenterTileY + 0.5f) * GameConfig.TileSize));
                _camera.Position = ConstrainCameraPosition(_defaultCameraPosition);
                QuantizeCameraPosition();
                Debug.Log($"[CameraController] SHIFT+RightClick reset zoom={_camera.RawZoom} positionX={_camera.Position.X} positionY={_camera.Position.Y}");
                return;
            }

            // SHIFT + Middle-Click: Reset zoom (preserve current window size and camera location)
            if (shiftDown && Input.MiddleMouseButtonPressed)
            {
                SetZoomPreservingFocus(GameConfig.CameraDefaultZoom, "SHIFT+MiddleClick reset");
                return;
            }

            var wheelDelta = Input.MouseWheelDelta;
            if (wheelDelta == 0)
                return;

            // CTRL + scroll: window resize
            if (ctrlDown)
            {
                HandleWindowResizeZoom(wheelDelta);
            }
            // Plain or SHIFT + scroll: camera zoom. The modifier-less path is skipped while the
            // pointer is over UI so scrolling a UI list doesn't also zoom the camera underneath.
            else if (shiftDown || IsPointerOverUI?.Invoke() != true)
            {
                HandleCameraOnlyZoom(wheelDelta);
            }
        }

        /// <summary>
        /// Handles the window shrinking/restoring behavior when SHIFT is held
        /// </summary>
        private void HandleWindowResizeZoom(int wheelDelta)
        {
            bool zoomingOut = wheelDelta < 0;
            var mouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
            var currentZoom = _camera.RawZoom;
            float newZoom = currentZoom;

            if (zoomingOut)
            {
                if (currentZoom <= 1f)
                {
                    if (!WindowManager.IsHalfHeightMode())
                    {
                        WindowManager.ShrinkToNextLevel(Core.Instance); // Half
                        CenterCameraOnMap();
                        QuantizeCameraPosition();
                        return;
                    }
                    else
                    {
                        if (currentZoom > _currentMinimumZoom)
                            newZoom = _currentMinimumZoom;
                    }
                }
                else
                {
                    newZoom = (float)System.Math.Floor(currentZoom) - 1f;
                    if (newZoom < 1f)
                        newZoom = 1f;
                }
            }
            else
            {
                if (WindowManager.IsHalfHeightMode())
                {
                    WindowManager.RestoreOriginalSize(Core.Instance);
                    _camera.Position = ConstrainCameraPosition(_camera.Position);
                    QuantizeCameraPosition();
                    return;
                }

                if (currentZoom == 0.5f)
                    newZoom = 1f;
                else
                    newZoom = (float)System.Math.Floor(currentZoom) + 1f;
            }

            newZoom = MathHelper.Clamp(newZoom, _currentMinimumZoom, _currentMaximumZoom);
            if (System.Math.Abs(newZoom - currentZoom) <= 0.01f)
                return;

            _camera.RawZoom = SnapZoomToStep(newZoom);
            RecenterAroundMouse(mouseWorldPos);
            Debug.Log($"[CameraController] SHIFT zoom newZoom={_camera.RawZoom}");
        }

        /// <summary>
        /// Handles camera zoom only (no window size changes) when SHIFT not held using fixed step increments
        /// </summary>
        private void HandleCameraOnlyZoom(int wheelDelta)
        {
            var mouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
            var currentZoom = _camera.RawZoom;

            // Determine direction only (mouse wheel delta magnitudes vary by platform). Each notch -> one step.
            int direction = wheelDelta > 0 ? 1 : -1;
            float newZoom = currentZoom + direction * PixelPerfectZoomStep;

            newZoom = SnapZoomToStep(newZoom);
            newZoom = MathHelper.Clamp(newZoom, _currentMinimumZoom, _currentMaximumZoom);

            if (System.Math.Abs(newZoom - currentZoom) <= 0.0001f)
                return;

            _camera.RawZoom = newZoom;
            RecenterAroundMouse(mouseWorldPos);
        }

        /// <summary>
        /// Snap zoom to fixed step increments to maintain pixel alignment and avoid texture sampling seams
        /// </summary>
        private float SnapZoomToStep(float value)
        {
            var snapped = (float)System.Math.Round(value / PixelPerfectZoomStep) * PixelPerfectZoomStep;
            return MathHelper.Clamp(snapped, _currentMinimumZoom, _currentMaximumZoom);
        }

        /// <summary>
        /// Keeps the point under the mouse stable while zooming
        /// </summary>
        private void RecenterAroundMouse(Vector2 originalMouseWorldPos)
        {
            var newMouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
            var worldPosDelta = originalMouseWorldPos - newMouseWorldPos;
            var desiredPosition = _camera.Position + new Vector2(
                (float)System.Math.Round(worldPosDelta.X),
                (float)System.Math.Round(worldPosDelta.Y));
            _camera.Position = ConstrainCameraPosition(desiredPosition);
            QuantizeCameraPosition();
        }

        /// <summary>
        /// Quantize camera position so that (position * zoom) lands on whole screen pixels (avoids seams)
        /// </summary>
        private void QuantizeCameraPosition()
        {
            var pos = _camera.Position;
            var z = _camera.RawZoom;
            if (z <= 0f)
                z = 1f;
            float step = 1f / z; // world units that map to 1 screen pixel
            pos.X = (float)System.Math.Round(pos.X / step) * step;
            pos.Y = (float)System.Math.Round(pos.Y / step) * step;
            _camera.Position = pos;
        }

        /// <summary>
        /// Returns the world-space center of the given horizontal map quadrant (0-based index).
        /// </summary>
        public static Vector2 GetQuadrantCenter(Rectangle mapBounds, int quadrantIndex)
        {
            return new Vector2(
                mapBounds.X + mapBounds.Width * (quadrantIndex + 0.5f) / GameConfig.MapQuadrantCount,
                mapBounds.Y + mapBounds.Height / 2f);
        }

        /// <summary>
        /// Centers the camera on one of the horizontal map quadrants when keys 1-4 are pressed.
        /// SHIFT+number is reserved for the shortcut bar and CTRL for zoom modifiers.
        /// </summary>
        private void HandleQuadrantJumpInput()
        {
            if (Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ||
                Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl))
                return;

            if (HasKeyboardFocus?.Invoke() == true)
                return;

            for (int i = 0; i < GameConfig.MapQuadrantCount; i++)
            {
                var key = (Keys)((int)Keys.D1 + i);
                if (!Input.IsKeyPressed(key)) continue;

                SwitchToManualControl();
                _manualControlTimer = 0f;
                _camera.Position = ConstrainCameraPosition(GetQuadrantCenter(_tileMapBounds, i));
                QuantizeCameraPosition();
                Debug.Log($"[CameraController] Quadrant jump {i + 1} positionX={_camera.Position.X} positionY={_camera.Position.Y}");
                break;
            }
        }

        private void CenterCameraOnMap()
        {
            var mapCenterX = _tileMapBounds.X + _tileMapBounds.Width / 2f;
            var mapCenterY = _tileMapBounds.Y + _tileMapBounds.Height / 2f;
            _camera.Position = new Vector2(mapCenterX, mapCenterY);
            QuantizeCameraPosition();
            Debug.Log($"CenterCameraOnMap -> CenterX={mapCenterX} CenterY={mapCenterY}");
        }

        private void HandlePanInput()
        {
            var currentMousePosition = Input.ScaledMousePosition;
            bool shiftDown = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);

            if (Input.MiddleMouseButtonPressed && Util.MouseUtils.IsMouseInsideWindow())
            {
                // Do not start panning if this press is used for SHIFT+Middle-Click reset
                if (shiftDown)
                {
                    _isPanning = false;
                    return;
                }

                _isPanning = true;
                _lastMousePosition = currentMousePosition;

                // Switch to manual control mode
                SwitchToManualControl();
            }
            else if (Input.MiddleMouseButtonReleased)
            {
                _isPanning = false;
            }

            if (_isPanning && Input.MiddleMouseButtonDown)
            {
                var mouseDelta = currentMousePosition - _lastMousePosition;
                var panDelta = -mouseDelta * GameConfig.CameraPanSpeed / _camera.RawZoom;
                var newPosition = _camera.Position + new Vector2(
                    (float)System.Math.Round(panDelta.X),
                    (float)System.Math.Round(panDelta.Y));
                newPosition = ConstrainCameraPosition(newPosition);
                _camera.Position = newPosition;
                QuantizeCameraPosition();
                _lastMousePosition = currentMousePosition;
                
                // Reset manual control timer on active panning
                _manualControlTimer = 0f;
            }
        }

        /// <summary>
        /// Smoothly scrolls the camera while arrow keys or WASD are held — an alternative panning
        /// method to the middle-mouse drag. Speed ramps from the starting to the top pan speed the
        /// longer keys are held continuously, so long trips get faster. Skipped while SHIFT/CTRL are
        /// held so modifier-based shortcuts (e.g. SHIFT+S) and zoom controls don't also pan the camera.
        /// </summary>
        private void HandleKeyboardPanInput()
        {
            if (Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ||
                Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl))
            {
                _keyboardPanHeldTime = 0f;
                _keyboardPanRemainder = Vector2.Zero;
                return;
            }

            var direction = Vector2.Zero;
            if (Input.IsKeyDown(Keys.Left) || Input.IsKeyDown(Keys.A))
                direction.X -= 1f;
            if (Input.IsKeyDown(Keys.Right) || Input.IsKeyDown(Keys.D))
                direction.X += 1f;
            if (Input.IsKeyDown(Keys.Up) || Input.IsKeyDown(Keys.W))
                direction.Y -= 1f;
            if (Input.IsKeyDown(Keys.Down) || Input.IsKeyDown(Keys.S))
                direction.Y += 1f;

            if (direction == Vector2.Zero)
            {
                _keyboardPanHeldTime = 0f;
                _keyboardPanRemainder = Vector2.Zero;
                return;
            }

            direction.Normalize();
            SwitchToManualControl();

            // Accelerate from starting to top speed over the ramp duration while keys stay held
            _keyboardPanHeldTime += Time.DeltaTime;
            var ramp = MathHelper.Clamp(_keyboardPanHeldTime / GameConfig.CameraKeyboardPanAccelSeconds, 0f, 1f);
            var speed = MathHelper.Lerp(GameConfig.CameraKeyboardPanSpeed, GameConfig.CameraKeyboardPanMaxSpeed, ramp);

            // Divide by zoom so on-screen scroll speed stays consistent at every zoom level.
            // Movement is released only in whole screen-pixel steps (banking the fractional part
            // in a remainder) so the camera never lands on sub-pixel positions mid-scroll, which
            // reads as blur/shimmer. At 1x zoom this is exactly integer world-position snapping.
            float pixelStep = 1f / _camera.RawZoom; // world units per screen pixel
            var desired = direction * speed * Time.DeltaTime / _camera.RawZoom + _keyboardPanRemainder;
            var applied = new Vector2(
                (float)System.Math.Truncate(desired.X / pixelStep) * pixelStep,
                (float)System.Math.Truncate(desired.Y / pixelStep) * pixelStep);
            _keyboardPanRemainder = desired - applied;
            _manualControlTimer = 0f;

            if (applied == Vector2.Zero)
                return;

            _camera.Position = ConstrainCameraPosition(_camera.Position + applied);
            QuantizeCameraPosition();
        }

        /// <summary>
        /// Handles automatic camera following of the hero
        /// </summary>
        private void HandleHeroFollowing()
        {
            // A replay is history viewed at the player's own pace: the camera never chases the hero
            if (Services.Replay.ReplayPlaybackService.IsPlaybackActive)
                return;

            // Check if auto-scroll to hero is enabled
            if (!UI.UIWindowManager.AutoScrollToHeroEnabled)
            {
                // Auto-scroll is disabled, don't follow hero
                return;
            }

            // Check if player is interacting with selectables - if so, reset timer to prevent auto-scroll
            var interactionService = Core.Services.GetService<PlayerInteractionService>();
            if (interactionService?.IsInteractingWithSelectable == true)
            {
                // Player is engaged with a selectable entity - reset timer to keep camera in manual mode
                if (!_isFollowingHero)
                {
                    _manualControlTimer = 0f;
                }
                else
                {
                    // Switch to manual mode if player starts interacting while in auto-follow
                    SwitchToManualControl();
                }
            }

            // Update manual control timer if in manual mode
            if (!_isFollowingHero)
            {
                _manualControlTimer += Time.UnscaledDeltaTime;
                
                // Resume auto-following after timeout
                if (_manualControlTimer >= GameConfig.CameraManualControlTimeout)
                {
                    _isFollowingHero = true;
                    Debug.Log("[CameraController] Resuming auto-follow after inactivity timeout");
                }
            }

            // Follow hero if in auto-follow mode
            if (_isFollowingHero && _heroEntity != null)
            {
                var heroPosition = _heroEntity.Transform.Position;
                var targetPosition = ConstrainCameraPosition(heroPosition);
                
                // Quantize target position to avoid sub-pixel artifacts
                var quantizedTarget = QuantizePosition(targetPosition);
                
                // Smoothly lerp camera to quantized hero position
                var lerpedPosition = Vector2.Lerp(_camera.Position, quantizedTarget, GameConfig.CameraFollowLerpSpeed * Time.DeltaTime);
                _camera.Position = lerpedPosition;
                QuantizeCameraPosition();
            }
        }
        
        /// <summary>
        /// Quantize a position to pixel boundaries (helper for target positions)
        /// </summary>
        private Vector2 QuantizePosition(Vector2 pos)
        {
            var z = _camera.RawZoom;
            if (z <= 0f)
                z = 1f;
            float step = 1f / z;
            return new Vector2(
                (float)System.Math.Round(pos.X / step) * step,
                (float)System.Math.Round(pos.Y / step) * step
            );
        }

        /// <summary>
        /// Switches camera to manual control mode
        /// </summary>
        private void SwitchToManualControl()
        {
            if (_isFollowingHero)
            {
                _isFollowingHero = false;
                _manualControlTimer = 0f;
                Debug.Log("[CameraController] Switched to manual camera control");
            }
        }

        private void InitializeTileMapBounds()
        {
            _tileMapBounds = new Rectangle(0, 0, 1920, 384);
            var tiledEntity = Entity.Scene.FindEntity("tilemap");
            if (tiledEntity != null)
            {
                var tiledMapRenderer = tiledEntity.GetComponent<TiledMapRenderer>();
                if (tiledMapRenderer != null && tiledMapRenderer.TiledMap != null)
                {
                    var tiledMap = tiledMapRenderer.TiledMap;
                    _tileMapBounds = new Rectangle(0, 0,
                        tiledMap.Width * tiledMap.TileWidth,
                        tiledMap.Height * tiledMap.TileHeight);
                    Debug.Log($"TileMap bounds initialized: X={_tileMapBounds.X}, Y={_tileMapBounds.Y}, Width={_tileMapBounds.Width}, Height={_tileMapBounds.Height}");
                }
                else
                {
                    Debug.Log("TiledMapRenderer or TiledMap not found, using default bounds");
                }
            }
            else
            {
                Debug.Log("Tilemap entity not found, using default bounds");
            }
        }

        /// <summary>
        /// Size of the scene render target in world pixels — the true visible area at 1x zoom.
        /// Falls back to the design consts if the render target does not exist yet.
        /// </summary>
        private Point GetSceneRenderTargetSize()
        {
            var scene = Entity?.Scene;
            if (scene?.SceneRenderTarget != null)
                return scene.SceneRenderTargetSize;
            return new Point(GameConfig.VirtualWidth, GameConfig.VirtualHeight);
        }

        private Vector2 ConstrainCameraPosition(Vector2 desiredPosition)
        {
            // Under FixedHeight the render target width varies with the window aspect, so the
            // visible viewport must come from the actual render target, not the design consts.
            var renderTargetSize = GetSceneRenderTargetSize();
            var viewportWidth = renderTargetSize.X / _camera.RawZoom;
            var viewportHeight = renderTargetSize.Y / _camera.RawZoom;

            var minX = _tileMapBounds.X + viewportWidth / 2f;
            var maxX = _tileMapBounds.Right - viewportWidth / 2f;
            var minY = _tileMapBounds.Y + viewportHeight / 2f;
            var maxY = _tileMapBounds.Bottom - viewportHeight / 2f;

            if (viewportWidth >= _tileMapBounds.Width)
                desiredPosition.X = _tileMapBounds.X + _tileMapBounds.Width / 2f;
            else
                desiredPosition.X = MathHelper.Clamp(desiredPosition.X, minX, maxX);

            if (viewportHeight >= _tileMapBounds.Height)
                desiredPosition.Y = _tileMapBounds.Y + _tileMapBounds.Height / 2f;
            else
                desiredPosition.Y = MathHelper.Clamp(desiredPosition.Y, minY, maxY);

            return desiredPosition;
        }

        /// <summary>
        /// Centers the camera on a world position (clamped to the map). Safe to call during scene
        /// setup before the camera is initialised: the request is latched and applied in
        /// OnAddedToEntity instead of the default map-center position. Does not switch to manual
        /// control, so hero auto-follow (if enabled) resumes seamlessly afterwards.
        /// </summary>
        public void CenterOnWorldPosition(Vector2 worldPosition)
        {
            if (_camera == null)
            {
                _pendingCenter = worldPosition;
                _hasPendingCenter = true;
                return;
            }

            _camera.Position = ConstrainCameraPosition(worldPosition);
            QuantizeCameraPosition();
        }

        /// <summary>
        /// World-space Y of the top edge of the visible area, computed from the scene render target
        /// (not Camera.Bounds, which reads the backbuffer viewport when called outside Scene.Update).
        /// </summary>
        public float GetVisibleWorldTop()
        {
            if (_camera == null)
                return _tileMapBounds.Y;
            return ComputeVisibleWorldTop(_camera.Position.Y, GetSceneRenderTargetSize().Y, _camera.RawZoom);
        }

        /// <summary>Pure helper: visible top edge for a camera centre Y, render-target height and zoom</summary>
        public static float ComputeVisibleWorldTop(float cameraY, int renderTargetHeight, float zoom)
        {
            if (zoom <= 0f)
                zoom = 1f;
            return cameraY - (renderTargetHeight / zoom) * 0.5f;
        }

        /// <summary>
        /// Resets camera zoom to the default while keeping the camera centered on the world
        /// position it is currently looking at (clamped to the new zoom's bounds).
        /// </summary>
        public void ResetZoomToDefault()
        {
            SetZoomPreservingFocus(GameConfig.CameraDefaultZoom, "ResetZoomToDefault");
        }

        /// <summary>
        /// Applies the half-window default zoom while keeping the camera centered on the world
        /// position it is currently looking at (clamped to the new zoom's bounds).
        /// </summary>
        public void ApplyHalfWindowZoom()
        {
            SetZoomPreservingFocus(GameConfig.CameraHalfSizeWindowZoom, "ApplyHalfWindowZoom");
        }

        private void SetZoomPreservingFocus(float zoom, string logContext)
        {
            if (_camera == null)
                return;
            var focus = _camera.Position;
            _camera.RawZoom = zoom;
            _camera.Position = ConstrainCameraPosition(focus);
            QuantizeCameraPosition();
            Debug.Log($"[CameraController] {logContext} zoom={_camera.RawZoom} positionX={_camera.Position.X} positionY={_camera.Position.Y}");
        }

        /// <summary>
        /// Configure zoom limits based on the map being loaded
        /// </summary>
        public void ConfigureZoomForMap(string mapPath)
        {
            if (!string.IsNullOrEmpty(mapPath) && mapPath.Contains("Large"))
            {
                _currentMinimumZoom = GameConfig.CameraMinimumZoomLargeMap;
                Debug.Log($"Large map detected: Zoom out enabled (minimum zoom: {_currentMinimumZoom}x)");
            }
            else
            {
                _currentMinimumZoom = GameConfig.CameraMinimumZoom;
                Debug.Log($"Normal map detected: Zoom out disabled (minimum zoom: {_currentMinimumZoom}x)");
            }
            if (_camera != null)
            {
                _camera.SetMinimumZoom(_currentMinimumZoom);
                _camera.SetMaximumZoom(_currentMaximumZoom);
                _camera.RawZoom = MathHelper.Clamp(SnapZoomToStep(_camera.RawZoom), _currentMinimumZoom, _currentMaximumZoom);
                QuantizeCameraPosition();
            }
        }
    }
}
namespace PitHero.ECS.Components
{
    /// <summary>A camera view snapshot: where the player was looking and whether the camera was following the hero.</summary>
    public struct CameraViewState
    {
        public Microsoft.Xna.Framework.Vector2 Position;
        public float RawZoom;
        public bool IsFollowingHero;
        public float ManualControlTimer;
    }
}
