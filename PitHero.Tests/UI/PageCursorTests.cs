using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// Covers the page arithmetic behind the "&lt; n &gt;" pager used by the aggregate Harvested Crops
    /// and Monsters views: wraparound in both directions, and clamping when pages disappear because a
    /// building was sold while the window was closed.
    /// </summary>
    [TestClass]
    public class PageCursorTests
    {
        private static PageCursor NewCursor(int pageCount)
        {
            var cursor = new PageCursor();
            cursor.SetPageCount(pageCount);
            return cursor;
        }

        [TestMethod]
        public void SetPageCount_StartsOnFirstPage()
        {
            Assert.AreEqual(0, NewCursor(3).PageIndex, "A freshly configured cursor should show page 1");
        }

        [TestMethod]
        public void Next_AdvancesThenWrapsToFirstPage()
        {
            var cursor = NewCursor(3);

            cursor.Next();
            Assert.AreEqual(1, cursor.PageIndex);
            cursor.Next();
            Assert.AreEqual(2, cursor.PageIndex);
            cursor.Next();
            Assert.AreEqual(0, cursor.PageIndex, "Advancing past the last page should wrap to the first");
        }

        [TestMethod]
        public void Previous_FromFirstPageWrapsToLastPage()
        {
            var cursor = NewCursor(3);

            cursor.Previous();
            Assert.AreEqual(2, cursor.PageIndex, "Going back from page 1 should wrap to the last page");
        }

        [TestMethod]
        public void Step_IsANoOpWithASinglePage()
        {
            var cursor = NewCursor(1);

            Assert.IsFalse(cursor.Next(), "A single page has nowhere to step to");
            Assert.IsFalse(cursor.Previous(), "A single page has nowhere to step to");
            Assert.AreEqual(0, cursor.PageIndex);
            Assert.IsFalse(cursor.HasMultiplePages, "A single page should hide the pager");
        }

        [TestMethod]
        public void Step_IsANoOpWithNoPages()
        {
            var cursor = NewCursor(0);

            Assert.IsFalse(cursor.Next());
            Assert.AreEqual(0, cursor.PageIndex, "A cursor with no pages should stay at index 0");
            Assert.IsFalse(cursor.HasMultiplePages, "No pages should hide the pager");
        }

        [TestMethod]
        public void HasMultiplePages_IsTrueOnlyBeyondOnePage()
        {
            Assert.IsTrue(NewCursor(2).HasMultiplePages, "Two buildings should show the pager");
        }

        [TestMethod]
        public void SetPageCount_ClampsWhenPageCountShrinks()
        {
            var cursor = NewCursor(4);
            cursor.Previous();
            Assert.AreEqual(3, cursor.PageIndex);

            // Two storages sold while the window was closed.
            cursor.SetPageCount(2);
            Assert.AreEqual(1, cursor.PageIndex, "The page index should clamp to the new last page");
        }

        [TestMethod]
        public void SetPageCount_ClampsToZeroWhenEveryPageDisappears()
        {
            var cursor = NewCursor(3);
            cursor.Next();

            cursor.SetPageCount(0);
            Assert.AreEqual(0, cursor.PageIndex, "With no pages left the index should fall back to 0");
        }

        [TestMethod]
        public void SetPageCount_KeepsThePageWhenTheCountGrows()
        {
            var cursor = NewCursor(2);
            cursor.Next();
            Assert.AreEqual(1, cursor.PageIndex);

            cursor.SetPageCount(5);
            Assert.AreEqual(1, cursor.PageIndex, "Adding a storage should not move the player off their page");
        }

        [TestMethod]
        public void Reset_ReturnsToTheFirstPage()
        {
            var cursor = NewCursor(3);
            cursor.Next();
            Assert.AreEqual(1, cursor.PageIndex);

            cursor.Reset();
            Assert.AreEqual(0, cursor.PageIndex, "Reset should return the cursor to page 1");
        }
    }
}
