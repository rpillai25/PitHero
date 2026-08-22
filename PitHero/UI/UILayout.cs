using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// Pure layout math shared by every tall UI window. The strip's design height
    /// (GameConfig.VirtualHeight) is configurable, so windows size and position themselves against
    /// Stage.GetHeight() at show time instead of hard-coding the height they were designed at.
    /// </summary>
    public static class UILayout
    {
        /// <summary>Smallest height a fitted window is allowed to shrink to.</summary>
        public const float MinWindowHeight = 32f;

        /// <summary>Smallest height a fitted scroll cell is allowed to shrink to (one row plus chrome).</summary>
        public const float MinScrollCellHeight = 44f;

        /// <summary>
        /// Height for a window that starts at topY: its design height, capped so the window still ends
        /// bottomMargin above the bottom of the stage.
        /// </summary>
        public static float FitHeight(float preferred, float stageH, float topY, float bottomMargin)
        {
            var available = stageH - topY - bottomMargin;
            var height = preferred < available ? preferred : available;
            if (height < MinWindowHeight)
                height = MinWindowHeight;
            return height;
        }

        /// <summary>
        /// Keeps a window of the given height on the stage: pushed up off the bottom edge first, then
        /// pinned to the top (the title bar wins when the window is taller than the stage).
        /// </summary>
        public static float ClampY(float y, float h, float stageH)
        {
            if (y + h > stageH)
                y = stageH - h;
            if (y < 0f)
                y = 0f;
            return y;
        }

        /// <summary>
        /// Vertically centers a window of the given height, shifted up by bias, clamped to the stage.
        /// </summary>
        public static float CenterY(float h, float stageH, float bias)
        {
            return ClampY((stageH - h) / 2f - bias, h, stageH);
        }

        /// <summary>
        /// Shrinks a packed window's scroll cell until the whole window fits the stage. Cell.Height does
        /// not invalidate the layout, so the owning table is invalidated and the window re-packed.
        /// Returns the cell height that ended up applied.
        /// </summary>
        public static float FitScrollCellToStage(Window window, Table owner, Cell scrollCell, float designCellHeight, float stageH, float margin)
        {
            if (window == null || owner == null || scrollCell == null)
                return designCellHeight;

            scrollCell.Height(designCellHeight);
            owner.InvalidateHierarchy();
            window.Pack();

            var overflow = window.GetHeight() - (stageH - 2f * margin);
            if (overflow <= 0f)
                return designCellHeight;

            var fitted = designCellHeight - overflow;
            if (fitted < MinScrollCellHeight)
                fitted = MinScrollCellHeight;

            scrollCell.Height(fitted);
            owner.InvalidateHierarchy();
            window.Pack();
            return fitted;
        }
    }
}
