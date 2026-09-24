using System.Collections.Generic;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Stable <c>ushort</c> recorder ids for live renderables, by reference. An id stays with its
    /// renderable for as long as the renderable is seen every tick; ids of renderables that vanished
    /// go to a free list and are reused (the stream tombstones the old entity first, so reuse is
    /// unambiguous). Id 0 is never handed out. No allocation after warm-up.
    /// </summary>
    public sealed class FrameEntityIdPool
    {
        private const int IdSpace = ushort.MaxValue + 1;

        private readonly Dictionary<object, ushort> _ids = new Dictionary<object, ushort>(1024, ReferenceEqualityComparer.Instance);
        private readonly object[] _owners = new object[IdSpace];
        private readonly int[] _seenStamp = new int[IdSpace];
        private readonly Stack<ushort> _free = new Stack<ushort>(256);
        private readonly List<ushort> _live = new List<ushort>(1024);
        private ushort _next = 1;
        private int _stamp;
        private bool _exhausted;

        /// <summary>Renderables holding an id right now.</summary>
        public int LiveCount => _live.Count;
        /// <summary>Ids parked for reuse.</summary>
        public int FreeCount => _free.Count;
        /// <summary>True once an acquire had to be refused because all 65,535 ids were live.</summary>
        public bool Exhausted => _exhausted;

        /// <summary>Starts a tick: ids acquired from now on count as seen this tick.</summary>
        public void BeginTick()
        {
            if (++_stamp == int.MaxValue)
            {
                System.Array.Clear(_seenStamp, 0, _seenStamp.Length);
                _stamp = 1;
            }
        }

        /// <summary>The id of a renderable (assigning one on first sight) and marks it seen; 0 when the pool is exhausted.</summary>
        public ushort Acquire(object owner)
        {
            if (_ids.TryGetValue(owner, out ushort id))
            {
                _seenStamp[id] = _stamp;
                return id;
            }
            if (_free.Count > 0)
            {
                id = _free.Pop();
            }
            else if (_next != 0)
            {
                id = _next++;
            }
            else
            {
                _exhausted = true;
                return 0;
            }
            _ids[owner] = id;
            _owners[id] = owner;
            _seenStamp[id] = _stamp;
            _live.Add(id);
            return id;
        }

        /// <summary>True when the renderable was acquired during the current tick.</summary>
        public bool WasSeenThisTick(object owner) => _ids.TryGetValue(owner, out ushort id) && _seenStamp[id] == _stamp;

        /// <summary>Ends a tick: releases every id not acquired since <see cref="BeginTick"/>. Returns how many.</summary>
        public int ReleaseUnseen()
        {
            int released = 0;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                ushort id = _live[i];
                if (_seenStamp[id] == _stamp)
                    continue;
                _ids.Remove(_owners[id]);
                _owners[id] = null;
                _free.Push(id);
                _live[i] = _live[_live.Count - 1];
                _live.RemoveAt(_live.Count - 1);
                released++;
            }
            return released;
        }

        /// <summary>Forgets every id.</summary>
        public void Clear()
        {
            for (int i = 0; i < _live.Count; i++)
                _owners[_live[i]] = null;
            _ids.Clear();
            _live.Clear();
            _free.Clear();
            _next = 1;
            _exhausted = false;
        }
    }
}
