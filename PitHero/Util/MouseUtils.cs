using Nez;

namespace PitHero.Util
{
    /// <summary>
    /// Helpers for validating raw mouse state before it is turned into a world-space interaction.
    /// </summary>
    public static class MouseUtils
    {
        /// <summary>
        /// Returns true when the window has OS focus and the raw cursor is within the client area.
        /// The OS reports mouse coordinates that run negative or past the window size when the cursor
        /// leaves the window (e.g. onto a second monitor), and Camera.MouseToWorldPoint() will happily
        /// map those to real tiles. Every world-space pick must gate on this first.
        /// </summary>
        public static bool IsMouseInsideWindow()
        {
            if (!Core.Instance.IsActive)
                return false;
            var raw = Input.RawMousePosition;
            return raw.X >= 0 && raw.Y >= 0 && raw.X < Screen.Width && raw.Y < Screen.Height;
        }
    }
}
