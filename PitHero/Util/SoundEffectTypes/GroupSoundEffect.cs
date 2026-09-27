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

        public int Play(float volume, uint frameInterval = 0)
        {
            if (disposed)
                return -1;
            int rand = PickVariant();
            soundEffectGroup[rand].Play(volume);
            return rand;
        }

        public int Play(float volume, float pitch, float pan)
        {
            if (disposed)
                return -1;
            int rand = PickVariant();
            soundEffectGroup[rand].Play(volume, pitch, pan);
            return rand;
        }

        /// <summary>
        /// A random member of the group. Audio stream, never Nez.Random: sounds fire from UI clicks
        /// at wall-clock times and must not perturb the seeded simulation stream.
        /// </summary>
        public int PickVariant()
        {
            if (disposed || soundEffectGroup == null || soundEffectGroup.Length == 0)
                return 0;
            return PitHero.Services.GameRandom.AudioRange(0, soundEffectGroup.Length);
        }

        public void PlayVariant(int variant, float volume, float pitch, float pan)
        {
            if (disposed || soundEffectGroup == null || soundEffectGroup.Length == 0)
                return;
            int index = variant % soundEffectGroup.Length;
            if (index < 0)
                index += soundEffectGroup.Length;
            soundEffectGroup[index].Play(volume, pitch, pan);
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
