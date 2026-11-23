namespace PitHero.Util
{
    /// <summary>
    /// Battle-skill effect animations in SkillsStencils.atlas (issue #458). The atlas animation name is
    /// "Skill" + the member name. Presentation-only: no gameplay reads it.
    /// </summary>
    public enum SkillEffect : byte
    {
        None = 0,
        /// <summary>Knight Spin Slash: two arcs spinning over the enemy group.</summary>
        SpinSlash,
        /// <summary>Knight Heavy Strike: crossing slashes into a burst on the target.</summary>
        HeavyStrike,
        /// <summary>Knight Provoke: a red rage aura on the knight.</summary>
        Provoke,
        /// <summary>Monk Roundhouse: a wind gust burst over the enemy group.</summary>
        Roundhouse,
        /// <summary>Monk Flaming Fist: a fire burst on the target.</summary>
        FlamingFist,
        /// <summary>Thief Sneak Attack: a dark burst and slash on the target.</summary>
        SneakAttack,
        /// <summary>Thief Vanish: a smoke poof on the thief.</summary>
        Vanish,
        /// <summary>Priest Defense Up: shield arcs and sparkles on the buffed ally.</summary>
        DefenseUp,
        /// <summary>Archer Power Shot: an impact streak at the target after the arrow lands.</summary>
        ShotImpact,
        /// <summary>Mage Fire: the fireball in flight (points right; rotated toward the target).</summary>
        Fireball,
        /// <summary>Mage Fire: the blast where the fireball lands.</summary>
        FireBlast,
        /// <summary>Mage Firestorm: fiery bursts raining onto the enemy group.</summary>
        MeteorShower,
        /// <summary>Skill heals (Priest Heal and the synergy heals): a green swirl on the healed ally.</summary>
        Heal,

        // ── Synergy skills ────────────────────────────────────────────────────
        /// <summary>Holy Strike: a short holy streak toward the target.</summary>
        HolyStrike,
        /// <summary>Iaido Slash: a thin white arc toward the target.</summary>
        Iaido,
        /// <summary>Flash Strike: a fast white streak toward the target; also Spin Slash's per-target hit.</summary>
        FlashStrike,
        /// <summary>Shadow Slash: a dark crescent wave toward the target.</summary>
        ShadowSlash,
        /// <summary>Sacred Blade: a holy crescent wave toward the target.</summary>
        SacredBlade,
        /// <summary>Spellblade: a fire crescent toward the target.</summary>
        Spellblade,
        /// <summary>Dragon Claw: a fiery claw cut on the target.</summary>
        DragonClaw,
        /// <summary>Sacred Strike and Lightshot's impact: a holy burst on the target.</summary>
        HolyBurst,
        /// <summary>Ki burst: Energy Burst (group and each target) and Ki Arrow's impact.</summary>
        KiBurst,
        /// <summary>Meteor: one big flaming meteor over the enemy group.</summary>
        Meteor,
        /// <summary>Shadow Bolt: a dark bolt in flight (points right; rotated toward the target).</summary>
        ShadowBolt,
        /// <summary>Blitz: a small wind tornado on the target.</summary>
        Blitz,
        /// <summary>Life Leech: a flash and red drain wisps on the target.</summary>
        LifeLeech,
        /// <summary>Fade: a flash into purple puffs on the caster.</summary>
        Fade,
        /// <summary>Ki Cloak: cyan ki rings on the caster.</summary>
        KiCloak,
        /// <summary>Poison Arrow's impact: a green poison splash.</summary>
        PoisonHit,
        /// <summary>Elemental Storm: fire bursts over a wind swirl on the enemy group.</summary>
        ElementalStorm,
        /// <summary>Flames bursting up a monster hit by a fire area skill (Firestorm, Meteor, Elemental Storm).</summary>
        Burn,
    }
}
