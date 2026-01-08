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

        private bool _isFollowingHero = true; // camera auto-follows hero by default
        private float _manualControlTimer = 0f; // tracks time since last manual control input
        private Entity _heroEntity; // cached reference to hero entity

        /// <summary>
        /// Gets whether this component should respect the global pause state.
        /// We shouldn't modify camera size or zoom while paused from menu.
        /// We'll have separate menu controls that handle this.
        /// </summary>
        public bool ShouldPause => true;

        public override void OnAddedToEntity()
        {
            _camera = Entity.GetComponent<Camera>() ?? Entity.Scene.Camera;
            if (_camera != null)
            {
                _camera.SetMinimumZoom(_currentMinimumZoom);
                _camera.SetMaximumZoom(_currentMaximumZoom);
                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                _defaultCameraPosition = new Vector2(GameConfig.VirtualWidth / 2f, GameConfig.VirtualHeight / 2f);
                _camera.Position = _defaultCameraPosition;
                QuantizeCameraPosition();
            }
            InitializeTileMapBounds();
        }

        public void Update()
        {
            if (_camera == null)
                return;

            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true && ShouldPause)
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
            HandleHeroFollowing();
        }

        private void HandleZoomInput()
        {
            // Pre-calc shift state since it impacts both right-click reset and wheel behavior
            bool shiftDown = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);

            // SHIFT + Right-Click: Reset window size (if shrunk) and reset zoom + recenter
            if (shiftDown && Input.RightMouseButtonPressed)
            {
                if (WindowManager.IsHalfHeightMode())
                    WindowManager.RestoreOriginalSize(Core.Instance);

                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                _camera.Position = ConstrainCameraPosition(_defaultCameraPosition);
                QuantizeCameraPosition();
                Debug.Log($"[CameraController] SHIFT+RightClick reset zoom={_camera.RawZoom} positionX={_camera.Position.X} positionY={_camera.Position.Y}");
                return;
            }

            // SHIFT + Middle-Click: Reset zoom (preserve current window size)
            if (shiftDown && Input.MiddleMouseButtonPressed)
            {
                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                _camera.Position = ConstrainCameraPosition(_defaultCameraPosition);
                QuantizeCameraPosition();
                Debug.Log($"[CameraController] SHIFT+MiddleClick reset zoom={_camera.RawZoom} positionX={_camera.Position.X} positionY={_camera.Position.Y}");
                return;
            }

            var wheelDelta = Input.MouseWheelDelta;
            if (wheelDelta == 0)
                return;

            if (shiftDown)
            {
                HandleWindowResizeZoom(wheelDelta);
            }
            else
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
            Debug.Log($"[CameraController] Wheel zoom newZoom={_camera.RawZoom}");
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

            if (Input.RightMouseButtonPressed)
            {
                // Do not start panning if this press is used for SHIFT+Right-Click reset
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
            else if (Input.RightMouseButtonReleased)
            {
                _isPanning = false;
            }

            if (_isPanning && Input.RightMouseButtonDown)
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
        /// Handles automatic camera following of the hero
        /// </summary>
        private void HandleHeroFollowing()
        {
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

        private Vector2 ConstrainCameraPosition(Vector2 desiredPosition)
        {
            var viewportWidth = GameConfig.VirtualWidth / _camera.RawZoom;
            var viewportHeight = GameConfig.VirtualHeight / _camera.RawZoom;

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