using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Basic renderable component that wraps Nez's RenderableComponent for simple rendering
    /// </summary>
    public class BasicRenderableComponent : RenderableComponent
    {
        public new Color Color { get; set; } = Color.White;
        public int RenderWidth { get; set; } = 32;
        public int RenderHeight { get; set; } = 32;
        
        public new Rectangle Bounds => new Rectangle(
            (int)Transform.Position.X,
            (int)Transform.Position.Y,
            RenderWidth,
            RenderHeight
        );

        public override float Width => RenderWidth;
        public override float Height => RenderHeight;

        public override void Render(Batcher batcher, Camera camera)
        {
            // Simple rectangle rendering - can be expanded later
            var destRect = new Rectangle(
                (int)Transform.Position.X,
                (int)Transform.Position.Y,
                RenderWidth,
                RenderHeight
            );
            
            batcher.DrawRect(destRect, Color);
        }
    }
}