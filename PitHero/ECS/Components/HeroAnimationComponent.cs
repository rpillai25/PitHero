using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using RolePlayingFramework.Heroes;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Abstract base class for hero sprite animations based on facing direction using the Actors.atlas
    /// </summary>
    public abstract class HeroAnimationComponent : PausableSpriteAnimator, IUpdatable, ICompositeLayer, ICompositeLayerGroup
    {
        private ActorFacingComponent _facing;
        private Direction _lastDirection = Direction.Down; // Default to down
        private readonly HeroLayerNames _names;

        // Tone layers (issue #462): the Midtone plays as this animator; the other tones show the same frame
        // of their own animation, each tinted from Color by its HeroToneProfile.
        private readonly HeroLayerNames[] _tones;
        private readonly TonePart[] _parts;
        private readonly SpriteAnimation[] _toneAnimations;
        private readonly Color[] _toneColors;
        private string _tonesResolvedFor;
        private uint _tintedFor;
        private bool _tintsValid;

        // Animation names come from the layer's HeroSpriteNames entry
        protected string DefaultAnimation => _names.Down;
        protected string AnimDown => _names.Down;
        protected string AnimLeft => _names.Right;   // Flipped in code
        protected string AnimRight => _names.Right;
        protected string AnimUp => _names.Up;
        protected string JumpAnimDown => _names.Jump;
        protected string JumpAnimLeft => _names.Jump;  // Flipped in code
        protected string JumpAnimRight => _names.Jump;
        protected string JumpAnimUp => _names.Jump;

        protected string SleepDown => _names.SleepDown;
        protected string SleepRight => _names.SleepRight;
        protected string SleepLeft => _names.SleepRight;   // Flipped in code, same sprite as SleepRight
        protected string SleepUp => _names.Up;

        private Color _componentColor = Color.White;
        /// <summary>Gets the tint color for this component (default: White)</summary>
        public Color ComponentColor
        {
            get => _componentColor;
            set { _componentColor = value; }
        }

        /// <summary>ICompositeLayer.LayerColor — wraps RenderableComponent.Color field.</summary>
        public Color LayerColor => Color;

        /// <summary>
        /// Set to true by MultiSpriteAnimator so this layer is composited rather than rendered directly.
        /// </summary>
        public bool OwnedByComposite { get; set; }

        public override bool IsVisibleFromCamera(Camera camera)
        {
            if (OwnedByComposite) return false;
            return base.IsVisibleFromCamera(camera);
        }

        /// <summary>Gets the move down animation name for this animation component</summary>
        public string MoveDownAnimationName => AnimDown;

        public HeroAnimationComponent(Color color, HeroLayerNames names)
        {
            _componentColor = color;
            _names = names;
            _tones = names.Tones ?? new[] { names };
            _parts = new TonePart[_tones.Length];
            _toneAnimations = new SpriteAnimation[_tones.Length];
            _toneColors = new Color[_tones.Length];
            for (int i = 0; i < _tones.Length; i++)
                _parts[i] = new TonePart(this, i);
        }

        /// <summary>Number of tone layers this part draws (1 when the part is not split).</summary>
        public int PartCount => _tones.Length;

        /// <summary>The composite view of one tone layer.</summary>
        public ICompositeLayer GetPart(int index) => _parts[index];

        /// <summary>The tone drawn by part <paramref name="index"/>.</summary>
        public HeroTone GetTone(int index) => _tones[index].Tone;

        /// <summary>
        /// Appends the first move-down frame of every tone, tinted from <see cref="ComponentColor"/>: the static
        /// HUD portrait. Stops when the arrays are full. False (nothing appended) when the Midtone has no frame.
        /// </summary>
        public bool AppendPortraitParts(Sprite[] sprites, Color[] colors, ref int count)
        {
            for (int i = 0; i < _tones.Length; i++)
            {
                if (ReferenceEquals(_tones[i], _names) && GetToneMoveDownFirstFrame(i) == null)
                    return false;
            }
            for (int i = 0; i < _tones.Length && count < sprites.Length; i++)
            {
                var sprite = GetToneMoveDownFirstFrame(i);
                if (sprite == null)
                    continue;
                sprites[count] = sprite;
                colors[count] = ToneTint(i, ComponentColor);
                count++;
            }
            return true;
        }

        /// <summary>The current frame of tone <paramref name="index"/>, or null when that tone has none.</summary>
        public Sprite GetToneSprite(int index)
        {
            if (ReferenceEquals(_tones[index], _names))
                return Sprite;
            if (CurrentAnimation == null)
                return null;
            if (!ReferenceEquals(_tonesResolvedFor, CurrentAnimationName))
                ResolveToneAnimations();
            var animation = _toneAnimations[index];
            if (animation?.Sprites == null || CurrentFrame >= animation.Sprites.Length)
                return null;
            return animation.Sprites[CurrentFrame];
        }

        /// <summary>The tint of tone <paramref name="index"/> for the current <see cref="RenderableComponent.Color"/>.</summary>
        public Color GetToneColor(int index)
        {
            var color = Color;
            if (!_tintsValid || color.PackedValue != _tintedFor)
            {
                for (int i = 0; i < _tones.Length; i++)
                    _toneColors[i] = ToneTint(i, color);
                _tintedFor = color.PackedValue;
                _tintsValid = true;
            }
            return _toneColors[index];
        }

        /// <summary>The tint tone <paramref name="index"/> gets when the part's chosen color is <paramref name="chosen"/>.</summary>
        public Color ToneTint(int index, Color chosen)
        {
            var names = _tones[index];
            return HeroToneProfiles.Tint(names.Gender, names.Layer, names.Tone, chosen, in _customTones);
        }

        private HeroCustomTones _customTones;
        /// <summary>
        /// The player's picked Shadow / Highlight colors for this part (issue #467). Unset (the default)
        /// makes every tone from the chosen color by its profile.
        /// </summary>
        public HeroCustomTones CustomTones
        {
            get => _customTones;
            set
            {
                _customTones = value;
                _tintsValid = false;
            }
        }

        /// <summary>First move-down frame of tone <paramref name="index"/> (static portraits), or null.</summary>
        public Sprite GetToneMoveDownFirstFrame(int index)
        {
            if (Animations == null || !Animations.TryGetValue(_tones[index].Down, out var animation))
                return null;
            return animation.Sprites != null && animation.Sprites.Length > 0 ? animation.Sprites[0] : null;
        }

        // Finds which of the Midtone's animations is playing and picks each tone's matching animation
        private void ResolveToneAnimations()
        {
            string playing = CurrentAnimationName;
            _tonesResolvedFor = playing;
            for (int i = 0; i < _tones.Length; i++)
            {
                string name = MatchingName(_tones[i], playing);
                SpriteAnimation animation = null;
                if (name != null && Animations != null)
                    Animations.TryGetValue(name, out animation);
                _toneAnimations[i] = animation;
            }
        }

        private string MatchingName(HeroLayerNames tone, string playing)
        {
            if (playing == _names.Down) return tone.Down;
            if (playing == _names.Right) return tone.Right;
            if (playing == _names.Up) return tone.Up;
            if (playing == _names.Jump) return tone.Jump;
            if (playing == _names.SleepDown) return tone.SleepDown;
            if (playing == _names.SleepRight) return tone.SleepRight;
            return null;
        }

        /// <summary>
        /// Drawn on its own (outside a MultiSpriteAnimator, e.g. the hero creation preview): every tone,
        /// back to front, at this renderer's transform.
        /// </summary>
        public override void Render(Batcher batcher, Camera camera)
        {
            if (_tones.Length == 1)
            {
                base.Render(batcher, camera);
                return;
            }
            var position = Entity.Transform.Position + LocalOffset;
            for (int i = 0; i < _tones.Length; i++)
            {
                var sprite = GetToneSprite(i);
                if (sprite == null)
                    continue;
                batcher.Draw(sprite, position, GetToneColor(i), Entity.Transform.Rotation, sprite.Origin,
                    Entity.Transform.Scale, SpriteEffects, _layerDepth);
            }
        }

        /// <summary>One tone layer as seen by the composite: the owner's offset and flip, the tone's sprite and tint.</summary>
        private sealed class TonePart : ICompositeLayer
        {
            private readonly HeroAnimationComponent _owner;
            private readonly int _index;

            public TonePart(HeroAnimationComponent owner, int index)
            {
                _owner = owner;
                _index = index;
            }

            public Sprite Sprite => _owner.GetToneSprite(_index);
            public Vector2 LocalOffset => _owner.LocalOffset;
            public bool FlipX => _owner.FlipX;
            public Color LayerColor => _owner.GetToneColor(_index);

            /// <summary>Parts are not components; the owner carries the flag.</summary>
            public bool OwnedByComposite
            {
                get => _owner.OwnedByComposite;
                set => _owner.OwnedByComposite = value;
            }
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            try
            {
                var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (actorsAtlas != null)
                {
                    AddAnimationsFromAtlas(actorsAtlas);
                    Play(DefaultAnimation, LoopMode.Loop);
                }
                else
                {
                    Debug.Warn("[HeroAnimationComponent] Failed to load Actors.atlas - atlas is null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[HeroAnimationComponent] Failed to load Actors.atlas: {ex.Message}");
            }

            _facing = Entity.GetComponent<ActorFacingComponent>();
            if (_facing == null)
            {
                _facing = Entity.AddComponent(new ActorFacingComponent());
            }

            this.SetColor(ComponentColor);
        }

        public new void Update()
        {
            base.Update();
            if (_facing == null)
                return;
            var direction = _facing.Facing;
            if (direction != _lastDirection || _facing.ConsumeDirtyFlag())
            {
                _lastDirection = direction;
                UpdateAnimationForDirection(direction);
            }
        }

        /// <summary>
        /// Plays the appropriate walking animation for the given direction
        /// </summary>
        public void UpdateAnimationForDirection(Direction direction)
        {
            string animationName = direction switch
            {
                Direction.Up => AnimUp,
                Direction.Down => AnimDown,
                Direction.Left => AnimLeft,
                Direction.Right => AnimRight,
                Direction.UpLeft => AnimLeft,
                Direction.UpRight => AnimRight,
                Direction.DownLeft => AnimLeft,
                Direction.DownRight => AnimRight,
                _ => AnimDown
            };

            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);

            if (IsAnimationActive(animationName))
                return;

            if (Animations != null && Animations.ContainsKey(animationName))
            {
                Play(animationName, LoopMode.Loop);
            }
            else
            {
                Debug.Warn($"[HeroAnimationComponent] Animation {animationName} not found - animations may not be loaded");
            }
        }

        /// <summary>Gets the appropriate jump animation name for the given direction</summary>
        public string GetJumpAnimationNameForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => JumpAnimUp,
                Direction.Down => JumpAnimDown,
                Direction.Left => JumpAnimLeft,
                Direction.Right => JumpAnimRight,
                Direction.UpLeft => JumpAnimLeft,
                Direction.UpRight => JumpAnimRight,
                Direction.DownLeft => JumpAnimLeft,
                Direction.DownRight => JumpAnimRight,
                _ => JumpAnimDown
            };
        }

        /// <summary>Starts playing a jump animation for the given direction (plays naturally)</summary>
        public void PlayJumpAnimation(Direction direction)
        {
            string animationName = GetJumpAnimationNameForDirection(direction);
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);
            if (Animations != null && Animations.ContainsKey(animationName))
            {
                Play(animationName, LoopMode.Once);
            }
            else
            {
                Debug.Warn($"[HeroAnimationComponent] Jump animation {animationName} not found - animations may not be loaded");
            }
        }

        /// <summary>
        /// Plays the appropriate sleep animation for the given direction.
        /// Most layers hold their move animation; EyesBase shows the closed slits and Eyes is empty.
        /// Falls back to the move animation if a sleep animation is not found.
        /// </summary>
        public void PlaySleepAnimationForDirection(Direction direction)
        {
            string animationName = direction switch
            {
                Direction.Up => SleepUp,
                Direction.Down => SleepDown,
                Direction.Left => SleepLeft,
                Direction.Right => SleepRight,
                Direction.UpLeft => SleepLeft,
                Direction.UpRight => SleepRight,
                Direction.DownLeft => SleepLeft,
                Direction.DownRight => SleepRight,
                _ => SleepDown
            };

            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);

            if (Animations != null && Animations.ContainsKey(animationName))
            {
                Play(animationName, LoopMode.Loop);
            }
            else
            {
                // Fall back to the regular walk animation for this direction
                UpdateAnimationForDirection(direction);
            }
        }

        /// <summary>
        /// Instantly freezes this layer in its sleep pose for the given direction. Syncs the
        /// direction cache so the next Update doesn't re-play the walk animation over the pose;
        /// callers must also consume the facing component's dirty flag.
        /// </summary>
        public void ForceSleepPose(Direction direction)
        {
            _lastDirection = direction;
            PlaySleepAnimationForDirection(direction);
            PauseAnimation();
        }

        /// <summary>Updates the jump animation flip state (called during jump updates)</summary>
        public void UpdateJumpAnimationFlip(Direction direction)
        {
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);
        }

        /// <summary>Sets a specific frame</summary>
        public void SetAnimationFrame(int frameIndex) => SetFrame(frameIndex);

        /// <summary>Pauses the current animation</summary>
        public void PauseAnimation() => Pause();

        /// <summary>Unpauses the current animation</summary>
        public void UnpauseAnimation() => UnPause();
    }
}