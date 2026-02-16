using System;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>
    /// Manages a global queue of hairstyle indices to ensure better distribution
    /// of hairstyles across heroes and mercenaries.
    /// </summary>
    public class HairstyleQueueService
    {
        private readonly Queue<int> _hairstyleQueue;
        private readonly int _hairstyleCount;

        /// <summary>Creates a new hairstyle queue service.</summary>
        /// <param name="hairstyleCount">Total number of available hairstyles.</param>
        public HairstyleQueueService(int hairstyleCount)
        {
            if (hairstyleCount <= 0)
            {
                throw new ArgumentException("Hairstyle count must be positive", nameof(hairstyleCount));
            }

            _hairstyleCount = hairstyleCount;
            _hairstyleQueue = new Queue<int>(hairstyleCount);
            
            // Pre-fill queue on initialization to ensure first hairstyle is already randomized
            RefillQueue();
        }

        /// <summary>Gets the next hairstyle index from the queue.</summary>
        /// <returns>A hairstyle index from 1 to hairstyleCount.</returns>
        public int GetNextHairstyle()
        {
            if (_hairstyleQueue.Count == 0)
            {
                RefillQueue();
            }

            return _hairstyleQueue.Dequeue();
        }

        /// <summary>Refills the queue with all hairstyle indices in shuffled order.</summary>
        private void RefillQueue()
        {
            // Create list of all hairstyle indices (1-based indexing)
            var indices = new List<int>(_hairstyleCount);
            for (int i = 1; i <= _hairstyleCount; i++)
            {
                indices.Add(i);
            }

            // Shuffle the indices using Nez.Random
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Nez.Random.Range(0, i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            // Fill the queue with shuffled indices
            foreach (int index in indices)
            {
                _hairstyleQueue.Enqueue(index);
            }
        }
    }
}
