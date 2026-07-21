using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// A "&lt; n &gt;" page selector: previous/next arrows flanking a 1-based page number, wrapping from
    /// the last page back to the first and vice versa. Populates itself only when there is more than
    /// one page, so it occupies no layout space when there is nothing to page through.
    /// Page state lives in <see cref="PageCursor"/>.
    /// </summary>
    public class PagerRow : Table
    {
        private const float ArrowWidth  = 30f;
        private const float ArrowHeight = 24f;
        private const float LabelWidth  = 40f;
        // The skin's nine-patch button padding makes the "<" render wider than its cell, pushing the
        // optical centre left; nudge the number back to sit between the two arrows.
        private const float LabelPadLeft = 8f;

        private readonly TextButton _prevButton;
        private readonly TextButton _nextButton;
        private readonly Label _pageLabel;
        private readonly PageCursor _cursor = new PageCursor();

        private bool _populated;

        /// <summary>Fired after the shown page changes; the owner should rebuild its contents.</summary>
        public event System.Action OnPageChanged;

        /// <summary>Zero-based index of the page currently shown. Valid once Configure has been called.</summary>
        public int PageIndex => _cursor.PageIndex;

        public PagerRow(Skin skin)
        {
            _prevButton = new TextButton("<", skin, "ph-default");
            _prevButton.OnClicked += (_) => PreviousPage();
            _nextButton = new TextButton(">", skin, "ph-default");
            _nextButton.OnClicked += (_) => NextPage();
            _pageLabel = new Label("1", skin, "ph-default");
            // Qualified: the inherited Table.Align(int) method otherwise shadows the Align class here.
            _pageLabel.SetAlignment(Nez.UI.Align.Center);
        }

        /// <summary>
        /// Points the pager at <paramref name="pageCount"/> pages, clamping the current page into range.
        /// Pass 0 to disable paging entirely. Only rebuilds the cells when visibility actually flips, so
        /// stepping pages never re-adds the button that is mid-click.
        /// </summary>
        public void Configure(int pageCount)
        {
            _cursor.SetPageCount(pageCount);

            bool shouldShow = _cursor.HasMultiplePages;
            if (shouldShow != _populated)
            {
                Clear();
                if (shouldShow)
                {
                    Add(_prevButton).Size(ArrowWidth, ArrowHeight);
                    Add(_pageLabel).Width(LabelWidth).SetPadLeft(LabelPadLeft);
                    Add(_nextButton).Size(ArrowWidth, ArrowHeight);
                }
                _populated = shouldShow;
            }

            if (shouldShow)
                _pageLabel.SetText((_cursor.PageIndex + 1).ToString());
        }

        /// <summary>Shows the next page, wrapping from the last page back to the first.</summary>
        public void NextPage()
        {
            if (_cursor.Next())
                OnPageChanged?.Invoke();
        }

        /// <summary>Shows the previous page, wrapping from the first page back to the last.</summary>
        public void PreviousPage()
        {
            if (_cursor.Previous())
                OnPageChanged?.Invoke();
        }

        /// <summary>Returns to the first page; call whenever the owning window closes.</summary>
        public void Reset()
        {
            _cursor.Reset();
        }
    }
}
