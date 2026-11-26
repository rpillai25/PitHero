using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// Wraps Content/Shaders/PitWarp.fxb (issue #469): the full-screen swirl that drains the pit into
    /// the abyss and brings the regenerated pit back up. Driven by <see cref="PitWarpPostProcessor"/>.
    /// </summary>
    public class PitWarpEffect : Effect
    {
        private static byte[] _shaderBytes;

        private readonly EffectParameter _regionParam;
        private readonly EffectParameter _twistParam;
        private readonly EffectParameter _pullParam;
        private readonly EffectParameter _spinParam;
        private readonly EffectParameter _abyssColorParam;

        public PitWarpEffect() : base(Core.GraphicsDevice, _shaderBytes ??= LoadShaderBytes())
        {
            _regionParam = Parameters["Region"];
            _twistParam = Parameters["Twist"];
            _pullParam = Parameters["Pull"];
            _spinParam = Parameters["Spin"];
            _abyssColorParam = Parameters["AbyssColor"];
            _abyssColorParam.SetValue(GameConfig.PitWarpAbyssColor.ToVector3());
        }

        /// <summary>Sets the swirl parameters for this frame. Region is (centre u, centre v, half width, half height) in render-target UV.</summary>
        public void Set(Vector4 region, float twist, float pull, float spin)
        {
            _regionParam.SetValue(region);
            _twistParam.SetValue(twist);
            _pullParam.SetValue(pull);
            _spinParam.SetValue(spin);
        }

        private static byte[] LoadShaderBytes()
        {
            using var stream = TitleContainer.OpenStream("Content/Shaders/PitWarp.fxb");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
