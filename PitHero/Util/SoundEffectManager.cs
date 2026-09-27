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
                    new SingleSoundEffect(Content.LoadSoundEffect("Content/Audio/SoundEffects/Digging.wav")));

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

        /// <summary>
        /// When true every play request is dropped. Set while a replay seeks: hundreds of simulated
        /// seconds pass per real second and their sounds would all fire at once.
        /// </summary>
        public static bool Muted;

        /// <summary>
        /// Raised for every sound the game plays (after the variant roll, before any positional culling),
        /// with the resolved variant, the world position (zero for plain sounds) and whether it was
        /// positional. The replay frame recorder appends a sound event from it (issue #431); nothing is
        /// raised while <see cref="Muted"/> (a replay seek), and never by <see cref="PlayRecorded"/>.
        /// </summary>
        public static event Action<SoundEffectType, int, Vector2, bool> OnSoundPlayed;

        /// <summary>
        /// True for the button and tab click sounds the global UI hooks play (Game1.HandleGlobalButtonClick /
        /// HandleGlobalTabClick): UI, not simulation, so the frame stream never records them.
        /// </summary>
        public static bool IsUiClick(SoundEffectType type)
        {
            return type == SoundEffectType.TopBarButtonClick || type == SoundEffectType.TabButtonClick
                || type == SoundEffectType.CancelButtonClick || type == SoundEffectType.NormalButtonClick;
        }

        private static void Raise(SoundEffectType type, int variant, in Vector2 position, bool positional)
        {
            if (variant < 0)
                return;
            OnSoundPlayed?.Invoke(type, variant, position, positional);
        }

        public void PlaySound(SoundEffectType soundEffectType, uint frameInterval = 0)
        {
            if (Muted)
                return;
            int variant = soundEffectDict[soundEffectType].Play(SoundVolume, frameInterval);
            Raise(soundEffectType, variant, Vector2.Zero, positional: false);
        }

        public void PlaySound(SoundEffectType soundEffectType, float volume, float pitch, float pan)
        {
            if (Muted)
                return;
            int variant = soundEffectDict[soundEffectType].Play(volume, pitch, pan);
            Raise(soundEffectType, variant, Vector2.Zero, positional: false);
        }

        /// <summary>Plays a world-positioned sound attenuated and panned by horizontal distance from the camera view;
        /// skipped entirely beyond GameConfig.MaxAudibleDistanceTiles past the nearest edge. Position is sampled at play time.
        /// The sound is recorded for replays before the distance check, so a replay camera elsewhere still hears it.</summary>
        public void PlaySoundAt(SoundEffectType soundEffectType, Vector2 sourceWorldPosition)
        {
            if (Muted)
                return;
            var effect = soundEffectDict[soundEffectType];
            int variant = effect.PickVariant();
            Raise(soundEffectType, variant, sourceWorldPosition, positional: true);
            PlayPositional(effect, variant, sourceWorldPosition);
        }

        /// <summary>
        /// Plays a sound the frame stream recorded (the replay frame viewer, issue #431): the exact variant,
        /// through the live camera's falloff and pan when it was positional. Honors <see cref="Muted"/>
        /// and never raises <see cref="OnSoundPlayed"/>, so a watched replay is not recorded again.
        /// </summary>
        public void PlayRecorded(SoundEffectType soundEffectType, int variant, Vector2 sourceWorldPosition, bool positional)
        {
            if (Muted || soundEffectDict == null || !soundEffectDict.TryGetValue(soundEffectType, out var effect))
                return;
            if (positional)
                PlayPositional(effect, variant, sourceWorldPosition);
            else
                effect.PlayVariant(variant, SoundVolume, 0f, 0f);
        }

        private void PlayPositional(IGameSoundEffect effect, int variant, Vector2 sourceWorldPosition)
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

            effect.PlayVariant(variant, SoundVolume * scale, 0f, pan);
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
