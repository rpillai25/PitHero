using System.Collections.Generic;
using RolePlayingFramework.Heroes;

namespace PitHero.Services
{
    /// <summary>
    /// Service that stores hero crystals of fallen heroes.
    /// Eventually a Crystal Merchant will sell these crystals back to the player.
    /// </summary>
    public class CrystalMerchantVault
    {
        private readonly List<HeroCrystal> _crystals = new List<HeroCrystal>();
        
        /// <summary>Gets a read-only collection of all crystals in the vault.</summary>
        public IReadOnlyList<HeroCrystal> Crystals => _crystals.AsReadOnly();
        
        /// <summary>Adds a hero crystal to the vault.</summary>
        /// <param name="crystal">The hero crystal to add.</param>
        public void AddCrystal(HeroCrystal crystal)
        {
            if (crystal != null)
            {
                _crystals.Add(crystal);
            }
        }
        
        /// <summary>Removes a crystal from the vault (e.g., when sold).</summary>
        /// <param name="crystal">The crystal to remove.</param>
        /// <returns>True if the crystal was found and removed.</returns>
        public bool RemoveCrystal(HeroCrystal crystal)
        {
            return _crystals.Remove(crystal);
        }
        
        /// <summary>Gets the total number of crystals in the vault.</summary>
        public int Count => _crystals.Count;
        
        /// <summary>Clears all crystals from the vault.</summary>
        public void Clear()
        {
            _crystals.Clear();
        }
    }
}
