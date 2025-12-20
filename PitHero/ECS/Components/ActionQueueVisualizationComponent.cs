using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.AI;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Represents a completed action that is animating out
    /// </summary>
    internal class AnimatingAction
    {
        public QueuedAction Action { get; set; }
        public float ElapsedTime { get; set; }
        public float YOffset { get; set; }

        public AnimatingAction(QueuedAction action)
        {
            Action = action;
            ElapsedTime = 0f;
            YOffset = 0f;
        }
    }

    /// <summary>
    /// Renders the action queue as sprites displayed to the right of the hero during battle.
    /// Shows up to 5 actions vertically stacked from top to bottom.
    /// Completed actions slide up and fade out over 0.5 seconds.
    /// </summary>
    public class ActionQueueVisualizationComponent : RenderableComponent, IUpdatable
    {
        private const int SpriteSize = 32; // Size of each action sprite
        private const int SpriteSpacing = 2; // Spacing between sprites
        private const int OffsetX = 40; // Distance from hero center to first sprite
        private const float AnimationDuration = 0.5f; // Duration of slide + fade animation in seconds
        private const float SlideDistance = SpriteSize + SpriteSpacing; // Distance to slide up (32 pixels)

        private HeroComponent _heroComponent;
        private object _itemsAtlas;
        private object _skillsAtlas;

        private int _lastQueueCount = 0;
        private QueuedAction _lastFirstAction = null;
        private List<AnimatingAction> _animatingActions = new List<AnimatingAction>();

        public override float Width => SpriteSize;
        public override float Height => SpriteSize * ActionQueue.MaxQueueSize + SpriteSpacing * (ActionQueue.MaxQueueSize - 1);

        /// <summary>Initialize the component with hero reference.</summary>
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _heroComponent = Entity.GetComponent<HeroComponent>();
            _lastQueueCount = 0;
            _lastFirstAction = null;
        }

        /// <summary>Update animations for completed actions.</summary>
        public void Update()
        {
            if (!PitHero.AI.HeroStateMachine.IsBattleInProgress)
            {
                // Clear animations when not in battle
                _animatingActions.Clear();
                _lastQueueCount = 0;
                _lastFirstAction = null;
                return;
            }

            if (_heroComponent == null || _heroComponent.BattleActionQueue == null)
                return;

            var actions = _heroComponent.BattleActionQueue.GetAll();
            int currentQueueCount = actions?.Length ?? 0;

            // Detect when an action is completed (queue count decreased or first action changed)
            if (_lastQueueCount > 0 && currentQueueCount < _lastQueueCount)
            {
                // An action was completed - start animating it
                if (_lastFirstAction != null)
                {
                    _animatingActions.Add(new AnimatingAction(_lastFirstAction));
                }
            }

            // Update last state
            _lastQueueCount = currentQueueCount;
            _lastFirstAction = currentQueueCount > 0 ? actions[0] : null;

            // Update all animating actions
            for (int i = _animatingActions.Count - 1; i >= 0; i--)
            {
                var animating = _animatingActions[i];
                animating.ElapsedTime += Time.DeltaTime;

                // Calculate slide progress (0 to 1)
                float progress = animating.ElapsedTime / AnimationDuration;
                if (progress > 1f) progress = 1f;

                // Slide up using easing
                animating.YOffset = -SlideDistance * progress;

                // Remove completed animations
                if (animating.ElapsedTime >= AnimationDuration)
                {
                    _animatingActions.RemoveAt(i);
                }
            }
        }

        /// <summary>Render the action queue sprites.</summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            // Only render during battle
            if (!PitHero.AI.HeroStateMachine.IsBattleInProgress)
                return;

            if (_heroComponent == null || _heroComponent.BattleActionQueue == null)
                return;

            var actions = _heroComponent.BattleActionQueue.GetAll();

            // Only load atlases if Core.Content is available and not already cached
            if (Core.Content != null)
            {
                // Lazy load atlases on first use
                if (_itemsAtlas == null)
                {
                    _itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                }
                if (_skillsAtlas == null)
                {
                    _skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                }
            }

            // Can't render without atlases
            if (_itemsAtlas == null || _skillsAtlas == null)
                return;

            // Cast to dynamic to call GetSprite (avoids type resolution issues)
            dynamic itemsAtlas = _itemsAtlas;
            dynamic skillsAtlas = _skillsAtlas;

            // Get hero position
            var heroPos = Entity.Transform.Position;

            // Calculate starting position (to the right of hero, starting from top)
            float startX = heroPos.X + OffsetX;
            float startY = heroPos.Y - (Height / 2f); // Center vertically around hero

            // Render animating (completed) actions first
            foreach (var animating in _animatingActions)
            {
                RenderAction(animating.Action, batcher, itemsAtlas, skillsAtlas, startX, startY, animating.YOffset, animating.ElapsedTime);
            }

            // Render active queue actions
            if (actions != null && actions.Length > 0)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    float yOffset = i * (SpriteSize + SpriteSpacing);
                    RenderAction(actions[i], batcher, itemsAtlas, skillsAtlas, startX, startY, yOffset, -1f);
                }
            }
        }

        /// <summary>Render a single action sprite with optional animation.</summary>
        private void RenderAction(QueuedAction action, Batcher batcher, dynamic itemsAtlas, dynamic skillsAtlas,
            float startX, float startY, float yOffset, float animationTime)
        {
            Sprite sprite = null;
            Sprite backgroundSprite = null;

            try
            {
                if (action.ActionType == QueuedActionType.UseItem && action.Consumable != null)
                {
                    // For items, use the item name as sprite key with empty background
                    backgroundSprite = skillsAtlas.GetSprite("base.empty");
                    sprite = itemsAtlas.GetSprite(action.Consumable.Name);
                }
                else if (action.ActionType == QueuedActionType.UseSkill && action.Skill != null)
                {
                    // For skills, use the skill ID as sprite key (skills already have their own backgrounds)
                    sprite = skillsAtlas.GetSprite(action.Skill.Id);
                }
                else if (action.ActionType == QueuedActionType.Attack)
                {
                    // For attacks, use weapon sprite if equipped, otherwise use "base.punch" sprite
                    if (action.WeaponItem != null)
                    {
                        // Use weapon item sprite from items atlas with empty background
                        backgroundSprite = skillsAtlas.GetSprite("base.empty");
                        sprite = itemsAtlas.GetSprite(action.WeaponItem.Name);
                    }
                    else
                    {
                        // Use base punch sprite for unarmed attacks (already has background)
                        sprite = skillsAtlas.GetSprite("base.punch");
                    }
                }
            }
            catch
            {
                // Silently ignore missing sprites
                return;
            }

            // Calculate position for this action
            float x = startX;
            float y = startY + yOffset;

            // Calculate alpha for fade out animation
            byte alpha = 255;
            if (animationTime >= 0f)
            {
                float fadeProgress = animationTime / AnimationDuration;
                if (fadeProgress > 1f) fadeProgress = 1f;
                alpha = (byte)(255 * (1f - fadeProgress));
            }

            Color colorWithAlpha = new Color(255, 255, 255, alpha);

            // Draw background sprite first if needed
            if (backgroundSprite != null)
            {
                var backgroundDrawable = new SpriteDrawable(backgroundSprite);
                backgroundDrawable.Draw(batcher, x, y, SpriteSize, SpriteSize, colorWithAlpha);
            }

            // Draw the action sprite on top
            if (sprite != null)
            {
                var drawable = new SpriteDrawable(sprite);
                drawable.Draw(batcher, x, y, SpriteSize, SpriteSize, colorWithAlpha);
            }
        }

        /// <summary>Check if battle is in progress (for rendering).</summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            return PitHero.AI.HeroStateMachine.IsBattleInProgress && base.IsVisibleFromCamera(camera);
        }
    }
}
