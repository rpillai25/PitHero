using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;

namespace PitHero.Tests
{
    /// <summary>
    /// Regression tests for Nez Deque mid-collection removal/insertion. The overlapping shift
    /// copies used to run ascending, duplicating earlier items over later ones (e.g. removing
    /// index 2 from [0..6] produced [0,0,3,4,5,6] — item 1 silently lost).
    /// </summary>
    [TestClass]
    public class DequeTests
    {
        private static int[] ToArray(Deque<int> deque)
        {
            var list = new List<int>(deque.Count);
            for (int i = 0; i < deque.Count; i++)
                list.Add(deque[i]);
            return list.ToArray();
        }

        [TestMethod]
        public void RemoveAt_FirstHalf_PreservesRemainingOrder()
        {
            var deque = new Deque<int>(8);
            for (int i = 0; i < 7; i++)
                deque.AddBack(i);

            deque.RemoveAt(2);   // first-half path: shifts [0,2) up by one

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4, 5, 6 }, ToArray(deque));
        }

        [TestMethod]
        public void RemoveAt_SecondHalf_PreservesRemainingOrder()
        {
            var deque = new Deque<int>(8);
            for (int i = 0; i < 7; i++)
                deque.AddBack(i);

            deque.RemoveAt(4);   // second-half path: shifts [5,7) down by one

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 5, 6 }, ToArray(deque));
        }

        [TestMethod]
        public void RemoveAt_WrappedBuffer_PreservesRemainingOrder()
        {
            var deque = new Deque<int>(8);
            // AddFront moves the start offset backwards so the ring buffer wraps
            for (int i = 3; i >= 0; i--)
                deque.AddFront(i);
            for (int i = 4; i < 7; i++)
                deque.AddBack(i);

            deque.RemoveAt(2);

            CollectionAssert.AreEqual(new[] { 0, 1, 3, 4, 5, 6 }, ToArray(deque));
        }

        [TestMethod]
        public void Insert_Middle_PreservesOrder()
        {
            var deque = new Deque<int>(8);
            for (int i = 0; i < 6; i++)
                deque.AddBack(i);

            deque.Insert(3, 99);   // second-half path: shifts [3,6) up by one

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 99, 3, 4, 5 }, ToArray(deque));
        }

        [TestMethod]
        public void RemoveAt_RepeatedMidRemovals_NeverLoseItems()
        {
            var deque = new Deque<int>(64);
            var expected = new List<int>();
            for (int i = 0; i < 40; i++)
            {
                deque.AddBack(i);
                expected.Add(i);
            }

            // Drain from a fixed mid position, mirroring the farm queue's claim pattern
            while (deque.Count > 0)
            {
                int index = (int)(0.37f * (deque.Count - 1) + 0.5f);
                Assert.AreEqual(expected[index], deque[index]);
                deque.RemoveAt(index);
                expected.RemoveAt(index);
            }
        }
    }
}
