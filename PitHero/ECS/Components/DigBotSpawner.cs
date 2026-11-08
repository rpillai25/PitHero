using Nez;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Keeps the DigBot (issue #452) in the world exactly while it should be there: the hero owns the
    /// DigBot artifact and a Digging Field is placed. Checked every fixed step from sim state only, so
    /// buying the artifact, placing the field, loading a save and a replay seek all spawn the bot on
    /// the same tick live and in a replay. Scene-scoped (added in MainGameScene.Begin).
    /// </summary>
    public class DigBotSpawner : SceneComponent
    {
        private Entity _bot;

        /// <summary>The bot's entity, or null while there is none.</summary>
        public Entity Bot => _bot;

        public override void Update()
        {
            var gameState = Core.Services.GetService<GameStateService>();
            var field = Core.Services.GetService<DiggingFieldService>();
            bool wanted = Artifacts.LocalArtifactEffects.HasDigBot(gameState) && field?.GetField() != null;

            if (wanted && _bot == null)
                _bot = CreateBot();
            else if (!wanted && _bot != null)
            {
                _bot.Destroy();
                _bot = null;
            }
        }

        private Entity CreateBot()
        {
            var entity = Scene.CreateEntity("digbot");
            var animator = entity.AddComponent(new PausableSpriteAnimator());
            animator.SetRenderLayer(GameConfig.RenderLayerActors); // Y-sorted with the workers who cross the field
            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            AddAnimation(animator, atlas, DigBotComponent.MoveAnimation);
            AddAnimation(animator, atlas, DigBotComponent.FindAnimation);
            entity.AddComponent(new DigBotComponent()); // positions itself on the field's first tile
            return entity;
        }

        private static void AddAnimation(PausableSpriteAnimator animator, Nez.Sprites.SpriteAtlas atlas, string name)
        {
            int index = atlas != null ? System.Array.IndexOf(atlas.AnimationNames, name) : -1;
            if (index < 0)
            {
                Debug.Warn("[DigBotSpawner] Animation " + name + " not found in Actors.atlas");
                return;
            }
            animator.AddAnimation(name, atlas.SpriteAnimations[index]);
        }
    }
}
