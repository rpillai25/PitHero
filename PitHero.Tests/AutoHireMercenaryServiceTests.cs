using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Jobs;

namespace PitHero.Tests
{
    /// <summary>Tests for the auto-hire mercenary matching and affordability logic (issue #350).</summary>
    [TestClass]
    public class AutoHireMercenaryServiceTests
    {
        [TestMethod]
        public void JobQualifies_BothSlotsNone_NeverQualifies()
        {
            Assert.IsFalse(AutoHireMercenaryService.JobQualifies(
                JobType.Knight, JobType.None, JobType.None, JobType.None, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_CandidateNone_NeverQualifies()
        {
            Assert.IsFalse(AutoHireMercenaryService.JobQualifies(
                JobType.None, JobType.Priest, JobType.Mage, JobType.None, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_MatchingSlot_Qualifies()
        {
            Assert.IsTrue(AutoHireMercenaryService.JobQualifies(
                JobType.Priest, JobType.Priest, JobType.None, JobType.None, JobType.None));
            Assert.IsTrue(AutoHireMercenaryService.JobQualifies(
                JobType.Mage, JobType.Priest, JobType.Mage, JobType.None, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_JobNotInSlots_DoesNotQualify()
        {
            Assert.IsFalse(AutoHireMercenaryService.JobQualifies(
                JobType.Thief, JobType.Priest, JobType.Mage, JobType.None, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_SlotSatisfiedByHiredMerc_DoesNotQualify()
        {
            Assert.IsFalse(AutoHireMercenaryService.JobQualifies(
                JobType.Priest, JobType.Priest, JobType.None, JobType.Priest, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_DuplicateSlotsWithOneHired_SecondStillQualifies()
        {
            Assert.IsTrue(AutoHireMercenaryService.JobQualifies(
                JobType.Knight, JobType.Knight, JobType.Knight, JobType.Knight, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_DuplicateSlotsWithBothHired_DoesNotQualify()
        {
            Assert.IsFalse(AutoHireMercenaryService.JobQualifies(
                JobType.Knight, JobType.Knight, JobType.Knight, JobType.Knight, JobType.Knight));
        }

        [TestMethod]
        public void JobQualifies_NonMatchingHiredMerc_ConsumesNoSlot()
        {
            // A manually hired Mage occupies a party slot but not a desired Knight entry
            Assert.IsTrue(AutoHireMercenaryService.JobQualifies(
                JobType.Knight, JobType.Knight, JobType.None, JobType.Mage, JobType.None));
        }

        [TestMethod]
        public void JobQualifies_OneHiredSatisfiesOnlyOneDuplicateSlot()
        {
            // Slots {Priest, Priest}, one Priest hired: a second Priest still qualifies,
            // but a third does not once both are hired.
            Assert.IsTrue(AutoHireMercenaryService.JobQualifies(
                JobType.Priest, JobType.Priest, JobType.Priest, JobType.Priest, JobType.None));
            Assert.IsFalse(AutoHireMercenaryService.JobQualifies(
                JobType.Priest, JobType.Priest, JobType.Priest, JobType.Priest, JobType.Priest));
        }

        [TestMethod]
        public void CanAffordHire_ExactlyAtBuffer_Affordable()
        {
            Assert.IsTrue(AutoHireMercenaryService.CanAffordHire(700, 500, 200));
        }

        [TestMethod]
        public void CanAffordHire_OneGoldBelowBuffer_NotAffordable()
        {
            Assert.IsFalse(AutoHireMercenaryService.CanAffordHire(699, 500, 200));
        }

        [TestMethod]
        public void SanitizeJob_ValidSingleJobs_RoundTrip()
        {
            Assert.AreEqual(JobType.Knight, AutoHireMercenaryService.SanitizeJob(JobType.Knight));
            Assert.AreEqual(JobType.Monk, AutoHireMercenaryService.SanitizeJob(JobType.Monk));
            Assert.AreEqual(JobType.Mage, AutoHireMercenaryService.SanitizeJob(JobType.Mage));
            Assert.AreEqual(JobType.Priest, AutoHireMercenaryService.SanitizeJob(JobType.Priest));
            Assert.AreEqual(JobType.Thief, AutoHireMercenaryService.SanitizeJob(JobType.Thief));
            Assert.AreEqual(JobType.Archer, AutoHireMercenaryService.SanitizeJob(JobType.Archer));
        }

        [TestMethod]
        public void SanitizeJob_UnknownValues_BecomeNone()
        {
            Assert.AreEqual(JobType.None, AutoHireMercenaryService.SanitizeJob(JobType.None));
            Assert.AreEqual(JobType.None, AutoHireMercenaryService.SanitizeJob(JobType.All));
            Assert.AreEqual(JobType.None, AutoHireMercenaryService.SanitizeJob(JobType.Knight | JobType.Mage));
            Assert.AreEqual(JobType.None, AutoHireMercenaryService.SanitizeJob((JobType)999));
        }

        [TestMethod]
        public void TryHirePass_NullDependencies_ReturnsZeroWithoutThrowing()
        {
            var service = new AutoHireMercenaryService(null, null, null) { Enabled = true };
            Assert.AreEqual(0, service.TryHirePass());
        }

        [TestMethod]
        public void TryAutoHire_NullEntity_ReturnsFalseWithoutThrowing()
        {
            var service = new AutoHireMercenaryService(null, null, null) { Enabled = true };
            Assert.IsFalse(service.TryAutoHire(null));
        }

        [TestMethod]
        public void GoldBuffer_NullSource_IsZero()
        {
            var service = new AutoHireMercenaryService(null, null, null);
            Assert.AreEqual(0, service.GoldBuffer);
        }
    }
}
