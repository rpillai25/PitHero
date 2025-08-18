using Microsoft.Xna.Framework;
using Nez;

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
            }
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
            // Get mouse wheel delta for zooming
            var wheelDelta = Input.MouseWheelDelta;
            if (wheelDelta != 0)
            {
                // Store current mouse position in world coordinates
                var mouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
                
                // Calculate new zoom level
                var zoomChange = wheelDelta * GameConfig.CameraZoomSpeed;
                var newZoom = _camera.RawZoom + zoomChange;
                
                // Clamp zoom to the configured limits
                newZoom = MathHelper.Clamp(newZoom, GameConfig.CameraMinimumZoom, GameConfig.CameraMaximumZoom);
                
                // Set the new zoom
                _camera.RawZoom = newZoom;
                
                // Adjust camera position so it zooms towards the mouse cursor
                var newMouseWorldPos = _camera.ScreenToWorldPoint(Input.ScaledMousePosition);
                var worldPosDelta = mouseWorldPos - newMouseWorldPos;
                _camera.Position += worldPosDelta;
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
                
                _camera.Position += panDelta;
                
                _lastMousePosition = currentMousePosition;
            }
        }
    }
}