using Microsoft.Xna.Framework;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>Manages active stencils placed on the inventory grid.</summary>
    public class ActiveStencilManager
    {
        private readonly List<PlacedStencil> _placedStencils;

        public IReadOnlyList<PlacedStencil> PlacedStencils => _placedStencils;

        public ActiveStencilManager()
        {
            _placedStencils = new List<PlacedStencil>();
        }

        /// <summary>Adds a stencil to the grid at the specified anchor position.</summary>
        public void PlaceStencil(SynergyPattern pattern, Point anchor)
        {
            // Remove existing stencil for this pattern if present
            RemoveStencilByPatternId(pattern.Id);

            _placedStencils.Add(new PlacedStencil(pattern, anchor));
        }

        /// <summary>Removes a stencil from the grid.</summary>
        public void RemoveStencil(PlacedStencil stencil)
        {
            _placedStencils.Remove(stencil);
        }

        /// <summary>Removes a stencil by pattern ID.</summary>
        public void RemoveStencilByPatternId(string patternId)
        {
            for (int i = _placedStencils.Count - 1; i >= 0; i--)
            {
                if (_placedStencils[i].Pattern.Id == patternId)
                {
                    _placedStencils.RemoveAt(i);
                }
            }
        }

        /// <summary>Moves a stencil to a new anchor position with clamping.</summary>
        public void MoveStencil(PlacedStencil stencil, Point newAnchor, int gridWidth, int gridHeight)
        {
            // Clamp to ensure stencil stays within grid bounds
            var clampedAnchor = ClampAnchorToGrid(stencil.Pattern, newAnchor, gridWidth, gridHeight);
            stencil.Anchor = clampedAnchor;
        }

        /// <summary>Finds a stencil at the given grid position.</summary>
        public PlacedStencil FindStencilAtPosition(Point gridPos)
        {
            for (int i = 0; i < _placedStencils.Count; i++)
            {
                var stencil = _placedStencils[i];
                if (IsPositionInStencil(gridPos, stencil))
                {
                    return stencil;
                }
            }
            return null;
        }

        /// <summary>Checks if a grid position is within a stencil's pattern.</summary>
        private bool IsPositionInStencil(Point gridPos, PlacedStencil stencil)
        {
            var offsets = stencil.Pattern.GridOffsets;
            for (int i = 0; i < offsets.Count; i++)
            {
                var targetX = stencil.Anchor.X + offsets[i].X;
                var targetY = stencil.Anchor.Y + offsets[i].Y;

                if (targetX == gridPos.X && targetY == gridPos.Y)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Clamps anchor position to keep all pattern offsets within grid bounds.</summary>
        private Point ClampAnchorToGrid(SynergyPattern pattern, Point anchor, int gridWidth, int gridHeight)
        {
            int minX = 0, maxX = gridWidth - 1;
            int minY = 0, maxY = gridHeight - 1;

            // Find the bounds of the pattern relative to anchor
            var offsets = pattern.GridOffsets;
            for (int i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];

                // Calculate required anchor bounds to keep this offset in grid
                int requiredMinX = -offset.X;
                int requiredMaxX = gridWidth - 1 - offset.X;
                int requiredMinY = -offset.Y;
                int requiredMaxY = gridHeight - 1 - offset.Y;

                minX = System.Math.Max(minX, requiredMinX);
                maxX = System.Math.Min(maxX, requiredMaxX);
                minY = System.Math.Max(minY, requiredMinY);
                maxY = System.Math.Min(maxY, requiredMaxY);
            }

            // Clamp anchor to calculated bounds
            int clampedX = System.Math.Max(minX, System.Math.Min(maxX, anchor.X));
            int clampedY = System.Math.Max(minY, System.Math.Min(maxY, anchor.Y));

            return new Point(clampedX, clampedY);
        }

        /// <summary>Clears all placed stencils.</summary>
        public void ClearAll()
        {
            _placedStencils.Clear();
        }
    }

    /// <summary>Represents a stencil placed on the inventory grid.</summary>
    public class PlacedStencil
    {
        public SynergyPattern Pattern { get; }
        public Point Anchor { get; set; }

        public PlacedStencil(SynergyPattern pattern, Point anchor)
        {
            Pattern = pattern;
            Anchor = anchor;
        }
    }
}
