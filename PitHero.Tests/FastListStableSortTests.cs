using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Nez sorts an entity's updatable components on every add. That sort must be stable: equal
    /// update orders keep insertion order regardless of what else the list held at sort time, or
    /// the hero's component update order (and the simulation) would depend on cosmetic history.
    /// </summary>
    [TestClass]
    public class FastListStableSortTests
    {
        private sealed class Item
        {
            public int Order;
            public int Seq;
        }

        private sealed class ByOrder : IComparer<Item>
        {
            public int Compare(Item a, Item b) => a.Order.CompareTo(b.Order);
        }

        [TestMethod]
        public void StableSort_KeepsInsertionOrderForTies_RegardlessOfListSize()
        {
            // Large enough that an introsort would stop using insertion sort and start reordering ties
            const int n = 40;
            var comparer = new ByOrder();

            var list = new FastList<Item>();
            for (int i = 0; i < n; i++)
                list.Add(new Item { Order = i % 3, Seq = i });
            list.StableSort(comparer);

            // Every run of equal orders must still be in ascending Seq
            for (int i = 1; i < list.Length; i++)
            {
                Assert.IsTrue(list.Buffer[i - 1].Order <= list.Buffer[i].Order, "sorted by order");
                if (list.Buffer[i - 1].Order == list.Buffer[i].Order)
                    Assert.IsTrue(list.Buffer[i - 1].Seq < list.Buffer[i].Seq, "ties keep insertion order");
            }

            // Adding one more equal element and re-sorting must not disturb the others
            var before = new List<Item>(list.Length);
            for (int i = 0; i < list.Length; i++) before.Add(list.Buffer[i]);
            list.Add(new Item { Order = 1, Seq = n });
            list.StableSort(comparer);
            int k = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list.Buffer[i].Seq == n) continue;
                Assert.AreSame(before[k++], list.Buffer[i], "existing relative order unchanged after an add");
            }
        }
    }
}
