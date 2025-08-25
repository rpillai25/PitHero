using PitHero.AI.Interfaces;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of ITileData for virtual layer
    /// </summary>
    public class VirtualTmxTile : ITileData
    {
        public int Gid { get; }

        public VirtualTmxTile(int gid)
        {
            Gid = gid;
        }
    }
}