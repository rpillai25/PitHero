using System.Collections.Generic;
using RolePlayingFramework.Equipment;

namespace PitHero.Services
{
    /// <summary>
    /// Session-only tracking of newly acquired gear the player has not yet viewed in the
    /// inventory UI. Unviewed gear renders with a sparkle overlay; the set is cleared when
    /// the player leaves the inventory tab or dismisses the Hero window. Never persisted.
    /// </summary>
    public static class UnviewedGearTracker
    {
        private static readonly HashSet<IItem> _unviewed = new HashSet<IItem>(ReferenceEqualityComparer.Instance);

        /// <summary>Number of unviewed items currently tracked.</summary>
        public static int Count => _unviewed.Count;

        /// <summary>Marks gear as newly acquired and not yet viewed. Non-gear items are ignored.</summary>
        public static void MarkNew(IItem item)
        {
            if (item is IGear)
                _unviewed.Add(item);
        }

        /// <summary>True when the item was acquired this session and not yet viewed in the inventory.</summary>
        public static bool IsUnviewed(IItem item)
        {
            return item != null && _unviewed.Contains(item);
        }

        /// <summary>Marks everything as viewed.</summary>
        public static void ClearAll()
        {
            _unviewed.Clear();
        }
    }
}
