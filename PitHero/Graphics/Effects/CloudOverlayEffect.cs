using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// Wraps CloudOverlay.fx — a pixel-shader-only effect that reconstructs per-pixel world position
    /// from <see cref="CameraTopLeft"/>/<see cref="CameraSize"/> and layers three scrolling samples of a
    /// tileable noise texture into a soft-thresholded cloud density field. See
    /// <see cref="CloudOverlayController"/> for ownership/update and <see cref="CloudNoiseGenerator"/>
    /// for the noise texture itself.
    /// </summary>
    public class CloudOverlayEffect : Effect
    {
        public Texture2D NoiseTexture
        {
            get => _noiseTexture;
            set
            {
                if (_noiseTexture != value)
                {
                    _noiseTexture = value;
                    _noiseTextureParam.SetValue(_noiseTexture);
                }
            }
        }

        public Vector2 CameraTopLeft
        {
            get => _cameraTopLeft;
            set
            {
                if (_cameraTopLeft != value)
                {
                    _cameraTopLeft = value;
                    _cameraTopLeftParam.SetValue(_cameraTopLeft);
                }
            }
        }

        public Vector2 CameraSize
        {
            get => _cameraSize;
            set
            {
                if (_cameraSize != value)
                {
                    _cameraSize = value;
                    _cameraSizeParam.SetValue(_cameraSize);
                }
            }
        }

        public Vector2 ScrollOffset1
        {
            get => _scrollOffset1;
            set
            {
                if (_scrollOffset1 != value)
                {
                    _scrollOffset1 = value;
                    _scrollOffset1Param.SetValue(_scrollOffset1);
                }
            }
        }

        public Vector2 ScrollOffset2
        {
            get => _scrollOffset2;
            set
            {
                if (_scrollOffset2 != value)
                {
                    _scrollOffset2 = value;
                    _scrollOffset2Param.SetValue(_scrollOffset2);
                }
            }
        }

        public Vector2 ScrollOffset3
        {
            get => _scrollOffset3;
            set
            {
                if (_scrollOffset3 != value)
                {
                    _scrollOffset3 = value;
                    _scrollOffset3Param.SetValue(_scrollOffset3);
                }
            }
        }

        public float MorphFactor
        {
            get => _morphFactor;
            set
            {
                if (_morphFactor != value)
                {
                    _morphFactor = value;
                    _morphFactorParam.SetValue(_morphFactor);
                }
            }
        }

        public float CoverageThreshold
        {
            get => _coverageThreshold;
            set
            {
                if (_coverageThreshold != value)
                {
                    _coverageThreshold = value;
                    _coverageThresholdParam.SetValue(_coverageThreshold);
                }
            }
        }

        public float CoverageSoftness
        {
            get => _coverageSoftness;
            set
            {
                if (_coverageSoftness != value)
                {
                    _coverageSoftness = value;
                    _coverageSoftnessParam.SetValue(_coverageSoftness);
                }
            }
        }

        public Vector4 CloudColor
        {
            get => _cloudColor;
            set
            {
                if (_cloudColor != value)
                {
                    _cloudColor = value;
                    _cloudColorParam.SetValue(_cloudColor);
                }
            }
        }

        Texture2D _noiseTexture;
        Vector2 _cameraTopLeft;
        Vector2 _cameraSize;
        Vector2 _scrollOffset1;
        Vector2 _scrollOffset2;
        Vector2 _scrollOffset3;
        float _morphFactor;
        float _coverageThreshold;
        float _coverageSoftness;
        Vector4 _cloudColor = Vector4.One;

        EffectParameter _noiseTextureParam;
        EffectParameter _cameraTopLeftParam;
        EffectParameter _cameraSizeParam;
        EffectParameter _noiseWorldScaleParam;
        EffectParameter _octave2MultParam;
        EffectParameter _octave3MultParam;
        EffectParameter _scrollOffset1Param;
        EffectParameter _scrollOffset2Param;
        EffectParameter _scrollOffset3Param;
        EffectParameter _morphFactorParam;
        EffectParameter _coverageThresholdParam;
        EffectParameter _coverageSoftnessParam;
        EffectParameter _cloudColorParam;

        static byte[] _shaderBytes;

        public CloudOverlayEffect() : base(Core.GraphicsDevice, _shaderBytes ??= LoadShaderBytes())
        {
            _noiseTextureParam      = Parameters["NoiseTexture"];
            _cameraTopLeftParam     = Parameters["CameraTopLeft"];
            _cameraSizeParam        = Parameters["CameraSize"];
            _noiseWorldScaleParam   = Parameters["NoiseWorldScale"];
            _octave2MultParam       = Parameters["Octave2Mult"];
            _octave3MultParam       = Parameters["Octave3Mult"];
            _scrollOffset1Param     = Parameters["ScrollOffset1"];
            _scrollOffset2Param     = Parameters["ScrollOffset2"];
            _scrollOffset3Param     = Parameters["ScrollOffset3"];
            _morphFactorParam       = Parameters["MorphFactor"];
            _coverageThresholdParam = Parameters["CoverageThreshold"];
            _coverageSoftnessParam  = Parameters["CoverageSoftness"];
            _cloudColorParam        = Parameters["CloudColor"];

            // One-shot: set from GameConfig so the C# scroll-offset math and the shader's own scale
            // factors can never drift apart.
            _noiseWorldScaleParam.SetValue(GameConfig.CloudNoiseWorldScale);
            _octave2MultParam.SetValue(GameConfig.CloudOctave2Mult);
            _octave3MultParam.SetValue(GameConfig.CloudOctave3Mult);

            _cloudColorParam.SetValue(_cloudColor);
        }

        static byte[] LoadShaderBytes()
        {
            using var stream = TitleContainer.OpenStream("Content/Shaders/CloudOverlay.fxb");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
