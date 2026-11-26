using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;

namespace PitHero.Services
{
    /// <summary>
    /// The wizard-orb warp transition (issue #469). Scene-scoped simulation state, stepped once per
    /// simulation tick by <see cref="AI.ActivateWizardOrbAction"/>:
    /// <list type="number">
    /// <item>Rise — the party lifts off, then floats, bobbing (each member out of step) and spinning
    /// through its facings (each member clockwise or counter-clockwise).</item>
    /// <item>Swirl in — the pit twists down into a drain (<see cref="Rendering.PitWarpPostProcessor"/>)
    /// until only the abyss is left; the action regenerates the pit behind it.</item>
    /// <item>Swirl out — the new pit twists back up.</item>
    /// <item>Glide — the party floats over to the new pit's start tile.</item>
    /// <item>Drop — and lands on it.</item>
    /// </list>
    /// Everything is a pure function of the warp tick, so the transition is replay-deterministic. The
    /// "random" spin directions and bob offsets are hashed from the pit level and member slot rather than
    /// drawn from an RNG stream. The party's sprites float by render offset only (<see cref="HeroJumpComponent"/>):
    /// the entities stay put until the pit is regenerated, then teleport to the start tile while the
    /// offset keeps them where they were on screen.
    /// </summary>
    public sealed class PitWarpService
    {
        /// <summary>The hero plus two hired mercenaries.</summary>
        public const int MaxMembers = 3;

        /// <summary>The tick at which the pit is fully drained and is regenerated.</summary>
        public const int RegenerationTick = GameConfig.PitWarpRiseTicks + GameConfig.PitWarpSwirlInTicks;

        /// <summary>The tick the glide starts (the new pit has finished twisting back up).</summary>
        public const int GlideStartTick = RegenerationTick + GameConfig.PitWarpSwirlOutTicks;

        /// <summary>The tick the drop starts.</summary>
        public const int DropStartTick = GlideStartTick + GameConfig.PitWarpGlideTicks;

        /// <summary>The tick the party lands and the warp ends.</summary>
        public const int TotalTicks = DropStartTick + GameConfig.PitWarpDropTicks;

        // Clockwise as seen from above: south, west, north, east
        private static readonly Direction[] SpinOrder = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };
        private const int LandingDirectionIndex = 1; // Left: the way the party faces after jumping into the pit

        private struct Member
        {
            public Entity Entity;
            public HeroJumpComponent Jump;
            public MultiSpriteAnimator Animator;
            public TileByTileMover Mover;
            public MercenaryComponent Merc;
            public int SpinSign;       // +1 clockwise, -1 counter-clockwise
            public int SpinPhase;      // tick offset of the spin steps
            public int BobPhase;       // tick offset of the bob cycle
            public int DirectionIndex; // into SpinOrder
            public Vector2 StartPosition;
            public Vector2 Carry;      // render offset that keeps the sprite in place after the teleport
        }

        private readonly Member[] _members = new Member[MaxMembers];
        private int _memberCount;
        private int _tick;
        private bool _regenerated;

        /// <summary>True from <see cref="Begin"/> until <see cref="End"/>.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Ticks since the warp began.</summary>
        public int Tick => _tick;

        /// <summary>Swirl angle at the pit centre this tick (radians).</summary>
        public float Twist { get; private set; }

        /// <summary>Drain amount this tick (0 = untouched pit, 1 = all abyss).</summary>
        public float Pull { get; private set; }

        /// <summary>Phase of the abyss spiral arms (radians).</summary>
        public float Spin { get; private set; }

        /// <summary>The swirling region in world pixels: every tile of the old and new pit rects (walls and rim included).</summary>
        public int RegionX { get; private set; }
        public int RegionY { get; private set; }
        public int RegionWidth { get; private set; }
        public int RegionHeight { get; private set; }

        /// <summary>True on the tick the pit should be regenerated (fully drained, not yet regenerated).</summary>
        public bool RegenerationDue => IsActive && !_regenerated && _tick >= RegenerationTick;

        /// <summary>True once the party has landed; the caller finishes with <see cref="End"/>.</summary>
        public bool IsFinished => IsActive && _regenerated && _tick >= TotalTicks;

        /// <summary>
        /// Starts the warp: lifts the hero and the in-pit mercenaries, holds their movers and AI, and moves
        /// their sprites to the after-post-processing party layer. The region covers every tile of the
        /// pit before and after (right edges <paramref name="oldRightEdge"/> and <paramref name="newRightEdge"/>),
        /// so a pit that grows or shrinks is swallowed whole. <paramref name="seed"/> picks the spin
        /// directions and bob offsets.
        /// </summary>
        public void Begin(Entity hero, Entity merc1, Entity merc2, int oldRightEdge, int newRightEdge, int seed)
        {
            if (IsActive)
                End();

            _tick = 0;
            _regenerated = false;
            _memberCount = 0;
            AddMember(hero, seed);
            AddMember(merc1, seed);
            AddMember(merc2, seed);

            int rightEdge = oldRightEdge > newRightEdge ? oldRightEdge : newRightEdge;
            int ts = GameConfig.TileSize;
            // Every pit tile: the outer wall ring sits one tile outside PitRect on the left, top and bottom
            // (column PitRectX-1, rows PitRectY-1 and PitRectY+PitRectHeight); the right edge column is rightEdge.
            RegionX = (GameConfig.PitRectX - 1) * ts;
            RegionY = (GameConfig.PitRectY - 1) * ts;
            RegionWidth = (rightEdge + 1) * ts - RegionX;
            RegionHeight = (GameConfig.PitRectY + GameConfig.PitRectHeight + 1) * ts - RegionY;

            IsActive = true;
            Apply();

            if (hero != null)
                Sound()?.PlaySoundAt(SoundEffectType.Jump, hero.Transform.Position);
            Debug.Log($"[PitWarp] Began with {_memberCount} members, region x={RegionX} y={RegionY} w={RegionWidth} h={RegionHeight}");
        }

        /// <summary>Advances one simulation tick. Holds at the regeneration tick until <see cref="OnPitRegenerated"/>.</summary>
        public void Step()
        {
            if (!IsActive)
                return;
            bool held = !_regenerated && _tick >= RegenerationTick;
            if (!held && _tick < TotalTicks)
                _tick++;
            Apply();
        }

        /// <summary>
        /// Called right after the pit was regenerated and the party teleported to the start tile: each
        /// member carries a render offset back to where it floated, which the glide then closes.
        /// </summary>
        public void OnPitRegenerated()
        {
            if (!IsActive)
                return;
            for (int i = 0; i < _memberCount; i++)
            {
                ref var m = ref _members[i];
                if (IsGone(in m))
                    continue;
                m.Carry = m.StartPosition - m.Entity.Transform.Position;
            }
            _regenerated = true;
            Apply();
        }

        /// <summary>True if <paramref name="entity"/> is one of the floating party members.</summary>
        public bool IsWarping(Entity entity)
        {
            if (!IsActive || entity == null)
                return false;
            for (int i = 0; i < _memberCount; i++)
                if (ReferenceEquals(_members[i].Entity, entity))
                    return true;
            return false;
        }

        /// <summary>
        /// Lands the party: restores the grounded pose (facing left), the actor layer, the movers and the
        /// mercenaries' AI, and clears the swirl. Safe to call at any point to abandon the warp.
        /// </summary>
        public void End()
        {
            if (!IsActive)
                return;
            Vector2? landing = null;
            for (int i = 0; i < _memberCount; i++)
            {
                ref var m = ref _members[i];
                if (!IsGone(in m))
                {
                    if (m.Jump != null)
                    {
                        m.Jump.BeginAirbornePose(SpinOrder[LandingDirectionIndex], keepRestOffset: true);
                        m.Jump.EndAirbornePose();
                    }
                    m.Animator?.SetRenderLayer(GameConfig.RenderLayerActors);
                    if (m.Mover != null)
                        m.Mover.Enabled = true;
                    if (m.Merc != null)
                        m.Merc.IsWarping = false;
                    landing ??= m.Entity.Transform.Position;
                }
                _members[i] = default;
            }
            _memberCount = 0;
            IsActive = false;
            Twist = 0f;
            Pull = 0f;
            Spin = 0f;

            if (landing.HasValue && _regenerated)
                Sound()?.PlaySoundAt(SoundEffectType.Land, landing.Value);
            Debug.Log("[PitWarp] Ended");
        }

        // ───────────────────────────── choreography (pure functions of the tick) ─────────────────────────────

        /// <summary>Swirl angle at the pit centre for a warp tick.</summary>
        public static float TwistAt(int tick)
        {
            float s = SwirlDepthAt(tick);
            return GameConfig.PitWarpMaxTwist * s * s;
        }

        /// <summary>Drain amount for a warp tick: 1 for a span around the regeneration so the swap is never seen.</summary>
        public static float PullAt(int tick)
        {
            float s = SwirlDepthAt(tick);
            return SmoothStep(GameConfig.PitWarpDrainStart, GameConfig.PitWarpDrainFull, s);
        }

        /// <summary>How deep into the drain the pit is: rises 0→1 while swirling in, falls 1→0 while swirling out.</summary>
        public static float SwirlDepthAt(int tick)
        {
            if (tick <= GameConfig.PitWarpRiseTicks || tick >= GlideStartTick)
                return 0f;
            if (tick <= RegenerationTick)
                return (tick - GameConfig.PitWarpRiseTicks) / (float)GameConfig.PitWarpSwirlInTicks;
            return 1f - (tick - RegenerationTick) / (float)GameConfig.PitWarpSwirlOutTicks;
        }

        /// <summary>Float height (without bob) for a warp tick: eases up, holds, then drops with gravity.</summary>
        public static float BaseHeightAt(int tick)
        {
            float h = GameConfig.PitWarpRiseHeightPx;
            if (tick <= 0)
                return 0f;
            if (tick < GameConfig.PitWarpRiseTicks)
            {
                float t = 1f - tick / (float)GameConfig.PitWarpRiseTicks;
                return h * (1f - t * t * t); // ease-out cubic
            }
            if (tick < DropStartTick)
                return h;
            if (tick >= TotalTicks)
                return 0f;
            float p = (tick - DropStartTick) / (float)GameConfig.PitWarpDropTicks;
            return h * (1f - p * p); // ease-in: accelerates onto the tile
        }

        /// <summary>How strongly the bob shows (fades in with the rise, out with the drop).</summary>
        public static float BobWeightAt(int tick)
        {
            if (tick <= 0 || tick >= TotalTicks)
                return 0f;
            if (tick < GameConfig.PitWarpRiseTicks)
                return tick / (float)GameConfig.PitWarpRiseTicks;
            if (tick < DropStartTick)
                return 1f;
            return 1f - (tick - DropStartTick) / (float)GameConfig.PitWarpDropTicks;
        }

        /// <summary>How much of the post-teleport carry offset is left: all of it until the glide, none after it.</summary>
        public static float CarryWeightAt(int tick)
        {
            if (tick <= GlideStartTick)
                return 1f;
            if (tick >= DropStartTick)
                return 0f;
            float p = (tick - GlideStartTick) / (float)GameConfig.PitWarpGlideTicks;
            return 1f - SmoothStep(0f, 1f, p);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = (x - edge0) / (edge1 - edge0);
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>Deterministic integer hash (no RNG stream is touched).</summary>
        private static int Hash(int seed, int slot)
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)(slot + 1) * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (int)(h & 0x7FFFFFFF);
        }

        // ───────────────────────────── members ─────────────────────────────

        private void AddMember(Entity entity, int seed)
        {
            if (entity == null || entity.IsDestroyed || _memberCount >= MaxMembers)
                return;
            int slot = _memberCount;
            int h = Hash(seed, slot);
            var m = new Member
            {
                Entity = entity,
                Jump = entity.GetComponent<HeroJumpComponent>(),
                Animator = entity.GetComponent<MultiSpriteAnimator>(),
                Mover = entity.GetComponent<TileByTileMover>(),
                Merc = entity.GetComponent<MercenaryComponent>(),
                SpinSign = (h & 1) == 0 ? 1 : -1,
                SpinPhase = (h >> 1) % GameConfig.PitWarpSpinStepTicks,
                BobPhase = (h >> 4) % GameConfig.PitWarpBobPeriodTicks,
                DirectionIndex = DirectionIndexOf(entity.GetComponent<ActorFacingComponent>()?.Facing ?? Direction.Down),
                StartPosition = entity.Transform.Position,
                Carry = Vector2.Zero,
            };

            if (m.Mover != null)
            {
                if (m.Mover.IsMoving)
                    m.Mover.StopMoving();
                m.Mover.Enabled = false;
            }
            if (m.Merc != null)
                m.Merc.IsWarping = true;
            m.Jump?.BeginAirbornePose(SpinOrder[m.DirectionIndex]);
            m.Animator?.SetRenderLayer(GameConfig.RenderLayerPitWarpParty);

            _members[slot] = m;
            _memberCount++;
        }

        private static int DirectionIndexOf(Direction direction)
        {
            switch (direction)
            {
                case Direction.Left:
                case Direction.UpLeft:
                    return 1;
                case Direction.Up:
                case Direction.UpRight:
                    return 2;
                case Direction.Right:
                case Direction.DownRight:
                    return 3;
                default:
                    return 0;
            }
        }

        /// <summary>The sound manager, or null headless (no Core instance).</summary>
        private static SoundEffectManager Sound() => Core.Instance != null ? Core.GetGlobalManager<SoundEffectManager>() : null;

        private static bool IsGone(in Member m) => m.Entity == null || m.Entity.IsDestroyed;

        /// <summary>Pushes this tick's choreography to the swirl values and the members' sprites.</summary>
        private void Apply()
        {
            Twist = TwistAt(_tick);
            Pull = PullAt(_tick);
            Spin = _tick * GameConfig.PitWarpAbyssSpinPerTick;

            float baseHeight = BaseHeightAt(_tick);
            float bobWeight = BobWeightAt(_tick);
            float carryWeight = _regenerated ? CarryWeightAt(_tick) : 0f;
            bool spinning = _tick < DropStartTick;

            for (int i = 0; i < _memberCount; i++)
            {
                ref var m = ref _members[i];
                if (IsGone(in m) || m.Jump == null)
                    continue;

                if (spinning)
                {
                    if (_tick > 0 && (_tick + m.SpinPhase) % GameConfig.PitWarpSpinStepTicks == 0)
                    {
                        m.DirectionIndex = (m.DirectionIndex + m.SpinSign + SpinOrder.Length) % SpinOrder.Length;
                        m.Jump.BeginAirbornePose(SpinOrder[m.DirectionIndex], keepRestOffset: true);
                    }
                }
                else if (m.DirectionIndex != LandingDirectionIndex)
                {
                    m.DirectionIndex = LandingDirectionIndex;
                    m.Jump.BeginAirbornePose(SpinOrder[m.DirectionIndex], keepRestOffset: true);
                }

                float bob = GameConfig.PitWarpBobAmplitudePx * bobWeight
                    * Mathf.Sin(MathHelper.TwoPi * (_tick + m.BobPhase) / GameConfig.PitWarpBobPeriodTicks);
                var carry = m.Carry * carryWeight;
                m.Jump.SetAirborneOffset(carry.X, carry.Y, baseHeight + bob);
            }
        }
    }
}
