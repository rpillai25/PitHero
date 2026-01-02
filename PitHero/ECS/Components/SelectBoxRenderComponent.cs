using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders a selection box around an entity (used for mercenary hover effect)
    /// </summary>
    public class SelectBoxRenderComponent : RenderableComponent
    {
        private const int BoxSize = 32; // Size of the selection box
        private const int LineThickness = 2; // Thickness of the box lines
        private Color _boxColor = Color.Yellow;

        public override float Width => BoxSize;
        public override float Height => BoxSize;

        public override void Render(Batcher batcher, Camera camera)
        {
            var position = Entity.Transform.Position;
            
            // Calculate box bounds (centered on entity)
            var left = position.X - BoxSize / 2;
            var top = position.Y - BoxSize / 2;
            var right = position.X + BoxSize / 2;
            var bottom = position.Y + BoxSize / 2;

            // Draw the four sides of the box
            // Top line
            batcher.DrawLine(new Vector2(left, top), new Vector2(right, top), _boxColor, LineThickness);
            // Right line
            batcher.DrawLine(new Vector2(right, top), new Vector2(right, bottom), _boxColor, LineThickness);
            // Bottom line
            batcher.DrawLine(new Vector2(right, bottom), new Vector2(left, bottom), _boxColor, LineThickness);
            // Left line
            batcher.DrawLine(new Vector2(left, bottom), new Vector2(left, top), _boxColor, LineThickness);
        }

        /// <summary>Sets the color of the selection box</summary>
        public void SetColor(Color color)
        {
            _boxColor = color;
        }
    }
}
