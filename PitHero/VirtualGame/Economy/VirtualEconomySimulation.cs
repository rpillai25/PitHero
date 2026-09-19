using System.Collections.Generic;
using PitHero.Config;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework.Utils;

namespace PitHero.VirtualGame.Economy
{
    /// <summary>
    /// Headless farm + kitchen economy simulation (issue #417). Steps one in-game minute (= one
    /// real second) at a time and reuses the real pricing, progression and market code —
    /// <see cref="CropConfig"/>, <see cref="CropUnlockConfig"/>, <see cref="DishConfig"/>,
    /// <see cref="DishUnlockConfig"/>, <see cref="CropMarketService"/>, <see cref="GameStateService"/>,
    /// <see cref="CropStorageInventoryService"/>, <see cref="AutoCropSellService"/> and
    /// <see cref="DishBagBuilder"/> — while modelling only what needs a Nez scene: crop growth
    /// (the exact wet-gated math of <c>CropGrowthService.Update</c>), farm workers (water →
    /// harvest → plant, base durations from <see cref="GameConfig"/> plus a flat travel overhead),
    /// the seed auto-purchase rule, and the tavern (arrivals per <see cref="TavernScheduleConfig"/>,
    /// nine seats, cook/server throughput, tips, the party's three meals). No pathing, no fridge
    /// pre-stock, no worker sleep beyond the 6 AM–10 PM day shift. One <see cref="System.Random"/>
    /// owned by the run; never <c>Nez.Random</c>.
    /// </summary>
    public class VirtualEconomySimulation
    {
        private const int MinutesPerDay = 24 * 60;
        private const int DayStartHour = 6;
        private const int WorkerWakeHour = 6;
        private const int WorkerSleepHour = 22;
        private const int TavernSeats = 9;
        private const int StorageBuildingIdBase = 100;
        private const int StepsPerRealHour = 3600;
        private const int StepsPerRealMinute = 60;

        private enum TaskKind { None, Water, Harvest, Plant }

        private struct Plot
        {
            public CropType Type;
            public bool Planted;
            public bool Wet;
            public bool Grown;
            public bool Claimed;
            public float AccumulatedHours;
            public float RegrowMultiplier;
        }

        private struct Worker
        {
            public int BusyUntil;
            public TaskKind Task;
            public int PlotIndex;
            public int Charges;
        }

        private enum PatronState { Waiting, Ordered, Cooking, Eating, Lingering }

        private class Patron
        {
            public PatronState State;
            public int Timer;       // patience while waiting, otherwise the minute the current phase ends
            public DishType Dish;
        }

        private EconomyScenario _scenario;
        private EconomyRunMetrics _metrics;
        private System.Random _rng;
        private GameStateService _gameState;
        private CropMarketService _market;
        private BuildingService _buildings;
        private CropStorageInventoryService _storage;
        private AutoCropSellService _autoSell;
        private readonly List<int> _storageIds = new List<int>(8);

        private Plot[] _plots;
        private Worker[] _workers;
        private int[] _seeds;
        private float _workerSpeed;      // water/plant duration multiplier for the worker level
        private float _harvestDuration;  // seconds a worker of that level spends picking one crop

        private readonly List<Patron> _patrons = new List<Patron>(TavernSeats);
        private int[] _cookBusyUntil;
        private int[] _serverBusyUntil;
        private float _nextArrivalSeconds;
        private ShuffleBag<DishType> _dishBag;
        private readonly List<DishType> _orderable = new List<DishType>(DishTypeInfo.Count);

        private long _busyWorkerMinutes;
        private long _awakeWorkerMinutes;
        private long _wetPlotMinutes;
        private long _growingPlotMinutes;

        /// <summary>Runs the scenario to completion and returns its metrics.</summary>
        public EconomyRunMetrics Run(EconomyScenario scenario)
        {
            _scenario = scenario;
            _rng = new System.Random(scenario.Seed);
            _metrics = new EconomyRunMetrics
            {
                ScenarioName = scenario.Name,
                Seed = scenario.Seed,
                RealHours = scenario.RealHours,
                StartingGold = scenario.StartingGold,
            };

            SetupServices();
            SetupFarm();
            SetupKitchen();
            RecordInitialUnlocks();

            // One step = one in-game minute = one real second, so a real hour is 3,600 steps
            int totalMinutes = (int)(scenario.RealHours * StepsPerRealHour);
            for (int minute = 0; minute < totalMinutes; minute++)
            {
                int minuteOfDay = (DayStartHour * 60 + minute) % MinutesPerDay;
                int hour = minuteOfDay / 60;

                if (minuteOfDay == DayStartHour * 60 && minute > 0)
                    ClearWet();

                _market.Update(1f / 60f);
                GrowCrops();
                UpdateWorkers(minute, hour);
                if (_scenario.AutoSellEnabled)
                    RunAutoSell();
                if (_scenario.AutoSeedEnabled)
                    RunAutoSeedPurchase();
                UpdateKitchen(minute, minuteOfDay, hour);
                if (minuteOfDay % 60 == 0 && (hour == TavernScheduleConfig.BreakfastHour
                    || hour == TavernScheduleConfig.LunchHour || hour == TavernScheduleConfig.DinnerHour))
                    ServePartyMeal();

                TrackMilestones(minute + 1);
                if ((minute + 1) % StepsPerRealHour == 0)
                    _metrics.GoldByHour.Add(_gameState.Funds);
            }

            Finish();
            return _metrics;
        }

        // ── Setup ──────────────────────────────────────────────────────────────────

        private void SetupServices()
        {
            _gameState = new GameStateService { Funds = _scenario.StartingGold };
            _gameState.SetProgressCounters(_scenario.InitialCropTotals, _scenario.InitialDishTotals);
            _market = new CropMarketService { SaturationScale = _scenario.GrowthSpeedMultiplier };
            _buildings = new BuildingService();
            _storageIds.Clear();
            for (int i = 0; i < _scenario.StorageBuildings; i++)
            {
                int id = StorageBuildingIdBase + i;
                _buildings.AddBuilding(new PlacedBuilding
                {
                    Type = BuildingType.CropStorage,
                    TileX = GameConfig.NewGameCropStorageAnchorTileX + i * 4,
                    TileY = GameConfig.NewGameCropStorageAnchorTileY,
                    UniqueId = id,
                });
                _storageIds.Add(id);
            }
            _storage = new CropStorageInventoryService(_buildings);
            _autoSell = new AutoCropSellService(_storage, _gameState, _market)
            {
                Enabled = _scenario.AutoSellEnabled,
                KeepStacks = _scenario.KeepStacks,
            };
        }

        private void SetupFarm()
        {
            _plots = new Plot[_scenario.PlotPlans.Count];
            for (int i = 0; i < _plots.Length; i++)
                _plots[i] = new Plot { Type = _scenario.PlotPlans[i], RegrowMultiplier = 1f };
            _seeds = new int[CropTypeInfo.Count];
            for (int i = 0; i < _seeds.Length; i++)
                _seeds[i] = i < _scenario.StartingSeeds.Length ? _scenario.StartingSeeds[i] : 0;
            _workers = new Worker[_scenario.FarmWorkers];
            for (int i = 0; i < _workers.Length; i++)
                _workers[i] = new Worker { Charges = GameConfig.WateringCanMaxCharges };
            int level = _scenario.FarmWorkerLevel < 1 ? 1 : (_scenario.FarmWorkerLevel > 9 ? 9 : _scenario.FarmWorkerLevel);
            _workerSpeed = FarmWorkDurations.GetSpeedScale(level);
            _harvestDuration = FarmWorkDurations.GetHarvestDuration(level);
        }

        private void SetupKitchen()
        {
            _cookBusyUntil = new int[_scenario.Cooks];
            _serverBusyUntil = new int[_scenario.Servers];
            _patrons.Clear();
            _nextArrivalSeconds = GameConfig.MercenaryMinSpawnIntervalSeconds;
            _dishBag = DishBagBuilder.BuildFullMenu();
        }

        private void RecordInitialUnlocks()
        {
            int cropMask = CropUnlockConfig.GetUnlockedMask(_gameState.CropHarvestedTotals);
            for (int c = 0; c < CropTypeInfo.Count; c++)
                _metrics.CropUnlockMinutes[c] = (cropMask & (1 << c)) != 0 ? 0 : -1;
            int dishMask = DishUnlockConfig.GetFullyUnlockedMask(_gameState.CropHarvestedTotals, _gameState.DishesServedTotals);
            for (int d = 0; d < DishTypeInfo.Count; d++)
                _metrics.DishUnlockMinutes[d] = (dishMask & (1 << d)) != 0 ? 0 : -1;
        }

        // ── Farm ───────────────────────────────────────────────────────────────────

        private void ClearWet()
        {
            for (int i = 0; i < _plots.Length; i++)
                _plots[i].Wet = false;
        }

        /// <summary>The exact accumulation and frame math of CropGrowthService.Update, one in-game minute at a time.</summary>
        private void GrowCrops()
        {
            for (int i = 0; i < _plots.Length; i++)
            {
                if (!_plots[i].Planted || _plots[i].Grown)
                    continue;
                _growingPlotMinutes++;
                if (!_plots[i].Wet)
                    continue;
                _wetPlotMinutes++;
                _plots[i].AccumulatedHours += _scenario.GrowthSpeedMultiplier / 60f;   // CropGrowthService: DeltaTime x GrowthSpeedMultiplier / 60

                int maxFrame = CropConfig.GetFrameCount(_plots[i].Type);
                float multiplier = _plots[i].RegrowMultiplier <= 0f ? 1f : _plots[i].RegrowMultiplier;
                float hoursPerStage = CropConfig.GetHoursPerStage(_plots[i].Type) * multiplier;
                int expectedFrame = 1 + (int)(_plots[i].AccumulatedHours / hoursPerStage);
                if (expectedFrame >= maxFrame)
                    _plots[i].Grown = true;
            }
        }

        private void UpdateWorkers(int minute, int hour)
        {
            bool awake = hour >= WorkerWakeHour && hour < WorkerSleepHour;
            for (int w = 0; w < _workers.Length; w++)
            {
                if (_workers[w].Task != TaskKind.None && minute >= _workers[w].BusyUntil)
                    CompleteTask(w);
                if (!awake)
                    continue;
                _awakeWorkerMinutes++;
                if (_workers[w].Task != TaskKind.None)
                {
                    _busyWorkerMinutes++;
                    continue;
                }
                if (TryAssignTask(w, minute))
                    _busyWorkerMinutes++;
            }
        }

        private bool TryAssignTask(int w, int minute)
        {
            float travel = _scenario.TravelOverheadSeconds;
            // Water first: a dry crop makes no progress at all
            int plot = FindPlot(planted: true, grown: false, wet: false);
            if (plot >= 0)
            {
                float duration = GameConfig.WaterBaseDurationSeconds * _workerSpeed + travel;
                if (_workers[w].Charges <= 0)
                {
                    duration += GameConfig.WateringCanFillDurationSeconds + travel;
                    _workers[w].Charges = GameConfig.WateringCanMaxCharges;
                }
                _workers[w].Charges--;
                StartTask(w, TaskKind.Water, plot, minute, duration);
                return true;
            }
            plot = FindPlot(planted: true, grown: true, wet: null);
            if (plot >= 0)
            {
                StartTask(w, TaskKind.Harvest, plot, minute, _harvestDuration + travel);
                return true;
            }
            plot = FindUnplantedPlotWithSeeds();
            if (plot >= 0)
            {
                _seeds[(int)_plots[plot].Type]--;
                StartTask(w, TaskKind.Plant, plot, minute, GameConfig.PlantBaseDurationSeconds * _workerSpeed + travel);
                return true;
            }
            return false;
        }

        private int FindPlot(bool planted, bool grown, bool? wet)
        {
            for (int i = 0; i < _plots.Length; i++)
            {
                if (_plots[i].Claimed || _plots[i].Planted != planted || _plots[i].Grown != grown)
                    continue;
                if (wet.HasValue && _plots[i].Wet != wet.Value)
                    continue;
                return i;
            }
            return -1;
        }

        private int FindUnplantedPlotWithSeeds()
        {
            for (int i = 0; i < _plots.Length; i++)
            {
                if (_plots[i].Claimed || _plots[i].Planted)
                    continue;
                if (_seeds[(int)_plots[i].Type] > 0)
                    return i;
            }
            return -1;
        }

        private void StartTask(int w, TaskKind task, int plot, int minute, float durationSeconds)
        {
            int duration = (int)System.Math.Ceiling(durationSeconds);
            if (duration < 1) duration = 1;
            _workers[w].Task = task;
            _workers[w].PlotIndex = plot;
            _workers[w].BusyUntil = minute + duration;
            _plots[plot].Claimed = true;
        }

        private void CompleteTask(int w)
        {
            int p = _workers[w].PlotIndex;
            switch (_workers[w].Task)
            {
                case TaskKind.Water:
                    _plots[p].Wet = true;
                    break;
                case TaskKind.Harvest:
                    Harvest(p);
                    break;
                case TaskKind.Plant:
                    _plots[p].Planted = true;
                    _plots[p].Grown = false;
                    _plots[p].AccumulatedHours = 0f;
                    _plots[p].RegrowMultiplier = 1f;
                    break;
            }
            _plots[p].Claimed = false;
            _workers[w].Task = TaskKind.None;
        }

        private void Harvest(int p)
        {
            var crop = _plots[p].Type;
            int units = CropConfig.GetHarvestYield(crop);
            _metrics.HarvestsByCrop[(int)crop]++;

            int before = CropUnlockConfig.GetUnlockedMask(_gameState.CropHarvestedTotals);
            _gameState.RecordHarvest(crop, units);
            int gained = CropUnlockConfig.GetUnlockedMask(_gameState.CropHarvestedTotals) & ~before;
            if (gained != 0)
                StampCropUnlocks(gained);

            int remaining = units;
            for (int b = 0; b < _storageIds.Count && remaining > 0; b++)
                remaining -= _storage.DepositReturningStored(_storageIds[b], crop, remaining);
            // Units that don't fit are dropped live and picked up later; with enough storage this stays 0

            if (CropConfig.IsRepeatHarvest(crop))
            {
                int revert = CropConfig.GetRevertFrame(crop);
                if (revert < 1) revert = 1;
                float mult = CropConfig.GetRegrowthRateMultiplier(crop);
                _plots[p].RegrowMultiplier = mult;
                _plots[p].AccumulatedHours = (revert - 1) * CropConfig.GetHoursPerStage(crop) * mult;
                _plots[p].Grown = false;
            }
            else
            {
                _plots[p].Planted = false;
                _plots[p].Grown = false;
                _plots[p].AccumulatedHours = 0f;
            }
        }

        private void StampCropUnlocks(int gained)
        {
            int minute = CurrentMinute();
            for (int c = 0; c < CropTypeInfo.Count; c++)
                if ((gained & (1 << c)) != 0 && _metrics.CropUnlockMinutes[c] < 0)
                    _metrics.CropUnlockMinutes[c] = minute;
            // A crop unlock can also soft-unlock dishes whose servings were already met
            StampDishUnlocks();
        }

        private void StampDishUnlocks()
        {
            int mask = DishUnlockConfig.GetFullyUnlockedMask(_gameState.CropHarvestedTotals, _gameState.DishesServedTotals);
            int minute = CurrentMinute();
            for (int d = 0; d < DishTypeInfo.Count; d++)
                if ((mask & (1 << d)) != 0 && _metrics.DishUnlockMinutes[d] < 0)
                    _metrics.DishUnlockMinutes[d] = minute;
        }

        private int _currentMinute;
        /// <summary>Elapsed real minutes (the report unit).</summary>
        private int CurrentMinute() => _currentMinute / StepsPerRealMinute;

        private void RunAutoSell()
        {
            int before = _gameState.Funds;
            var soldBefore = CopyUnitsInStorage();
            _autoSell.TrySellPass();
            int gained = _gameState.Funds - before;
            if (gained <= 0)
                return;
            _metrics.IncomeCrops += gained;
            // Attribute the pass's gold per crop by the base value of the units that left storage
            long baseTotal = 0;
            for (int c = 0; c < CropTypeInfo.Count; c++)
            {
                int sold = soldBefore[c] - _storage.CountTotal((CropType)c);
                _soldScratch[c] = sold > 0 ? sold : 0;
                if (sold <= 0)
                    continue;
                _metrics.UnitsSoldByCrop[c] += sold;
                long baseGold = (long)System.Math.Ceiling(CropConfig.GetHarvestUnitSellPrice((CropType)c) * sold);
                _metrics.BaseGoldSoldByCrop[c] += baseGold;
                baseTotal += baseGold;
            }
            for (int c = 0; c < CropTypeInfo.Count && baseTotal > 0; c++)
            {
                if (_soldScratch[c] <= 0)
                    continue;
                long baseGold = (long)System.Math.Ceiling(CropConfig.GetHarvestUnitSellPrice((CropType)c) * _soldScratch[c]);
                _metrics.GoldRealizedByCrop[c] += (long)System.Math.Round(gained * (double)baseGold / baseTotal);
            }
        }

        private readonly int[] _soldScratch = new int[CropTypeInfo.Count];

        private readonly int[] _unitsScratch = new int[CropTypeInfo.Count];

        private int[] CopyUnitsInStorage()
        {
            for (int c = 0; c < CropTypeInfo.Count; c++)
                _unitsScratch[c] = _storage.CountTotal((CropType)c);
            return _unitsScratch;
        }

        /// <summary>Mirror of AutoSeedPurchaseService.TryPurchasePass over the sim's own plots.</summary>
        private void RunAutoSeedPurchase()
        {
            for (int c = 0; c < CropTypeInfo.Count; c++)
            {
                var crop = (CropType)c;
                int needed = 0;
                for (int i = 0; i < _plots.Length; i++)
                    if (!_plots[i].Planted && !_plots[i].Claimed && _plots[i].Type == crop) needed++;
                if (needed == 0 || _seeds[c] >= needed)
                    continue;
                if (!CropUnlockConfig.IsUnlocked(crop, _gameState.CropHarvestedTotals))
                    continue;
                if (needed > GameConfig.SeedInventoryMaxPerCrop) needed = GameConfig.SeedInventoryMaxPerCrop;
                int price = CropConfig.GetSeedPrice(crop);
                while (_seeds[c] < needed && _gameState.Funds - price >= _scenario.GoldBuffer)
                {
                    _gameState.SpendFunds(price, "seeds");
                    _seeds[c]++;
                    _metrics.SpendSeeds += price;
                }
            }
        }

        // ── Kitchen ────────────────────────────────────────────────────────────────

        private bool KitchenStaffed => _scenario.Cooks > 0 && _scenario.Servers > 0;

        private bool IsOrderable(DishType dish)
        {
            if (!DishUnlockConfig.IsFullyUnlocked(dish, _gameState.CropHarvestedTotals, _gameState.DishesServedTotals))
                return false;
            var def = DishConfig.GetDefinition(dish);
            for (int i = 0; i < def.Recipe.Length; i++)
                if (_storage.AvailableTotal(def.Recipe[i].Crop) < def.Recipe[i].Qty)
                    return false;
            return true;
        }

        private void RefreshOrderable()
        {
            _orderable.Clear();
            for (int d = 0; d < DishTypeInfo.Count; d++)
                if (IsOrderable((DishType)d))
                    _orderable.Add((DishType)d);
        }

        private bool TryWithdrawRecipe(DishType dish)
        {
            var def = DishConfig.GetDefinition(dish);
            for (int i = 0; i < def.Recipe.Length; i++)
                if (_storage.AvailableTotal(def.Recipe[i].Crop) < def.Recipe[i].Qty)
                    return false;
            for (int i = 0; i < def.Recipe.Length; i++)
                _storage.TryWithdrawAcrossBuildings(def.Recipe[i].Crop, def.Recipe[i].Qty);
            return true;
        }

        /// <summary>Bounded draw-and-skip over the full-menu bag, like KitchenTaskCoordinator.PickPatronDish.</summary>
        private DishType PickPatronDish()
        {
            int limit = _dishBag.Count;
            for (int i = 0; i < limit; i++)
            {
                var dish = _dishBag.Next(_rng);
                for (int j = 0; j < _orderable.Count; j++)
                    if (_orderable[j] == dish)
                        return dish;
            }
            return _orderable[_rng.Next(_orderable.Count)];
        }

        private int FindFree(int[] busyUntil, int minute)
        {
            for (int i = 0; i < busyUntil.Length; i++)
                if (busyUntil[i] <= minute)
                    return i;
            return -1;
        }

        private void UpdateKitchen(int minute, int minuteOfDay, int hour)
        {
            if (!KitchenStaffed)
                return;
            bool closed = TavernScheduleConfig.IsKitchenClosed(hour);

            // Arrivals: base interval x schedule multiplier; an empty open tavern refills fast (MercenaryManager)
            _nextArrivalSeconds -= 1f;
            if (_nextArrivalSeconds <= 0f)
            {
                if (_patrons.Count < TavernSeats)
                {
                    _patrons.Add(new Patron { State = PatronState.Waiting, Timer = (int)GameConfig.PatronPatiencePreOrderSeconds });
                    _metrics.PatronsArrived++;
                }
                float mult = TavernScheduleConfig.GetArrivalIntervalMultiplier(hour);
                bool empty = _patrons.Count == 0;
                float baseInterval = empty && !closed
                    ? GameConfig.MercenaryMinSpawnIntervalSeconds
                    : GameConfig.MercenarySpawnIntervalMinSeconds
                      + (float)_rng.NextDouble() * (GameConfig.MercenarySpawnIntervalMaxSeconds - GameConfig.MercenarySpawnIntervalMinSeconds);
                _nextArrivalSeconds = baseInterval * mult;
            }

            int overhead = (int)System.Math.Ceiling(_scenario.KitchenOverheadSeconds);
            bool orderableFresh = false;
            for (int i = _patrons.Count - 1; i >= 0; i--)
            {
                var p = _patrons[i];
                switch (p.State)
                {
                    case PatronState.Waiting:
                    {
                        if (closed)
                        {
                            _patrons.RemoveAt(i);
                            _metrics.PatronsLeftHungry++;
                            break;
                        }
                        int server = FindFree(_serverBusyUntil, minute);
                        if (server >= 0)
                        {
                            if (!orderableFresh) { RefreshOrderable(); orderableFresh = true; }
                            if (_orderable.Count > 0)
                            {
                                p.Dish = PickPatronDish();
                                TryWithdrawRecipe(p.Dish);
                                orderableFresh = false;
                                _serverBusyUntil[server] = minute + overhead;
                                p.State = PatronState.Ordered;
                                break;
                            }
                        }
                        p.Timer--;
                        if (p.Timer <= 0)
                        {
                            _patrons.RemoveAt(i);
                            _metrics.PatronsLeftHungry++;
                        }
                        break;
                    }
                    case PatronState.Ordered:
                    {
                        int cook = FindFree(_cookBusyUntil, minute);
                        if (cook >= 0)
                        {
                            int cookMinutes = (int)System.Math.Ceiling(DishConfig.GetCookDuration(p.Dish, _scenario.CookProficiency)) + overhead;
                            _cookBusyUntil[cook] = minute + cookMinutes;
                            p.Timer = minute + cookMinutes;
                            p.State = PatronState.Cooking;
                        }
                        break;
                    }
                    case PatronState.Cooking:
                    {
                        if (minute < p.Timer)
                            break;
                        int server = FindFree(_serverBusyUntil, minute);
                        if (server < 0)
                            break;
                        _serverBusyUntil[server] = minute + overhead;
                        p.Timer = minute + overhead + (int)System.Math.Ceiling(DishConfig.GetEatSeconds(p.Dish));
                        p.State = PatronState.Eating;
                        break;
                    }
                    case PatronState.Eating:
                    {
                        if (minute < p.Timer)
                            break;
                        int price = DishConfig.GetPrice(p.Dish);
                        _gameState.AddFunds(price, "dish_sale");
                        _metrics.IncomeDishes += price;
                        if (_rng.NextDouble() < GameConfig.DishTipChance)
                        {
                            float pct = GameConfig.DishTipMinPercent + (float)_rng.NextDouble() * (GameConfig.DishTipMaxPercent - GameConfig.DishTipMinPercent);
                            int tip = (int)System.Math.Ceiling(price * pct);
                            _gameState.AddFunds(tip, "dish_tip");
                            _metrics.IncomeTips += tip;
                        }
                        RecordServed(p.Dish);
                        _metrics.PatronsServed++;
                        p.Timer = minute + (int)GameConfig.PatronLingerAfterEatingSeconds;
                        p.State = PatronState.Lingering;
                        break;
                    }
                    case PatronState.Lingering:
                        if (minute >= p.Timer)
                            _patrons.RemoveAt(i);
                        break;
                }
            }
        }

        private void RecordServed(DishType dish)
        {
            _metrics.DishesServedByDish[(int)dish]++;
            DishUnlockTracker.RecordDishServed(_gameState, dish);
            StampDishUnlocks();
        }

        /// <summary>The party's meal at 6/12/18: the hero pays for his dish, mercenaries eat free (ingredients only).</summary>
        private void ServePartyMeal()
        {
            if (!_scenario.EatAtTavern || !KitchenStaffed)
                return;
            bool ok = PartyDiningService.TryPickHeroDishCore(_scenario.FavoriteDish, _scenario.HeroJobName,
                _gameState.Funds, IsOrderable, out var dish, out _);
            if (!ok)
            {
                _metrics.HeroMealsSkipped++;
                return;
            }
            int price = DishConfig.GetPrice(dish);
            _gameState.SpendFunds(price, "party_meal");
            _metrics.SpendMeals += price;
            TryWithdrawRecipe(dish);
            RecordServed(dish);
            _metrics.HeroMeals++;

            for (int m = 0; m < _scenario.Mercenaries; m++)
            {
                DishType mercDish = default;
                bool found = false;
                for (int c = 0; c < 3 && !found; c++)
                {
                    var candidate = c == 0 ? _scenario.FavoriteDish : DishConfig.GetFallbackForJob(_scenario.HeroJobName, c - 1);
                    if (IsOrderable(candidate)) { mercDish = candidate; found = true; }
                }
                if (!found)
                    continue;
                TryWithdrawRecipe(mercDish);
                RecordServed(mercDish);
            }
        }

        // ── Bookkeeping ────────────────────────────────────────────────────────────

        private void TrackMilestones(int minute)
        {
            _currentMinute = minute;
            for (int i = 0; i < EconomyRunMetrics.Milestones.Length; i++)
                if (_metrics.MilestoneMinutes[i] < 0 && _gameState.Funds >= EconomyRunMetrics.Milestones[i])
                    _metrics.MilestoneMinutes[i] = minute / StepsPerRealMinute;
        }

        private void Finish()
        {
            _metrics.FinalGold = _gameState.Funds;
            for (int c = 0; c < CropTypeInfo.Count; c++)
                _metrics.FinalDemand[c] = _market.GetDemand((CropType)c);
            _metrics.WorkerUtilization = _awakeWorkerMinutes > 0 ? (float)_busyWorkerMinutes / _awakeWorkerMinutes : 0f;
            _metrics.WetFraction = _growingPlotMinutes > 0 ? (float)_wetPlotMinutes / _growingPlotMinutes : 0f;
        }
    }
}
