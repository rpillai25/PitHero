using PitHero.Farming;
using PitHero.Util;
using RolePlayingFramework.Combat;

namespace PitHero.Dining
{
    /// <summary>
    /// Static metadata and formulas for all dishes (issue #319). Pure static — no service
    /// dependencies — so pricing and timing stay testable headless. Menu prices derive from
    /// the crop sell formula (issue #287): rebalance crops and the menu reprices itself.
    /// </summary>
    public static class DishConfig
    {
        private static readonly DishDefinition[] _definitions;
        private static readonly int[] _priceCache;

        static DishConfig()
        {
            _definitions = new DishDefinition[DishTypeInfo.Count];

            _definitions[(int)DishType.RoastedOnionSkewers] = new DishDefinition(
                DishType.RoastedOnionSkewers,
                new[] { new RecipeEntry(CropType.Onion, 3) },
                false, false,
                new[] { new DishBuffEntry(BuffType.AttackUp, 1) },
                CookTimeClass.Simple, EatTimeClass.Snack,
                "RoastedOnionSkewers", UITextKey.DishRoastedOnionSkewers);

            _definitions[(int)DishType.TurnipOnionStew] = new DishDefinition(
                DishType.TurnipOnionStew,
                new[] { new RecipeEntry(CropType.Turnip, 3), new RecipeEntry(CropType.Onion, 1) },
                false, false,
                new[] { new DishBuffEntry(BuffType.HPRegen, 1) },
                CookTimeClass.Simple, EatTimeClass.Meal,
                "TurnipOnionStew", UITextKey.DishTurnipOnionStew);

            _definitions[(int)DishType.ButteredBread] = new DishDefinition(
                DishType.ButteredBread,
                new[] { new RecipeEntry(CropType.Wheat, 1) },
                true, false,
                // Was restore-only; food no longer heals, so the cheapest snack gets a token buff
                new[] { new DishBuffEntry(BuffType.DefenseUp, 1) },
                CookTimeClass.Simple, EatTimeClass.Snack,
                "ButteredBread", UITextKey.DishButteredBread);

            _definitions[(int)DishType.CheesyMashedPotatoes] = new DishDefinition(
                DishType.CheesyMashedPotatoes,
                new[] { new RecipeEntry(CropType.Potato, 2) },
                true, true,
                new[] { new DishBuffEntry(BuffType.DefenseUp, 2) },
                CookTimeClass.Simple, EatTimeClass.Meal,
                "CheesyMashedPotatoes", UITextKey.DishCheesyMashedPotatoes);

            _definitions[(int)DishType.GardenSalad] = new DishDefinition(
                DishType.GardenSalad,
                new[] { new RecipeEntry(CropType.Lettuce, 2), new RecipeEntry(CropType.Tomato, 2) },
                false, false,
                new[] { new DishBuffEntry(BuffType.AgilityUp, 2) },
                CookTimeClass.Simple, EatTimeClass.Snack,
                "GardenSalad", UITextKey.DishGardenSalad);

            _definitions[(int)DishType.GrilledCornWithButter] = new DishDefinition(
                DishType.GrilledCornWithButter,
                new[] { new RecipeEntry(CropType.Corn, 4) },
                true, false,
                new[] { new DishBuffEntry(BuffType.AttackUp, 2) },
                CookTimeClass.Standard, EatTimeClass.Snack,
                "GrilledCornWithButter", UITextKey.DishGrilledCornWithButter);

            _definitions[(int)DishType.TomatoCheeseBisque] = new DishDefinition(
                DishType.TomatoCheeseBisque,
                new[] { new RecipeEntry(CropType.Tomato, 5) },
                true, true,
                new[] { new DishBuffEntry(BuffType.EvasionUp, 10) },
                CookTimeClass.Standard, EatTimeClass.Meal,
                "TomatoCheeseBisque", UITextKey.DishTomatoCheeseBisque);

            _definitions[(int)DishType.CornChowder] = new DishDefinition(
                DishType.CornChowder,
                new[] { new RecipeEntry(CropType.Corn, 2), new RecipeEntry(CropType.Potato, 1), new RecipeEntry(CropType.Onion, 1) },
                true, false,
                new[] { new DishBuffEntry(BuffType.HPRegen, 2) },
                CookTimeClass.Standard, EatTimeClass.Meal,
                "CornChowder", UITextKey.DishCornChowder);

            _definitions[(int)DishType.EggplantParmesan] = new DishDefinition(
                DishType.EggplantParmesan,
                new[] { new RecipeEntry(CropType.Eggplant, 1), new RecipeEntry(CropType.Tomato, 3), new RecipeEntry(CropType.Wheat, 1) },
                false, true,
                new[] { new DishBuffEntry(BuffType.DefenseUp, 3) },
                CookTimeClass.Standard, EatTimeClass.Meal,
                "EggplantParmesan", UITextKey.DishEggplantParmesan);

            _definitions[(int)DishType.GrapeJuice] = new DishDefinition(
                DishType.GrapeJuice,
                new[] { new RecipeEntry(CropType.Grapes, 2), new RecipeEntry(CropType.Sugarcane, 1) },
                false, false,
                new[] { new DishBuffEntry(BuffType.MPRegen, 1) },
                CookTimeClass.Simple, EatTimeClass.Snack,
                "GrapeJuice", UITextKey.DishGrapeJuice);

            _definitions[(int)DishType.GrapeTart] = new DishDefinition(
                DishType.GrapeTart,
                new[] { new RecipeEntry(CropType.Grapes, 1), new RecipeEntry(CropType.Wheat, 1), new RecipeEntry(CropType.Sugarcane, 1) },
                false, false,
                new[] { new DishBuffEntry(BuffType.MagicUp, 6) },
                CookTimeClass.Standard, EatTimeClass.Snack,
                "GrapeTart", UITextKey.DishGrapeTart);

            _definitions[(int)DishType.SpicedEggplantSteak] = new DishDefinition(
                DishType.SpicedEggplantSteak,
                new[] { new RecipeEntry(CropType.Eggplant, 4), new RecipeEntry(CropType.Onion, 2) },
                false, true,
                new[] { new DishBuffEntry(BuffType.AttackUp, 3) },
                CookTimeClass.Complex, EatTimeClass.Meal,
                "SpicedEggplantSteak", UITextKey.DishSpicedEggplantSteak);

            _definitions[(int)DishType.ApplePie] = new DishDefinition(
                DishType.ApplePie,
                new[] { new RecipeEntry(CropType.AppleTree, 4), new RecipeEntry(CropType.Wheat, 2), new RecipeEntry(CropType.Sugarcane, 1) },
                true, false,
                new[] { new DishBuffEntry(BuffType.AttackUp, 2), new DishBuffEntry(BuffType.DefenseUp, 2) },
                CookTimeClass.Complex, EatTimeClass.Meal,
                "ApplePie", UITextKey.DishApplePie);

            _definitions[(int)DishType.PumpkinCreamSoup] = new DishDefinition(
                DishType.PumpkinCreamSoup,
                new[] { new RecipeEntry(CropType.Pumpkin, 1), new RecipeEntry(CropType.Onion, 1) },
                true, false,
                new[] { new DishBuffEntry(BuffType.DefenseUp, 4) },
                CookTimeClass.Standard, EatTimeClass.Meal,
                "PumpkinCreamSoup", UITextKey.DishPumpkinCreamSoup);

            _definitions[(int)DishType.ChilledWatermelonSorbet] = new DishDefinition(
                DishType.ChilledWatermelonSorbet,
                new[] { new RecipeEntry(CropType.Watermelon, 1), new RecipeEntry(CropType.Sugarcane, 2) },
                true, false,
                new[] { new DishBuffEntry(BuffType.MPRegen, 2) },
                CookTimeClass.Standard, EatTimeClass.Snack,
                "ChilledWatermelonSorbet", UITextKey.DishChilledWatermelonSorbet);

            _definitions[(int)DishType.HarvestFeastPlatter] = new DishDefinition(
                DishType.HarvestFeastPlatter,
                new[]
                {
                    new RecipeEntry(CropType.Pumpkin, 1),
                    new RecipeEntry(CropType.Turnip, 1),
                    new RecipeEntry(CropType.Lettuce, 1),
                    new RecipeEntry(CropType.Onion, 1),
                    new RecipeEntry(CropType.Potato, 1),
                    new RecipeEntry(CropType.Corn, 1),
                    new RecipeEntry(CropType.Tomato, 1),
                    new RecipeEntry(CropType.Eggplant, 1),
                    new RecipeEntry(CropType.Grapes, 1),
                    new RecipeEntry(CropType.Wheat, 2),
                    new RecipeEntry(CropType.Sugarcane, 1),
                },
                true, true,
                new[]
                {
                    new DishBuffEntry(BuffType.AttackUp, 1),
                    new DishBuffEntry(BuffType.DefenseUp, 1),
                    new DishBuffEntry(BuffType.AgilityUp, 1),
                    new DishBuffEntry(BuffType.MagicUp, 3),
                    new DishBuffEntry(BuffType.HPRegen, 1),
                    new DishBuffEntry(BuffType.MPRegen, 1),
                },
                CookTimeClass.Complex, EatTimeClass.Feast,
                "HarvestFeastPlatter", UITextKey.DishHarvestFeastPlatter);

            // Menu prices derive from crop sell values, so compute them once up front
            _priceCache = new int[DishTypeInfo.Count];
            for (int i = 0; i < DishTypeInfo.Count; i++)
                _priceCache[i] = ComputePrice(_definitions[i]);

            EnforceEffectMonotonicity();
        }

        /// <summary>
        /// Guarantees that among single-buff dishes sharing a buff type, a strictly higher
        /// magnitude always costs strictly more — ingredient values alone can't be trusted for
        /// this (cheap crops can carry strong buffs). Runs to a fixed point on the price cache.
        /// </summary>
        private static void EnforceEffectMonotonicity()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < _definitions.Length; i++)
                {
                    var weaker = _definitions[i];
                    if (weaker.Buffs.Length != 1)
                        continue;
                    for (int j = 0; j < _definitions.Length; j++)
                    {
                        var stronger = _definitions[j];
                        if (stronger.Buffs.Length != 1 || stronger.Buffs[0].Type != weaker.Buffs[0].Type)
                            continue;
                        if (stronger.Buffs[0].Magnitude > weaker.Buffs[0].Magnitude
                            && _priceCache[j] <= _priceCache[i])
                        {
                            _priceCache[j] = _priceCache[i] + GameConfig.DishPriceRoundTo;
                            changed = true;
                        }
                    }
                }
            }
        }

        /// <summary>Returns the static definition for a dish.</summary>
        public static DishDefinition GetDefinition(DishType dish) => _definitions[(int)dish];

        /// <summary>Menu price in gold: ingredient sell value x markup, rounded to nearest 5, min 10.</summary>
        public static int GetPrice(DishType dish) => _priceCache[(int)dish];

        private static int ComputePrice(DishDefinition def)
        {
            float ingredientValue = 0f;
            for (int i = 0; i < def.Recipe.Length; i++)
                ingredientValue += CropConfig.GetHarvestUnitSellPrice(def.Recipe[i].Crop) * def.Recipe[i].Qty;

            // Effect premium: better buffs always cost more, independent of ingredient value
            // (without this, cheap-ingredient strong dishes could undercut weaker ones)
            int effectPremium = 0;
            for (int b = 0; b < def.Buffs.Length; b++)
            {
                int perPoint;
                switch (def.Buffs[b].Type)
                {
                    case BuffType.MagicUp:   perPoint = GameConfig.DishBuffMagicGoldPerPoint;   break;
                    case BuffType.EvasionUp: perPoint = GameConfig.DishBuffEvasionGoldPerPoint; break;
                    case BuffType.HPRegen:
                    case BuffType.MPRegen:   perPoint = GameConfig.DishBuffRegenGoldPerPoint;   break;
                    default:                 perPoint = GameConfig.DishBuffStatGoldPerPoint;    break;
                }
                effectPremium += perPoint * def.Buffs[b].Magnitude;
            }

            float marked = ingredientValue * GameConfig.DishPriceMarkup + effectPremium;
            int rounded = (int)System.Math.Round(marked / GameConfig.DishPriceRoundTo,
                System.MidpointRounding.AwayFromZero) * GameConfig.DishPriceRoundTo;
            return rounded < GameConfig.DishPriceMin ? GameConfig.DishPriceMin : rounded;
        }

        /// <summary>Base cook time in real seconds (= in-game minutes) by dish complexity.</summary>
        public static float GetCookBaseSeconds(DishType dish)
        {
            switch (_definitions[(int)dish].CookClass)
            {
                case CookTimeClass.Simple: return GameConfig.CookSimpleBaseSeconds;
                case CookTimeClass.Standard: return GameConfig.CookStandardBaseSeconds;
                default: return GameConfig.CookComplexBaseSeconds;
            }
        }

        /// <summary>Cook duration for a cook of the given proficiency, floored at CookDurationFloorSeconds.</summary>
        public static float GetCookDuration(DishType dish, int cookingProficiency)
        {
            float scale = 1f - GameConfig.CookProficiencySpeedStep * (cookingProficiency - 1);
            float duration = GetCookBaseSeconds(dish) * scale;
            return duration < GameConfig.CookDurationFloorSeconds ? GameConfig.CookDurationFloorSeconds : duration;
        }

        /// <summary>Eat duration in real seconds (= in-game minutes) by dish size.</summary>
        public static float GetEatSeconds(DishType dish)
        {
            switch (_definitions[(int)dish].EatClass)
            {
                case EatTimeClass.Snack: return GameConfig.EatSnackSeconds;
                case EatTimeClass.Meal: return GameConfig.EatMealSeconds;
                default: return GameConfig.EatFeastSeconds;
            }
        }

        /// <summary>Chance a cook of the given proficiency produces a Deluxe dish.</summary>
        public static float GetDeluxeChance(int cookingProficiency)
        {
            float chance = cookingProficiency * GameConfig.DeluxeChancePerProficiency;
            return chance > GameConfig.DeluxeChanceMax ? GameConfig.DeluxeChanceMax : chance;
        }

        /// <summary>Buff magnitude after the deluxe multiplier (+50%, rounded up).</summary>
        public static int GetDeluxeMagnitude(int magnitude)
        {
            return (int)System.Math.Ceiling(magnitude * GameConfig.DeluxeMagnitudeMultiplier);
        }

        /// <summary>Predetermined favorite dish for a mercenary job class.</summary>
        public static DishType GetFavoriteForJob(string jobName)
        {
            switch (jobName)
            {
                case "Knight": return DishType.PumpkinCreamSoup;
                case "Mage": return DishType.GrapeTart;
                case "Priest": return DishType.ChilledWatermelonSorbet;
                case "Thief": return DishType.GardenSalad;
                case "Monk": return DishType.SpicedEggplantSteak;
                case "Archer": return DishType.GrilledCornWithButter;
                default: return DishType.RoastedOnionSkewers;
            }
        }

        /// <summary>
        /// Cheap fallback dishes per job class, tried in order (index 0 then 1) when the
        /// favorite can't be made or afforded — the party member still eats something on-theme
        /// rather than skipping the meal.
        /// </summary>
        public static DishType GetFallbackForJob(string jobName, int fallbackIndex)
        {
            switch (jobName)
            {
                case "Knight": return fallbackIndex == 0 ? DishType.CheesyMashedPotatoes : DishType.ButteredBread;
                case "Mage":   return fallbackIndex == 0 ? DishType.GrapeJuice : DishType.ButteredBread;
                case "Priest": return fallbackIndex == 0 ? DishType.GrapeJuice : DishType.TurnipOnionStew;
                case "Thief":  return fallbackIndex == 0 ? DishType.TomatoCheeseBisque : DishType.RoastedOnionSkewers;
                case "Monk":   return fallbackIndex == 0 ? DishType.GrilledCornWithButter : DishType.RoastedOnionSkewers;
                case "Archer": return fallbackIndex == 0 ? DishType.RoastedOnionSkewers : DishType.GardenSalad;
                default:       return fallbackIndex == 0 ? DishType.ButteredBread : DishType.TurnipOnionStew;
            }
        }
    }
}
