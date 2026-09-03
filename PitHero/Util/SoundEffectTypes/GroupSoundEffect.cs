using Microsoft.Xna.Framework.Audio;
using Nez;
using PitHero.Util.Extensions;
using System;

namespace PitHero.Util.SoundEffectTypes
{
    /// <summary>
    /// Sound effect that plays a random sound from a group.
    /// </summary>
    public class GroupSoundEffect : IGameSoundEffect
    {
        SoundEffect[] soundEffectGroup;
        private bool disposed = false;

        public GroupSoundEffect(SoundEffect[] soundEffects)
        {
            soundEffectGroup = soundEffects;
        }

        public void Play(float volume, uint frameInterval = 0)
        {
            if (!disposed)
            {
                //Play random sound in group once. Audio stream, never Nez.Random: sounds fire from UI
                //clicks at wall-clock times and must not perturb the seeded simulation stream.
                int rand = PitHero.Services.GameRandom.AudioRange(0, soundEffectGroup.Length);
                soundEffectGroup[rand].Play(volume);
            }
        }

        public void Play(float volume, float pitch, float pan)
        {
            if (!disposed)
            {
                //Play random sound in group once. Audio stream, never Nez.Random: sounds fire from UI
                //clicks at wall-clock times and must not perturb the seeded simulation stream.
                int rand = PitHero.Services.GameRandom.AudioRange(0, soundEffectGroup.Length);
                soundEffectGroup[rand].Play(volume, pitch, pan);
            }
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
                if (soundEffectGroup != null)
                {
                    for (int i = 0; i < soundEffectGroup.Length; i++)
                    {
                        soundEffectGroup[i]?.Dispose();
                    }
                    soundEffectGroup = null;
                }
                disposed = true;
            }
        }
    }
}
