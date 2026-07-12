using System;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.Combat
{
    /// <summary>
    /// Drives a coroutine tree to completion synchronously without a Nez game host.
    /// Used by the virtual layer to run BattleEngine.Run() in deterministic headless mode.
    ///
    /// Rules:
    /// - When an enumerator yields another IEnumerator, that child is pushed onto the
    ///   stack and executed to completion before the parent resumes (depth-first).
    /// - Any other yielded value (null, WaitForSeconds, etc.) is ignored — the runner
    ///   treats them as "no-op, continue immediately".
    /// - A step guard prevents infinite loops from hanging the test suite.
    /// </summary>
    public static class HeadlessCoroutineRunner
    {
        /// <summary>
        /// Runs <paramref name="root"/> and every nested IEnumerator it yields to
        /// completion, synchronously, in a depth-first traversal.
        /// </summary>
        /// <param name="root">The top-level enumerator to drain.</param>
        /// <param name="maxSteps">
        /// Maximum number of MoveNext() calls before a <see cref="InvalidOperationException"/>
        /// is thrown. Prevents runaway loops in tests. Default: 1,000,000.
        /// </param>
        /// <exception cref="InvalidOperationException">Thrown when the step guard is exceeded.</exception>
        public static void RunToCompletion(IEnumerator root, int maxSteps = 1_000_000)
        {
            if (root == null) return;

            var stack = new Stack<IEnumerator>(16);
            stack.Push(root);
            int steps = 0;

            while (stack.Count > 0)
            {
                if (steps++ >= maxSteps)
                    throw new InvalidOperationException(
                        $"HeadlessCoroutineRunner exceeded {maxSteps} steps — possible infinite loop.");

                var current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                // If the yielded value is a nested enumerator, push it so it runs
                // to completion before the parent resumes.
                var yielded = current.Current;
                if (yielded is IEnumerator child)
                    stack.Push(child);
                // All other yield values (null, Coroutine, WaitForSeconds, etc.) are ignored.
            }
        }
    }
}
