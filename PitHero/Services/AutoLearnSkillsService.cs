using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>Which skills the Auto-Learn service prioritises for purchase.</summary>
    public enum AutoLearnMode
    {
        /// <summary>Learn skills in the job-defined priority order (signature active first, then passives, then second active).</summary>
        Smart = 0,

        /// <summary>Cheapest unlearned active first; passives only after all actives are learned.</summary>
        Active = 1,

        /// <summary>Cheapest unlearned passive first; actives only after all passives are learned.</summary>
        Passive = 2,
    }

    /// <summary>
    /// Automatically spends available JP on hero skills once per second (issue #353).
    /// Enabled = false by default; mode defaults to Smart.
    /// </summary>
    public class AutoLearnSkillsService
    {
        // Static smart-rank lookup built once. Unknown IDs get int.MaxValue (cheap-first fallback).
        private static readonly Dictionary<string, int> _smartRankTable;

        static AutoLearnSkillsService()
        {
            _smartRankTable = new Dictionary<string, int>(24);

            // Knight
            _smartRankTable["knight.spin_slash"]   = 0;
            _smartRankTable["knight.provoke"]      = 1;
            _smartRankTable["knight.heavy_armor"]  = 2;
            _smartRankTable["knight.heavy_strike"] = 3;

            // Mage
            _smartRankTable["mage.fire"]       = 0;
            _smartRankTable["mage.heart_fire"] = 1;
            _smartRankTable["mage.economist"]  = 2;
            _smartRankTable["mage.firestorm"]  = 3;

            // Priest
            _smartRankTable["priest.heal"]        = 0;
            _smartRankTable["priest.calm_spirit"] = 1;
            _smartRankTable["priest.mender"]      = 2;
            _smartRankTable["priest.defup"]       = 3;

            // Monk
            _smartRankTable["monk.roundhouse"]   = 0;
            _smartRankTable["monk.counter"]      = 1;
            _smartRankTable["monk.deflect"]      = 2;
            _smartRankTable["monk.flaming_fist"] = 3;

            // Thief
            _smartRankTable["thief.sneak_attack"] = 0;
            _smartRankTable["thief.shadowstep"]   = 1;
            _smartRankTable["thief.trap_sense"]   = 2;
            _smartRankTable["thief.vanish"]       = 3;

            // Archer
            _smartRankTable["archer.power_shot"] = 0;
            _smartRankTable["archer.eagle_eye"]  = 1;
            _smartRankTable["archer.quickdraw"]  = 2;
            _smartRankTable["archer.volley"]     = 3;
        }

        private float _throttleTimer;

        /// <summary>Whether automatic skill learning is active. Off by default.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Which prioritisation mode is used when selecting the next skill.</summary>
        public AutoLearnMode Mode { get; set; } = AutoLearnMode.Smart;

        /// <summary>
        /// Advances the throttle and, when enabled, attempts one learn pass per second.
        /// Called once per game frame while the game is unpaused.
        /// </summary>
        public void Update()
        {
            if (!Enabled) return;

            _throttleTimer += Time.DeltaTime;
            if (_throttleTimer < 1f) return;
            _throttleTimer = 0f;

            TryLearnNow();
        }

        /// <summary>
        /// Immediately attempts one learn pass for the current hero. Returns the number of skills
        /// learned (0 when no hero is found, the service is disabled, or no skill is affordable).
        /// Guards <c>Core.Instance != null</c> so it is safe to call in headless test contexts that
        /// supply a <see cref="Hero"/> directly via <see cref="TryLearnPass"/> instead.
        /// </summary>
        public int TryLearnNow()
        {
            if (Core.Instance == null) return 0;

            var heroEntity = Core.Scene?.FindEntity("hero");
            var hero = heroEntity?.GetComponent<HeroComponent>()?.LinkedHero;
            if (hero == null) return 0;

            return TryLearnPass(hero);
        }

        /// <summary>
        /// Executes one full learn pass for <paramref name="hero"/>: loops <see cref="SelectNextSkill"/>
        /// until either null (nothing left to learn) or the next skill is unaffordable (strict-order
        /// wait). Emits one console line per skill learned. Returns the count of skills purchased.
        /// </summary>
        public int TryLearnPass(Hero hero)
        {
            if (hero == null) return 0;

            int learned = 0;
            while (true)
            {
                var skill = SelectNextSkill(hero, Mode);
                if (skill == null) break;                         // nothing left to learn
                if (hero.GetCurrentJP() < skill.JPCost) break;   // strict-order: wait for JP

                if (!hero.TryPurchaseSkill(skill)) break;        // shouldn't happen; safety exit
                learned++;
                EmitLearnConsole(hero.Name, skill.Name);
            }
            return learned;
        }

        /// <summary>
        /// Selects the next skill to learn for <paramref name="hero"/> according to <paramref name="mode"/>.
        /// Returns null when every skill in the job is already learned. Returns the skill even if
        /// currently unaffordable — the caller decides whether to wait or skip.
        /// Pure and headless-testable (no Nez/scene access).
        /// </summary>
        public static ISkill SelectNextSkill(Hero hero, AutoLearnMode mode)
        {
            if (hero == null) return null;

            var skills = hero.Job.Skills;
            int count = skills.Count;

            if (mode == AutoLearnMode.Smart)
            {
                // Min by (smartRank, JPCost, job-list index). Unknown IDs fall back to int.MaxValue.
                ISkill best = null;
                int bestRank  = int.MaxValue;
                int bestCost  = int.MaxValue;
                int bestIndex = int.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    var s = skills[i];
                    if (hero.LearnedSkills.ContainsKey(s.Id)) continue;

                    int rank = GetSmartRank(s.Id);
                    if (rank < bestRank
                        || (rank == bestRank && s.JPCost < bestCost)
                        || (rank == bestRank && s.JPCost == bestCost && i < bestIndex))
                    {
                        best      = s;
                        bestRank  = rank;
                        bestCost  = s.JPCost;
                        bestIndex = i;
                    }
                }
                return best;
            }
            else if (mode == AutoLearnMode.Active)
            {
                // Cheapest unlearned active first; when all actives learned → cheapest unlearned passive.
                ISkill bestActive  = null;
                int    baCost      = int.MaxValue;
                int    baIndex     = int.MaxValue;
                ISkill bestPassive = null;
                int    bpCost      = int.MaxValue;
                int    bpIndex     = int.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    var s = skills[i];
                    if (hero.LearnedSkills.ContainsKey(s.Id)) continue;

                    if (s.Kind == SkillKind.Active)
                    {
                        if (s.JPCost < baCost || (s.JPCost == baCost && i < baIndex))
                        {
                            bestActive = s;
                            baCost     = s.JPCost;
                            baIndex    = i;
                        }
                    }
                    else
                    {
                        if (s.JPCost < bpCost || (s.JPCost == bpCost && i < bpIndex))
                        {
                            bestPassive = s;
                            bpCost      = s.JPCost;
                            bpIndex     = i;
                        }
                    }
                }
                return bestActive ?? bestPassive;
            }
            else // Passive
            {
                // Cheapest unlearned passive first; when all passives learned → cheapest unlearned active.
                ISkill bestPassive = null;
                int    bpCost      = int.MaxValue;
                int    bpIndex     = int.MaxValue;
                ISkill bestActive  = null;
                int    baCost      = int.MaxValue;
                int    baIndex     = int.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    var s = skills[i];
                    if (hero.LearnedSkills.ContainsKey(s.Id)) continue;

                    if (s.Kind == SkillKind.Passive)
                    {
                        if (s.JPCost < bpCost || (s.JPCost == bpCost && i < bpIndex))
                        {
                            bestPassive = s;
                            bpCost      = s.JPCost;
                            bpIndex     = i;
                        }
                    }
                    else
                    {
                        if (s.JPCost < baCost || (s.JPCost == baCost && i < baIndex))
                        {
                            bestActive = s;
                            baCost     = s.JPCost;
                            baIndex    = i;
                        }
                    }
                }
                return bestPassive ?? bestActive;
            }
        }

        /// <summary>
        /// Returns the smart-priority rank for a skill ID (0 = highest priority).
        /// Unknown IDs return <see cref="int.MaxValue"/> so they sort last but deterministically by
        /// cost and job-list index.
        /// </summary>
        public static int GetSmartRank(string skillId)
        {
            if (skillId == null) return int.MaxValue;
            int rank;
            if (_smartRankTable.TryGetValue(skillId, out rank))
                return rank;
            return int.MaxValue;
        }

        /// <summary>
        /// Clamps a raw persisted integer to a valid <see cref="AutoLearnMode"/>.
        /// Out-of-range values (negative or &gt; 2) become <see cref="AutoLearnMode.Smart"/>.
        /// </summary>
        public static AutoLearnMode SanitizeMode(int raw)
        {
            switch (raw)
            {
                case 0: return AutoLearnMode.Smart;
                case 1: return AutoLearnMode.Active;
                case 2: return AutoLearnMode.Passive;
                default: return AutoLearnMode.Smart;
            }
        }

        /// <summary>Emits a console line when a skill is learned. No-ops in headless test contexts.</summary>
        private static void EmitLearnConsole(string heroName, string skillName)
        {
            if (Core.Instance == null) return;
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleAutoLearnedSkill,
                (heroName, GameConfig.ConsoleColorHeroName),
                (skillName, Color.White));
        }
    }
}
