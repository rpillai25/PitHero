using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.Services.Replay;
using RolePlayingFramework.Equipment;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>The Digging Field's outcome table (issue #452): the 500-marble bag of junk and gems.</summary>
    [TestClass]
    public class GemDigConfigTests
    {
        private static readonly int[] ExpectedValues = { 1, 2, 5, 10, 50, 100, 300, 700, 1000, 1500, 2000, 5000, 10000 };

        [TestMethod]
        public void Values_MatchTheIssue_AndJunkIsWorthNothing()
        {
            Assert.AreEqual(ExpectedValues.Length, GemDigConfig.GemCount);
            Assert.AreEqual(GemDigConfig.GemCount, Enum.GetValues(typeof(GemType)).Length);
            Assert.AreEqual(GemDigConfig.JunkCount, Enum.GetValues(typeof(DigJunkType)).Length);
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                Assert.AreEqual(ExpectedValues[i], GemDigConfig.GetValue((GemType)i), ((GemType)i).ToString());
            for (int i = 0; i < GemDigConfig.JunkCount; i++)
                Assert.AreEqual(0, GemDigConfig.GetValue(DigFind.OfJunk((DigJunkType)i)), ((DigJunkType)i).ToString());
            Assert.AreEqual(0, GemDigConfig.GetValue(DigFind.Nothing));
            Assert.AreEqual(10000, GemDigConfig.GetValue(DigFind.OfGem(GemType.Diamond)));
        }

        [TestMethod]
        public void Ordinals_ArePinned()
        {
            Assert.AreEqual(0, (int)GemType.Agate);
            Assert.AreEqual(12, (int)GemType.Diamond);
            Assert.AreEqual(5, (int)DigJunkType.YarnBall);
            Assert.AreEqual(127, (int)PlayerCommandType.DigGem);
        }

        [TestMethod]
        public void OutcomeIndex_RoundTrips()
        {
            Assert.AreEqual(GemDigConfig.OutcomeCount, 1 + GemDigConfig.JunkCount + GemDigConfig.GemCount);
            for (int i = 0; i < GemDigConfig.OutcomeCount; i++)
                Assert.AreEqual(i, DigFind.FromOutcomeIndex(i).OutcomeIndex);
            Assert.AreEqual(0, DigFind.Nothing.OutcomeIndex);
            Assert.AreEqual(DigFindKind.Junk, DigFind.FromOutcomeIndex(1).Kind);
            Assert.AreEqual(GemType.Agate, DigFind.FromOutcomeIndex(1 + GemDigConfig.JunkCount).Gem);
        }

        [TestMethod]
        public void Marbles_FiveHundred_HalfEmptyOrJunk_HalfGems()
        {
            int gems = 0, junk = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                Assert.IsTrue(GemDigConfig.GetMarbles((GemType)i) > 0, ((GemType)i).ToString());
                gems += GemDigConfig.GetMarbles((GemType)i);
            }
            for (int i = 0; i < GemDigConfig.JunkCount; i++)
            {
                Assert.IsTrue(GemDigConfig.GetMarbles((DigJunkType)i) > 0, ((DigJunkType)i).ToString());
                junk += GemDigConfig.GetMarbles((DigJunkType)i);
            }
            Assert.AreEqual(500, GemDigConfig.BagSize);
            Assert.AreEqual(GemDigConfig.BagSize, GemDigConfig.NothingMarbles + junk + gems);
            Assert.AreEqual(135, GemDigConfig.NothingMarbles, "empty digs");
            Assert.AreEqual(115, junk, "junk");
            Assert.AreEqual(250, GemDigConfig.NothingMarbles + junk, "half the bag pays nothing");
            Assert.AreEqual(250, gems, "half the bag is gems");
        }

        [TestMethod]
        public void Diamond_IsOneMarbleInFiveHundred_AndTiersFallOff()
        {
            Assert.AreEqual(1, GemDigConfig.GetMarbles(GemType.Diamond));
            Assert.AreEqual(1.0 / 500, GemDigConfig.GetProbability(GemType.Diamond), 1e-12);
            for (int t = 2; t < 5; t++)
                Assert.IsTrue(GemTierProbability((ItemRarity)t) < GemTierProbability((ItemRarity)(t - 1)) / 1.5,
                    ((ItemRarity)t) + " should be well under the chance of " + ((ItemRarity)(t - 1)));
        }

        [TestMethod]
        public void NoGemIsMoreCommonThanTheOneBeforeIt_AndJunkFallsOffToo()
        {
            for (int i = 1; i < GemDigConfig.GemCount; i++)
                Assert.IsTrue(GemDigConfig.GetMarbles((GemType)i) <= GemDigConfig.GetMarbles((GemType)(i - 1)),
                    ((GemType)i) + " should not be more common than " + ((GemType)(i - 1)));
            for (int i = 1; i < GemDigConfig.JunkCount; i++)
                Assert.IsTrue(GemDigConfig.GetMarbles((DigJunkType)i) <= GemDigConfig.GetMarbles((DigJunkType)(i - 1)),
                    ((DigJunkType)i) + " should not be more common than " + ((DigJunkType)(i - 1)));
            Assert.IsTrue(GemDigConfig.GetMarbles(DigJunkType.YarnBall) < GemDigConfig.GetMarbles(DigJunkType.Shell),
                "the last junk listed is rarer than the first");
        }

        [TestMethod]
        public void LabelColors_LightenUncommonAndRare_KeepTheOthers()
        {
            Assert.AreEqual(GemDigConfig.UncommonLabelColor, GemDigConfig.GetTextColor(GemType.Garnet));
            Assert.AreEqual(GemDigConfig.RareLabelColor, GemDigConfig.GetTextColor(GemType.Tanzanite));
            Assert.AreEqual(GameConfig.RARITY_NORMAL, GemDigConfig.GetTextColor(GemType.Agate));
            Assert.AreEqual(GameConfig.RARITY_EPIC, GemDigConfig.GetTextColor(GemType.Ruby));
            Assert.AreEqual(GameConfig.RARITY_LEGENDARY, GemDigConfig.GetTextColor(GemType.Diamond));

            // Lighter than the item rarity shades they replace (perceived brightness)
            Assert.IsTrue(Luma(GemDigConfig.UncommonLabelColor) > Luma(GameConfig.RARITY_UNCOMMON));
            Assert.IsTrue(Luma(GemDigConfig.RareLabelColor) > Luma(GameConfig.RARITY_RARE));
        }

        private static double Luma(Microsoft.Xna.Framework.Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

        [TestMethod]
        public void Rarities_FollowTheIssueTiers()
        {
            Assert.AreEqual(ItemRarity.Normal, GemDigConfig.GetRarity(GemType.Peridot));
            Assert.AreEqual(ItemRarity.Uncommon, GemDigConfig.GetRarity(GemType.Garnet));
            Assert.AreEqual(ItemRarity.Uncommon, GemDigConfig.GetRarity(GemType.Aquamarine));
            Assert.AreEqual(ItemRarity.Rare, GemDigConfig.GetRarity(GemType.Spinel));
            Assert.AreEqual(ItemRarity.Rare, GemDigConfig.GetRarity(GemType.Tanzanite));
            Assert.AreEqual(ItemRarity.Epic, GemDigConfig.GetRarity(GemType.Sapphire));
            Assert.AreEqual(ItemRarity.Epic, GemDigConfig.GetRarity(GemType.Alexandrite));
            Assert.AreEqual(ItemRarity.Legendary, GemDigConfig.GetRarity(GemType.Diamond));
        }

        [TestMethod]
        public void ExpectedValue_Is28280GoldPerBag()
        {
            int perBag = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                perBag += GemDigConfig.GetMarbles((GemType)i) * GemDigConfig.GetValue((GemType)i);
            Assert.AreEqual(28_280, perBag);
            Assert.AreEqual(56.56, (double)perBag / GemDigConfig.BagSize, 1e-9);
        }

        [TestMethod]
        public void Bag_EveryCycleOfFiveHundredDigsHoldsTheExactComposition()
        {
            var bag = GemDigConfig.BuildBag();
            Assert.AreEqual(GemDigConfig.BagSize, bag.Count);
            var random = new SeedableRandom(452);
            for (int cycle = 0; cycle < 4; cycle++)
            {
                var counts = new int[GemDigConfig.OutcomeCount];
                for (int d = 0; d < GemDigConfig.BagSize; d++)
                    counts[GemDigConfig.Draw(bag, random.NextInt64(GemDigConfig.RollRange)).OutcomeIndex]++;
                for (int i = 0; i < GemDigConfig.OutcomeCount; i++)
                {
                    var find = DigFind.FromOutcomeIndex(i);
                    Assert.AreEqual(GemDigConfig.GetMarbles(find), counts[i], "cycle " + cycle + ": " + find);
                }
            }
        }

        [TestMethod]
        public void Bag_SameRollsDrawTheSameFinds()
        {
            var a = GemDigConfig.BuildBag();
            var b = GemDigConfig.BuildBag();
            var random = new SeedableRandom(7);
            for (int i = 0; i < 3_000; i++)
            {
                long roll = random.NextInt64(GemDigConfig.RollRange);
                Assert.AreEqual(GemDigConfig.Draw(a, roll).OutcomeIndex, GemDigConfig.Draw(b, roll).OutcomeIndex);
            }
        }

        [TestMethod]
        public void RollToUnit_ClampsIntoZeroToOne()
        {
            Assert.AreEqual(0f, GemDigConfig.RollToUnit(-5));
            Assert.AreEqual(0f, GemDigConfig.RollToUnit(0));
            Assert.AreEqual(0.5f, GemDigConfig.RollToUnit(GemDigConfig.RollRange / 2));
            Assert.IsTrue(GemDigConfig.RollToUnit(long.MaxValue) <= 1f);
            // A full bag drawn with the extreme rolls still returns a marble from the bag
            Assert.AreEqual(DigFindKind.Nothing, GemDigConfig.Draw(GemDigConfig.BuildBag(), -1).Kind, "first marble in outcome order");
            Assert.AreEqual(GemType.Diamond, GemDigConfig.Draw(GemDigConfig.BuildBag(), long.MaxValue).Gem, "last marble in outcome order");
        }

        [TestMethod]
        public void NameKeys_AndSpriteNames_MatchEachFind()
        {
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                var gem = (GemType)i;
                Assert.AreEqual("Gem" + gem, GemDigConfig.GetNameKey(gem));
                Assert.AreEqual("Gem" + gem, GemDigConfig.GetSpriteName(gem));
            }
            for (int i = 0; i < GemDigConfig.JunkCount; i++)
            {
                var junk = (DigJunkType)i;
                Assert.AreEqual("DigJunk" + junk, GemDigConfig.GetNameKey(junk));
                Assert.AreEqual("Junk" + junk, GemDigConfig.GetSpriteName(junk));
            }
        }

        [TestMethod]
        public void FormatLabel_ShowsNameAndGold()
        {
            Assert.AreEqual("Agate (+1)", GemDigConfig.FormatLabel("{0} (+{1})", "Agate", 1));
            Assert.AreEqual("Shell (+0)", GemDigConfig.FormatLabel("{0} (+{1})", "Shell", 0));
        }

        [TestMethod]
        public void UiText_HasTheLabelsAndEveryName()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            string uiPath = null;
            while (dir != null && uiPath == null)
            {
                var candidate = Path.Combine(dir.FullName, "PitHero", "Content", "Localization", "en-us", "UI.txt");
                if (File.Exists(candidate))
                    uiPath = candidate;
                dir = dir.Parent;
            }
            Assert.IsNotNull(uiPath, "Could not locate PitHero/Content/Localization/en-us/UI.txt");

            var values = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(uiPath))
            {
                int comma = line.IndexOf(',');
                if (comma > 0 && !line.StartsWith("#"))
                    values[line.Substring(0, comma)] = line.Substring(comma + 1);
            }
            Assert.AreEqual("{0} (+{1})", values[UITextKey.GemDigLabelFormat]);
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                Assert.AreEqual(((GemType)i).ToString(), values[GemDigConfig.GetNameKey((GemType)i)]);
            string[] junkNames = { "Shell", "Cup", "Plate", "Spoon", "Bottle", "Yarn Ball" };
            for (int i = 0; i < GemDigConfig.JunkCount; i++)
                Assert.AreEqual(junkNames[i], values[GemDigConfig.GetNameKey((DigJunkType)i)]);
        }

        private static double GemTierProbability(ItemRarity rarity)
        {
            double p = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                if (GemDigConfig.GetRarity((GemType)i) == rarity)
                    p += GemDigConfig.GetProbability((GemType)i);
            return p;
        }
    }
}
