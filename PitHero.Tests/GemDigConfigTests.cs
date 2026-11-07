using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.Services.Replay;
using RolePlayingFramework.Equipment;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>The Digging Field's outcome table (issue #452): empty digs, junk and gems, and their odds.</summary>
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
        public void Weights_SumToTotal_ThirtyFiveThirtyThirtyFive()
        {
            long gems = 0, junk = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                Assert.IsTrue(GemDigConfig.GetWeight((GemType)i) > 0, ((GemType)i).ToString());
                gems += GemDigConfig.GetWeight((GemType)i);
            }
            for (int i = 0; i < GemDigConfig.JunkCount; i++)
                junk += GemDigConfig.GetWeight((DigJunkType)i);
            Assert.AreEqual(GemDigConfig.TotalWeight, GemDigConfig.NothingWeight + junk + gems);
            Assert.AreEqual(350_000, GemDigConfig.NothingWeight, "35% of digs turn up nothing");
            Assert.AreEqual(300_000, junk, "30% turn up junk");
            Assert.AreEqual(350_000, gems, "35% turn up a gem");
        }

        [TestMethod]
        public void Diamond_IsOneInAThousand_AndTiersFallOff()
        {
            Assert.AreEqual(1.0 / 1000, GemDigConfig.GetProbability(GemType.Diamond), 1e-12);
            for (int t = 2; t < 5; t++)
                Assert.IsTrue(GemTierProbability((ItemRarity)t) < GemTierProbability((ItemRarity)(t - 1)) / 1.5,
                    ((ItemRarity)t) + " should be well under the chance of " + ((ItemRarity)(t - 1)));
        }

        [TestMethod]
        public void GemWeights_AreGeometric_AboutOnePointFourEightFiveApart()
        {
            for (int i = 1; i < GemDigConfig.GemCount; i++)
            {
                double ratio = (double)GemDigConfig.GetWeight((GemType)(i - 1)) / GemDigConfig.GetWeight((GemType)i);
                Assert.AreEqual(1.485, ratio, 0.01, ((GemType)(i - 1)) + " vs " + ((GemType)i));
            }
        }

        [TestMethod]
        public void EveryGemIsRarerThanTheOneBeforeIt_AndJunkFallsOffToo()
        {
            for (int i = 1; i < GemDigConfig.GemCount; i++)
                Assert.IsTrue(GemDigConfig.GetWeight((GemType)i) < GemDigConfig.GetWeight((GemType)(i - 1)),
                    ((GemType)i) + " should be rarer than " + ((GemType)(i - 1)));
            Assert.IsTrue(GemDigConfig.GetWeight(DigJunkType.YarnBall) < GemDigConfig.GetWeight(DigJunkType.Shell),
                "the last junk listed is rarer than the first");
        }

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
        public void FromRoll_CoversEveryRollExactlyByWeight()
        {
            int start = 0;
            for (int i = 0; i < GemDigConfig.OutcomeCount; i++)
            {
                var find = DigFind.FromOutcomeIndex(i);
                int end = start + (int)Math.Round(GemDigConfig.GetProbability(find) * GemDigConfig.TotalWeight);
                Assert.AreEqual(i, GemDigConfig.FromRoll(start).OutcomeIndex, "first roll of " + find);
                Assert.AreEqual(i, GemDigConfig.FromRoll(end - 1).OutcomeIndex, "last roll of " + find);
                start = end;
            }
            Assert.AreEqual(GemDigConfig.TotalWeight, start);
            Assert.AreEqual(DigFindKind.Nothing, GemDigConfig.FromRoll(-5).Kind);
            Assert.AreEqual(GemType.Diamond, GemDigConfig.FromRoll(GemDigConfig.TotalWeight + 5).Gem);
        }

        [TestMethod]
        public void ExpectedValue_IsAboutFortyThreeGoldPerDig()
        {
            double ev = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                ev += GemDigConfig.GetProbability((GemType)i) * GemDigConfig.GetValue((GemType)i);
            Assert.AreEqual(43.5, ev, 0.1);
        }

        [TestMethod]
        public void Roll_FrequenciesMatchTheTable()
        {
            const int Digs = 400_000;
            int nothing = 0, junk = 0, gems = 0;
            var random = new SeedableRandom(452);
            for (int i = 0; i < Digs; i++)
            {
                var find = GemDigConfig.Roll(random);
                if (find.Kind == DigFindKind.Nothing) nothing++;
                else if (find.Kind == DigFindKind.Junk) junk++;
                else gems++;
            }
            Assert.AreEqual(0.35, (double)nothing / Digs, 0.005, "nothing");
            Assert.AreEqual(0.30, (double)junk / Digs, 0.005, "junk");
            Assert.AreEqual(0.35, (double)gems / Digs, 0.005, "gems");
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
            Assert.AreEqual("Nothing", values[UITextKey.DigNothing]);
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
