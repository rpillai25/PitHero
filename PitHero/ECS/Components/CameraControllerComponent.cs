using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that handles camera zoom and pan controls via mouse input
    /// </summary>
    public class CameraControllerComponent : Component, IUpdatable
    {
        private Camera _camera;
        private Vector2 _lastMousePosition;
        private bool _isPanning;
        private Vector2 _defaultCameraPosition;
        private Rectangle _tileMapBounds;

        public override void OnAddedToEntity()
        {
            // Get the camera component from the same entity
            _camera = Entity.GetComponent<Camera>();
            if (_camera == null)
            {
                // If no camera on this entity, try to get the scene camera
                _camera = Entity.Scene.Camera;
            }

            // Set up initial camera zoom limits using Nez's built-in methods
            if (_camera != null)
            {
                _camera.SetMinimumZoom(GameConfig.CameraMinimumZoom);
                _camera.SetMaximumZoom(GameConfig.CameraMaximumZoom);
                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                
                // Store the default position for resetting and ensure camera starts centered
                _defaultCameraPosition = new Vector2(GameConfig.VirtualWidth / 2f, GameConfig.VirtualHeight / 2f);
                _camera.Position = _defaultCameraPosition;
            }

            // Get TileMap bounds for panning constraints
            InitializeTileMapBounds();
        }

        public void Update()
        {
            if (_camera == null)
                return;

            HandleZoomInput();
            HandlePanInput();
        }

        private void HandleZoomInput()
        {
            // Handle middle mouse button click to reset zoom
            if (Input.MiddleMouseButtonPressed)
            {
                _camera.RawZoom = GameConfig.CameraDefaultZoom;
                _camera.Position = ConstrainCameraPosition(_defaultCameraPosition);
                return;
            }

            // Get mouse wheel delta for zooming
            var wheelDelta = Input.MouseWheelDelta;
            if (wheelDelta != 0)
            {
                // Store current mouse position in world coordinates
                var mouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
                
                // Calculate new zoom level using integer increments
                var currentZoom = (int)_camera.RawZoom;
                var zoomChange = wheelDelta > 0 ? 1 : -1; // Increment/decrement by 1
                var newZoom = currentZoom + zoomChange;
                
                // Clamp zoom to the configured limits (integer values only)
                newZoom = (int)MathHelper.Clamp(newZoom, GameConfig.CameraMinimumZoom, GameConfig.CameraMaximumZoom);
                
                // Only update if zoom actually changed
                if (newZoom != currentZoom)
                {
                    // Set the new zoom
                    _camera.RawZoom = (float)newZoom;
                    
                    // Adjust camera position so it zooms towards the mouse cursor
                    var newMouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
                    var worldPosDelta = mouseWorldPos - newMouseWorldPos;
                    
                    // Round to integer pixels to avoid artifacts
                    var desiredPosition = _camera.Position + new Vector2(
                        (float)System.Math.Round(worldPosDelta.X),
                        (float)System.Math.Round(worldPosDelta.Y)
                    );

                    // Constrain camera position to TileMap bounds
                    _camera.Position = ConstrainCameraPosition(desiredPosition);
                }
            }
        }

        private void HandlePanInput()
        {
            var currentMousePosition = Input.ScaledMousePosition;

            // Start panning when right mouse button is pressed
            if (Input.RightMouseButtonPressed)
            {
                _isPanning = true;
                _lastMousePosition = currentMousePosition;
            }
            // Stop panning when right mouse button is released
            else if (Input.RightMouseButtonReleased)
            {
                _isPanning = false;
            }

            // Pan the camera while right mouse button is held down
            if (_isPanning && Input.RightMouseButtonDown)
            {
                var mouseDelta = currentMousePosition - _lastMousePosition;
                
                // Move camera in opposite direction of mouse movement
                // Scale by zoom level so panning feels consistent at different zoom levels
                var panDelta = -mouseDelta * GameConfig.CameraPanSpeed / _camera.RawZoom;
                
                // Round to integer pixels to avoid artifacts
                var newPosition = _camera.Position + new Vector2(
                    (float)System.Math.Round(panDelta.X),
                    (float)System.Math.Round(panDelta.Y)
                );

                // Constrain camera position to TileMap bounds
                newPosition = ConstrainCameraPosition(newPosition);
                _camera.Position = newPosition;
                
                _lastMousePosition = currentMousePosition;
            }
        }

        /// <summary>
        /// Initialize TileMap bounds by finding the TiledMapRenderer in the scene
        /// </summary>
        private void InitializeTileMapBounds()
        {
            // Default bounds based on TMX file: 60×12 tiles at 32×32 pixels each
            _tileMapBounds = new Rectangle(0, 0, 1920, 384);

            // Try to get actual bounds from TiledMapRenderer if available
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
                    Debug.Log($"TileMap bounds initialized: {_tileMapBounds}");
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
        /// Constrains camera position to ensure viewport doesn't go outside TileMap bounds
        /// </summary>
        private Vector2 ConstrainCameraPosition(Vector2 desiredPosition)
        {
            // Calculate viewport size based on current zoom
            var viewportWidth = GameConfig.VirtualWidth / _camera.RawZoom;
            var viewportHeight = GameConfig.VirtualHeight / _camera.RawZoom;

            // Calculate camera bounds (camera position is center of viewport)
            var minX = _tileMapBounds.X + viewportWidth / 2f;
            var maxX = _tileMapBounds.Right - viewportWidth / 2f;
            var minY = _tileMapBounds.Y + viewportHeight / 2f;
            var maxY = _tileMapBounds.Bottom - viewportHeight / 2f;

            // If viewport is larger than tilemap, center it
            if (viewportWidth >= _tileMapBounds.Width)
            {
                desiredPosition.X = _tileMapBounds.X + _tileMapBounds.Width / 2f;
            }
            else
            {
                desiredPosition.X = MathHelper.Clamp(desiredPosition.X, minX, maxX);
            }

            if (viewportHeight >= _tileMapBounds.Height)
            {
                desiredPosition.Y = _tileMapBounds.Y + _tileMapBounds.Height / 2f;
            }
            else
            {
                desiredPosition.Y = MathHelper.Clamp(desiredPosition.Y, minY, maxY);
            }

            return desiredPosition;
        }
    }
}