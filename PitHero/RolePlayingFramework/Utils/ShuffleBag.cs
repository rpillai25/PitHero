using System.Collections.Generic;

namespace RolePlayingFramework.Utils
{
    /// <summary>
    /// Classic shuffle bag ("marble bag"): a weighted finite pool drawn without
    /// replacement, refilled automatically when exhausted. Over any full cycle the
    /// draw counts exactly match the added composition, so streaks and droughts are
    /// bounded while individual draws still feel random.
    /// The core primitive <see cref="NextFromRoll"/> consumes no RNG itself — the
    /// caller supplies a [0,1) roll — so bags can sit behind existing RNG call
    /// sites without changing the number or order of global RNG calls (the battle
    /// Nez.Random stream is a determinism contract).
    /// </summary>
    public sealed class ShuffleBag<T>
    {
        private readonly List<T> _items;
        // Insertion-order snapshot so Reset can restore the exact starting arrangement (draws permute _items)
        private readonly List<T> _initial;

        /// <summary>Index of the last undrawn slot; bag is exhausted when below 0.</summary>
        private int _cursor;

        public ShuffleBag(int capacity)
        {
            _items = new List<T>(capacity);
            _initial = new List<T>(capacity);
            _cursor = -1;
        }

        /// <summary>
        /// Restores the bag to its state right after construction/Add: original marble order, full
        /// cursor. Lets a seeded consumer reproduce the same draw sequence after a session restart.
        /// </summary>
        public void Reset()
        {
            _items.Clear();
            for (var i = 0; i < _initial.Count; i++)
                _items.Add(_initial[i]);
            _cursor = _items.Count - 1;
        }

        /// <summary>Total marbles in the bag (full-cycle size).</summary>
        public int Count => _items.Count;

        /// <summary>Marbles left before the bag refills.</summary>
        public int Remaining => _cursor + 1;

        /// <summary>
        /// Adds <paramref name="count"/> copies of an item. Adding resets the cursor
        /// to a full bag, restarting the current cycle.
        /// </summary>
        public void Add(T item, int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                _items.Add(item);
                _initial.Add(item);
            }
            _cursor = _items.Count - 1;
        }

        /// <summary>Removes all marbles.</summary>
        public void Clear()
        {
            _items.Clear();
            _initial.Clear();
            _cursor = -1;
        }

        /// <summary>
        /// Draws using a caller-supplied roll in [0, 1). Consumes no RNG. Swap-to-boundary:
        /// the drawn marble trades places with the cursor slot and the cursor shrinks, so
        /// every marble is seen exactly once per cycle.
        /// </summary>
        public T NextFromRoll(float roll01)
        {
            if (_cursor < 0)
                _cursor = _items.Count - 1;

            var index = (int)(roll01 * (_cursor + 1));
            if (index < 0) index = 0;
            else if (index > _cursor) index = _cursor;

            var selected = _items[index];
            _items[index] = _items[_cursor];
            _items[_cursor] = selected;
            _cursor--;
            return selected;
        }

        /// <summary>Draws using the global Nez.Random stream.</summary>
        public T Next()
        {
            return NextFromRoll(Nez.Random.NextFloat());
        }

        /// <summary>Draws using a caller-owned RNG (virtual layer / deterministic paths).</summary>
        public T Next(System.Random rng)
        {
            return NextFromRoll((float)rng.NextDouble());
        }
    }
}
