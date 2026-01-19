using System;

namespace PitHero.Util.SoundEffectTypes
{
    public interface IGameSoundEffect : IDisposable
    {
        void Play(float volume, uint frameInterval = 0);
        void Play(float volume, float pitch, float pan);
        void Stop();
        bool IsSoundPlaying();
    }
}
