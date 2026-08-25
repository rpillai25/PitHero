using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;

namespace PitHero.Tests
{
    /// <summary>
    /// The prompt registry is what makes a blocking dialog visible to drag suppression, hover-card
    /// suppression, outside-click dismissal, and Escape. Its pruning and cancel ordering are relied
    /// on by all four, so they are pinned here.
    /// </summary>
    [TestClass]
    public class UIPromptRegistryTests
    {
        private sealed class FakePrompt : IUIPrompt
        {
            public bool Visible = true;
            public int CancelCount;

            public bool IsPromptVisible => Visible;
            public void CancelPrompt() { CancelCount++; Visible = false; }
        }

        [TestInitialize]
        public void Setup() => UIPromptRegistry.Clear();

        [TestCleanup]
        public void Cleanup() => UIPromptRegistry.Clear();

        [TestMethod]
        public void AnyVisible_FalseWhenNothingRegistered()
        {
            Assert.IsFalse(UIPromptRegistry.AnyVisible);
        }

        [TestMethod]
        public void AnyVisible_TracksPromptVisibility()
        {
            var prompt = new FakePrompt();
            UIPromptRegistry.Register(prompt);
            Assert.IsTrue(UIPromptRegistry.AnyVisible);

            // A prompt that hides itself (Yes/No pressed) must drop out without deregistering.
            prompt.Visible = false;
            Assert.IsFalse(UIPromptRegistry.AnyVisible,
                "A hidden prompt must not keep drag-and-drop suppressed");
        }

        [TestMethod]
        public void Register_IsIdempotentForTheSameInstance()
        {
            // Long-lived prompts (InventoryContextMenu) re-register on every show.
            var prompt = new FakePrompt();
            UIPromptRegistry.Register(prompt);
            UIPromptRegistry.Register(prompt);
            UIPromptRegistry.Register(prompt);

            Assert.IsTrue(UIPromptRegistry.TryCancelTopMost());
            Assert.IsFalse(UIPromptRegistry.TryCancelTopMost(),
                "Duplicate registrations would leave phantom prompts behind after one cancel");
            Assert.AreEqual(1, prompt.CancelCount);
        }

        [TestMethod]
        public void TryCancelTopMost_CancelsMostRecentFirst()
        {
            var first = new FakePrompt();
            var second = new FakePrompt();
            UIPromptRegistry.Register(first);
            UIPromptRegistry.Register(second);

            Assert.IsTrue(UIPromptRegistry.TryCancelTopMost());
            Assert.AreEqual(1, second.CancelCount, "Escape must dismiss the topmost prompt");
            Assert.AreEqual(0, first.CancelCount);

            Assert.IsTrue(UIPromptRegistry.TryCancelTopMost());
            Assert.AreEqual(1, first.CancelCount);
            Assert.IsFalse(UIPromptRegistry.TryCancelTopMost());
        }

        [TestMethod]
        public void TryCancelTopMost_SkipsAlreadyHiddenPrompts()
        {
            var stale = new FakePrompt { Visible = false };
            var live = new FakePrompt();
            UIPromptRegistry.Register(stale);
            UIPromptRegistry.Register(live);

            Assert.IsTrue(UIPromptRegistry.TryCancelTopMost());
            Assert.AreEqual(1, live.CancelCount);
            Assert.AreEqual(0, stale.CancelCount, "A hidden prompt must never be cancelled again");
            Assert.IsFalse(UIPromptRegistry.TryCancelTopMost());
        }

        [TestMethod]
        public void Clear_DropsPromptsStrandedByASceneSwap()
        {
            // A prompt open when the scene swaps keeps its dead-stage parent and stays "visible",
            // so without the scene-change Clear it would suppress drags for the rest of the session.
            var stranded = new FakePrompt();
            UIPromptRegistry.Register(stranded);
            Assert.IsTrue(UIPromptRegistry.AnyVisible);

            UIPromptRegistry.Clear();

            Assert.IsFalse(UIPromptRegistry.AnyVisible);
            Assert.IsFalse(UIPromptRegistry.TryCancelTopMost());
            Assert.AreEqual(0, stranded.CancelCount, "Clear discards prompts, it does not cancel them");
        }

        [TestMethod]
        public void Register_IgnoresNull()
        {
            UIPromptRegistry.Register(null);
            Assert.IsFalse(UIPromptRegistry.AnyVisible);
        }
    }
}
