using Microsoft.Xna.Framework.Audio;
using Nez;
using Nez.Systems;
using PitHero.Util.SoundEffectTypes;
using System;
using System.Collections.Generic;

namespace PitHero.Util
{
    public class SoundEffectManager : GlobalManager, IDisposable
    {
        public bool Initialized = false;

        private Dictionary<SoundEffectType, IGameSoundEffect> soundEffectDict;
        private bool disposed = false;

        public float SoundVolume;
        public void Init(NezContentManager Content)
        {
            if (!Initialized)
            {
                soundEffectDict = new Dictionary<SoundEffectType, IGameSoundEffect>(new SoundEffectTypeComparer());

                soundEffectDict.Add(SoundEffectType.Jump,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/HeroMercJump.wav")));

                soundEffectDict.Add(SoundEffectType.Land,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/HeroMercLand.wav")));

                soundEffectDict.Add(SoundEffectType.ChestOpen,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/ChestOpen.wav")));

                soundEffectDict.Add(SoundEffectType.Punch,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/Punch.wav")));

                soundEffectDict.Add(SoundEffectType.EnemyDefeat,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/EnemyDefeat.wav")));

                soundEffectDict.Add(SoundEffectType.Restorative,
                    new GroupSoundEffect(new SoundEffect[]
                    {
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/Restore1.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/Restore2.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/Restore3.wav")
                    }));
               

                SoundVolume = GameConfig.MasterVolume;

                Initialized = true;
            }
        }


        public bool IsSoundPlaying(SoundEffectType soundEffectType)
        {
            return soundEffectDict[soundEffectType].IsSoundPlaying();
        }

        public void PlaySound(SoundEffectType soundEffectType, uint frameInterval = 0)
        {
            soundEffectDict[soundEffectType].Play(SoundVolume, frameInterval);
        }

        public void PlaySound(SoundEffectType soundEffectType, float volume, float pitch, float pan)
        {
            soundEffectDict[soundEffectType].Play(volume, pitch, pan);
        }

        public void StopSound(SoundEffectType soundEffectType)
        {
            if (!disposed)
                soundEffectDict[soundEffectType].Stop();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (soundEffectDict != null)
                {
                    foreach (var kvp in soundEffectDict)
                    {
                        kvp.Value?.Dispose();
                    }
                    soundEffectDict.Clear();
                    soundEffectDict = null;
                }
                disposed = true;
            }
        }
    }
}
