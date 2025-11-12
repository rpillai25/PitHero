using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Detects synergy patterns in an inventory grid.</summary>
    public sealed class SynergyDetector
    {
        private readonly List<SynergyPattern> _allPatterns;
        
        public SynergyDetector()
        {
            _allPatterns = new List<SynergyPattern>();
        }
        
        /// <summary>Registers a synergy pattern for detection.</summary>
        public void RegisterPattern(SynergyPattern pattern)
        {
            if (pattern == null) return;
            _allPatterns.Add(pattern);
        }
        
        /// <summary>Detects all active synergies in the given inventory grid.</summary>
        /// <param name="gridItems">2D array of items in the grid (null for empty slots).</param>
        /// <param name="gridWidth">Width of the inventory grid.</param>
        /// <param name="gridHeight">Height of the inventory grid.</param>
        public List<ActiveSynergy> DetectSynergies(IItem?[,] gridItems, int gridWidth, int gridHeight)
        {
            var activeSynergies = new List<ActiveSynergy>();
            
            // For each registered pattern
            for (int i = 0; i < _allPatterns.Count; i++)
            {
                var pattern = _allPatterns[i];
                
                // Try each grid position as anchor
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        var anchor = new Point(x, y);
                        if (TryMatchPattern(gridItems, gridWidth, gridHeight, pattern, anchor, out var slots))
                        {
                            activeSynergies.Add(new ActiveSynergy(pattern, anchor, slots));
                        }
                    }
                }
            }
            
            return activeSynergies;
        }
        
        /// <summary>Attempts to match a pattern at the given anchor position.</summary>
        private bool TryMatchPattern(
            IItem?[,] gridItems,
            int gridWidth,
            int gridHeight,
            SynergyPattern pattern,
            Point anchor,
            out List<Point> matchedSlots)
        {
            matchedSlots = new List<Point>();
            
            // Check if all required items exist at offset positions
            var offsets = pattern.GridOffsets;
            var requiredKinds = pattern.RequiredKinds;
            
            if (offsets.Count != requiredKinds.Count)
                return false;
            
            for (int i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                var targetX = anchor.X + offset.X;
                var targetY = anchor.Y + offset.Y;
                
                // Check bounds
                if (targetX < 0 || targetX >= gridWidth || targetY < 0 || targetY >= gridHeight)
                    return false;
                
                var item = gridItems[targetX, targetY];
                
                // Check if item exists and matches required kind
                if (item == null || item.Kind != requiredKinds[i])
                    return false;
                
                matchedSlots.Add(new Point(targetX, targetY));
            }
            
            return true;
        }
    }
}
