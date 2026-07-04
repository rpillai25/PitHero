using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.Services;

namespace PitHero.Rendering
{
    /// <summary>
    /// Owns the day/night LUT grading <see cref="ColorGradingEffect"/> and a shared
    /// <see cref="Material"/> that is attached to the tilemap terrain renderers. Unlike the
    /// old full-screen post-processor, this grades only the layers whose renderers use
    /// <see cref="Material"/>, leaving actors/monsters/objects/UI untinted.
    /// </summary>
    public class ColorGradingController : IDisposable
    {
        /// <summary>Shared material to assign to the tilemap terrain renderers via SetMaterial.</summary>
        public Material Material { get; private set; }

        ColorGradingEffect _effect;

        Texture2D _lutDay;
        Texture2D _lutDuskDawn;
        Texture2D _lutNight;

        public ColorGradingController()
        {
            _effect = new ColorGradingEffect();
            Material = new Material(_effect);

            _lutDay      = LoadLUTFromPath("Content/Shaders/lut_default.png");
            _lutDuskDawn = LoadLUTFromPath("Content/Shaders/lut_dusk_dawn.png");
            _lutNight    = LoadLUTFromPath("Content/Shaders/lut_night.png");

            _effect.LookUpTableA = _lutDay;
            _effect.LookUpTableB = _lutDay;
            _effect.BlendFactor  = 0f;
        }

        public void UpdateTimeOfDay()
        {
            var ts = Core.Services.GetService<InGameTimeService>();
            if (ts == null || _effect == null) return;

            float h = ts.Hour + ts.Minute / 60f;

            Texture2D lutA, lutB;
            float blend;

            if      (h >= 4f    && h < 5f)    { lutA = _lutNight;    lutB = _lutDuskDawn; blend = h - 4f; }
            else if (h >= 5f    && h < 5.5f)  { lutA = _lutDuskDawn; lutB = _lutDuskDawn; blend = 0f; }
            else if (h >= 5.5f  && h < 6.5f)  { lutA = _lutDuskDawn; lutB = _lutDay;      blend = h - 5.5f; }
            else if (h >= 6.5f  && h < 17.5f) { lutA = _lutDay;      lutB = _lutDay;      blend = 0f; }
            else if (h >= 17.5f && h < 18.5f) { lutA = _lutDay;      lutB = _lutDuskDawn; blend = h - 17.5f; }
            else if (h >= 18.5f && h < 19f)   { lutA = _lutDuskDawn; lutB = _lutDuskDawn; blend = 0f; }
            else if (h >= 19f   && h < 21f)   { lutA = _lutDuskDawn; lutB = _lutNight;    blend = (h - 19f) / 2f; }
            else                               { lutA = _lutNight;    lutB = _lutNight;    blend = 0f; }

            _effect.LookUpTableA = lutA;
            _effect.LookUpTableB = lutB;
            _effect.BlendFactor  = blend;
        }

        public void Dispose()
        {
            Material = null;
            _effect?.Dispose();      _effect = null;
            _lutDay?.Dispose();      _lutDay = null;
            _lutDuskDawn?.Dispose(); _lutDuskDawn = null;
            _lutNight?.Dispose();    _lutNight = null;
        }

        static Texture2D LoadLUTFromPath(string contentPath)
        {
            using var stream = TitleContainer.OpenStream(contentPath);
            return Texture2D.FromStream(Core.GraphicsDevice, stream);
        }
    }
}
