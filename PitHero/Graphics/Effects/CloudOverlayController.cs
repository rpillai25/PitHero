using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.Services;

namespace PitHero.Rendering
{
    /// <summary>
    /// Owns the <see cref="CloudOverlayEffect"/>, its shared <see cref="Material"/>, the generated
    /// tileable noise texture, and per-frame weather/drift/tint state for the volumetric scrolling cloud
    /// overlay (see <see cref="PitHero.ECS.Components.CloudOverlayComponent"/>). Mirrors
    /// <see cref="ColorGradingController"/>'s ownership shape. Updated once per frame from
    /// <c>MainGameScene.Update</c>; not registered as a service since nothing else consumes it.
    /// </summary>
    public class CloudOverlayController : IDisposable
    {
        /// <summary>Shared material assigned to the cloud overlay entity via SetMaterial.</summary>
        public Material Material { get; private set; }

        /// <summary>The generated tileable noise texture — also the quad texture drawn by the component.</summary>
        public Texture2D NoiseTexture { get; private set; }

        CloudOverlayEffect _effect;

        // Time-of-day tint keyframes (hour-of-day -> color). Alpha doubles as max opacity-by-time so
        // clouds are barely visible at deep night without a separate opacity curve.
        static readonly float[] TintHours =
        {
            0f, 4f, 6f, 8f, 17f, 19f, 21f, 24f
        };
        static readonly Color[] TintColors =
        {
            new Color(45, 52, 78, 140),   // 0h  - night blue-grey
            new Color(45, 52, 78, 140),   // 4h  - night blue-grey
            new Color(255, 168, 112, 200),// 6h  - dawn orange
            new Color(255, 255, 255, 215),// 8h  - white
            new Color(255, 255, 255, 215),// 17h - white
            new Color(255, 168, 112, 200),// 19h - dusk orange
            new Color(45, 52, 78, 140),   // 21h - night blue-grey
            new Color(45, 52, 78, 140),   // 24h - night blue-grey (wrap)
        };

        public CloudOverlayController()
        {
            NoiseTexture = CloudNoiseGenerator.CreateTileableNoise(
                GameConfig.CloudNoiseTextureSize, GameConfig.CloudNoiseLatticeCells, GameConfig.CloudNoiseSeed);

            _effect = new CloudOverlayEffect();
            _effect.NoiseTexture = NoiseTexture;

            Material = new Material(_effect);
        }

        /// <summary>Advances drift/weather/morph/tint from the in-game clock. No-ops headlessly (the
        /// service is null in tests/virtual-game runs, which never construct this controller anyway).</summary>
        public void Update()
        {
            var ts = Core.Services.GetService<InGameTimeService>();
            if (ts == null)
                return;
            Update(ts.AccumulatedSeconds, ts.AccumulatedSeconds);
        }

        /// <summary>
        /// Advances the clouds for a replayed tick (frame viewer, issue #428): every input is a closed
        /// form of time, so the viewer supplies one. <paramref name="driftSeconds"/> drives drift, weather
        /// and morph (only continuity matters, so the viewer passes its own cursor time);
        /// <paramref name="clockSeconds"/> is the recorded in-game clock that sets the day/night tint.
        /// </summary>
        public void Update(float driftSeconds, float clockSeconds)
        {
            if (_effect == null)
                return;
            UpdateScrollOffsets(driftSeconds);
            UpdateWeather(driftSeconds);
            UpdateMorph(driftSeconds);
            UpdateTint(clockSeconds);
            UpdatePitDeadZone();
        }

        /// <summary>
        /// Keeps the pit cloud dead zone's right edge in step with the live pit width. Polled per frame
        /// (the manager has no change event and widening/shrinking happens on level transitions); the
        /// effect setter de-dupes so the uniform is only re-pushed when the edge actually moves.
        /// </summary>
        void UpdatePitDeadZone()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null || pitWidthManager.CurrentPitRightEdge <= 0)
                return;

            _effect.PitDeadZoneMaxTileX = pitWidthManager.CurrentPitRightEdge + GameConfig.CloudDeadZonePitEdgeMarginTiles;
        }

        void UpdateScrollOffsets(float t)
        {
            var dir = new Vector2(GameConfig.CloudDriftDirX, GameConfig.CloudDriftDirY);

            // Horizontal parallax (issue #413): the shader samples at world position, which follows the
            // camera 1:1. Shifting every octave's offset by -(1 - rate) × cameraX makes the field track
            // the camera at `rate` of the ground's speed. Presentation-only (camera is a view value)
            // and the world-space dead zones are untouched because they read the world position directly.
            float parallaxPx = 0f;
            var camera = Core.Scene?.Camera;
            if (camera != null)
                parallaxPx = -(1f - GameConfig.CloudParallaxRateX) * camera.Bounds.Left;

            _effect.ScrollOffset1 = WrappedOffset(dir, t, GameConfig.CloudDriftSpeedPx, GameConfig.CloudNoiseWorldScale, parallaxPx);
            _effect.ScrollOffset2 = WrappedOffset(dir, t,
                GameConfig.CloudDriftSpeedPx * GameConfig.CloudOctave2SpeedMult,
                GameConfig.CloudNoiseWorldScale * GameConfig.CloudOctave2Mult, parallaxPx);
            _effect.ScrollOffset3 = WrappedOffset(dir, t,
                GameConfig.CloudDriftSpeedPx * GameConfig.CloudOctave3SpeedMult,
                GameConfig.CloudNoiseWorldScale * GameConfig.CloudOctave3Mult, parallaxPx);
            _effect.ScrollOffsetMacro = WrappedOffset(dir, t,
                GameConfig.CloudDriftSpeedPx * GameConfig.CloudMacroSpeedMult,
                GameConfig.CloudNoiseWorldScale * GameConfig.CloudMacroMult, parallaxPx);
            _effect.ScrollOffsetGiant = WrappedOffset(dir, t,
                GameConfig.CloudDriftSpeedPx * GameConfig.CloudGiantSpeedMult,
                GameConfig.CloudNoiseWorldScale * GameConfig.CloudGiantMult, parallaxPx);
        }

        /// <summary>
        /// off = (dir * speedPx * t + (parallaxPx, 0)) * octaveWorldScale, each component wrapped into
        /// [0,1) so float precision never degrades in long sessions (the shader only ever sees a
        /// pre-wrapped offset, never a raw unbounded time uniform). The parallax term is scaled by the
        /// same octave world scale as the drift so every octave shifts by the same world distance.
        /// </summary>
        static Vector2 WrappedOffset(Vector2 dir, float t, float speedPx, float octaveWorldScale, float parallaxPx)
        {
            var off = dir * (speedPx * t);
            off.X += parallaxPx;
            off *= octaveWorldScale;
            off.X -= MathF.Floor(off.X);
            off.Y -= MathF.Floor(off.Y);
            return off;
        }

        void UpdateWeather(float t)
        {
            var coverage = 0.5f
                + 0.35f * MathF.Sin(2f * MathF.PI * t / GameConfig.CloudCoveragePeriod1Seconds)
                + 0.15f * MathF.Sin(2f * MathF.PI * t / GameConfig.CloudCoveragePeriod2Seconds + 1.7f);
            coverage = MathHelper.Clamp(coverage, 0f, 1f);

            // Normal weather wobbles inside a narrow sparse band so distribution stays uniform across
            // the day/night cycle; CloudThresholdOvercast is reserved for a future rainy-day state.
            _effect.CoverageThreshold = MathHelper.Lerp(GameConfig.CloudThresholdClear, GameConfig.CloudThresholdPartly, coverage);
            _effect.CoverageSoftness = GameConfig.CloudCoverageSoftness;
        }

        void UpdateMorph(float t)
        {
            var m = 0.5f + 0.5f * MathF.Sin(2f * MathF.PI * t / GameConfig.CloudMorphPeriodSeconds);
            _effect.MorphFactor = m;
            // Renormalize contrast: averaging two independent fields squashes values toward the mean
            // (worst at m=0.5), which would pulse cloud cover with the morph cycle.
            _effect.MorphGain = 1f / MathF.Sqrt((1f - m) * (1f - m) + m * m);
        }

        void UpdateTint(float t)
        {
            var h = (t / 60f) % 24f;
            if (h < 0f)
                h += 24f;

            var color = TintColors[TintColors.Length - 1];
            for (var i = 0; i < TintHours.Length - 1; i++)
            {
                if (h >= TintHours[i] && h <= TintHours[i + 1])
                {
                    var span = TintHours[i + 1] - TintHours[i];
                    var frac = span > 0f ? (h - TintHours[i]) / span : 0f;
                    color = Color.Lerp(TintColors[i], TintColors[i + 1], frac);
                    break;
                }
            }

            _effect.CloudColor = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        public void Dispose()
        {
            Material = null;
            _effect?.Dispose();      _effect = null;
            NoiseTexture?.Dispose(); NoiseTexture = null;
        }
    }
}
