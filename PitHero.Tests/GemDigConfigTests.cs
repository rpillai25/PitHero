using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.Services.Replay;
using RolePlayingFramework.Equipment;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>The gem dig site's gem table and drop odds (issue #452).</summary>
    [TestClass]
    public class GemDigConfigTests
    {
        private static readonly int[] ExpectedValues = { 1, 2, 5, 10, 50, 100, 300, 700, 1000, 1500, 2000, 5000, 10000 };

        [TestMethod]
        public void Values_MatchTheIssue()
        {
            Assert.AreEqual(ExpectedValues.Length, GemDigConfig.GemCount);
            Assert.AreEqual(GemDigConfig.GemCount, Enum.GetValues(typeof(GemType)).Length);
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                Assert.AreEqual(ExpectedValues[i], GemDigConfig.GetValue((GemType)i), ((GemType)i).ToString());
        }

        [TestMethod]
        public void GemOrdinals_ArePinned()
        {
            // Ordinals travel in recorded DigGem commands: never renumber
            Assert.AreEqual(0, (int)GemType.Agate);
            Assert.AreEqual(12, (int)GemType.Diamond);
            Assert.AreEqual(127, (int)PlayerCommandType.DigGem);
        }

        [TestMethod]
        public void Weights_SumToTotal()
        {
            long sum = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                Assert.IsTrue(GemDigConfig.GetWeight((GemType)i) > 0, ((GemType)i).ToString());
                sum += GemDigConfig.GetWeight((GemType)i);
            }
            Assert.AreEqual(GemDigConfig.TotalWeight, sum);
        }

        [TestMethod]
        public void TierOdds_AreTwentyTimesRarerPerTier()
        {
            Assert.AreEqual(1.0 / 20, TierProbability(ItemRarity.Uncommon), 1e-12);
            Assert.AreEqual(1.0 / 400, TierProbability(ItemRarity.Rare), 1e-12);
            Assert.AreEqual(1.0 / 8000, TierProbability(ItemRarity.Epic), 1e-12);
            Assert.AreEqual(1.0 / 160000, TierProbability(ItemRarity.Legendary), 1e-12);
        }

        [TestMethod]
        public void Odds_FallWithinEveryTierAndAcrossTiers()
        {
            // Each gem is strictly rarer than the one listed before it, so Agate is the most
            // common gem and Diamond the rarest, and the last gem of a tier is its rarest
            for (int i = 1; i < GemDigConfig.GemCount; i++)
                Assert.IsTrue(GemDigConfig.GetWeight((GemType)i) < GemDigConfig.GetWeight((GemType)(i - 1)),
                    ((GemType)i) + " should be rarer than " + ((GemType)(i - 1)));
            Assert.IsTrue(GemDigConfig.GetProbability(GemType.Agate) > 0.5);
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
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                var gem = (GemType)i;
                int end = start + GemDigConfig.GetWeight(gem);
                Assert.AreEqual(gem, GemDigConfig.FromRoll(start), "first roll of " + gem);
                Assert.AreEqual(gem, GemDigConfig.FromRoll(end - 1), "last roll of " + gem);
                start = end;
            }
            Assert.AreEqual(GemType.Agate, GemDigConfig.FromRoll(-5));
            Assert.AreEqual(GemType.Diamond, GemDigConfig.FromRoll(GemDigConfig.TotalWeight + 5));
        }

        [TestMethod]
        public void ExpectedValue_IsAboutFourGoldPerDig()
        {
            double ev = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                ev += GemDigConfig.GetProbability((GemType)i) * GemDigConfig.GetValue((GemType)i);
            Assert.AreEqual(3.79, ev, 0.01);
        }

        [TestMethod]
        public void Roll_FrequenciesMatchTheTable()
        {
            const int Digs = 400_000;
            var counts = new int[GemDigConfig.GemCount];
            var random = new SeedableRandom(452);
            for (int i = 0; i < Digs; i++)
                counts[(int)GemDigConfig.Roll(random)]++;

            // The three common gems are frequent enough to check closely (within 1%)
            for (int i = 0; i < 3; i++)
            {
                double expected = GemDigConfig.GetProbability((GemType)i);
                Assert.AreEqual(expected, (double)counts[i] / Digs, 0.01, ((GemType)i).ToString());
            }
            Assert.AreEqual(1.0 / 20, (double)(counts[3] + counts[4]) / Digs, 0.003, "Uncommon tier");
        }

        [TestMethod]
        public void NameKey_AndSpriteName_MatchEachGem()
        {
            for (int i = 0; i < GemDigConfig.GemCount; i++)
            {
                var gem = (GemType)i;
                Assert.AreEqual("Gem" + gem, GemDigConfig.GetNameKey(gem));
                Assert.AreEqual("Gem" + gem, GemDigConfig.GetSpriteName(gem));
            }
        }

        [TestMethod]
        public void FormatLabel_ShowsNameAndGold()
        {
            Assert.AreEqual("Agate (+1)", GemDigConfig.FormatLabel("{0} (+{1})", "Agate", GemType.Agate));
            Assert.AreEqual("Diamond (+10000)", GemDigConfig.FormatLabel("{0} (+{1})", "Diamond", GemType.Diamond));
        }

        [TestMethod]
        public void UiText_HasTheLabelFormatAndEveryGemName()
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
            {
                var gem = (GemType)i;
                Assert.IsTrue(values.TryGetValue(GemDigConfig.GetNameKey(gem), out var name), "missing " + gem);
                Assert.AreEqual(gem.ToString(), name);
            }
        }

        private static double TierProbability(ItemRarity rarity)
        {
            double p = 0;
            for (int i = 0; i < GemDigConfig.GemCount; i++)
                if (GemDigConfig.GetRarity((GemType)i) == rarity)
                    p += GemDigConfig.GetProbability((GemType)i);
            return p;
        }
    }
}
