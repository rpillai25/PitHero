using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// Builds a small tileable 2-field value-noise texture consumed by the CloudOverlay pixel shader
    /// (see <see cref="Effects.CloudOverlayEffect"/> / <see cref="Effects.CloudOverlayController"/>).
    /// R and G hold two independent noise fields that the shader crossfades over time (MorphFactor), so
    /// cloud shapes genuinely morph rather than just scroll. B mirrors R and A is opaque; both are unused
    /// by the shader but keep the texture visually sane if ever inspected.
    /// </summary>
    public static class CloudNoiseGenerator
    {
        /// <summary>
        /// Generates a <paramref name="size"/>x<paramref name="size"/> tileable noise texture. Uses a
        /// local <see cref="System.Random"/> seeded deterministically so clouds look identical every run,
        /// without disturbing the global Nez.Random stream — the same documented deviation as
        /// TreeBandComponent.PaintTrees (a one-time load must not perturb the combat RNG call-order
        /// contract). This runs once at map load; the per-field allocations below are fine per AOT rules.
        /// </summary>
        public static Texture2D CreateTileableNoise(int size, int latticeCells, int seed)
        {
            var rng = new System.Random(seed);

            // Two lattices per field (base + 2x baked octave) so each field already has some internal
            // detail before the shader layers its own multi-octave scroll on top.
            var latticeR1 = BuildLattice(latticeCells, rng);
            var latticeR2 = BuildLattice(latticeCells * 2, rng);
            var latticeG1 = BuildLattice(latticeCells, rng);
            var latticeG2 = BuildLattice(latticeCells * 2, rng);

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                var v = (float)y / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (float)x / size;

                    var r = 0.65f * SampleLattice(latticeR1, latticeCells, u, v)
                          + 0.35f * SampleLattice(latticeR2, latticeCells * 2, u, v);
                    var g = 0.65f * SampleLattice(latticeG1, latticeCells, u, v)
                          + 0.35f * SampleLattice(latticeG2, latticeCells * 2, u, v);

                    var rb = (byte)MathHelper.Clamp(r * 255f, 0f, 255f);
                    var gb = (byte)MathHelper.Clamp(g * 255f, 0f, 255f);
                    pixels[y * size + x] = new Color(rb, gb, rb, (byte)255);
                }
            }

            var tex = new Texture2D(Core.GraphicsDevice, size, size);
            tex.SetData(pixels);
            return tex;
        }

        /// <summary>Fills a cells x cells lattice of independent random values in [0,1).</summary>
        static float[,] BuildLattice(int cells, System.Random rng)
        {
            var lattice = new float[cells, cells];
            for (var y = 0; y < cells; y++)
            {
                for (var x = 0; x < cells; x++)
                    lattice[y, x] = (float)rng.NextDouble();
            }
            return lattice;
        }

        /// <summary>
        /// Bilinear-interpolates a tileable lattice at normalized (u, v) in [0,1). Lattice indices wrap
        /// (`(i+1) % cells`), which is what makes the field tile seamlessly; a quintic fade curve (Perlin's
        /// improved fade, smoother than Hermite) avoids visible grid-cell seams once repeated on screen.
        /// </summary>
        static float SampleLattice(float[,] lattice, int cells, float u, float v)
        {
            var fx = u * cells;
            var fy = v * cells;

            var x0 = (int)MathF.Floor(fx);
            var y0 = (int)MathF.Floor(fy);
            var tx = fx - x0;
            var ty = fy - y0;

            var x0w = ((x0 % cells) + cells) % cells;
            var y0w = ((y0 % cells) + cells) % cells;
            var x1w = (x0w + 1) % cells;
            var y1w = (y0w + 1) % cells;

            var sx = QuinticFade(tx);
            var sy = QuinticFade(ty);

            var n00 = lattice[y0w, x0w];
            var n10 = lattice[y0w, x1w];
            var n01 = lattice[y1w, x0w];
            var n11 = lattice[y1w, x1w];

            var nx0 = MathHelper.Lerp(n00, n10, sx);
            var nx1 = MathHelper.Lerp(n01, n11, sx);
            return MathHelper.Lerp(nx0, nx1, sy);
        }

        static float QuinticFade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
