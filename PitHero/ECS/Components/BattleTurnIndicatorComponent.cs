using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Tweens;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Displays a CharacterSelector sprite above the battle participant whose turn it is.
    /// Lives on its own entity and follows the target entity's position.
    /// </summary>
    public class BattleTurnIndicatorComponent : Component, IUpdatable
    {
        private const string CHARACTER_SELECTOR_SPRITE = "CharacterSelector";
        private const float COLOR_TWEEN_DURATION = 1.9f;
        private const int BOB_SPEED = 8;
        private const int BOB_AXIS_Y = 1;

        private SpriteRenderer _renderer;
        private ITween<Color> _colorTween;
        private Entity _targetEntity;
        private bool _isShowing;
        private bool _isMonsterMode;
        private Vector2 _baseOffset = new Vector2(0, -48);

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            try
            {
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                if (uiAtlas == null)
                {
                    Debug.Warn("[BattleTurnIndicator] Failed to load UI.atlas - atlas is null");
                    return;
                }

                var sprite = uiAtlas.GetSprite(CHARACTER_SELECTOR_SPRITE);
                if (sprite == null)
                {
                    Debug.Warn("[BattleTurnIndicator] CharacterSelector sprite not found in UI.atlas");
                    return;
                }

                _renderer = Entity.AddComponent(new SpriteRenderer(sprite));
                _renderer.SetRenderLayer(GameConfig.RenderLayerTop);
                _renderer.SetEnabled(false);
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[BattleTurnIndicator] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the selector above the given entity. Uses Red-to-Yellow tween for monsters,
        /// Green-to-Blue tween for allies (hero/mercenary).
        /// </summary>
        public void Show(Entity target, bool isMonster = false)
        {
            if (_renderer == null || target == null) return;

            _targetEntity = target;

            if (!_isShowing || _isMonsterMode != isMonster)
            {
                _isMonsterMode = isMonster;
                _isShowing = true;
                _renderer.SetEnabled(true);

                if (_colorTween != null)
                {
                    _colorTween.Stop();
                    _colorTween = null;
                }

                var baseColor = isMonster ? Color.Red : Color.Green;
                var targetColor = isMonster ? Color.Yellow : Color.Blue;

                _renderer.SetColor(baseColor);
                _colorTween = _renderer.TweenColorTo(targetColor, COLOR_TWEEN_DURATION)
                    .SetEaseType(EaseType.SineIn)
                    .SetLoops(LoopType.PingPong, -1);
                _colorTween.Start();
            }

            Entity.Transform.Position = _targetEntity.Transform.Position;
            _renderer.LocalOffset = _baseOffset + BobScaleHelper.GetPositionOffset(BOB_SPEED, BOB_AXIS_Y, 0);
        }

        /// <summary>
        /// Hides the selector
        /// </summary>
        public void Hide()
        {
            _isShowing = false;
            _targetEntity = null;

            if (_renderer != null)
            {
                _renderer.SetEnabled(false);
            }

            if (_colorTween != null)
            {
                _colorTween.Stop();
                _colorTween = null;
            }
        }

        public void Update()
        {
            if (!_isShowing || _targetEntity == null || _renderer == null) return;

            Entity.Transform.Position = _targetEntity.Transform.Position;
            _renderer.LocalOffset = _baseOffset + BobScaleHelper.GetPositionOffset(BOB_SPEED, BOB_AXIS_Y, 0);
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
