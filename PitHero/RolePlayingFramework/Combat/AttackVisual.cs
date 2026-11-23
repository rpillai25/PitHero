using RolePlayingFramework.Equipment;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// The animation a physical attack plays (issue #458). Presentation-only: it never changes damage,
    /// RNG or any other battle result, and it is not saved (gear rebuilds it from its factory).
    /// </summary>
    public enum AttackVisual : byte
    {
        /// <summary>No attack animation.</summary>
        None = 0,
        /// <summary>Sword slash arc in front of the attacker.</summary>
        Slash = 1,
        /// <summary>Knife stab: a short straight slash.</summary>
        Stab = 2,
        /// <summary>Spear thrust.</summary>
        Thrust = 3,
        /// <summary>An arrow flying from the attacker to the target (ranged: mercenaries stay in place).</summary>
        Arrow = 4,
        /// <summary>Axe swing.</summary>
        AxeSwing = 5,
        /// <summary>Hammer blow.</summary>
        Hammer = 6,
        /// <summary>Unarmed / knuckle fist punch with a wind gust.</summary>
        Punch = 7,
        /// <summary>Rod: the attacker holds a rod up and an energy bolt flies from its tip to the target (ranged).</summary>
        Rod = 8,
        /// <summary>Staff swing: a wooden smear arc in front of the attacker.</summary>
        Staff = 9,
    }

    /// <summary>Resolves which <see cref="AttackVisual"/> a weapon plays.</summary>
    public static class AttackVisuals
    {
        /// <summary>
        /// The visual for a physical attack made with <paramref name="weapon"/> (null = unarmed).
        /// A gear's own override wins (spears and axes are tagged as swords); otherwise the item kind decides.
        /// </summary>
        public static AttackVisual For(IItem weapon)
        {
            if (weapon == null)
                return AttackVisual.Punch;
            if (weapon is Gear gear && gear.AttackVisual != AttackVisual.None)
                return gear.AttackVisual;

            switch (weapon.Kind)
            {
                case ItemKind.WeaponSword: return AttackVisual.Slash;
                case ItemKind.WeaponKnife: return AttackVisual.Stab;
                case ItemKind.WeaponKnuckle: return AttackVisual.Punch;
                case ItemKind.WeaponBow: return AttackVisual.Arrow;
                case ItemKind.WeaponHammer: return AttackVisual.Hammer;
                case ItemKind.WeaponStaff: return AttackVisual.Staff;
                case ItemKind.WeaponRod: return AttackVisual.Rod;
                default:
                    // A shield or other non-weapon in the weapon hand fights bare-handed
                    return AttackVisual.Punch;
            }
        }

        /// <summary>True when the attacker fights from where it stands (bows and rods); every other attack is melee.</summary>
        public static bool IsRanged(AttackVisual visual) => visual == AttackVisual.Arrow || visual == AttackVisual.Rod;
    }
}
