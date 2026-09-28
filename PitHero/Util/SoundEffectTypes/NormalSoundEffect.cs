using Microsoft.Xna.Framework.Audio;
using PitHero.Util.Extensions;
using System;

namespace PitHero.Util.SoundEffectTypes
{
    /// <summary>
    /// Sound effect that allows multiple instances to play simultaneously.
    /// </summary>
    public class NormalSoundEffect : IGameSoundEffect
    {
        private SoundEffect soundEffect;
        private bool disposed = false;

        public NormalSoundEffect(SoundEffect soundEffect)
        {
            this.soundEffect = soundEffect;
        }

        public int Play(float volume, uint frameInterval = 0)
        {
            if (disposed)
                return -1;
            soundEffect.Play(volume);
            return 0;
        }

        public int Play(float volume, float pitch, float pan)
        {
            if (disposed)
                return -1;
            soundEffect.Play(volume, pitch, pan);
            return 0;
        }

        public int PickVariant() => 0;

        public void PlayVariant(int variant, float volume, float pitch, float pan)
        {
            if (!disposed)
                soundEffect.Play(volume, pitch, pan);
        }

        public void Stop()
        {
            //Not applicable
        }

        public bool IsSoundPlaying()
        {
            return false;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                soundEffect?.Dispose();
                soundEffect = null;
                disposed = true;
            }
        }
    }
}
