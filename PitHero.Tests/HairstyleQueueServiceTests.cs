using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero.Services;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>Tests for HairstyleQueueService.</summary>
    [TestClass]
    public class HairstyleQueueServiceTests
    {
        /// <summary>Test that hairstyle queue returns values in the expected range.</summary>
        [TestMethod]
        public void GetNextHairstyle_ReturnsValidRange()
        {
            // Arrange
            const int hairstyleCount = 5;
            var service = new HairstyleQueueService(hairstyleCount);

            // Act & Assert - get many hairstyles and verify all are in range
            for (int i = 0; i < 100; i++)
            {
                int hairstyle = service.GetNextHairstyle();
                Assert.IsTrue(hairstyle >= 1 && hairstyle <= hairstyleCount,
                    $"Hairstyle {hairstyle} is not in range [1, {hairstyleCount}]");
            }
        }

        /// <summary>Test that all hairstyles appear before repetition.</summary>
        [TestMethod]
        public void GetNextHairstyle_ReturnsAllHairstylesBeforeRepetition()
        {
            // Arrange
            const int hairstyleCount = 5;
            var service = new HairstyleQueueService(hairstyleCount);
            var seenHairstyles = new HashSet<int>();

            // Act - get exactly 'hairstyleCount' hairstyles
            for (int i = 0; i < hairstyleCount; i++)
            {
                int hairstyle = service.GetNextHairstyle();
                seenHairstyles.Add(hairstyle);
            }

            // Assert - we should have seen all hairstyles exactly once
            Assert.AreEqual(hairstyleCount, seenHairstyles.Count);
            for (int i = 1; i <= hairstyleCount; i++)
            {
                Assert.IsTrue(seenHairstyles.Contains(i),
                    $"Hairstyle {i} was not found in the first {hairstyleCount} results");
            }
        }

        /// <summary>Test that queue refills after all hairstyles are used.</summary>
        [TestMethod]
        public void GetNextHairstyle_RefillsQueueAfterExhaustion()
        {
            // Arrange
            const int hairstyleCount = 5;
            var service = new HairstyleQueueService(hairstyleCount);

            // Act - exhaust the queue twice
            var firstRound = new HashSet<int>();
            var secondRound = new HashSet<int>();

            for (int i = 0; i < hairstyleCount; i++)
            {
                firstRound.Add(service.GetNextHairstyle());
            }

            for (int i = 0; i < hairstyleCount; i++)
            {
                secondRound.Add(service.GetNextHairstyle());
            }

            // Assert - both rounds should contain all hairstyles
            Assert.AreEqual(hairstyleCount, firstRound.Count);
            Assert.AreEqual(hairstyleCount, secondRound.Count);
        }

        /// <summary>Test that constructor validates hairstyle count.</summary>
        [TestMethod]
        public void Constructor_ThrowsExceptionForInvalidHairstyleCount()
        {
            // Act & Assert
            Assert.ThrowsException<System.ArgumentException>(() => new HairstyleQueueService(0));
            Assert.ThrowsException<System.ArgumentException>(() => new HairstyleQueueService(-1));
        }
    }
}
