using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Generic placeholder monster animation component using PlaceholderMonster sprite.
    /// This allows for easy future sprite replacement by subclassing or updating the animation names.
    /// </summary>
    public class PlaceholderMonsterAnimationComponent : EnemyAnimationComponent
    {
        public PlaceholderMonsterAnimationComponent(Color color = default) : base(color == default ? Color.White : color)
        {
        }

        // PlaceholderMonster is a static sprite, so we use the same sprite for all directions
        protected override string DefaultAnimation => "PlaceholderMonster";
        protected override string AnimDown => "PlaceholderMonster";
        protected override string AnimLeft => "PlaceholderMonster";
        protected override string AnimRight => "PlaceholderMonster";
        protected override string AnimUp => "PlaceholderMonster";

        public override void OnAddedToEntity()
        {
            // Don't call base, as it tries to load animations for animated enemies
            try
            {
                var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (actorsAtlas != null)
                {
                    var sprite = actorsAtlas.GetSprite("PlaceholderMonster");
                    if (sprite != null)
                    {
                        this.Sprite = sprite;
                        this.SetColor(ComponentColor);
                        Debug.Log("[PlaceholderMonsterAnimationComponent] Loaded static sprite PlaceholderMonster");
                    }
                    else
                    {
                        Debug.Warn("[PlaceholderMonsterAnimationComponent] Sprite 'PlaceholderMonster' not found in Actors.atlas");
                    }
                }
                else
                {
                    Debug.Warn("[PlaceholderMonsterAnimationComponent] Failed to load Actors.atlas - atlas is null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[PlaceholderMonsterAnimationComponent] Failed to load sprite: {ex.Message}");
            }

            // Add facing component if not present (for consistency with other enemies)
            var facing = Entity.GetComponent<ActorFacingComponent>();
            if (facing == null)
            {
                Entity.AddComponent(new ActorFacingComponent());
            }
        }

        public new void Update()
        {
            // Do nothing - static sprite, no animation updates needed
        }
    }
}
