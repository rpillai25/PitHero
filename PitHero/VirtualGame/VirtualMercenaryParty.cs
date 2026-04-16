using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;
using System;
using System.Collections.Generic;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Result of a party dismissal operation, reporting where each gear piece was placed.
    /// </summary>
    public readonly struct DismissalResult
    {
        /// <summary>Number of gear items successfully added to the hero inventory.</summary>
        public int ItemsReturnedToInventory { get; }

        /// <summary>Number of gear items that overflowed into the Second Chance Vault.</summary>
        public int ItemsOverflowedToVault { get; }

        /// <summary>Name of the dismissed mercenary.</summary>
        public string MercenaryName { get; }

        /// <summary>Initializes a new DismissalResult.</summary>
        public DismissalResult(string mercenaryName, int itemsToInventory, int itemsToVault)
        {
            MercenaryName = mercenaryName;
            ItemsReturnedToInventory = itemsToInventory;
            ItemsOverflowedToVault = itemsToVault;
        }
    }

    /// <summary>
    /// Virtual Game Layer simulation of the mercenary party and tavern, used to test
    /// dismissal logic without ECS, graphics, or a live scene.
    /// Tracks hired mercenaries (max 2) and tavern mercenaries, and provides methods
    /// that mirror the two dismissal flows:
    ///   1. Party Dismissal  – dismiss a hired mercenary and return gear to inventory/vault.
    ///   2. Tavern Dismissal – dismiss a tavern mercenary, triggering a walk-out.
    /// </summary>
    public sealed class VirtualMercenaryParty
    {
        private const int MaxHiredMercenaries = 2;

        private readonly List<Mercenary> _hiredMercenaries;
        private readonly List<Mercenary> _tavernMercenaries;

        /// <summary>Read-only view of currently hired mercenaries (max 2).</summary>
        public IReadOnlyList<Mercenary> HiredMercenaries => _hiredMercenaries;

        /// <summary>Read-only view of mercenaries waiting in the tavern.</summary>
        public IReadOnlyList<Mercenary> TavernMercenaries => _tavernMercenaries;

        /// <summary>Number of hired mercenaries.</summary>
        public int HiredCount => _hiredMercenaries.Count;

        /// <summary>Number of mercenaries waiting in the tavern.</summary>
        public int TavernCount => _tavernMercenaries.Count;

        /// <summary>Initializes an empty party with no hired or tavern mercenaries.</summary>
        public VirtualMercenaryParty()
        {
            _hiredMercenaries = new List<Mercenary>(MaxHiredMercenaries);
            _tavernMercenaries = new List<Mercenary>(9);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Hiring

        /// <summary>
        /// Returns true when a mercenary can still be hired (fewer than 2 are currently hired).
        /// </summary>
        public bool CanHire() => _hiredMercenaries.Count < MaxHiredMercenaries;

        /// <summary>
        /// Moves a mercenary from the tavern into the hired party.
        /// Returns false when the party is already full or the mercenary is not found in the tavern.
        /// </summary>
        public bool HireFromTavern(Mercenary mercenary)
        {
            if (mercenary == null) throw new ArgumentNullException(nameof(mercenary));
            if (!CanHire()) return false;

            int index = _tavernMercenaries.IndexOf(mercenary);
            if (index < 0) return false;

            _tavernMercenaries.RemoveAt(index);
            _hiredMercenaries.Add(mercenary);
            return true;
        }

        /// <summary>
        /// Adds a mercenary directly to the hired party (used when loading save data or test setup).
        /// Returns false when the party is already full.
        /// </summary>
        public bool AddHired(Mercenary mercenary)
        {
            if (mercenary == null) throw new ArgumentNullException(nameof(mercenary));
            if (!CanHire()) return false;
            _hiredMercenaries.Add(mercenary);
            return true;
        }

        /// <summary>
        /// Adds a mercenary to the tavern (used for test setup or simulated spawning).
        /// </summary>
        public void AddToTavern(Mercenary mercenary)
        {
            if (mercenary == null) throw new ArgumentNullException(nameof(mercenary));
            _tavernMercenaries.Add(mercenary);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Party Dismissal

        /// <summary>
        /// Simulates the Party Dismissal flow:
        /// removes the hired mercenary at <paramref name="hiredIndex"/>, collects all gear
        /// from their equipment slots, then attempts to place each item into
        /// <paramref name="heroInventory"/>. Any item that does not fit overflows into
        /// <paramref name="vault"/>. Returns a <see cref="DismissalResult"/> describing what happened.
        /// </summary>
        /// <param name="hiredIndex">Zero-based index into <see cref="HiredMercenaries"/>.</param>
        /// <param name="heroInventory">Hero inventory to receive the mercenary's gear.</param>
        /// <param name="vault">Second Chance Vault for overflow items.</param>
        public DismissalResult DismissHiredMercenary(
            int hiredIndex,
            Inventory heroInventory,
            SecondChanceMerchantVault vault)
        {
            if (heroInventory == null) throw new ArgumentNullException(nameof(heroInventory));
            if (vault == null) throw new ArgumentNullException(nameof(vault));
            if (hiredIndex < 0 || hiredIndex >= _hiredMercenaries.Count)
                throw new ArgumentOutOfRangeException(nameof(hiredIndex));

            Mercenary merc = _hiredMercenaries[hiredIndex];
            string mercName = merc.Name;

            // Collect all gear from the six equipment slots
            IGear[] gear = CollectGear(merc);

            // Unequip every slot so the mercenary has no gear after dismissal
            merc.Unequip(EquipmentSlot.WeaponShield1);
            merc.Unequip(EquipmentSlot.Armor);
            merc.Unequip(EquipmentSlot.Hat);
            merc.Unequip(EquipmentSlot.WeaponShield2);
            merc.Unequip(EquipmentSlot.Accessory1);
            merc.Unequip(EquipmentSlot.Accessory2);

            // Return gear to hero inventory, overflow to vault
            int toInventory = 0;
            int toVault = 0;

            for (int i = 0; i < gear.Length; i++)
            {
                if (gear[i] == null) continue;

                if (heroInventory.TryAdd(gear[i]))
                {
                    toInventory++;
                }
                else
                {
                    vault.AddItem(gear[i]);
                    toVault++;
                }
            }

            _hiredMercenaries.RemoveAt(hiredIndex);

            Nez.Debug.Log(
                $"[VirtualMercenaryParty] Dismissed hired mercenary '{mercName}': " +
                $"{toInventory} items to inventory, {toVault} items to vault.");

            return new DismissalResult(mercName, toInventory, toVault);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Tavern Dismissal

        /// <summary>
        /// Simulates the Tavern Dismissal flow: removes the tavern mercenary at
        /// <paramref name="tavernIndex"/> (the walk-out animation would be triggered here
        /// in the real ECS layer). No gear is returned to the hero.
        /// </summary>
        /// <param name="tavernIndex">Zero-based index into <see cref="TavernMercenaries"/>.</param>
        public void DismissTavernMercenary(int tavernIndex)
        {
            if (tavernIndex < 0 || tavernIndex >= _tavernMercenaries.Count)
                throw new ArgumentOutOfRangeException(nameof(tavernIndex));

            Mercenary merc = _tavernMercenaries[tavernIndex];
            _tavernMercenaries.RemoveAt(tavernIndex);

            Nez.Debug.Log(
                $"[VirtualMercenaryParty] Dismissed tavern mercenary '{merc.Name}' (walk-out triggered).");
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Context menu availability

        /// <summary>
        /// Returns true when the tavern context menu should display a "Dismiss" option.
        /// Per the feature spec the context menu is always shown for tavern mercenaries,
        /// regardless of how many are currently hired.
        /// </summary>
        public bool ShouldShowTavernContextMenu() => true;

        /// <summary>
        /// Returns true when the "Hire" option should be visible inside the tavern
        /// context menu (only when fewer than 2 mercenaries are hired).
        /// </summary>
        public bool ShouldShowHireOption() => CanHire();

        // ──────────────────────────────────────────────────────────────────────────────
        // Helpers

        /// <summary>
        /// Collects all currently equipped gear items from a mercenary into a fixed-size array.
        /// Null entries represent empty slots.
        /// </summary>
        private static IGear[] CollectGear(Mercenary merc)
        {
            return new IGear[]
            {
                merc.WeaponShield1,
                merc.Armor,
                merc.Hat,
                merc.WeaponShield2,
                merc.Accessory1,
                merc.Accessory2
            };
        }
    }
}
