using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders a rectangular outline sized to a placed building's footprint, drawn from the entity
    /// position as the top-left corner. Used as the hover affordance that signals a building is
    /// clickable. Mirrors <see cref="SelectBoxRenderComponent"/> but spans an arbitrary rectangle.
    /// </summary>
    public class BuildingOutlineRenderComponent : RenderableComponent
    {
        private const int LineThickness = 2;
        private float _boxWidth = 32f;
        private float _boxHeight = 32f;
        private Color _boxColor = Color.White;

        public override float Width => _boxWidth;
        public override float Height => _boxHeight;

        /// <summary>Sets the pixel dimensions of the outline box (entity position is its top-left corner).</summary>
        public void SetSize(float width, float height)
        {
            _boxWidth = width;
            _boxHeight = height;
        }

        /// <summary>Sets the color of the outline box.</summary>
        public void SetColor(Color color)
        {
            _boxColor = color;
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            var position = Entity.Transform.Position;
            var left = position.X;
            var top = position.Y;
            var right = position.X + _boxWidth;
            var bottom = position.Y + _boxHeight;

            // Draw the four sides of the box
            batcher.DrawLine(new Vector2(left, top), new Vector2(right, top), _boxColor, LineThickness);
            batcher.DrawLine(new Vector2(right, top), new Vector2(right, bottom), _boxColor, LineThickness);
            batcher.DrawLine(new Vector2(right, bottom), new Vector2(left, bottom), _boxColor, LineThickness);
            batcher.DrawLine(new Vector2(left, bottom), new Vector2(left, top), _boxColor, LineThickness);
        }
    }
}
