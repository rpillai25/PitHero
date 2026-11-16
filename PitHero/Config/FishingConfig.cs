using Microsoft.Xna.Framework;

namespace PitHero.Config
{
    /// <summary>
    /// Everything a Fishing Pond can yield: the ten fish smallest to largest, then the junk. Ordinals
    /// are logged in analytics and pinned by tests: append only, never renumber.
    /// </summary>
    public enum FishCatchType
    {
        Minnow = 0,
        Bluegill = 1,
        Perch = 2,
        RainbowTrout = 3,
        LargemouthBass = 4,
        GoldenKoi = 5,
        ChannelCatfish = 6,
        NorthernPike = 7,
        LakeSturgeon = 8,
        AlligatorGar = 9,
        JunkCan = 10,
        JunkBottle = 11,
        JunkShoe = 12,
    }

    /// <summary>
    /// The fishing catch table (see docs/FishLibrary.md): gold, marbles in the 200-marble bag, sprite
    /// and name of every catch. Fish gold grows by the same factor per step (50 G x 20^(k/9), rounded to
    /// 5); marbles shrink with value so the Alligator Gar is 2 marbles in 200. Junk is worth nothing.
    /// </summary>
    public static class FishingConfig
    {
        /// <summary>Number of catch kinds (the enum is dense from 0).</summary>
        public const int CatchCount = 13;

        /// <summary>Marbles in one full bag; every 200 catches contain exactly the composition below.</summary>
        public const int BagSize = 200;

        private static readonly int[] Gold = { 50, 70, 100, 135, 190, 265, 370, 515, 720, 1000, 0, 0, 0 };
        private static readonly int[] Marbles = { 40, 32, 25, 20, 15, 11, 8, 5, 3, 2, 13, 13, 13 };

        private static readonly string[] SpriteNames =
        {
            "Minnow", "Bluegill", "Perch", "RainbowTrout", "LargemouthBass", "GoldenKoi",
            "ChannelCatfish", "NorthernPike", "LakeSturgeon", "AlligatorGar",
            "JunkCan", "JunkBottle", "JunkShoe",
        };

        private static readonly string[] NameKeys =
        {
            UITextKey.FishMinnow, UITextKey.FishBluegill, UITextKey.FishPerch, UITextKey.FishRainbowTrout,
            UITextKey.FishLargemouthBass, UITextKey.FishGoldenKoi, UITextKey.FishChannelCatfish,
            UITextKey.FishNorthernPike, UITextKey.FishLakeSturgeon, UITextKey.FishAlligatorGar,
            UITextKey.FishJunkCan, UITextKey.FishJunkBottle, UITextKey.FishJunkShoe,
        };

        /// <summary>Label color of a fish's "+N G" value: the pay-gold yellow.</summary>
        public static readonly Color FishTextColor = new Color(0xfb, 0xf2, 0x36);

        /// <summary>Label color of junk (worth nothing): the same gray the Digging Field uses.</summary>
        public static Color JunkTextColor => GemDigConfig.NoValueTextColor;

        /// <summary>Gold the catch sells for on the spot (0 for junk).</summary>
        public static int GetGold(FishCatchType type) => Gold[(int)type];

        /// <summary>Marbles of this catch in the 200-marble bag.</summary>
        public static int GetMarbles(FishCatchType type) => Marbles[(int)type];

        /// <summary>True for worthless junk (can, bottle, shoe).</summary>
        public static bool IsJunk(FishCatchType type) => type >= FishCatchType.JunkCan;

        /// <summary>Sprite name in the Actors atlas (Actors/Fish and Actors/FishingJunk).</summary>
        public static string GetSpriteName(FishCatchType type) => SpriteNames[(int)type];

        /// <summary>UI.txt key of the catch's display name.</summary>
        public static string GetNameKey(FishCatchType type) => NameKeys[(int)type];
    }
}
