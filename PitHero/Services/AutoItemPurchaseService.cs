using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.Services.Analytics;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;

namespace PitHero.Services
{
    /// <summary>
    /// Buys gear and consumables back from the Second Chance shop right before the party jumps into
    /// the pit (issue #345). Call-driven (no update loop): <see cref="AI.JumpIntoPitAction"/> invokes
    /// <see cref="TryPurchasePass(HeroComponent)"/> on the frame the jump starts.
    ///
    /// Gear is only bought when it beats everything the party already possesses for that member and
    /// category — equipped or sitting in the bag — and only for rarities/categories the player left
    /// checked. Consumables are topped up to a per-item stack target. Every purchase honors the
    /// shared Gold Buffer and needs a free bag slot.
    /// </summary>
    public class AutoItemPurchaseService
    {
        private const string PurchaseSource = "auto_prepit";

        /// <summary>The five gear categories mapped onto the equipment slot each one occupies.</summary>
        private static readonly EquipmentSlot[] CategorySlots =
        {
            EquipmentSlot.WeaponShield1,   // Weapon
            EquipmentSlot.Hat,             // Helm
            EquipmentSlot.WeaponShield2,   // Shield
            EquipmentSlot.Armor,           // Armor
            EquipmentSlot.Accessory1       // Accessory (handled specially — both slots considered)
        };

        private readonly GameStateService _gameState;
        private readonly SecondChanceMerchantVault _vault;
        private readonly AutoSeedPurchaseService _goldBufferSource;

        // Reused across passes so a purchase pass allocates nothing during gameplay
        private readonly List<Mercenary> _members = new List<Mercenary>(2);
        private readonly List<IItem> _purchased = new List<IItem>(8);

        /// <summary>Master toggle. Off by default.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Purchase priority: false (default) buys gear before consumables, true reverses it.</summary>
        public bool ConsumablesFirst { get; set; } = false;

        /// <summary>Whether gear is also bought for hired mercenaries. Off by default.</summary>
        public bool PurchaseMercenaryGear { get; set; } = false;

        /// <summary>Whether gear of each rarity may be auto-purchased, indexed by ItemRarity. All true by default.</summary>
        public bool[] BuyRarityAllowed { get; } = new bool[5];

        /// <summary>Whether gear of each category may be auto-purchased, indexed by GearCategory. All true by default.</summary>
        public bool[] BuyGearTypeAllowed { get; } = new bool[GearCategoryUtils.Count];

        /// <summary>Which catalog consumables are auto-purchased, indexed by ConsumableCatalog index. All false by default.</summary>
        public bool[] ConsumableSelected { get; } = new bool[ConsumableCatalog.Count];

        /// <summary>How many stacks of each selected consumable to hold, indexed by ConsumableCatalog index. 1 by default.</summary>
        public int[] ConsumableStackTargets { get; } = new int[ConsumableCatalog.Count];

        /// <summary>Lowest and highest stack target the options slider offers.</summary>
        public const int MinStackTarget = 1;
        public const int MaxStackTarget = 3;

        /// <summary>
        /// Initialises the service. <paramref name="goldBufferSource"/> owns the single shared
        /// Gold Buffer setting, so this service must be registered after it.
        /// </summary>
        public AutoItemPurchaseService(GameStateService gameState, SecondChanceMerchantVault vault, AutoSeedPurchaseService goldBufferSource)
        {
            _gameState = gameState;
            _vault = vault;
            _goldBufferSource = goldBufferSource;

            for (int i = 0; i < BuyRarityAllowed.Length; i++)
                BuyRarityAllowed[i] = true;
            for (int i = 0; i < BuyGearTypeAllowed.Length; i++)
                BuyGearTypeAllowed[i] = true;
            for (int i = 0; i < ConsumableStackTargets.Length; i++)
                ConsumableStackTargets[i] = MinStackTarget;
        }

        /// <summary>Gold floor shared with seed auto-purchasing; no buy may take funds below it.</summary>
        public int GoldBuffer => _goldBufferSource?.GoldBuffer ?? 0;

        /// <summary>True when gear of the given rarity may be auto-purchased.</summary>
        public bool IsRarityAllowed(ItemRarity rarity)
        {
            int i = (int)rarity;
            return i < 0 || i >= BuyRarityAllowed.Length || BuyRarityAllowed[i];
        }

        /// <summary>True when gear of the given kind's category may be auto-purchased.</summary>
        public bool IsGearTypeAllowed(ItemKind kind)
        {
            return GearCategoryUtils.IsAllowed(BuyGearTypeAllowed, kind);
        }

        /// <summary>
        /// Runs one purchase pass for the party and auto-equips whatever was bought.
        /// Safe to call on every jump attempt: it is a no-op when disabled or when nothing qualifies.
        /// </summary>
        public void TryPurchasePass(HeroComponent heroComp)
        {
            if (!Enabled || heroComp?.LinkedHero == null || heroComp.Bag == null)
                return;

            _members.Clear();
            if (PurchaseMercenaryGear)
            {
                var hired = Core.Services?.GetService<MercenaryManager>()?.GetHiredMercenaries();
                if (hired != null)
                {
                    for (int i = 0; i < hired.Count; i++)
                    {
                        var mercComp = hired[i].GetComponent<MercenaryComponent>();
                        if (mercComp?.LinkedMercenary != null)
                            _members.Add(mercComp.LinkedMercenary);
                    }
                }
            }

            _purchased.Clear();
            RunPurchasePass(heroComp.LinkedHero, heroComp.Bag, _members, _purchased);

            if (_purchased.Count == 0)
                return;

            for (int i = 0; i < _purchased.Count; i++)
            {
                UnviewedGearTracker.MarkNew(_purchased[i]);
                PartyAutoEquipHelper.TryAutoEquipForParty(heroComp, _purchased[i]);
            }

            // The bag changed outside the inventory UI — refresh any open grid
            UI.InventorySelectionManager.OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Executes one purchase pass against the vault and appends every purchased item to
        /// <paramref name="purchasedOut"/>. Contains no Nez dependencies so tests can drive it
        /// directly. Returns the number of items bought.
        /// </summary>
        public int RunPurchasePass(Hero hero, ItemBag bag, IReadOnlyList<Mercenary> mercenaries, List<IItem> purchasedOut)
        {
            if (!Enabled || hero == null || bag == null || _gameState == null || _vault == null)
                return 0;

            int bought;
            if (ConsumablesFirst)
            {
                bought = BuyConsumables(bag, purchasedOut);
                bought += BuyGear(hero, bag, mercenaries, purchasedOut);
            }
            else
            {
                bought = BuyGear(hero, bag, mercenaries, purchasedOut);
                bought += BuyConsumables(bag, purchasedOut);
            }
            return bought;
        }

        /// <summary>Buys at most one upgrade per party member per gear category.</summary>
        private int BuyGear(Hero hero, ItemBag bag, IReadOnlyList<Mercenary> mercenaries, List<IItem> purchasedOut)
        {
            int bought = 0;

            for (int c = 0; c < GearCategoryUtils.Count; c++)
            {
                if (!BuyGearTypeAllowed[c])
                    continue;

                if (BuyBestForCategory(hero, null, bag, (GearCategory)c, purchasedOut))
                    bought++;

                if (mercenaries == null)
                    continue;

                for (int m = 0; m < mercenaries.Count; m++)
                {
                    if (BuyBestForCategory(null, mercenaries[m], bag, (GearCategory)c, purchasedOut))
                        bought++;
                }
            }

            return bought;
        }

        /// <summary>
        /// Buys the single best vault upgrade for one member and one category, or nothing.
        /// Exactly one of <paramref name="hero"/> / <paramref name="merc"/> is non-null.
        /// </summary>
        private bool BuyBestForCategory(Hero hero, Mercenary merc, ItemBag bag, GearCategory category, List<IItem> purchasedOut)
        {
            if (bag.Capacity - bag.Count < 1)
                return false;

            var baseline = GetBaselineGear(hero, merc, bag, category);

            SecondChanceMerchantVault.StackedItem bestStack = null;
            IGear bestGear = null;

            var stacks = _vault.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (stack == null || stack.Quantity <= 0)
                    continue;
                if (!(stack.ItemTemplate is IGear candidate))
                    continue;
                if (!GearCategoryUtils.TryGetCategory(candidate.Kind, out GearCategory candidateCategory) || candidateCategory != category)
                    continue;
                if (!IsRarityAllowed(candidate.Rarity))
                    continue;
                if (!CanMemberEquip(hero, merc, candidate))
                    continue;
                if (!GearAutoEquipService.IsNewGearBetter(candidate, baseline))
                    continue;
                if (!CanAfford(candidate.Price, 1))
                    continue;
                if (bestGear != null && !IsBetterCandidate(candidate, bestGear))
                    continue;

                bestStack = stack;
                bestGear = candidate;
            }

            if (bestGear == null)
                return false;

            int price = bestGear.Price;
            _gameState.Funds -= price;
            _vault.RemoveQuantity(bestStack, 1);
            bag.TryAdd(bestGear);
            purchasedOut?.Add(bestGear);

            AnalyticsService.LogItemPurchased(bestGear, 1, price, PurchaseSource, _gameState.Funds);
            EmitConsole(bestGear, price);
            Debug.Log($"[AutoItemPurchase] Bought {bestGear.Name} for {price}G");
            return true;
        }

        /// <summary>Best gear the party already has available to this member for this category (equipped or in the bag).</summary>
        private IGear GetBaselineGear(Hero hero, Mercenary merc, ItemBag bag, GearCategory category)
        {
            IGear baseline = null;

            // Accessories occupy two slots: an empty one means anything is an upgrade, so the
            // baseline stays null. Otherwise the weaker of the two is the one worth replacing.
            if (category == GearCategory.Accessory)
            {
                var acc1 = hero != null
                    ? GearAutoEquipService.GetHeroItemInSlot(hero, EquipmentSlot.Accessory1)
                    : GearAutoEquipService.GetMercItemInSlot(merc, EquipmentSlot.Accessory1);
                var acc2 = hero != null
                    ? GearAutoEquipService.GetHeroItemInSlot(hero, EquipmentSlot.Accessory2)
                    : GearAutoEquipService.GetMercItemInSlot(merc, EquipmentSlot.Accessory2);

                if (acc1 != null && acc2 != null)
                    baseline = GearAutoEquipService.IsNewGearBetter(acc1, acc2) ? acc2 : acc1;
            }
            else
            {
                var slot = CategorySlots[(int)category];
                baseline = hero != null
                    ? GearAutoEquipService.GetHeroItemInSlot(hero, slot)
                    : GearAutoEquipService.GetMercItemInSlot(merc, slot);
            }

            // Gear already carried in the bag counts as "possessed" — never buy a duplicate upgrade
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (!(bag.GetSlotItem(i) is IGear carried))
                    continue;
                if (!GearCategoryUtils.TryGetCategory(carried.Kind, out GearCategory carriedCategory) || carriedCategory != category)
                    continue;
                if (!CanMemberEquip(hero, merc, carried))
                    continue;
                if (GearAutoEquipService.IsNewGearBetter(carried, baseline))
                    baseline = carried;
            }

            return baseline;
        }

        private static bool CanMemberEquip(Hero hero, Mercenary merc, IGear gear)
        {
            if (hero != null)
                return hero.CanEquipItem(gear);
            return merc != null && merc.CanEquipItem(gear);
        }

        /// <summary>Ranks two affordable candidates: higher score, then resistances, then cheaper.</summary>
        private static bool IsBetterCandidate(IGear candidate, IGear current)
        {
            int candidateScore = GearAutoEquipService.GetGearScore(candidate);
            int currentScore = GearAutoEquipService.GetGearScore(current);
            if (candidateScore != currentScore)
                return candidateScore > currentScore;

            float candidateRes = GearAutoEquipService.GetElementalResistanceScore(candidate);
            float currentRes = GearAutoEquipService.GetElementalResistanceScore(current);
            if (candidateRes != currentRes)
                return candidateRes > currentRes;

            return candidate.Price < current.Price;
        }

        /// <summary>Tops each selected consumable up to its stack target, buying whole stacks from the vault.</summary>
        private int BuyConsumables(ItemBag bag, List<IItem> purchasedOut)
        {
            int bought = 0;
            for (int i = 0; i < ConsumableCatalog.Count; i++)
            {
                if (!ConsumableSelected[i])
                    continue;

                string spriteName = ConsumableCatalog.GetSpriteName(i);
                if (string.IsNullOrEmpty(spriteName))
                    continue;

                int target = ConsumableStackTargets[i];
                if (target < MinStackTarget) target = MinStackTarget;
                if (target > MaxStackTarget) target = MaxStackTarget;

                int owned = CountStacks(bag, spriteName);
                while (owned < target)
                {
                    if (!TryBuyOneStack(bag, spriteName, purchasedOut))
                        break;
                    owned++;
                    bought++;
                }
            }
            return bought;
        }

        /// <summary>Number of bag slots holding the given consumable (a partial stack still counts as one).</summary>
        private static int CountStacks(ItemBag bag, string spriteName)
        {
            int count = 0;
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is Consumable c && c.SpriteName == spriteName)
                    count++;
            }
            return count;
        }

        /// <summary>Buys as much of one stack of the consumable as the vault, gold buffer and bag space allow.</summary>
        private bool TryBuyOneStack(ItemBag bag, string spriteName, List<IItem> purchasedOut)
        {
            int freeIndex = FindEmptySlot(bag);
            if (freeIndex < 0)
                return false;

            SecondChanceMerchantVault.StackedItem stack = null;
            Consumable template = null;
            var stacks = _vault.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i]?.ItemTemplate is Consumable c && c.SpriteName == spriteName && stacks[i].Quantity > 0)
                {
                    stack = stacks[i];
                    template = c;
                    break;
                }
            }
            if (template == null || template.Price <= 0)
                return false;

            int qty = template.StackSize;
            if (qty > stack.Quantity)
                qty = stack.Quantity;

            int affordable = (_gameState.Funds - GoldBuffer) / template.Price;
            if (affordable < qty)
                qty = affordable;
            if (qty <= 0)
                return false;

            // Never hand out the vault's template instance — each bag stack owns its own StackCount
            var fresh = template.CreateFreshInstance();
            fresh.StackCount = qty;
            bag.SetSlotItem(freeIndex, fresh);

            int totalPrice = template.Price * qty;
            _gameState.Funds -= totalPrice;
            _vault.RemoveQuantity(stack, qty);
            purchasedOut?.Add(fresh);

            AnalyticsService.LogItemPurchased(fresh, qty, totalPrice, PurchaseSource, _gameState.Funds);
            EmitConsole(fresh, totalPrice);
            Debug.Log($"[AutoItemPurchase] Bought {fresh.Name} x{qty} for {totalPrice}G");
            return true;
        }

        private static int FindEmptySlot(ItemBag bag)
        {
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) == null)
                    return i;
            }
            return -1;
        }

        /// <summary>True when buying qty units at unitPrice leaves funds at or above the gold buffer.</summary>
        private bool CanAfford(int unitPrice, int qty)
        {
            return _gameState.Funds - (unitPrice * qty) >= GoldBuffer;
        }

        private static void EmitConsole(IItem item, int gold)
        {
            if (Core.Instance == null || item == null)
                return;
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleAutoPurchasedItem,
                (item.Name, RarityUtils.GetRarityColor(item.Rarity)),
                (gold.ToString(), Color.White));
        }
    }
}
