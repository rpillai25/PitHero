using Nez;
using Nez.Tiled;

namespace PitHero.Util
{
    public class TiledMapHelper
    {
        public static void RemoveTile(TmxMap tmxMap, string layerName, int x, int y)
        {
            if (IsOutOfBounds(tmxMap, x, y))
            {
                Debug.Log("WARNING: RemoveTile out of bounds!!!");
                return;
            }

            var layer = (TmxLayer)tmxMap.GetLayer(layerName);
            if (layer == null)
            {
                Debug.Log("WARNING: Layer null for RemoveTile!!!");
                return;
            }
            layer.RemoveTile(x, y);
        }

        public static void SetTile(TmxMap tmxMap, string layerName, int x, int y, int tileIndex)
        {
            if (IsOutOfBounds(tmxMap, x, y))
            {
                Debug.Log("WARNING: SetTile out of bounds!!!");
                return;
            }

            var layer = (TmxLayer)tmxMap.GetLayer(layerName);
            if (layer == null)
            {
                Debug.Log("WARNING: Layer null for SetTile!!!");
                return;
            }
            //var newTile = new TmxLayerTile(tiledMap, tileIndex, x, y);
            //layer.SetTile(newTile);
            layer.SetTile(x, y, tileIndex);
        }

        private static bool IsOutOfBounds(TmxMap tmxMap, int x, int y)
        {
            return (y * tmxMap.Width + x < 0 || y * tmxMap.Width + x >= tmxMap.Width * tmxMap.Height);
        }
    }
}
