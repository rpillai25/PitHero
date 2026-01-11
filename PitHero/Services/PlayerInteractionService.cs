using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.Services
{
    /// <summary>
    /// Service that tracks player interaction with selectable entities on the map.
    /// Used to prevent camera auto-scroll from interrupting player focus.
    /// </summary>
    public class PlayerInteractionService
    {
        private bool _isInteractingWithSelectable;
        private Vector2 _lastMousePosition;
        private Entity _currentHoveredEntity;

        /// <summary>
        /// Returns true if the player is currently interacting with a selectable entity
        /// </summary>
        public bool IsInteractingWithSelectable => _isInteractingWithSelectable;

        /// <summary>
        /// Notifies the service that the player has started hovering over a selectable entity
        /// </summary>
        public void OnSelectableHoverStart(Entity selectableEntity)
        {
            if (_currentHoveredEntity != selectableEntity)
            {
                _currentHoveredEntity = selectableEntity;
                _isInteractingWithSelectable = true;
                _lastMousePosition = Input.ScaledMousePosition;
                Debug.Log($"[PlayerInteraction] Started hovering over selectable: {selectableEntity?.Name}");
            }
        }

        /// <summary>
        /// Notifies the service that the player has stopped hovering over selectables
        /// </summary>
        public void OnSelectableHoverEnd()
        {
            if (_currentHoveredEntity != null)
            {
                _currentHoveredEntity = null;
                _isInteractingWithSelectable = false;
                Debug.Log("[PlayerInteraction] Stopped hovering over selectables");
            }
        }

        /// <summary>
        /// Notifies the service that the player clicked on a selectable entity
        /// </summary>
        public void OnSelectableClicked(Entity selectableEntity)
        {
            _currentHoveredEntity = selectableEntity;
            _isInteractingWithSelectable = true;
            _lastMousePosition = Input.ScaledMousePosition;
            Debug.Log($"[PlayerInteraction] Clicked on selectable: {selectableEntity?.Name}");
        }

        /// <summary>
        /// Updates the interaction state based on mouse movement while hovering
        /// Call this from Update() when a selectable is hovered
        /// </summary>
        public void UpdateHoverState()
        {
            if (!_isInteractingWithSelectable || _currentHoveredEntity == null)
                return;

            var currentMousePosition = Input.ScaledMousePosition;
            
            // If mouse moved while hovering, reset interaction state to keep camera from auto-scrolling
            if (Vector2.Distance(currentMousePosition, _lastMousePosition) > 0.5f)
            {
                _lastMousePosition = currentMousePosition;
                // Don't log every mouse move to avoid spam
            }
        }

        /// <summary>
        /// Resets the interaction state (call when player finishes with a selectable)
        /// </summary>
        public void Reset()
        {
            _isInteractingWithSelectable = false;
            _currentHoveredEntity = null;
            Debug.Log("[PlayerInteraction] Interaction state reset");
        }
    }
}
