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
    /// Renders the action queue as sprites displayed over the HUD head during battle.
    /// Monitors the hero's or a mercenary's BattleActionQueue: shows up to 5 queued actions
    /// and animates completed ones floating up. ShowAction() triggers a one-off animation
    /// for actions that never pass through the monitored queue (e.g. mercenary AI actions).
    /// </summary>
    public class ActionQueueVisualizationComponent : RenderableComponent, IUpdatable
    {
        private const int SpriteSize = 32; // Size of each action sprite
        private const int SpriteSpacing = 2; // Spacing between sprites
        private const float AnimationDuration = 1.0f; // Duration of slide + fade animation in seconds (increase to slow down, decrease to speed up)
        private const float SlideDistance = SpriteSize + SpriteSpacing; // Distance to slide up (34 pixels)

        private HeroComponent _heroComponent;
        private MercenaryComponent _mercenaryComponent;
        private object _itemsAtlas;
        private object _skillsAtlas;

        private int _lastQueueCount = 0;
        private QueuedAction _lastFirstAction = null;
        private List<AnimatingAction> _animatingActions = new List<AnimatingAction>();

        public override float Width => SpriteSize;
        public override float Height => SpriteSize * ActionQueue.MaxQueueSize + SpriteSpacing * (ActionQueue.MaxQueueSize - 1);

        /// <summary>The action queue this component monitors and renders (hero's or mercenary's).</summary>
        private ActionQueue MonitoredQueue =>
            _heroComponent != null ? _heroComponent.BattleActionQueue : _mercenaryComponent?.BattleActionQueue;

        /// <summary>Set the hero component to continuously monitor its action queue for automatic animation triggers.</summary>
        public void SetHeroComponent(HeroComponent heroComponent)
        {
            _heroComponent = heroComponent;
            _mercenaryComponent = null;
        }

        /// <summary>Set the mercenary component to continuously monitor its action queue (player-queued shortcut skills).</summary>
        public void SetMercenaryComponent(MercenaryComponent mercenaryComponent)
        {
            _mercenaryComponent = mercenaryComponent;
            _heroComponent = null;
        }

        /// <summary>Show a single action animation (used for mercenaries and external triggers).</summary>
        public void ShowAction(QueuedAction action)
        {
            if (action != null)
            {
                _animatingActions.Add(new AnimatingAction(action));
            }
        }

        /// <summary>Initialize the component.</summary>
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
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

            // Queue monitoring: detect when an action is dequeued
            var monitoredQueue = MonitoredQueue;
            if (monitoredQueue != null)
            {
                var actions = monitoredQueue.GetAll();
                int currentQueueCount = actions?.Length ?? 0;

                // Detect when an action is completed (queue count decreased)
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
            }

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

            // Entity position is the head position on the HUD (screen space)
            var pos = Entity.Transform.Position;
            float startX = pos.X;
            float startY = pos.Y;

            // Render animating (completed/triggered) actions first
            for (int i = 0; i < _animatingActions.Count; i++)
            {
                var animating = _animatingActions[i];
                RenderAction(animating.Action, batcher, itemsAtlas, skillsAtlas, startX, startY, animating.YOffset, animating.ElapsedTime);
            }

            // Render active queue actions (hero or monitored mercenary)
            var monitoredQueue = MonitoredQueue;
            if (monitoredQueue != null)
            {
                var actions = monitoredQueue.GetAll();
                if (actions != null && actions.Length > 0)
                {
                    for (int i = 0; i < actions.Length; i++)
                    {
                        float yOffset = i * (SpriteSize + SpriteSpacing);
                        RenderAction(actions[i], batcher, itemsAtlas, skillsAtlas, startX, startY, yOffset, -1f);
                    }
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
                    // For items, use the sprite key with empty background
                    backgroundSprite = skillsAtlas.GetSprite("base.empty");
                    sprite = itemsAtlas.GetSprite(action.Consumable.SpriteName);
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
                        // Use weapon item sprite from items atlas with empty background.
                        // SpriteName, not Name — tiered weapons ("DepthsReaver+2") aren't atlas keys.
                        backgroundSprite = skillsAtlas.GetSprite("base.empty");
                        sprite = itemsAtlas.GetSprite(action.WeaponItem.SpriteName);
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

        /// <summary>Visible during battle (screen-space rendering ignores camera bounds).</summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            return PitHero.AI.HeroStateMachine.IsBattleInProgress;
        }
    }
}
