using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>Identity of a sprite for the frame stream: its texture's asset name and source rectangle.</summary>
    public readonly struct SpriteKey : IEquatable<SpriteKey>
    {
        public readonly string TextureName;
        public readonly int X, Y, Width, Height;

        public SpriteKey(string textureName, int x, int y, int width, int height)
        {
            TextureName = textureName ?? string.Empty;
            X = x; Y = y; Width = width; Height = height;
        }

        public bool Equals(SpriteKey o)
            => X == o.X && Y == o.Y && Width == o.Width && Height == o.Height && string.Equals(TextureName, o.TextureName, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SpriteKey k && Equals(k);
        public override int GetHashCode()
        {
            int h = TextureName.GetHashCode();
            h = h * 31 + X;
            h = h * 31 + Y;
            h = h * 31 + Width;
            h = h * 31 + Height;
            return h;
        }
    }

    /// <summary>
    /// Interns sprite keys, strings and nine-patch names to <c>ushort</c> ids for the frame stream. Ids are
    /// assigned in first-seen order starting at 1 (0 = none). Each chunk carries a <b>table delta</b>: the
    /// entries first seen since the previous chunk was finished; a reader rebuilds the whole table by
    /// applying every chunk's delta in order (<see cref="ReadTableDelta"/>). The Nez Sprite → id map that
    /// resolves live sprites to keys lives in the capture layer (issue #427); this class is Nez-free.
    /// </summary>
    public sealed class SpriteKeyRegistry
    {
        /// <summary>Highest id a table can hold; interning beyond it returns <see cref="None"/>.</summary>
        public const int MaxEntries = ushort.MaxValue;
        /// <summary>The "no sprite / no string" id.</summary>
        public const ushort None = 0;

        private struct FlushMark
        {
            public int Sprites, Strings, Patches;
        }

        private readonly Dictionary<SpriteKey, ushort> _spriteIds = new Dictionary<SpriteKey, ushort>(1024);
        private readonly List<SpriteKey> _sprites = new List<SpriteKey>(1024);
        private readonly Dictionary<string, ushort> _stringIds = new Dictionary<string, ushort>(1024, StringComparer.Ordinal);
        private readonly List<string> _strings = new List<string>(1024);
        private readonly Dictionary<string, ushort> _patchIds = new Dictionary<string, ushort>(16, StringComparer.Ordinal);
        private readonly List<string> _patches = new List<string>(16);
        private readonly List<FlushMark> _marks = new List<FlushMark>(4096);
        private int _flushedSprites, _flushedStrings, _flushedPatches;

        /// <summary>Sprite keys interned so far (ids 1..SpriteCount).</summary>
        public int SpriteCount => _sprites.Count;
        public int StringCount => _strings.Count;
        public int NinePatchCount => _patches.Count;
        /// <summary>Entries interned since the last table delta was written.</summary>
        public int PendingCount => (_sprites.Count - _flushedSprites) + (_strings.Count - _flushedStrings) + (_patches.Count - _flushedPatches);
        /// <summary>Table deltas written or read so far (one per chunk).</summary>
        public int FlushCount => _marks.Count;
        /// <summary>True once a table has refused an entry because it was full.</summary>
        public bool Overflowed { get; private set; }

        /// <summary>Id of a sprite key, interning it on first sight; <see cref="None"/> when the table is full.</summary>
        public ushort InternSprite(in SpriteKey key)
        {
            if (_spriteIds.TryGetValue(key, out ushort id))
                return id;
            if (_sprites.Count >= MaxEntries)
            {
                Overflowed = true;
                return None;
            }
            id = (ushort)(_sprites.Count + 1);
            _sprites.Add(key);
            _spriteIds[key] = id;
            return id;
        }

        /// <summary>Id of a string, interning it on first sight; <see cref="None"/> for null/empty or a full table.</summary>
        public ushort InternString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return None;
            if (_stringIds.TryGetValue(s, out ushort id))
                return id;
            if (_strings.Count >= MaxEntries)
            {
                Overflowed = true;
                return None;
            }
            id = (ushort)(_strings.Count + 1);
            _strings.Add(s);
            _stringIds[s] = id;
            return id;
        }

        /// <summary>Id of a nine-patch name, interning it on first sight.</summary>
        public ushort InternNinePatch(string name)
        {
            if (string.IsNullOrEmpty(name))
                return None;
            if (_patchIds.TryGetValue(name, out ushort id))
                return id;
            if (_patches.Count >= MaxEntries)
            {
                Overflowed = true;
                return None;
            }
            id = (ushort)(_patches.Count + 1);
            _patches.Add(name);
            _patchIds[name] = id;
            return id;
        }

        public bool TryGetSpriteKey(ushort id, out SpriteKey key)
        {
            if (id == None || id > _sprites.Count)
            {
                key = default;
                return false;
            }
            key = _sprites[id - 1];
            return true;
        }

        /// <summary>The string for an id, or null for <see cref="None"/> / unknown.</summary>
        public string GetString(ushort id) => id == None || id > _strings.Count ? null : _strings[id - 1];

        /// <summary>The nine-patch name for an id, or null for <see cref="None"/> / unknown.</summary>
        public string GetNinePatch(ushort id) => id == None || id > _patches.Count ? null : _patches[id - 1];

        /// <summary>
        /// Writes the entries interned since the last flush and marks them flushed. Layout: spriteCount u16
        /// {textureName str, x i32, y i32, w i32, h i32}, stringCount u16 {str}, patchCount u16 {str}; ids
        /// are implicit (sequential).
        /// </summary>
        public void WriteTableDelta(ref FrameWriter w)
        {
            w.WriteU16((ushort)(_sprites.Count - _flushedSprites));
            for (int i = _flushedSprites; i < _sprites.Count; i++)
            {
                var k = _sprites[i];
                w.WriteString(k.TextureName);
                w.WriteI32(k.X);
                w.WriteI32(k.Y);
                w.WriteI32(k.Width);
                w.WriteI32(k.Height);
            }
            w.WriteU16((ushort)(_strings.Count - _flushedStrings));
            for (int i = _flushedStrings; i < _strings.Count; i++)
                w.WriteString(_strings[i]);
            w.WriteU16((ushort)(_patches.Count - _flushedPatches));
            for (int i = _flushedPatches; i < _patches.Count; i++)
                w.WriteString(_patches[i]);
            MarkFlushed();
        }

        /// <summary>Appends a chunk's table delta (from a sidecar); deltas must be applied in chunk order.</summary>
        public void ReadTableDelta(ref FrameReader r)
        {
            int spriteCount = r.ReadU16();
            for (int i = 0; i < spriteCount; i++)
            {
                string tex = r.ReadString();
                int x = r.ReadI32(), y = r.ReadI32(), w = r.ReadI32(), h = r.ReadI32();
                var key = new SpriteKey(tex, x, y, w, h);
                if (_spriteIds.ContainsKey(key))
                    throw new InvalidDataException("Duplicate sprite key in frame table delta");
                if (_sprites.Count >= MaxEntries)
                    throw new InvalidDataException("Frame sprite table overflow");
                _sprites.Add(key);
                _spriteIds[key] = (ushort)_sprites.Count;
            }
            int stringCount = r.ReadU16();
            for (int i = 0; i < stringCount; i++)
            {
                string s = r.ReadString();
                if (_stringIds.ContainsKey(s))
                    throw new InvalidDataException("Duplicate string in frame table delta");
                if (_strings.Count >= MaxEntries)
                    throw new InvalidDataException("Frame string table overflow");
                _strings.Add(s);
                _stringIds[s] = (ushort)_strings.Count;
            }
            int patchCount = r.ReadU16();
            for (int i = 0; i < patchCount; i++)
            {
                string s = r.ReadString();
                if (_patchIds.ContainsKey(s))
                    throw new InvalidDataException("Duplicate nine-patch in frame table delta");
                if (_patches.Count >= MaxEntries)
                    throw new InvalidDataException("Frame nine-patch table overflow");
                _patches.Add(s);
                _patchIds[s] = (ushort)_patches.Count;
            }
            MarkFlushed();
        }

        private void MarkFlushed()
        {
            _flushedSprites = _sprites.Count;
            _flushedStrings = _strings.Count;
            _flushedPatches = _patches.Count;
            _marks.Add(new FlushMark { Sprites = _flushedSprites, Strings = _flushedStrings, Patches = _flushedPatches });
        }

        /// <summary>
        /// After the frame store drops chunks (Time Travel), keeps only the first <paramref name="keepFlushes"/>
        /// table deltas as flushed: entries first written in a dropped chunk become pending again, so the
        /// next chunk re-declares them and a reader of the truncated stream still resolves every id.
        /// </summary>
        public void RewindFlushes(int keepFlushes)
        {
            if (keepFlushes < 0) keepFlushes = 0;
            if (keepFlushes >= _marks.Count)
                return;
            if (keepFlushes == 0)
            {
                _flushedSprites = _flushedStrings = _flushedPatches = 0;
            }
            else
            {
                var m = _marks[keepFlushes - 1];
                _flushedSprites = m.Sprites;
                _flushedStrings = m.Strings;
                _flushedPatches = m.Patches;
            }
            _marks.RemoveRange(keepFlushes, _marks.Count - keepFlushes);
        }

        /// <summary>Forgets every entry and flush mark.</summary>
        public void Clear()
        {
            _spriteIds.Clear();
            _sprites.Clear();
            _stringIds.Clear();
            _strings.Clear();
            _patchIds.Clear();
            _patches.Clear();
            _marks.Clear();
            _flushedSprites = _flushedStrings = _flushedPatches = 0;
            Overflowed = false;
        }
    }
}
