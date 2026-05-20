using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    public class ColorGradingEffect : Effect
    {
        public Texture2D LookUpTableA
        {
            get => _lutA;
            set
            {
                if (_lutA != value)
                {
                    _lutA = value;
                    _lutAParam.SetValue(_lutA);
                }
            }
        }

        public Texture2D LookUpTableB
        {
            get => _lutB;
            set
            {
                if (_lutB != value)
                {
                    _lutB = value;
                    _lutBParam.SetValue(_lutB);
                }
            }
        }

        public float BlendFactor
        {
            get => _blendFactor;
            set
            {
                if (_blendFactor != value)
                {
                    _blendFactor = value;
                    _blendFactorParam.SetValue(_blendFactor);
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

        Texture2D _lutA;
        Texture2D _lutB;
        float _blendFactor = 0f;
        float _size        = 16f;
        float _sizeRoot    = 4f;

        EffectParameter _lutAParam;
        EffectParameter _lutBParam;
        EffectParameter _blendFactorParam;
        EffectParameter _sizeParam;
        EffectParameter _sizeRootParam;
        EffectParameter _invLUTSizeParam;

        static byte[] _shaderBytes;

        public ColorGradingEffect() : base(Core.GraphicsDevice, _shaderBytes ??= LoadShaderBytes())
        {
            _lutAParam        = Parameters["LUTTextureA"];
            _lutBParam        = Parameters["LUTTextureB"];
            _blendFactorParam = Parameters["BlendFactor"];
            _sizeParam        = Parameters["Size"];
            _sizeRootParam    = Parameters["SizeRoot"];
            _invLUTSizeParam  = Parameters["InvLUTSize"];

            _sizeParam.SetValue(_size);
            _sizeRootParam.SetValue(_sizeRoot);
            _blendFactorParam.SetValue(_blendFactor);
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
