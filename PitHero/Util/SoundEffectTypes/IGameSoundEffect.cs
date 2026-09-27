using System;

namespace PitHero.Util.SoundEffectTypes
{
    /// <summary>
    /// One playable sound effect. Every play reports the <b>variant</b> it resolved to (the index a
    /// group sound rolled on the audio stream; 0 for single sounds; -1 when nothing played) so the
    /// replay frame stream can record the exact sound and <see cref="PlayVariant"/> can play it again.
    /// </summary>
    public interface IGameSoundEffect : IDisposable
    {
        /// <summary>Plays at the volume; returns the variant played, or -1 when throttled.</summary>
        int Play(float volume, uint frameInterval = 0);
        /// <summary>Plays with pitch and pan; returns the variant played, or -1 when throttled.</summary>
        int Play(float volume, float pitch, float pan);
        /// <summary>Rolls the variant a play would use now (the audio stream for groups) without playing.</summary>
        int PickVariant();
        /// <summary>Plays a specific variant (a recorded one); out-of-range variants wrap.</summary>
        void PlayVariant(int variant, float volume, float pitch, float pan);
        void Stop();
        bool IsSoundPlaying();
    }
}
