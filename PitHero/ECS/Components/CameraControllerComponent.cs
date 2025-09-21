using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that handles camera zoom and pan controls via mouse input
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

        /// <summary>
        /// Gets whether this component should respect the global pause state.
        /// Camera controls are allowed during pause for UI navigation.
        /// </summary>
        public bool ShouldPause => false;

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

            HandleZoomInput();
            HandlePanInput();
        }

        private void HandleZoomInput()
        {
            // Middle click reset
            if (Input.MiddleMouseButtonPressed)
            {
                // restore window if in half-height mode
                if (WindowManager.IsHalfHeightMode())
                    WindowManager.RestoreOriginalSize(Core.Instance);
                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                _camera.Position = ConstrainCameraPosition(_defaultCameraPosition);
                return;
            }

            var wheelDelta = Input.MouseWheelDelta;
            if (wheelDelta == 0)
                return;

            bool zoomingOut = wheelDelta < 0;
            var mouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
            var currentZoom = _camera.RawZoom;
            float newZoom = currentZoom;

            if (zoomingOut)
            {
                // progressive shrink levels before actually reducing zoom
                if (currentZoom <= 1f)
                {
                    if (!WindowManager.IsHalfHeightMode())
                    {
                        WindowManager.ShrinkToNextLevel(Core.Instance); // Half
                        CenterCameraOnMap();
                        return; // do not change zoom
                    }
                    else if (!WindowManager.IsQuarterHeightMode())
                    {
                        WindowManager.ShrinkToNextLevel(Core.Instance); // Quarter
                        CenterCameraOnMap();
                        return; // still do not change zoom
                    }
                    else
                    {
                        // already at smallest window. Now allow real zoom decrement (e.g., to 0.5) if permitted
                        if (currentZoom > _currentMinimumZoom)
                            newZoom = _currentMinimumZoom;
                    }
                }
                else
                {
                    // higher than 1x: integer step down
                    newZoom = (float)System.Math.Floor(currentZoom) - 1f;
                    if (newZoom < 1f)
                        newZoom = 1f; // let shrink sequence handle further perceived zooming out
                }
            }
            else
            {
                // zooming in
                if (WindowManager.IsQuarterHeightMode())
                {
                    // first restore to half (one level visually) before actual zoom change
                    WindowManager.RestoreOriginalSize(Core.Instance); // restore fully first then re-shrink to half if still below 1? Simpler: full restore
                    // remain at same zoom but full size; user can zoom in again to adjust zoom levels
                    _camera.Position = ConstrainCameraPosition(_camera.Position);
                    return;
                }
                else if (WindowManager.IsHalfHeightMode())
                {
                    WindowManager.RestoreOriginalSize(Core.Instance);
                    _camera.Position = ConstrainCameraPosition(_camera.Position);
                    return;
                }
                // actual zoom in (integer steps)
                if (currentZoom == 0.5f)
                    newZoom = 1f;
                else
                    newZoom = (float)System.Math.Floor(currentZoom) + 1f;
            }

            newZoom = MathHelper.Clamp(newZoom, _currentMinimumZoom, _currentMaximumZoom);

            if (System.Math.Abs(newZoom - currentZoom) <= 0.01f)
                return;

            _camera.RawZoom = newZoom;

            var newMouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
            var worldPosDelta = mouseWorldPos - newMouseWorldPos;
            var desiredPosition = _camera.Position + new Vector2(
                (float)System.Math.Round(worldPosDelta.X),
                (float)System.Math.Round(worldPosDelta.Y));
            _camera.Position = ConstrainCameraPosition(desiredPosition);
        }

        private void CenterCameraOnMap()
        {
            var mapCenterX = _tileMapBounds.X + _tileMapBounds.Width / 2f;
            var mapCenterY = _tileMapBounds.Y + _tileMapBounds.Height / 2f;
            _camera.Position = new Vector2(mapCenterX, mapCenterY);
            Debug.Log($"CenterCameraOnMap -> CenterX={mapCenterX} CenterY={mapCenterY}");
        }

        private void HandlePanInput()
        {
            var currentMousePosition = Input.ScaledMousePosition;

            if (Input.RightMouseButtonPressed)
            {
                _isPanning = true;
                _lastMousePosition = currentMousePosition;
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
                _lastMousePosition = currentMousePosition;
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
            }
        }
    }
}