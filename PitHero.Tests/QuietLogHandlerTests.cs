using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;

namespace PitHero.Tests
{
    /// <summary>
    /// Debug.Log($"...") must bind to the interpolated-string handler overload so that, under
    /// QuietMode (replay seeks), the holes are never evaluated and no string is built.
    /// </summary>
    [TestClass]
    public class QuietLogHandlerTests
    {
        private int _holeEvaluations;

        private string Hole()
        {
            _holeEvaluations++;
            return "x";
        }

        [TestMethod]
        public void InterpolatedLog_SkipsHoleEvaluation_WhenQuiet()
        {
            bool previous = Debug.QuietMode;
            try
            {
                _holeEvaluations = 0;
                Debug.QuietMode = true;
                Debug.Log($"quiet {Hole()} {42:0.0}");
                Assert.AreEqual(0, _holeEvaluations, "Under QuietMode the interpolation must not run at all");

                Debug.QuietMode = false;
                Debug.Log($"loud {Hole()} {42:0.0}");
                Assert.AreEqual(1, _holeEvaluations, "With QuietMode off the message is built and logged as before");
            }
            finally
            {
                Debug.QuietMode = previous;
            }
        }
    }
}
