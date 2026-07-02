using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Tweens;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Displays a small UIMarker sprite where an auto-hidden UI element normally rests, so the
    /// player can see where to hover to reveal it. Lives on its own screen-space entity; its color
    /// continuously ping-pongs between white and a target color. Show/hide only toggles the renderer;
    /// the tween runs permanently.
    /// </summary>
    public class UIMarkerComponent : Component
    {
        private const string MARKER_SPRITE = "UIMarker";
        private const float COLOR_TWEEN_DURATION = 1.0f;

        private SpriteRenderer _renderer;
        private ITween<Color> _colorTween;
        private readonly Color _targetColor;
        private readonly bool _flipY;

        public UIMarkerComponent(Color targetColor, bool flipY)
        {
            _targetColor = targetColor;
            _flipY = flipY;
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            if (uiAtlas == null)
            {
                Debug.Warn("[UIMarkerComponent] Failed to load UI.atlas - atlas is null");
                return;
            }

            var sprite = uiAtlas.GetSprite(MARKER_SPRITE);
            if (sprite == null)
            {
                Debug.Warn("[UIMarkerComponent] UIMarker sprite not found in UI.atlas");
                return;
            }

            _renderer = Entity.AddComponent(new SpriteRenderer(sprite));
            _renderer.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD); // screen space, aligned with stage coords
            _renderer.FlipY = _flipY;
            _renderer.SetColor(Color.White);
            _renderer.SetEnabled(false); // start hidden until the tracked element auto-hides

            _colorTween = _renderer.TweenColorTo(_targetColor, COLOR_TWEEN_DURATION)
                .SetEaseType(EaseType.SineIn)
                .SetLoops(LoopType.PingPong, -1);
            _colorTween.Start();
        }

        /// <summary>Positions the marker's center (origin is centered) in stage/design-resolution coords.</summary>
        public void SetCenter(float x, float y)
        {
            if (_renderer != null)
                Entity.Transform.Position = new Vector2(x, y);
        }

        /// <summary>Shows or hides the marker sprite.</summary>
        public void SetVisible(bool visible)
        {
            if (_renderer != null)
                _renderer.SetEnabled(visible);
        }

        public override void OnRemovedFromEntity()
        {
            if (_colorTween != null)
            {
                _colorTween.Stop();
                _colorTween = null;
            }

            base.OnRemovedFromEntity();
        }
    }
}
