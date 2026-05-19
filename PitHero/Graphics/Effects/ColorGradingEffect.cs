using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    public class ColorGradingEffect : Effect
    {
        public Texture2D LookUpTable
        {
            get => _lut;
            set
            {
                if (_lut != value)
                {
                    _lut = value;
                    _lutParam.SetValue(_lut);
                }
            }
        }

        [Range(4, 64)]
        public float Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    _sizeParam.SetValue(_size);
                    UpdateInvLUTSize();
                }
            }
        }

        [Range(2, 8)]
        public float SizeRoot
        {
            get => _sizeRoot;
            set
            {
                if (_sizeRoot != value)
                {
                    _sizeRoot = value;
                    _sizeRootParam.SetValue(_sizeRoot);
                    UpdateInvLUTSize();
                }
            }
        }

        Texture2D _lut;
        float _size     = 16f;
        float _sizeRoot = 4f;

        EffectParameter _lutParam;
        EffectParameter _sizeParam;
        EffectParameter _sizeRootParam;
        EffectParameter _invLUTSizeParam;

        static byte[] _shaderBytes;

        public ColorGradingEffect() : base(Core.GraphicsDevice, _shaderBytes ??= LoadShaderBytes())
        {
            _lutParam        = Parameters["LUTTexture"];
            _sizeParam       = Parameters["Size"];
            _sizeRootParam   = Parameters["SizeRoot"];
            _invLUTSizeParam = Parameters["InvLUTSize"];

            _sizeParam.SetValue(_size);
            _sizeRootParam.SetValue(_sizeRoot);
            UpdateInvLUTSize();
        }

        void UpdateInvLUTSize() => _invLUTSizeParam.SetValue(1f / (_sizeRoot * _size));

        static byte[] LoadShaderBytes()
        {
            using var stream = TitleContainer.OpenStream("Content/Shaders/ColorGrading.fxb");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
