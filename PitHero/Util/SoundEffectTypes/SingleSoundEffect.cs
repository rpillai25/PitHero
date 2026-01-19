using Microsoft.Xna.Framework.Audio;
using Nez;
using PitHero.Util.Extensions;
using System;

namespace PitHero.Util.SoundEffectTypes
{
    /// <summary>
    /// Sound effect that only allows one instance to play at a time.
    /// </summary>
    public class SingleSoundEffect : IGameSoundEffect
    {
        private SoundEffectInstance soundEffectInstance;
        private uint lastPlayedFrames;
        private bool disposed = false;

        public SingleSoundEffect(SoundEffect soundEffect)
        {
            soundEffectInstance = soundEffect.CreateInstance();
        }

        public void Play(float volume, uint frameInterval = 0)
        {
            if (disposed)
                return;

            if (frameInterval == 0 && soundEffectInstance.State == SoundState.Playing)
            {
                //Normal behavior is to only play a sound effect if it's not already playing
                return;
            }
            if (Time.FrameCount - lastPlayedFrames > frameInterval)
            {
                //With a specified delay, we'll immediately stop and replay the sound at each interval
                soundEffectInstance.Stop();
                lastPlayedFrames = Time.FrameCount;
                soundEffectInstance.Play(volume);
            }
        }

        public void Play(float volume, float pitch, float pan)
        {
            //sound effect instance has no pitch and pan
            Play(volume);
        }

        public void Stop()
        {
            if (!disposed)
                soundEffectInstance.Stop();
        }

        public bool IsSoundPlaying()
        {
            return !disposed && soundEffectInstance.State == SoundState.Playing;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                soundEffectInstance?.Dispose();
                soundEffectInstance = null;
                disposed = true;
            }
        }
    }
}
