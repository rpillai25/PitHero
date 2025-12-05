using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using PitHero.AI;
using RolePlayingFramework.Equipment;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders the action queue as sprites displayed to the right of the hero during battle.
    /// Shows up to 5 actions vertically stacked from top to bottom.
    /// </summary>
    public class ActionQueueVisualizationComponent : RenderableComponent, IUpdatable
    {
        private const int SpriteSize = 32; // Size of each action sprite
        private const int SpriteSpacing = 2; // Spacing between sprites
        private const int OffsetX = 40; // Distance from hero center to first sprite
        
        private HeroComponent _heroComponent;
        
        public override float Width => SpriteSize;
        public override float Height => SpriteSize * ActionQueue.MaxQueueSize + SpriteSpacing * (ActionQueue.MaxQueueSize - 1);
        
        /// <summary>Initialize the component with hero reference.</summary>
        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _heroComponent = Entity.GetComponent<HeroComponent>();
        }
        
        /// <summary>Update the component (required by IUpdatable).</summary>
        public void Update()
        {
            // Nothing to update per frame
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
            
            // Only render if Core.Content is available
            if (Core.Content == null)
                return;
            
            // Load sprite atlases
            var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
            var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
            
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
                
                // If we found a sprite, render it
                if (sprite != null)
                {
                    float x = startX;
                    float y = startY + i * (SpriteSize + SpriteSpacing);
                    
                    var destRect = new Rectangle((int)x, (int)y, SpriteSize, SpriteSize);
                    batcher.Draw(sprite, destRect, Color.White);
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
