using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.AI;
using PitHero.ECS.Scenes;
using RolePlayingFramework.Enemies;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that renders a HP bar above a monster during battle.
    /// Shows green for remaining HP and red for lost HP, with monster name above.
    /// </summary>
    public class MonsterHPBarComponent : RenderableComponent
    {
        private EnemyComponent _enemyComponent;
        private IEnemy _enemy;

        // Bar dimensions (screen-space pixels)
        private const float BAR_WIDTH = 60f;
        private const float BAR_HEIGHT = 8f;
        private const float NAME_OFFSET_Y = -35f; // Name above bar
        private const float BAR_OFFSET_Y = -25f;   // Bar above monster

        // Colors
        private static readonly Color GREEN_HP = Color.Green;
        private static readonly Color RED_LOST_HP = Color.Red;
        private static readonly Color NAME_COLOR = Color.White;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            _enemyComponent = Entity.GetComponent<EnemyComponent>();
            if (_enemyComponent != null)
            {
                _enemy = _enemyComponent.Enemy;
            }
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            if (!HeroStateMachine.IsBattleInProgress || _enemy == null)
                return;

            var worldPos = Entity.Position;
            var camBounds = camera.Bounds;

            // Only render if monster is visible on screen (with margin)
            const int margin = 64;
            if (worldPos.X < camBounds.X - margin || worldPos.X > camBounds.Right + margin ||
                worldPos.Y < camBounds.Y - margin || worldPos.Y > camBounds.Bottom + margin)
                return;

            // Get HUD font for name
            var scene = (MainGameScene)Entity.Scene;
            var hudFont = scene?.GetHudFontForCurrentMode();
            if (hudFont == null)
                return;

            // Calculate positions (world space, but we'll scale for constant screen size)
            float inverseZoom = 1f / camera.RawZoom;
            float barWorldX = worldPos.X - BAR_WIDTH * inverseZoom * 0.5f; // Center bar
            float barWorldY = worldPos.Y + BAR_OFFSET_Y * inverseZoom;
            float nameWorldX = worldPos.X;
            float nameWorldY = worldPos.Y + NAME_OFFSET_Y * inverseZoom;

            // Calculate HP ratio
            float hpRatio = (float)_enemy.CurrentHP / _enemy.MaxHP;
            hpRatio = Mathf.Clamp(hpRatio, 0f, 1f);

            // Bar dimensions in world space
            float barWidthWorld = BAR_WIDTH * inverseZoom;
            float barHeightWorld = BAR_HEIGHT * inverseZoom;
            float greenWidthWorld = barWidthWorld * hpRatio;

            // Draw red background (lost HP)
            batcher.DrawRect(barWorldX, barWorldY, barWidthWorld, barHeightWorld, RED_LOST_HP);

            // Draw green foreground (remaining HP)
            if (greenWidthWorld > 0)
            {
                batcher.DrawRect(barWorldX, barWorldY, greenWidthWorld, barHeightWorld, GREEN_HP);
            }

            // Draw monster name centered above bar
            var nameSize = hudFont.MeasureString(_enemy.Name);
            float nameScale = inverseZoom;
            Vector2 namePos = new Vector2(
                nameWorldX - nameSize.X * nameScale * 0.5f,
                nameWorldY - nameSize.Y * nameScale * 0.5f
            );

            hudFont.DrawInto(batcher, _enemy.Name, namePos, NAME_COLOR,
                0, Vector2.Zero, new Vector2(nameScale, nameScale), SpriteEffects.None, 0);
        }

        public override bool IsVisibleFromCamera(Camera camera)
        {
            if (!HeroStateMachine.IsBattleInProgress || _enemy == null)
                return false;

            var camBounds = camera.Bounds;
            var worldPos = Entity.Position;
            const int margin = 64;
            return !(worldPos.X < camBounds.X - margin || worldPos.X > camBounds.Right + margin ||
                     worldPos.Y < camBounds.Y - margin || worldPos.Y > camBounds.Bottom + margin);
        }
    }
}