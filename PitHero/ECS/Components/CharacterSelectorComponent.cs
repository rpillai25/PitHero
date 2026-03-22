using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Tweens;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Displays a CharacterSelector sprite above the hero when seated at the tavern,
    /// with a Green-to-Blue color tween and vertical bobbing motion
    /// </summary>
    public class CharacterSelectorComponent : Component, IUpdatable
    {
        private const string CHARACTER_SELECTOR_SPRITE = "CharacterSelector";
        private const float COLOR_TWEEN_DURATION = 0.9f;
        private const int BOB_SPEED = 8;
        private const int BOB_AXIS_Y = 1;

        private SpriteRenderer _renderer;
        private HeroComponent _hero;
        private ITween<Color> _colorTween;
        private bool _isShowing;
        private Vector2 _baseOffset = new Vector2(0, -48);

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            _hero = Entity.GetComponent<HeroComponent>();

            try
            {
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                if (uiAtlas == null)
                {
                    Debug.Warn("[CharacterSelectorComponent] Failed to load UI.atlas - atlas is null");
                    return;
                }

                var sprite = uiAtlas.GetSprite(CHARACTER_SELECTOR_SPRITE);
                if (sprite == null)
                {
                    Debug.Warn("[CharacterSelectorComponent] CharacterSelector sprite not found in UI.atlas");
                    return;
                }

                _renderer = Entity.AddComponent(new SpriteRenderer(sprite));
                _renderer.SetRenderLayer(GameConfig.RenderLayerTop);
                _renderer.SetLocalOffset(_baseOffset);
                _renderer.SetEnabled(false);
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[CharacterSelectorComponent] Failed to initialize: {ex.Message}");
            }
        }

        public void Update()
        {
            if (_hero == null || _renderer == null) return;

            if (_hero.SeatedInTavern && !_isShowing)
            {
                Show();
            }
            else if (!_hero.SeatedInTavern && _isShowing)
            {
                Hide();
            }

            if (_isShowing)
            {
                _renderer.LocalOffset = _baseOffset + BobScaleHelper.GetPositionOffset(BOB_SPEED, BOB_AXIS_Y, 0);
            }
        }

        /// <summary>
        /// Shows the selector sprite with color tween and bobbing
        /// </summary>
        private void Show()
        {
            _isShowing = true;
            _renderer.SetEnabled(true);
            _renderer.SetColor(Color.Green);
            _colorTween = _renderer.TweenColorTo(Color.Blue, COLOR_TWEEN_DURATION)
                .SetEaseType(EaseType.SineIn)
                .SetLoops(LoopType.PingPong, -1);
            _colorTween.Start();
        }

        /// <summary>
        /// Hides the selector sprite and stops the color tween
        /// </summary>
        private void Hide()
        {
            _isShowing = false;
            _renderer.SetEnabled(false);

            if (_colorTween != null)
            {
                _colorTween.Stop();
                _colorTween = null;
            }
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
