using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;

namespace PitHero.Rendering
{
    /// <summary>
    /// One character's baked poses in the <see cref="CharacterFrameAtlas"/> (issue #459). A pose is the full
    /// look of the layer stack: every layer's sprite, offset, flip and tint. Looking a pose up returns the
    /// atlas cell sprite that shows it, baking it first if this character has never shown it.
    ///
    /// Keying on the whole look rather than on animation names means nothing has to announce a change:
    /// a new hairstyle, the Armor layer appearing, a recolor or layers drifting out of step all show up as
    /// new poses. Stale poses stay until the cache holds <see cref="GameConfig.CharacterFrameMaxPosesPerCharacter"/>,
    /// then it is flushed and refills on demand. No allocation after construction.
    /// </summary>
    public sealed class CharacterPoseCache
    {
        /// <summary>Most layers a pose can have; deeper stacks are not baked.</summary>
        public const int MaxLayers = 16; // a female hero with Hair1 has 13 tone layers (issue #462)

        private const int MaxPoses = GameConfig.CharacterFrameMaxPosesPerCharacter;

        private readonly Vector2 _origin;
        private readonly Sprite[] _sprites = new Sprite[MaxPoses * MaxLayers];
        private readonly Vector2[] _offsets = new Vector2[MaxPoses * MaxLayers];
        private readonly bool[] _flips = new bool[MaxPoses * MaxLayers];
        private readonly Color[] _colors = new Color[MaxPoses * MaxLayers];
        private readonly int[] _layerCounts = new int[MaxPoses];
        private readonly int[] _cells = new int[MaxPoses];
        private readonly Dictionary<int, int> _byHash = new Dictionary<int, int>(MaxPoses);
        private int _count;
        private int _last = -1;

        /// <param name="origin">Pixel inside a cell that maps to the entity position.</param>
        public CharacterPoseCache(Vector2 origin)
        {
            _origin = origin;
        }

        /// <summary>Poses currently baked for this character.</summary>
        public int Count => _count;

        /// <summary>
        /// Returns the atlas sprite for the pose held in <paramref name="sprites"/> and its sibling arrays from
        /// <paramref name="start"/> for <paramref name="count"/> layers, baking it when needed (which ends and
        /// re-begins <paramref name="batcher"/>). Returns null when the pose cannot be baked (no graphics device,
        /// too many layers, atlas full); the caller then draws it another way.
        /// </summary>
        public Sprite Resolve(Batcher batcher, Camera camera, Sprite[] sprites, Vector2[] offsets, bool[] flips,
            Color[] colors, int start, int count)
        {
            if (count <= 0 || count > MaxLayers)
                return null;

            // Most frames show the same pose as the last one
            if (_last >= 0 && Matches(_last, sprites, offsets, flips, colors, start, count))
                return CharacterFrameAtlas.GetSprite(_cells[_last]);

            int hash = Hash(sprites, offsets, flips, colors, start, count);
            if (_byHash.TryGetValue(hash, out int entry))
            {
                _last = entry;
                if (Matches(entry, sprites, offsets, flips, colors, start, count))
                    return CharacterFrameAtlas.GetSprite(_cells[entry]);

                // A different pose with the same hash: it takes over that entry and its cell
                Store(entry, sprites, offsets, flips, colors, start, count);
                return Bake(entry, batcher, camera);
            }

            if (_count >= MaxPoses)
                ReleaseAll();

            int cell = CharacterFrameAtlas.Allocate(_origin);
            if (cell < 0)
                return null;

            entry = _count++;
            _cells[entry] = cell;
            Store(entry, sprites, offsets, flips, colors, start, count);
            _byHash[hash] = entry;
            _last = entry;
            return Bake(entry, batcher, camera);
        }

        /// <summary>Returns every cell to the atlas and forgets all poses.</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < _count; i++)
                CharacterFrameAtlas.Release(_cells[i]);
            for (int i = 0; i < _count * MaxLayers; i++)
                _sprites[i] = null;
            _byHash.Clear();
            _count = 0;
            _last = -1;
        }

        private Sprite Bake(int entry, Batcher batcher, Camera camera)
        {
            CharacterFrameAtlas.Bake(batcher, camera, _cells[entry], _sprites, _offsets, _flips, _colors,
                entry * MaxLayers, _layerCounts[entry]);
            return CharacterFrameAtlas.GetSprite(_cells[entry]);
        }

        private void Store(int entry, Sprite[] sprites, Vector2[] offsets, bool[] flips, Color[] colors,
            int start, int count)
        {
            int baseIndex = entry * MaxLayers;
            for (int i = 0; i < count; i++)
            {
                _sprites[baseIndex + i] = sprites[start + i];
                _offsets[baseIndex + i] = offsets[start + i];
                _flips[baseIndex + i] = flips[start + i];
                _colors[baseIndex + i] = colors[start + i];
            }
            for (int i = count; i < MaxLayers; i++)
                _sprites[baseIndex + i] = null;
            _layerCounts[entry] = count;
        }

        private bool Matches(int entry, Sprite[] sprites, Vector2[] offsets, bool[] flips, Color[] colors,
            int start, int count)
        {
            if (_layerCounts[entry] != count)
                return false;
            int baseIndex = entry * MaxLayers;
            for (int i = 0; i < count; i++)
            {
                int k = baseIndex + i;
                int s = start + i;
                if (!ReferenceEquals(_sprites[k], sprites[s]) || _offsets[k] != offsets[s]
                    || _flips[k] != flips[s] || _colors[k].PackedValue != colors[s].PackedValue)
                    return false;
            }
            return true;
        }

        private static int Hash(Sprite[] sprites, Vector2[] offsets, bool[] flips, Color[] colors, int start, int count)
        {
            unchecked
            {
                int h = count;
                for (int i = start; i < start + count; i++)
                {
                    h = h * 31 + (sprites[i] != null ? RuntimeHelpers.GetHashCode(sprites[i]) : 0);
                    h = h * 31 + offsets[i].X.GetHashCode();
                    h = h * 31 + offsets[i].Y.GetHashCode();
                    h = h * 31 + (flips[i] ? 1 : 0);
                    h = h * 31 + (int)colors[i].PackedValue;
                }
                return h;
            }
        }
    }
}
