using Microsoft.Xna.Framework.Audio;

namespace PitHero.Util.Extensions
{
    public static class SoundEffectExtensions
    {
        public static void Play(this SoundEffect soundEffect, float volume = 1.0f)
        {
            soundEffect.Play(volume, 0f, 0f);
        }

        public static void Play(this SoundEffectInstance soundEffectInstance, float volume = 1.0f)
        {
            soundEffectInstance.Volume = volume;
            soundEffectInstance.Play();
        }
    }
}
