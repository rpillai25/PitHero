using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>
    /// A dialog that blocks interaction while it waits for an answer and can be dismissed by Escape.
    /// Implemented by every buy/sell/confirm prompt so they share one registry.
    /// </summary>
    public interface IUIPrompt
    {
        /// <summary>True while this prompt is on a stage and visible.</summary>
        bool IsPromptVisible { get; }

        /// <summary>Dismisses the prompt as if its Cancel/No button were clicked.</summary>
        void CancelPrompt();
    }

    /// <summary>
    /// Tracks every visible blocking prompt in one place.
    ///
    /// None of these dialogs are modal — Nez has no modal support in this fork — so the rest of the UI
    /// has to ask whether a prompt is up before acting: drag-and-drop suppression, hover-card
    /// suppression, outside-click dismissal, and Escape handling all consult this. Registering here is
    /// what makes a new prompt visible to ALL of them; a prompt with its own private flag silently
    /// misses every one.
    /// </summary>
    public static class UIPromptRegistry
    {
        private static readonly List<IUIPrompt> _shown = new List<IUIPrompt>();

        /// <summary>Registers a prompt as shown. Safe to call repeatedly for the same instance.</summary>
        public static void Register(IUIPrompt prompt)
        {
            if (prompt != null && !_shown.Contains(prompt))
                _shown.Add(prompt);
        }

        /// <summary>True if any registered prompt is currently on a stage and visible.</summary>
        public static bool AnyVisible
        {
            get
            {
                Prune();
                return _shown.Count > 0;
            }
        }

        /// <summary>
        /// Cancels the most recently shown visible prompt as if its Cancel/No button were clicked.
        /// Returns true if one was cancelled (used by Escape handling).
        /// </summary>
        public static bool TryCancelTopMost()
        {
            Prune();
            if (_shown.Count == 0)
                return false;
            var prompt = _shown[_shown.Count - 1];
            _shown.RemoveAt(_shown.Count - 1);
            prompt.CancelPrompt();
            return true;
        }

        /// <summary>
        /// Drops every registered prompt. Called on scene change: a prompt that was open when the
        /// scene swapped keeps its dead-stage parent and its visible flag, so it would otherwise
        /// report visible forever and permanently suppress drags and hover cards.
        /// </summary>
        public static void Clear() => _shown.Clear();

        /// <summary>Drops prompts that have been removed from their stage or hidden.</summary>
        private static void Prune()
        {
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                if (!_shown[i].IsPromptVisible)
                    _shown.RemoveAt(i);
            }
        }
    }
}
