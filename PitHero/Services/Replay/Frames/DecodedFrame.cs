using System;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// The presentation state of one tick: every entity's op bytes plus the HUD record. Owned and reused
    /// by the decoder (a <see cref="FrameStore"/> hands out the same instance on every call), so a caller
    /// draws from it immediately and never keeps it. Entities keep the order they first appeared in
    /// (the scene's renderable order at the base tick); removed entities leave a dead slot that is
    /// compacted away lazily. No allocation after warm-up.
    /// </summary>
    public sealed class DecodedFrame
    {
        private const int DeadLength = -1;
        private const int IdSpace = ushort.MaxValue + 1;

        private FrameEntity[] _slots = new FrameEntity[512];
        private int _slotCount, _deadCount;
        private readonly int[] _slotOfId = new int[IdSpace]; // slot + 1; 0 = never seen since the last Clear
        private byte[] _ops = new byte[64 * 1024];
        private byte[] _opsScratch;
        private int _opsLength, _liveOpsBytes;

        /// <summary>The decoded tick, or -1 when empty.</summary>
        public long Tick { get; internal set; } = -1;
        internal DecodedChunk Source;
        internal int SourceGeneration = -1;

        public HudRecord Hud;

        /// <summary>Slots to iterate (some may be dead; see <see cref="TryGetSlot"/>).</summary>
        public int SlotCount => _slotCount;
        /// <summary>Entities present at this tick.</summary>
        public int LiveCount => _slotCount - _deadCount;
        /// <summary>Backing array of every entity's op bytes (see <see cref="FrameEntity.Offset"/>).</summary>
        public byte[] OpsBuffer => _ops;

        /// <summary>The entity in a slot; false for a dead (removed) slot.</summary>
        public bool TryGetSlot(int slot, out FrameEntity entity)
        {
            entity = _slots[slot];
            return entity.Length != DeadLength;
        }

        /// <summary>The entity with a recorder id, if present at this tick.</summary>
        public bool TryGetEntity(ushort id, out FrameEntity entity)
        {
            int s = _slotOfId[id];
            if (s == 0)
            {
                entity = default;
                return false;
            }
            entity = _slots[s - 1];
            return entity.Length != DeadLength;
        }

        /// <summary>A reader positioned at the entity's first op.</summary>
        public FrameReader ReadOps(in FrameEntity entity) => new FrameReader(_ops, entity.Offset, entity.Length);

        /// <summary>True when both frames hold the same entity ids with identical op bytes and the same HUD.</summary>
        public bool ContentEquals(DecodedFrame other)
        {
            if (other == null || other.LiveCount != LiveCount || !Hud.Equals(other.Hud))
                return false;
            for (int i = 0; i < _slotCount; i++)
            {
                if (!TryGetSlot(i, out var e))
                    continue;
                if (!other.TryGetEntity(e.Id, out var o) || o.Length != e.Length)
                    return false;
                if (!new ReadOnlySpan<byte>(_ops, e.Offset, e.Length).SequenceEqual(new ReadOnlySpan<byte>(other._ops, o.Offset, o.Length)))
                    return false;
            }
            return true;
        }

        /// <summary>Makes this frame an independent copy of <paramref name="other"/> (a frozen frame the store may overwrite).</summary>
        public void CopyFrom(DecodedFrame other)
        {
            Clear();
            if (other == null)
                return;
            for (int i = 0; i < other._slotCount; i++)
            {
                if (!other.TryGetSlot(i, out var e))
                    continue;
                Upsert(e.Id, other._ops, e.Offset, e.Length);
            }
            Hud = other.Hud;
            Tick = other.Tick;
        }

        internal void Clear()
        {
            for (int i = 0; i < _slotCount; i++)
                _slotOfId[_slots[i].Id] = 0;
            _slotCount = 0;
            _deadCount = 0;
            _opsLength = 0;
            _liveOpsBytes = 0;
            Tick = -1;
            Source = null;
            SourceGeneration = -1;
        }

        internal void Upsert(ushort id, byte[] src, int offset, int length)
        {
            int at = Append(src, offset, length);
            int s = _slotOfId[id];
            if (s != 0)
            {
                ref var e = ref _slots[s - 1];
                if (e.Length != DeadLength)
                    _liveOpsBytes -= e.Length;
                else
                    _deadCount--;
                e.Offset = at;
                e.Length = length;
            }
            else
            {
                if (_slotCount == _slots.Length)
                    Array.Resize(ref _slots, _slots.Length * 2);
                _slots[_slotCount] = new FrameEntity(id, at, length);
                _slotOfId[id] = ++_slotCount;
            }
            _liveOpsBytes += length;
        }

        internal void Remove(ushort id)
        {
            int s = _slotOfId[id];
            if (s == 0)
                return;
            ref var e = ref _slots[s - 1];
            if (e.Length == DeadLength)
                return;
            _liveOpsBytes -= e.Length;
            e.Length = DeadLength;
            _deadCount++;
        }

        private int Append(byte[] src, int offset, int length)
        {
            int need = _opsLength + length;
            if (need > _ops.Length)
            {
                int size = _ops.Length * 2;
                while (size < need)
                    size *= 2;
                var grown = new byte[size];
                Buffer.BlockCopy(_ops, 0, grown, 0, _opsLength);
                _ops = grown;
            }
            Buffer.BlockCopy(src, offset, _ops, _opsLength, length);
            int at = _opsLength;
            _opsLength += length;
            return at;
        }

        /// <summary>Drops dead slots and unreferenced op bytes once they outweigh the live data.</summary>
        internal void CompactIfNeeded()
        {
            if (_deadCount > 64 && _deadCount * 2 > _slotCount)
                CompactSlots();
            if (_opsLength > 64 * 1024 && _liveOpsBytes * 2 < _opsLength)
                CompactOps();
        }

        private void CompactSlots()
        {
            int j = 0;
            for (int i = 0; i < _slotCount; i++)
            {
                var e = _slots[i];
                if (e.Length == DeadLength)
                {
                    _slotOfId[e.Id] = 0;
                    continue;
                }
                _slots[j] = e;
                _slotOfId[e.Id] = j + 1;
                j++;
            }
            _slotCount = j;
            _deadCount = 0;
        }

        private void CompactOps()
        {
            if (_opsScratch == null || _opsScratch.Length < _ops.Length)
                _opsScratch = new byte[_ops.Length];
            int len = 0;
            for (int i = 0; i < _slotCount; i++)
            {
                ref var e = ref _slots[i];
                if (e.Length == DeadLength)
                    continue;
                Buffer.BlockCopy(_ops, e.Offset, _opsScratch, len, e.Length);
                e.Offset = len;
                len += e.Length;
            }
            var t = _ops;
            _ops = _opsScratch;
            _opsScratch = t;
            _opsLength = len;
            _liveOpsBytes = len;
        }
    }
}
