using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Combat;

namespace PitHero.Util
{
    /// <summary>
    /// Battle visuals drawn from SkillsStencils.atlas animations (issue #458):
    /// <list type="bullet">
    /// <item>physical attacks: a melee effect drawn in front of the attacker and rotated toward the target,
    /// or an arrow flying from attacker to target;</item>
    /// <item>battle skills (<see cref="SkillEffect"/>): effects played at a point, projectiles, arrow volleys,
    /// and detached effects that play by themselves (heals, buffs, Provoke).</item>
    /// </list>
    /// Every coroutine runs for a fixed simulation time taken from <see cref="GameConfig"/>, never from the
    /// art, and holds while the game is paused, so a battle takes the same number of ticks live, in a replay
    /// seek (where nothing is drawn) and with or without the animations present. Each cropped animation's
    /// atlas origin is its anchor, so the entity position is where the effect lands.
    /// </summary>
    public static class AttackEffects
    {
        private const string AtlasPath = "Content/Atlases/SkillsStencils.atlas";
        private const string EntityName = "attack-effect";
        private const int MaxVolleyArrows = 8;

        /// <summary>Atlas animation names, indexed by <see cref="AttackVisual"/>.</summary>
        private static readonly string[] AttackNames =
        {
            null,               // None
            "AttackSlash",      // Slash
            "AttackStab",       // Stab
            "AttackThrust",     // Thrust
            "AttackArrow",      // Arrow
            "AttackAxe",        // AxeSwing
            "AttackHammer",     // Hammer
            "AttackPunch",      // Punch
            "AttackRod",        // Rod (the raised rod; the bolt and burst are below)
            "AttackStaff",      // Staff
        };

        /// <summary>The rod attack's energy bolt and the burst where it lands.</summary>
        public const string RodBoltAnimation = "AttackRodBolt";
        public const string RodBurstAnimation = "AttackRodBurst";

        /// <summary>Atlas animation names, indexed by <see cref="SkillEffect"/>.</summary>
        private static readonly string[] SkillNames =
        {
            null,                   // None
            "SkillSpinSlash",
            "SkillHeavyStrike",
            "SkillProvoke",
            "SkillRoundhouse",
            "SkillFlamingFist",
            "SkillSneakAttack",
            "SkillVanish",
            "SkillDefenseUp",
            "SkillShotImpact",
            "SkillFireball",
            "SkillFireBlast",
            "SkillMeteorShower",
            "SkillHeal",
            "SkillHolyStrike",
            "SkillIaido",
            "SkillFlashStrike",
            "SkillShadowSlash",
            "SkillSacredBlade",
            "SkillSpellblade",
            "SkillDragonClaw",
            "SkillHolyBurst",
            "SkillKiBurst",
            "SkillMeteor",
            "SkillShadowBolt",
            "SkillBlitz",
            "SkillLifeLeech",
            "SkillFade",
            "SkillKiCloak",
            "SkillPoisonHit",
            "SkillElementalStorm",
            "SkillBurn",
        };

        private static readonly HashSet<string> _warnedMissing = new HashSet<string>();
        private static readonly AttackEffectRenderer[] _volley = new AttackEffectRenderer[MaxVolleyArrows];
        private static readonly Vector2[] _volleyTargets = new Vector2[MaxVolleyArrows];
        private static readonly bool[] _volleyLanded = new bool[MaxVolleyArrows];

        // ── Physical attacks ─────────────────────────────────────────────────────

        /// <summary>
        /// Plays a melee effect between attacker and target and completes after
        /// <see cref="GameConfig.AttackEffectDuration"/>. Null for <see cref="AttackVisual.None"/>.
        /// </summary>
        public static IEnumerator PlayMelee(Scene scene, AttackVisual visual, Vector2 attackerPos, Vector2 targetPos)
        {
            if (visual == AttackVisual.None || AttackVisuals.IsRanged(visual))
                return null;
            var delta = targetPos - attackerPos;
            var dir = delta.LengthSquared() > 0.0001f ? Vector2.Normalize(delta) : Vector2.UnitX;
            var center = attackerPos + dir * GameConfig.AttackEffectReach;
            return PlayRoutine(scene, AttackNames[(int)visual], center, MathF.Atan2(delta.Y, delta.X),
                GameConfig.AttackEffectDuration);
        }

        /// <summary>Flies an arrow from <paramref name="from"/> to <paramref name="to"/>; completes on impact.</summary>
        public static IEnumerator FlyArrow(Scene scene, Vector2 from, Vector2 to)
            => FlyRoutine(scene, AttackNames[(int)AttackVisual.Arrow], from, to, GameConfig.AttackArrowSpeed,
                GameConfig.AttackArrowMinFlight, GameConfig.AttackArrowMaxFlight);

        /// <summary>
        /// Rod attack: the attacker holds a rod up beside its head (mirrored toward a target on the left) while a
        /// glow charges at the tip for <see cref="GameConfig.AttackRodRaiseDuration"/>, then an energy bolt flies
        /// from the tip to the target and a small burst plays there. Completes when the bolt lands; the burst is
        /// detached and adds no time.
        /// </summary>
        public static IEnumerator PlayRod(Scene scene, Vector2 attackerPos, Vector2 targetPos)
            => RodRoutine(scene, attackerPos, targetPos);

        // ── Battle skills ────────────────────────────────────────────────────────

        /// <summary>The SkillsStencils.atlas animation a skill effect plays (null for None or out of range).</summary>
        public static string AnimationName(SkillEffect effect)
        {
            int key = (int)effect;
            return key > 0 && key < SkillNames.Length ? SkillNames[key] : null;
        }

        /// <summary>The SkillsStencils.atlas animation an attack visual plays (null for None or out of range).</summary>
        public static string AnimationName(AttackVisual visual)
        {
            int key = (int)visual;
            return key > 0 && key < AttackNames.Length ? AttackNames[key] : null;
        }

        /// <summary>
        /// Plays a skill effect at <paramref name="position"/> and completes after <paramref name="duration"/>.
        /// <paramref name="rotation"/> (radians, 0 = art pointing right) turns directional art toward a target.
        /// </summary>
        public static IEnumerator PlaySkill(Scene scene, SkillEffect effect, Vector2 position, float duration, float rotation = 0f)
        {
            if (effect == SkillEffect.None)
                return null;
            return PlayRoutine(scene, SkillNames[(int)effect], position, rotation, duration);
        }

        /// <summary>
        /// Starts a skill effect at <paramref name="position"/> that plays by itself for <paramref name="duration"/>
        /// and cleans itself up; nothing waits on it, so the battle's timing is unchanged.
        /// </summary>
        public static void PlaySkillDetached(Scene scene, SkillEffect effect, Vector2 position, float duration, float rotation = 0f)
        {
            if (effect == SkillEffect.None)
                return;
            var renderer = Spawn(scene, SkillNames[(int)effect], position, rotation);
            renderer?.AutoPlay(duration);
        }

        /// <summary>
        /// Starts a detached skill effect anchored to <paramref name="target"/>: it follows the target while it
        /// plays (a heal on a character who keeps walking) and cleans itself up after <paramref name="duration"/>.
        /// </summary>
        public static void PlaySkillOn(Entity target, SkillEffect effect, float duration)
        {
            if (target == null || target.IsDestroyed || effect == SkillEffect.None)
                return;
            var renderer = Spawn(target.Scene, SkillNames[(int)effect], target.Transform.Position, 0f);
            if (renderer == null)
                return;
            renderer.Follow = target;
            renderer.AutoPlay(duration);
        }

        /// <summary>
        /// Plays an area effect at <paramref name="areaPos"/> for <paramref name="duration"/> (blocking) and, at
        /// <see cref="GameConfig.SkillAreaHitDelayFraction"/> of the way through, starts a detached
        /// <paramref name="hit"/> effect on every position in <paramref name="hitPositions"/> (up to 8), as if the
        /// area effect is striking each target. With <paramref name="rotateFrom"/> set, each hit points away from
        /// that point (a slash radiating from a spinning knight). The hits add no time to the battle.
        /// </summary>
        public static IEnumerator PlayAreaWithHits(Scene scene, SkillEffect area, Vector2 areaPos, float duration,
            SkillEffect hit, List<Vector2> hitPositions, float hitDuration, Vector2? rotateFrom = null)
        {
            int count = CopyHitPositions(hitPositions);
            return AreaWithHitsRoutine(scene, area, areaPos, duration, hit, count, hitDuration, rotateFrom);
        }

        /// <summary>
        /// Starts the detached <paramref name="hit"/> effect on each of <paramref name="hitPositions"/> (up to 8) now;
        /// with <paramref name="rotateFrom"/> set each hit points away from that point.
        /// </summary>
        public static void PlayHitsDetached(Scene scene, SkillEffect hit, List<Vector2> hitPositions, float hitDuration,
            Vector2? rotateFrom = null)
        {
            if (hit == SkillEffect.None || hitPositions == null)
                return;
            int count = hitPositions.Count < MaxVolleyArrows ? hitPositions.Count : MaxVolleyArrows;
            for (int i = 0; i < count; i++)
                PlaySkillDetached(scene, hit, hitPositions[i], hitDuration, RotationFrom(rotateFrom, hitPositions[i]));
        }

        /// <summary>
        /// Flies a skill projectile (art pointing right, rotated toward the target) from <paramref name="from"/>
        /// to <paramref name="to"/> at <paramref name="speed"/> px/s, clamped to the flight-time bounds.
        /// </summary>
        public static IEnumerator FlySkill(Scene scene, SkillEffect effect, Vector2 from, Vector2 to,
            float speed, float minFlight, float maxFlight)
            => FlyRoutine(scene, SkillNames[(int)effect], from, to, speed, minFlight, maxFlight);

        /// <summary>
        /// Flies one arrow from <paramref name="from"/> to each target at once (up to 8); completes when the
        /// longest flight lands. Each arrow uses the shared arrow speed and clamps.
        /// </summary>
        public static IEnumerator FlyVolley(Scene scene, Vector2 from, List<Vector2> targets,
            SkillEffect impact = SkillEffect.None, float impactDuration = 0f)
        {
            if (targets == null || targets.Count == 0)
                return null;
            return FlyVolleyRoutine(scene, from, targets, impact, impactDuration);
        }

        // ── Routines ─────────────────────────────────────────────────────────────

        private static IEnumerator PlayRoutine(Scene scene, string anim, Vector2 position, float rotation, float duration)
        {
            var renderer = Spawn(scene, anim, position, rotation);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (renderer != null)
                    renderer.Progress = Mathf.Clamp01(elapsed / duration);
            }

            if (renderer != null)
                renderer.Entity.Destroy();
        }

        private static IEnumerator FlyRoutine(Scene scene, string anim, Vector2 from, Vector2 to,
            float speed, float minFlight, float maxFlight)
        {
            var delta = to - from;
            float duration = Mathf.Clamp(delta.Length() / speed, minFlight, maxFlight);
            var renderer = Spawn(scene, anim, from, MathF.Atan2(delta.Y, delta.X));
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (renderer != null)
                {
                    float t = Mathf.Clamp01(elapsed / duration);
                    renderer.Entity.SetPosition(Vector2.Lerp(from, to, t));
                    renderer.Progress = t;
                }
            }

            if (renderer != null)
                renderer.Entity.Destroy();
        }

        private static IEnumerator RodRoutine(Scene scene, Vector2 attackerPos, Vector2 targetPos)
        {
            bool left = targetPos.X < attackerPos.X;
            float side = left ? -1f : 1f;
            var hand = attackerPos + new Vector2(side * GameConfig.AttackRodHandX, GameConfig.AttackRodHandY);
            var tip = hand + new Vector2(side * GameConfig.AttackRodTipX, GameConfig.AttackRodTipY);

            // Raise the rod and charge the glow; the art's last frame (orb released) is held during the flight
            var rod = SpawnMirrored(scene, AttackNames[(int)AttackVisual.Rod], hand, left);
            float raiseShare = rod != null && rod.FrameCount > 1 ? (rod.FrameCount - 1f) / rod.FrameCount : 1f;
            float elapsed = 0f;
            while (elapsed < GameConfig.AttackRodRaiseDuration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (rod != null)
                    rod.Progress = Mathf.Clamp01(elapsed / GameConfig.AttackRodRaiseDuration) * raiseShare * 0.999f;
            }
            if (rod != null)
                rod.Progress = 1f;

            // The bolt flies from the tip to the target
            var delta = targetPos - tip;
            float flight = Mathf.Clamp(delta.Length() / GameConfig.AttackRodBoltSpeed,
                GameConfig.AttackRodBoltMinFlight, GameConfig.AttackRodBoltMaxFlight);
            var bolt = Spawn(scene, RodBoltAnimation, tip, MathF.Atan2(delta.Y, delta.X));
            elapsed = 0f;
            while (elapsed < flight)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (bolt != null)
                {
                    float t = Mathf.Clamp01(elapsed / flight);
                    bolt.Entity.SetPosition(Vector2.Lerp(tip, targetPos, t));
                    bolt.Progress = t;
                }
            }

            if (bolt != null)
                bolt.Entity.Destroy();
            if (rod != null)
                rod.Entity.Destroy();
            Spawn(scene, RodBurstAnimation, targetPos, 0f)?.AutoPlay(GameConfig.AttackRodBurstDuration);
        }

        private static IEnumerator AreaWithHitsRoutine(Scene scene, SkillEffect area, Vector2 areaPos, float duration,
            SkillEffect hit, int hitCount, float hitDuration, Vector2? rotateFrom)
        {
            var renderer = area != SkillEffect.None ? Spawn(scene, SkillNames[(int)area], areaPos, 0f) : null;
            float hitAt = duration * GameConfig.SkillAreaHitDelayFraction;
            bool hitsStarted = false;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (renderer != null)
                    renderer.Progress = Mathf.Clamp01(elapsed / duration);
                if (!hitsStarted && elapsed >= hitAt)
                {
                    hitsStarted = true;
                    for (int i = 0; i < hitCount; i++)
                        PlaySkillDetached(scene, hit, _hitPositions[i], hitDuration, RotationFrom(rotateFrom, _hitPositions[i]));
                }
            }

            if (renderer != null)
                renderer.Entity.Destroy();
        }

        private static readonly Vector2[] _hitPositions = new Vector2[MaxVolleyArrows];

        /// <summary>Copies up to 8 hit positions into the shared buffer (callers reuse their lists).</summary>
        private static int CopyHitPositions(List<Vector2> positions)
        {
            if (positions == null)
                return 0;
            int count = positions.Count < MaxVolleyArrows ? positions.Count : MaxVolleyArrows;
            for (int i = 0; i < count; i++)
                _hitPositions[i] = positions[i];
            return count;
        }

        /// <summary>Rotation pointing from <paramref name="origin"/> to <paramref name="to"/>; 0 without an origin.</summary>
        private static float RotationFrom(Vector2? origin, Vector2 to)
        {
            if (origin == null)
                return 0f;
            var d = to - origin.Value;
            return d.LengthSquared() > 0.0001f ? MathF.Atan2(d.Y, d.X) : 0f;
        }

        private static IEnumerator FlyVolleyRoutine(Scene scene, Vector2 from, List<Vector2> targets,
            SkillEffect impact, float impactDuration)
        {
            int count = targets.Count < MaxVolleyArrows ? targets.Count : MaxVolleyArrows;
            float longest = 0f;
            for (int i = 0; i < count; i++)
            {
                var delta = targets[i] - from;
                _volleyTargets[i] = targets[i];
                _volleyLanded[i] = false;
                _volley[i] = Spawn(scene, AttackNames[(int)AttackVisual.Arrow], from, MathF.Atan2(delta.Y, delta.X));
                float flight = Mathf.Clamp(delta.Length() / GameConfig.AttackArrowSpeed,
                    GameConfig.AttackArrowMinFlight, GameConfig.AttackArrowMaxFlight);
                if (flight > longest) longest = flight;
            }

            float elapsed = 0f;
            while (elapsed < longest)
            {
                yield return null;
                elapsed += UnpausedDelta();
                for (int i = 0; i < count; i++)
                {
                    float flight = Mathf.Clamp(Vector2.Distance(from, _volleyTargets[i]) / GameConfig.AttackArrowSpeed,
                        GameConfig.AttackArrowMinFlight, GameConfig.AttackArrowMaxFlight);
                    float t = Mathf.Clamp01(elapsed / flight);
                    if (t >= 1f && !_volleyLanded[i])
                    {
                        // Landed: the impact plays on that target (independent of whether the arrow was drawn)
                        _volleyLanded[i] = true;
                        PlaySkillDetached(scene, impact, _volleyTargets[i], impactDuration,
                            RotationFrom(from, _volleyTargets[i]));
                    }
                    var r = _volley[i];
                    if (r == null || r.Entity == null || r.Entity.IsDestroyed)
                        continue;
                    r.Entity.SetPosition(Vector2.Lerp(from, _volleyTargets[i], t));
                    r.Progress = t;
                    if (t >= 1f)
                        r.Entity.Destroy();
                }
            }

            for (int i = 0; i < count; i++)
            {
                var r = _volley[i];
                if (r != null && r.Entity != null && !r.Entity.IsDestroyed)
                    r.Entity.Destroy();
                _volley[i] = null;
            }
        }

        /// <summary>
        /// Creates the effect entity, or null when nothing should be drawn: during a replay seek
        /// (cosmetics suspended), headless, or when the animation is missing from the atlas.
        /// </summary>
        private static AttackEffectRenderer Spawn(Scene scene, string anim, Vector2 position, float rotation)
        {
            if (scene == null || anim == null || Core.CosmeticUpdatesSuspended)
                return null;
            var animation = FindAnimation(anim);
            if (animation == null)
                return null;

            var entity = scene.CreateEntity(EntityName);
            entity.SetPosition(position);
            var renderer = entity.AddComponent(new AttackEffectRenderer());
            renderer.SetRenderLayer(GameConfig.RenderLayerLowest);
            renderer.Setup(animation, rotation);
            return renderer;
        }

        /// <summary>Like <see cref="Spawn"/> for upright art, mirrored left-right about its anchor when <paramref name="mirror"/>.</summary>
        private static AttackEffectRenderer SpawnMirrored(Scene scene, string anim, Vector2 position, bool mirror)
        {
            var renderer = Spawn(scene, anim, position, 0f);
            if (renderer != null && mirror)
                renderer.SetupMirrored(FindAnimation(anim), true);
            return renderer;
        }

        private static SpriteAnimation FindAnimation(string anim)
        {
            var atlas = Core.Content?.LoadSpriteAtlas(AtlasPath);
            if (atlas?.AnimationNames != null)
            {
                int index = Array.IndexOf(atlas.AnimationNames, anim);
                if (index >= 0)
                    return atlas.SpriteAnimations[index];
            }
            if (_warnedMissing.Add(anim))
                Debug.Warn($"[AttackEffects] Animation {anim} not found in SkillsStencils.atlas; effect skipped");
            return null;
        }

        /// <summary>The fixed step's delta, or zero while the game is paused (the effect holds).</summary>
        private static float UnpausedDelta()
        {
            var pause = Core.Instance != null ? Core.Services.GetService<PauseService>() : null;
            return pause?.IsPaused == true ? 0f : Time.DeltaTime;
        }
    }
}
