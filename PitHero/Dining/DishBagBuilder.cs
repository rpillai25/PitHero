using System.Collections.Generic;
using RolePlayingFramework.Utils;

namespace PitHero.Dining
{
    /// <summary>
    /// Builds the walk-in patron dish shuffle bag (#382, reweighted in #417): every dish weighted
    /// inversely to its menu price, clamped to <see cref="GameConfig.DishBagMaxMarbles"/>, so cheap
    /// dishes are ordered a little more often but the whole unlocked menu cycles through. Shared by
    /// the live kitchen coordinator and the headless economy simulation.
    /// </summary>
    public static class DishBagBuilder
    {
        /// <summary>Marbles a dish gets in the bag: clamp(round(maxPrice / price), 1, DishBagMaxMarbles).</summary>
        public static int GetMarbles(DishType dish, int maxPrice)
        {
            int marbles = (int)System.Math.Round((float)maxPrice / DishConfig.GetPrice(dish));
            if (marbles < 1) marbles = 1;
            if (marbles > GameConfig.DishBagMaxMarbles) marbles = GameConfig.DishBagMaxMarbles;
            return marbles;
        }

        /// <summary>The priciest menu price, the reference every other dish is weighted against.</summary>
        public static int GetMaxPrice()
        {
            int maxPrice = 0;
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                int price = DishConfig.GetPrice((DishType)d);
                if (price > maxPrice) maxPrice = price;
            }
            return maxPrice;
        }

        /// <summary>Builds a full-menu bag (every dish; unorderable draws are skipped by the caller).</summary>
        public static ShuffleBag<DishType> BuildFullMenu()
        {
            int maxPrice = GetMaxPrice();
            var bag = new ShuffleBag<DishType>(DishTypeInfo.Count * GameConfig.DishBagMaxMarbles);
            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var dish = (DishType)d;
                bag.Add(dish, GetMarbles(dish, maxPrice));
            }
            return bag;
        }

        /// <summary>Builds a bag over only the given dishes (the economy simulation's unlocked menu).</summary>
        public static ShuffleBag<DishType> Build(List<DishType> dishes)
        {
            int maxPrice = GetMaxPrice();
            var bag = new ShuffleBag<DishType>(dishes.Count * GameConfig.DishBagMaxMarbles);
            for (int i = 0; i < dishes.Count; i++)
                bag.Add(dishes[i], GetMarbles(dishes[i], maxPrice));
            return bag;
        }
    }
}
