using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the shuffle-bag dialogue selection core (issue #385):
    /// SpeechBubbleDialogue.SelectKey draws from a shared per-event bag with
    /// bounded draw-and-skip over gate-ineligible options.
    /// </summary>
    [TestClass]
    public class SpeechBubbleShuffleBagTests
    {
        private static SpeechBubbleDialogue.OptionBag MakeBag(params SpeechBubbleDialogue.Option[] options)
        {
            return new SpeechBubbleDialogue.OptionBag(options);
        }

        [TestMethod]
        public void SelectKey_UngatedOptions_EachKeyOncePerCycle()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("B"),
                new SpeechBubbleDialogue.Option("C"));
            var rng = new System.Random(42);

            for (int cycle = 0; cycle < 10; cycle++)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < 3; i++)
                {
                    var key = SpeechBubbleDialogue.SelectKey(bag, hasMerc: false, tipPaid: null, rng);
                    Assert.IsNotNull(key, "Ungated table must always yield a key");
                    Assert.IsTrue(seen.Add(key), $"Key '{key}' repeated within cycle {cycle}");
                }
                Assert.AreEqual(3, seen.Count);
            }
        }

        [TestMethod]
        public void SelectKey_UngatedOptions_FairOverManyDraws()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("B"),
                new SpeechBubbleDialogue.Option("C"));
            var rng = new System.Random(7);

            var counts = new Dictionary<string, int> { { "A", 0 }, { "B", 0 }, { "C", 0 } };
            for (int i = 0; i < 30; i++)
                counts[SpeechBubbleDialogue.SelectKey(bag, false, null, rng)]++;

            Assert.AreEqual(10, counts["A"]);
            Assert.AreEqual(10, counts["B"]);
            Assert.AreEqual(10, counts["C"]);
        }

        [TestMethod]
        public void SelectKey_SilentVariant_OncePerCycle()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("B"),
                new SpeechBubbleDialogue.Option(null));
            var rng = new System.Random(123);

            for (int cycle = 0; cycle < 10; cycle++)
            {
                int silent = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (SpeechBubbleDialogue.SelectKey(bag, false, null, rng) == null)
                        silent++;
                }
                Assert.AreEqual(1, silent, $"Silent variant should appear exactly once per cycle (cycle {cycle})");
            }
        }

        [TestMethod]
        public void SelectKey_MercGateOff_GatedKeyNeverReturned()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("MercOnly", SpeechBubbleDialogue.Gate.Merc),
                new SpeechBubbleDialogue.Option("B"));
            var rng = new System.Random(99);

            var seen = new HashSet<string>();
            for (int i = 0; i < 50; i++)
            {
                var key = SpeechBubbleDialogue.SelectKey(bag, hasMerc: false, tipPaid: null, rng);
                Assert.AreNotEqual("MercOnly", key);
                Assert.IsNotNull(key, "Ungated keys remain eligible");
                seen.Add(key);
            }
            Assert.IsTrue(seen.Contains("A") && seen.Contains("B"));
        }

        [TestMethod]
        public void SelectKey_MercGateOn_GatedKeyAppears()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("MercOnly", SpeechBubbleDialogue.Gate.Merc),
                new SpeechBubbleDialogue.Option("B"));
            var rng = new System.Random(5);

            int mercCount = 0;
            for (int i = 0; i < 30; i++)
            {
                if (SpeechBubbleDialogue.SelectKey(bag, hasMerc: true, tipPaid: null, rng) == "MercOnly")
                    mercCount++;
            }
            Assert.AreEqual(10, mercCount, "With all options eligible, cycles are exact: 10 of 30 draws");
        }

        [TestMethod]
        public void SelectKey_TipGates_FilterByTipPaid()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("Always"),
                new SpeechBubbleDialogue.Option("TipOnly", SpeechBubbleDialogue.Gate.Tip),
                new SpeechBubbleDialogue.Option("NoTipOnly", SpeechBubbleDialogue.Gate.NoTip));
            var rng = new System.Random(11);

            for (int i = 0; i < 30; i++)
            {
                Assert.AreNotEqual("NoTipOnly", SpeechBubbleDialogue.SelectKey(bag, false, tipPaid: true, rng));
                Assert.AreNotEqual("TipOnly", SpeechBubbleDialogue.SelectKey(bag, false, tipPaid: false, rng));
                Assert.AreEqual("Always", SpeechBubbleDialogue.SelectKey(bag, false, tipPaid: null, rng));
            }
        }

        [TestMethod]
        public void SelectKey_NothingEligible_ReturnsNullWithoutDrawing()
        {
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("MercOnly", SpeechBubbleDialogue.Gate.Merc),
                new SpeechBubbleDialogue.Option("TipOnly", SpeechBubbleDialogue.Gate.Tip));
            var rng = new System.Random(1);

            int remainingBefore = bag.Bag.Remaining;
            var key = SpeechBubbleDialogue.SelectKey(bag, hasMerc: false, tipPaid: null, rng);

            Assert.IsNull(key);
            Assert.AreEqual(remainingBefore, bag.Bag.Remaining, "Bag must not advance when nothing is eligible");
        }

        // ── Lunch and Dinner bags (issue #392) ───────────────────────────────────

        [TestMethod]
        public void SelectKey_LunchBag_TwoOptionsNoGate_EachKeyOncePerCycle()
        {
            // Mirrors the LunchOptions bag: 2 options, no gates
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("HeroLunchTime"),
                new SpeechBubbleDialogue.Option("HeroLunchOptions"));
            var rng = new System.Random(42);

            for (int cycle = 0; cycle < 10; cycle++)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < 2; i++)
                {
                    var key = SpeechBubbleDialogue.SelectKey(bag, hasMerc: false, tipPaid: null, rng);
                    Assert.IsNotNull(key, "Ungated lunch bag must always yield a key");
                    Assert.IsTrue(seen.Add(key), $"Key '{key}' repeated within cycle {cycle}");
                }
                Assert.AreEqual(2, seen.Count);
            }
        }

        [TestMethod]
        public void SelectKey_DinnerBag_TwoOptionsNoGate_EachKeyOncePerCycle()
        {
            // Mirrors the DinnerOptions bag: 2 options, no gates
            var bag = MakeBag(
                new SpeechBubbleDialogue.Option("HeroDinnerTime"),
                new SpeechBubbleDialogue.Option("HeroDinnerServing"));
            var rng = new System.Random(77);

            for (int cycle = 0; cycle < 10; cycle++)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < 2; i++)
                {
                    var key = SpeechBubbleDialogue.SelectKey(bag, hasMerc: false, tipPaid: null, rng);
                    Assert.IsNotNull(key, "Ungated dinner bag must always yield a key");
                    Assert.IsTrue(seen.Add(key), $"Key '{key}' repeated within cycle {cycle}");
                }
                Assert.AreEqual(2, seen.Count);
            }
        }

        [TestMethod]
        public void SelectKey_LunchBag_SameSeed_SameSequence()
        {
            var bag1 = MakeBag(
                new SpeechBubbleDialogue.Option("HeroLunchTime"),
                new SpeechBubbleDialogue.Option("HeroLunchOptions"));
            var bag2 = MakeBag(
                new SpeechBubbleDialogue.Option("HeroLunchTime"),
                new SpeechBubbleDialogue.Option("HeroLunchOptions"));

            var rng1 = new System.Random(2026);
            var rng2 = new System.Random(2026);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(
                    SpeechBubbleDialogue.SelectKey(bag1, false, null, rng1),
                    SpeechBubbleDialogue.SelectKey(bag2, false, null, rng2),
                    $"Lunch bag sequences diverged at draw {i}");
            }
        }

        [TestMethod]
        public void SelectKey_SameSeed_SameSequence()
        {
            var bag1 = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("B"),
                new SpeechBubbleDialogue.Option("C"),
                new SpeechBubbleDialogue.Option("D"));
            var bag2 = MakeBag(
                new SpeechBubbleDialogue.Option("A"),
                new SpeechBubbleDialogue.Option("B"),
                new SpeechBubbleDialogue.Option("C"),
                new SpeechBubbleDialogue.Option("D"));

            var rng1 = new System.Random(2026);
            var rng2 = new System.Random(2026);

            for (int i = 0; i < 40; i++)
            {
                Assert.AreEqual(
                    SpeechBubbleDialogue.SelectKey(bag1, false, null, rng1),
                    SpeechBubbleDialogue.SelectKey(bag2, false, null, rng2),
                    $"Sequences diverged at draw {i}");
            }
        }
    }
}
