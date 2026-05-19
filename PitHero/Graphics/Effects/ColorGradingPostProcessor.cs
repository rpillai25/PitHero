using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    public class ColorGradingPostProcessor : PostProcessor<ColorGradingEffect>
    {
        Texture2D _lut;

        public ColorGradingPostProcessor(int executionOrder) : base(executionOrder)
        {
        }

        public override void OnAddedToScene(Scene scene)
        {
            base.OnAddedToScene(scene);
            Effect = new ColorGradingEffect();
            _lut = LoadLUTFromPath("Content/Shaders/lut_default.png");
            Effect.LookUpTable = _lut;
        }

        // Swap the LUT at runtime — call this to change the look (day, night, etc.)
        public void SwapLUT(string contentPath)
        {
            _lut?.Dispose();
            _lut = LoadLUTFromPath(contentPath);
            Effect.LookUpTable = _lut;
        }

        public override void Unload()
        {
            Effect?.Dispose();
            Effect = null;
            _lut?.Dispose();
            _lut = null;
            base.Unload();
        }

        static Texture2D LoadLUTFromPath(string contentPath)
        {
            using var stream = TitleContainer.OpenStream(contentPath);
            return Texture2D.FromStream(Core.GraphicsDevice, stream);
        }
    }
}
