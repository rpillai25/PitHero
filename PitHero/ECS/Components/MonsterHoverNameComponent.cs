using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Scenes;
using PitHero.Services;
using RolePlayingFramework.Enemies;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Draws the name of the pit monster under the mouse, centred just above its sprite, and nothing
    /// otherwise: the name appears the frame the pointer enters the monster and is gone the frame it
    /// leaves. One instance on its own screen-space entity serves every monster. Drawn at 1:1 screen
    /// pixels (anchored through the world camera's WorldToScreenPoint) so the font stays crisp at any
    /// zoom. Hover is live input, so this is never recorded, and it stays silent while a replay's
    /// recorded frame is on screen (the live monsters are not where the recorded ones are drawn).
    /// </summary>
    public sealed class MonsterHoverNameComponent : RenderableComponent, Services.Replay.Frames.ILiveOnlyRenderable
    {
        private const float NameGapY = 2f; // screen pixels between the sprite's top and the name
        private static readonly Color NameColor = Color.White;
        private static readonly Color ShadowColor = Color.Black;

        private TextService _textService;
        private IEnemy _namedEnemy;
        private string _name;

        public override float Width => 1f;
        public override float Height => 1f;

        /// <summary>Called with the screen-space renderer's static camera; hover decides visibility in Render.</summary>
        public override bool IsVisibleFromCamera(Camera camera) => true;

        public override void Render(Batcher batcher, Camera camera)
        {
            var scene = Entity.Scene as MainGameScene;
            var sceneCamera = scene?.Camera;
            if (sceneCamera == null)
                return;

            var replay = Services.Replay.ReplayPlaybackService.Current;
            if (replay != null && replay.IsActive && replay.Viewer != null)
                return;
            // A window, button or panel under the pointer owns the hover
            if (scene.IsPointerOverUI())
                return;

            var font = scene.GetHudFontForCurrentMode();
            if (font == null)
                return;

            var mouseWorld = sceneCamera.MouseToWorldPoint();
            var monsters = scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
            for (int i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                if (monster == null || monster.IsDestroyed || !monster.Enabled)
                    continue;
                // A disabled animator is a monster hidden under fog: it must not give itself away
                var animation = monster.GetComponent<EnemyAnimationComponent>();
                if (animation == null || !animation.Enabled)
                    continue;
                var bounds = animation.Bounds;
                if (!bounds.Contains(mouseWorld))
                    continue;
                var enemy = monster.GetComponent<EnemyComponent>()?.Enemy;
                if (enemy == null)
                    continue;

                if (!ReferenceEquals(enemy, _namedEnemy))
                {
                    _namedEnemy = enemy;
                    _textService ??= Core.Services.GetService<TextService>();
                    _name = _textService?.DisplayText(TextType.Monster, enemy.Name) ?? enemy.Name;
                }

                var anchor = sceneCamera.WorldToScreenPoint(new Vector2(bounds.Center.X, bounds.Top));
                var size = font.MeasureString(_name);
                var position = new Vector2(Mathf.Round(anchor.X - size.X * 0.5f), Mathf.Round(anchor.Y - NameGapY - size.Y));
                batcher.DrawString(font, _name, position + Vector2.One, ShadowColor);
                batcher.DrawString(font, _name, position, NameColor);
                break;
            }
            ListPool<Entity>.Free(monsters);
        }
    }
}
