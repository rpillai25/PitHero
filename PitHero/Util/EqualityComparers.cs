using PitHero.Util.SoundEffectTypes;
using System.Collections.Generic;

namespace PitHero.Util
{
    /// <summary>
    /// IEqualityComparers that should be passed to a dictionary constructor to avoid boxing/unboxing when using an enum as a key
    /// on Mono
    /// </summary>
    public struct SoundEffectTypeComparer : IEqualityComparer<SoundEffectType>
    {
        public bool Equals(SoundEffectType x, SoundEffectType y)
        {
            return x == y;
        }


        public int GetHashCode(SoundEffectType obj)
        {
            return (int)obj;
        }
    }
}
