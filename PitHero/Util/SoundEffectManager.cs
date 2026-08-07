using Microsoft.Xna.Framework;
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

                soundEffectDict.Add(SoundEffectType.TakeDamage,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/TakeDamage.wav")));

                soundEffectDict.Add(SoundEffectType.PayGold,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/PayGold.wav")));

                soundEffectDict.Add(SoundEffectType.Restorative,
                    new GroupSoundEffect(new SoundEffect[]
                    {
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/Restore1.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/Restore2.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/Restore3.wav")
                    }));

                soundEffectDict.Add(SoundEffectType.ItemPurchase,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/ItemPurchase.wav")));

                soundEffectDict.Add(SoundEffectType.ItemSell,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/ItemSell.wav")));

                soundEffectDict.Add(SoundEffectType.PickCrop,
                    new GroupSoundEffect(new SoundEffect[]
                    {
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/PickCrop1.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/PickCrop2.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/PickCrop3.wav")
                    }));

                soundEffectDict.Add(SoundEffectType.StoreCrop,
                    new GroupSoundEffect(new SoundEffect[]
                    {
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/StoreCrop1.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/StoreCrop2.wav")
                    }));

                soundEffectDict.Add(SoundEffectType.RetrieveCrop,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/RetrieveCrop.wav")));

                soundEffectDict.Add(SoundEffectType.TopBarButtonClick,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/TopBarButtonClick.wav")));

                soundEffectDict.Add(SoundEffectType.TabButtonClick,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/TabButtonClick.wav")));

                soundEffectDict.Add(SoundEffectType.CancelButtonClick,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/CancelButtonClick.wav")));

                soundEffectDict.Add(SoundEffectType.NormalButtonClick,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/NormalButtonClick.wav")));

                soundEffectDict.Add(SoundEffectType.FoodReady,
                    new GroupSoundEffect(new SoundEffect[]
                    {
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/FoodReady1.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/FoodReady2.wav")
                    }));

                soundEffectDict.Add(SoundEffectType.TicketPosted,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/TicketPosted.wav")));

                soundEffectDict.Add(SoundEffectType.TakeOrder,
                    new GroupSoundEffect(new SoundEffect[]
                    {
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/TakeOrder1.wav"),
                        Content.LoadSoundEffect("Content/Audio/SoundEffects/TakeOrder2.wav")
                    }));

                soundEffectDict.Add(SoundEffectType.PartyFinishedEating,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/PartyFinishedEating.wav")));

                soundEffectDict.Add(SoundEffectType.Digging,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/Digging.wav")));

                soundEffectDict.Add(SoundEffectType.Watering,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/Watering.wav")));

                soundEffectDict.Add(SoundEffectType.PlaceFoodOnTable,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/PlaceFoodOnTable.wav")));

                soundEffectDict.Add(SoundEffectType.DropEmptyDish,
                    new NormalSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/DropEmptyDish.wav")));

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

        /// <summary>Plays a world-positioned sound attenuated and panned by horizontal distance from the camera view;
        /// skipped entirely beyond GameConfig.MaxAudibleDistanceTiles past the nearest edge. Position is sampled at play time.</summary>
        public void PlaySoundAt(SoundEffectType soundEffectType, Vector2 sourceWorldPosition)
        {
            float scale = 1f;
            float pan = 0f;
            var camera = Core.Scene?.Camera;
            if (camera != null)
            {
                var bounds = camera.Bounds;
                float maxAudiblePx = GameConfig.MaxAudibleDistanceTiles * GameConfig.TileSize;
                scale = PositionalAudio.CalculateVolumeScale(sourceWorldPosition.X, bounds.Left, bounds.Right, maxAudiblePx);
                pan = PositionalAudio.CalculatePan(sourceWorldPosition.X, bounds.Left, bounds.Right, maxAudiblePx);
            }

            if (scale <= 0f)
                return;

            soundEffectDict[soundEffectType].Play(SoundVolume * scale, 0f, pan);
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
