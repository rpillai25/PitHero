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
                //Play random sound in group once
                int rand = Nez.Random.Range(0, soundEffectGroup.Length * 500) / 500;
                soundEffectGroup[rand].Play(volume);
            }
        }

        public void Play(float volume, float pitch, float pan)
        {
            if (!disposed)
            {
                //Play random sound in group once
                int rand = Nez.Random.Range(0, soundEffectGroup.Length * 500) / 500;
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
