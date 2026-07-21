namespace PitHero.UI
{
    /// <summary>
    /// The page state behind <see cref="PagerRow"/>, kept separate from the widgets so the arithmetic
    /// (wraparound, clamping when pages disappear) is exercisable without a graphics device.
    /// </summary>
    public class PageCursor
    {
        private int _pageCount;
        private int _pageIndex;

        /// <summary>Zero-based index of the page currently shown.</summary>
        public int PageIndex => _pageIndex;

        /// <summary>Total number of pages, as last set by <see cref="SetPageCount"/>.</summary>
        public int PageCount => _pageCount;

        /// <summary>Whether there is anything to page through; false hides the pager entirely.</summary>
        public bool HasMultiplePages => _pageCount > 1;

        /// <summary>
        /// Sets the number of pages, clamping the current page into range — pages disappear when a
        /// building is sold while the window is closed. Pass 0 to disable paging.
        /// </summary>
        public void SetPageCount(int pageCount)
        {
            _pageCount = pageCount > 0 ? pageCount : 0;
            if (_pageIndex >= _pageCount)
                _pageIndex = _pageCount > 0 ? _pageCount - 1 : 0;
        }

        /// <summary>Advances one page, wrapping past the last back to the first. True if the page moved.</summary>
        public bool Next() => Step(1);

        /// <summary>Goes back one page, wrapping past the first back to the last. True if the page moved.</summary>
        public bool Previous() => Step(-1);

        /// <summary>Returns to the first page.</summary>
        public void Reset()
        {
            _pageIndex = 0;
        }

        private bool Step(int delta)
        {
            if (_pageCount <= 1)
                return false;
            _pageIndex = (_pageIndex + delta + _pageCount) % _pageCount;
            return true;
        }
    }
}
