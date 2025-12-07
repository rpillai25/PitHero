using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.AI;
using RolePlayingFramework.Equipment;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders the action queue as sprites displayed to the right of the hero during battle.
    /// Shows up to 5 actions vertically stacked from top to bottom.
    /// </summary>
    public class ActionQueueVisualizationComponent : RenderableComponent
    {
        private const int SpriteSize = 32; // Size of each action sprite
        private const int SpriteSpacing = 2; // Spacing between sprites
        private const int OffsetX = 40; // Distance from hero center to first sprite
        
        private HeroComponent _heroComponent;
        private object _itemsAtlas;
        private object _skillsAtlas;
        
        public override float Width => SpriteSize;
        public override float Height => SpriteSize * ActionQueue.MaxQueueSize + SpriteSpacing * (ActionQueue.MaxQueueSize - 1);
        
        /// <summary>Initialize the component with hero reference.</summary>
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _heroComponent = Entity.GetComponent<HeroComponent>();
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
            if (actions == null || actions.Length == 0)
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
            
            // Get hero position
            var heroPos = Entity.Transform.Position;
            
            // Calculate starting position (to the right of hero, starting from top)
            float startX = heroPos.X + OffsetX;
            float startY = heroPos.Y - (Height / 2f); // Center vertically around hero
            
            // Render each action sprite
            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                Sprite sprite = null;
                
                try
                {
                    if (action.ActionType == QueuedActionType.UseItem && action.Consumable != null)
                    {
                        // For items, use the item name as sprite key
                        sprite = itemsAtlas.GetSprite(action.Consumable.Name);
                    }
                    else if (action.ActionType == QueuedActionType.UseSkill && action.Skill != null)
                    {
                        // For skills, use the skill ID as sprite key
                        sprite = skillsAtlas.GetSprite(action.Skill.Id);
                    }
                    else if (action.ActionType == QueuedActionType.Attack)
                    {
                        // For attacks, use weapon sprite if equipped, otherwise use "base.punch" sprite
                        if (action.WeaponItem != null)
                        {
                            // Use weapon item sprite from items atlas
                            sprite = itemsAtlas.GetSprite(action.WeaponItem.Name);
                        }
                        else
                        {
                            // Use base punch sprite for unarmed attacks
                            sprite = skillsAtlas.GetSprite("base.punch");
                        }
                    }
                }
                catch
                {
                    // Silently ignore missing sprites
                    continue;
                }
                
                // If we found a sprite, render it using SpriteDrawable
                if (sprite != null)
                {
                    float x = startX;
                    float y = startY + i * (SpriteSize + SpriteSpacing);
                    
                    var drawable = new SpriteDrawable(sprite);
                    drawable.Draw(batcher, x, y, SpriteSize, SpriteSize, Color.White);
                }
            }
        }
        
        /// <summary>Check if battle is in progress (for rendering).</summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            return PitHero.AI.HeroStateMachine.IsBattleInProgress && base.IsVisibleFromCamera(camera);
        }
    }
}
