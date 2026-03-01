using System;
using RolePlayingFramework.Heroes;

namespace PitHero.Services
{
    /// <summary>Service that stores the current HeroDesign after creation</summary>
    public class HeroDesignService
    {
        /// <summary>The current hero design (null until set)</summary>
        public HeroDesign? CurrentDesign { get; private set; }

        /// <summary>Whether a design has been set</summary>
        public bool HasDesign => CurrentDesign.HasValue;

        /// <summary>Sets the current hero design</summary>
        public void SetDesign(HeroDesign design)
        {
            CurrentDesign = design;
        }

        /// <summary>Returns the current hero design (throws if not set)</summary>
        public HeroDesign GetDesign()
        {
            if (!CurrentDesign.HasValue)
                throw new InvalidOperationException("No hero design has been set.");

            return CurrentDesign.Value;
        }
    }
}
