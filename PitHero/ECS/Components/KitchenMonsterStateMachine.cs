using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.FSM;
using PitHero.Config;
using PitHero.Dining;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Drives a single kitchen monster through the tavern service loop.
    ///
    /// Server: take up to 3 orders from patrons at its tables (1 server works all 4 tables;
    /// with 2 servers the first works the top pair, the second the bottom pair), post them at
    /// the ticket board, deliver up to 2 cooked dishes from the serving tables (only for its
    /// own tables), bus finished plates, otherwise wander its table area.
    /// Cook: read one ticket at the board, gather ingredients at the fridge (waiting for the
    /// runner if the fridge is short), cook at a free station, place the dish on a serving
    /// table — holding it if all three are full.
    /// Runner: proactively haul each order's ingredient shortfall from Crop Storage to the
    /// fridge, one claimed job at a time.
    /// </summary>
    public class KitchenMonsterStateMachine : SimpleStateMachine<KitchenMonsterState>, IPausableComponent
    {
        private readonly AlliedMonster _monster;
        private readonly KitchenTaskCoordinator _coordinator;
        private readonly KitchenRole _role;

        private Point _houseAnchorTile;
        private FarmMonsterMover _mover;
        private ActorFacingComponent _facing;
        private PauseService _pauseService;
        private KitchenHatService _hatService;
        private Entity _hat;

        /// <summary>Body animator for the monster.</summary>
        public EnemyAnimationComponent BodyAnimator;

        /// <summary>Carry renderer (dish sprite held during delivery).</summary>
        public Nez.Sprites.SpriteRenderer CarryRenderer;

        private bool _goHome;
        private bool _returnReachedExit;

        // ── Server state ────────────────────────────────────────────────────────
        private struct CarriedDish
        {
            public KitchenTicket Ticket; // null for orphaned dishes headed to the sink
            public DishType Dish;
            public bool ToSink;
        }

        private readonly System.Collections.Generic.List<KitchenTicket> _takenOrders =
            new System.Collections.Generic.List<KitchenTicket>(GameConfig.ServerOrderMemoryLimit);
        private readonly System.Collections.Generic.List<CarriedDish> _carried =
            new System.Collections.Generic.List<CarriedDish>(GameConfig.ServerCarryDishLimit);
        private Entity _targetPatron;
        private int _targetPartySlot = -1;
        private DishType _targetPartyDish;
        private KitchenTaskCoordinator.BusJob _busJob;
        private bool _busPickedUp;

        // ── Cook state ──────────────────────────────────────────────────────────
        private KitchenTicket _cookTicket;
        private float _cookReadElapsed;
        private bool _cookTicketInHand; // read pause finished, station claimed
        private float _cookElapsed;
        private bool _cookCarryToSink;  // carried dish's ticket was canceled → sink it

        // ── Runner state ────────────────────────────────────────────────────────
        private KitchenTicket _fetchTicket;

        public bool ShouldPause => true;

        /// <summary>True once the monster has been asked to walk home and despawn.</summary>
        public bool IsReturningHome => _goHome;

        private Point DoorTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 2);
        private Point ExitTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 3);

        public KitchenMonsterStateMachine(AlliedMonster monster, KitchenTaskCoordinator coordinator,
            Point houseAnchorTile, KitchenRole role)
        {
            _monster = monster;
            _coordinator = coordinator;
            _houseAnchorTile = houseAnchorTile;
            _role = role;
        }

        public override void OnAddedToEntity()
        {
            _mover = Entity.GetComponent<FarmMonsterMover>();
            _facing = Entity.GetComponent<ActorFacingComponent>();
            _pauseService = Core.Services.GetService<PauseService>();
            _hatService = Core.Services.GetService<KitchenHatService>();
            // Wear the job hat while doing kitchen work (BodyAnimator's sprite is set by now —
            // it was added before this component, so its OnAddedToEntity already ran)
            _hat = _hatService?.AttachHat(_role, Entity, BodyAnimator);
            InitialState = KitchenMonsterState.EmergeFromHouse;
        }

        public override void OnRemovedFromEntity()
        {
            _hatService?.DetachHat(_hat);
            _hat = null;

            // Fault tolerance: never strand work when this entity dies for any reason.
            for (int i = 0; i < _takenOrders.Count; i++)
                _coordinator.PostTicket(_takenOrders[i]);
            _takenOrders.Clear();

            for (int i = 0; i < _carried.Count; i++)
            {
                var c = _carried[i];
                if (c.Ticket == null || c.Ticket.State == TicketState.Canceled)
                    continue; // orphan/canceled dish in hand — it's simply gone
                // Put the dish back on a serving table so the next zone server delivers it
                if (!_coordinator.TryReserveServingSlot(c.Ticket, out int slot))
                    slot = _coordinator.ForceReserveServingSlot(c.Ticket);
                var entity = _coordinator.DishService?.SpawnDishAtTile(
                    c.Ticket.Dish, KitchenTaskCoordinator.GetServingTile(slot));
                _coordinator.PlaceDishOnServing(c.Ticket, entity);
            }
            _carried.Clear();

            if (_cookTicket != null && _cookTicket.State != TicketState.Canceled)
            {
                if (_cookTicket.State == TicketState.Cooking || !_cookTicketInHand
                    || _cookTicket.State == TicketState.ReadyToCook
                    || _cookTicket.State == TicketState.AwaitingIngredients)
                {
                    _coordinator.ReleaseCookTicket(_cookTicket);
                }
            }
            _cookTicket = null;

            _coordinator.ReleaseFetchJob(_fetchTicket);
            _fetchTicket = null;

            base.OnRemovedFromEntity();
        }

        public override void Update()
        {
            if (_pauseService?.IsPaused == true)
                return;
            base.Update();
        }

        /// <summary>Asks the monster to finish what it's doing, walk back into its house, and despawn.</summary>
        public void RequestReturnHome() => _goHome = true;

        /// <summary>Cancels a pending return (assignment restored before the monster got home).</summary>
        public void CancelReturnHome()
        {
            if (!_goHome) return;
            _goHome = false;
            if (CurrentState == KitchenMonsterState.ReturnHome)
            {
                _mover.Stop();
                switch (_role)
                {
                    case KitchenRole.Cook:   CurrentState = KitchenMonsterState.CookWalkToBoard; break;
                    case KitchenRole.Server: CurrentState = KitchenMonsterState.ServerDecide;    break;
                    default:                 CurrentState = KitchenMonsterState.RunnerIdle;      break;
                }
            }
        }

        // ─────────────────────────────────── EmergeFromHouse

        private void EmergeFromHouse_Enter()
        {
            _facing?.SetFacing(Direction.Down);
            if (_coordinator.Pathfinder.IsPassable(ExitTile))
                _mover.SetSingleTarget(TileCenter(ExitTile));
        }

        private void EmergeFromHouse_Tick()
        {
            if (_mover.IsMoving)
                return;
            switch (_role)
            {
                case KitchenRole.Cook:   CurrentState = KitchenMonsterState.CookWalkToBoard; break;
                case KitchenRole.Server: CurrentState = KitchenMonsterState.ServerDecide;    break;
                default:                 CurrentState = KitchenMonsterState.RunnerIdle;      break;
            }
        }

        // ═══════════════════════════════════ SERVER ═══════════════════════════════

        private ServerZone Zone => _coordinator.GetServerZone(this);

        private void ServerDecide_Tick()
        {
            if (_carried.Count > 0)
            {
                BeginNextCarriedLeg();
                return;
            }
            if (_goHome)
            {
                // Post anything still in memory so cooks aren't left waiting on us
                for (int i = 0; i < _takenOrders.Count; i++)
                    _coordinator.PostTicket(_takenOrders[i]);
                _takenOrders.Clear();
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }
            if (_takenOrders.Count > 0)
            {
                // Landed here with unposted orders (e.g. patron vanished mid-batch) — post them
                CurrentState = KitchenMonsterState.ServerWalkToBoard;
                return;
            }

            var zone = Zone;

            // 1) Cooked food waiting → deliver
            if (_coordinator.HasReadyDishForZone(zone))
            {
                CurrentState = KitchenMonsterState.ServerWalkToPickup;
                return;
            }
            // 2) Someone at my tables needs to order
            if (TryPickNextOrderTarget(zone))
            {
                CurrentState = KitchenMonsterState.ServerWalkToPatron;
                return;
            }
            // 3) Dirty plates at my tables
            if (_coordinator.TryClaimBusJob(zone, out _busJob))
            {
                _busPickedUp = false;
                CurrentState = KitchenMonsterState.ServerBusPlate;
                return;
            }
            // 4) Nothing to do — wander my table area
            CurrentState = KitchenMonsterState.ServerWander;
        }

        /// <summary>
        /// Picks the next order target in the zone: party members first (their table must be in
        /// this zone), then the nearest waiting patron. Sets _targetPatron/_targetPartySlot.
        /// </summary>
        private bool TryPickNextOrderTarget(ServerZone zone)
        {
            _targetPatron = null;
            _targetPartySlot = -1;

            var partyTable = TavernSeatConfig.GetTableTile(KitchenTaskCoordinator.GetPartySeatTile(0));
            if (KitchenTaskCoordinator.ZoneContainsTable(zone, partyTable)
                && _coordinator.TryGetNextPartyOrder(out int slot, out var dish))
            {
                _targetPartySlot = slot;
                _targetPartyDish = dish;
                return true;
            }

            _targetPatron = FindPatronNeedingOrder(zone);
            return _targetPatron != null;
        }

        private void ServerWalkToPatron_Enter()
        {
            Point seat;
            if (_targetPartySlot >= 0)
                seat = KitchenTaskCoordinator.GetPartySeatTile(_targetPartySlot);
            else if (_targetPatron != null && !_targetPatron.IsDestroyed)
                seat = WorldToTile(_targetPatron.Transform.Position);
            else
            {
                CurrentState = KitchenMonsterState.ServerDecide;
                return;
            }
            if (!TrySetPathToTileOrNeighbor(seat))
                CurrentState = KitchenMonsterState.ServerDecide;
        }

        private void ServerWalkToPatron_Tick()
        {
            if (_mover.IsMoving)
                return;

            TakeOrderAtTarget();

            // Keep batching while there's memory left and someone else is waiting
            if (_takenOrders.Count > 0 && _takenOrders.Count < GameConfig.ServerOrderMemoryLimit
                && TryPickNextOrderTarget(Zone))
            {
                CurrentState = KitchenMonsterState.ServerWalkToPatron;
                // Same-state assignment doesn't re-run Enter — do it explicitly
                ServerWalkToPatron_Enter();
                return;
            }

            CurrentState = _takenOrders.Count > 0
                ? KitchenMonsterState.ServerWalkToBoard
                : KitchenMonsterState.ServerDecide;
        }

        private void TakeOrderAtTarget()
        {
            if (_targetPartySlot >= 0)
            {
                // Re-check: zones may have shifted mid-walk and another server taken it
                if (_coordinator.GetPartyTicket(_targetPartySlot) == null)
                {
                    var seat = KitchenTaskCoordinator.GetPartySeatTile(_targetPartySlot);
                    var ticket = _coordinator.CreateTicket(_targetPartyDish, true, _targetPartySlot, null, seat);
                    if (ticket != null)
                    {
                        _coordinator.NotifyPartyOrderTaken(_targetPartySlot, ticket);
                        _takenOrders.Add(ticket);
                    }
                }
                _targetPartySlot = -1;
                return;
            }

            var patron = _targetPatron;
            _targetPatron = null;
            if (patron == null || patron.IsDestroyed)
                return;
            var comp = patron.GetComponent<TavernPatronComponent>();
            if (comp == null || comp.State != PatronState.WaitingToOrder || comp.ActiveTicket != null)
                return;

            var orderable = new System.Collections.Generic.List<DishType>(DishTypeInfo.Count);
            _coordinator.GetOrderableDishes(orderable);
            if (orderable.Count == 0)
                return;
            var dish = orderable[Nez.Random.Range(0, orderable.Count)];
            var t = _coordinator.CreateTicket(dish, false, -1, patron, comp.SeatTile);
            if (t != null)
            {
                comp.OnOrderTaken(t);
                _takenOrders.Add(t);
            }
        }

        private void ServerWalkToBoard_Enter()
        {
            if (!TrySetPathToTileOrNeighbor(KitchenTaskCoordinator.TicketBoardTile))
                CurrentState = KitchenMonsterState.ServerPostTickets; // board unreachable — post in place
        }

        private void ServerWalkToBoard_Tick()
        {
            if (!_mover.IsMoving)
                CurrentState = KitchenMonsterState.ServerPostTickets;
        }

        private void ServerPostTickets_Tick()
        {
            if (elapsedTimeInState < GameConfig.TicketBoardPauseSeconds)
                return;
            for (int i = 0; i < _takenOrders.Count; i++)
                _coordinator.PostTicket(_takenOrders[i]);
            _takenOrders.Clear();
            CurrentState = KitchenMonsterState.ServerDecide;
        }

        private void ServerWalkToPickup_Enter()
        {
            // Middle serving table is a fine approach point for all three
            if (!TrySetPathToTileOrNeighbor(KitchenTaskCoordinator.GetServingTile(1)))
                CurrentState = KitchenMonsterState.ServerDecide;
        }

        private void ServerWalkToPickup_Tick()
        {
            if (_mover.IsMoving)
                return;

            var zone = Zone;
            while (_carried.Count < GameConfig.ServerCarryDishLimit
                && _coordinator.TryPickupReadyDish(zone, out var ticket, out var dish, out bool toSink))
            {
                _carried.Add(new CarriedDish { Ticket = ticket, Dish = dish, ToSink = toSink });
            }

            if (_carried.Count == 0)
            {
                CurrentState = KitchenMonsterState.ServerDecide;
                return;
            }
            BeginNextCarriedLeg();
        }

        /// <summary>Starts walking the current carried item to its destination (table or sink).</summary>
        private void BeginNextCarriedLeg()
        {
            if (_carried.Count == 0)
            {
                HideCarryDish();
                CurrentState = KitchenMonsterState.ServerDecide;
                return;
            }

            var c = _carried[0];
            bool sink = c.ToSink || c.Ticket == null || c.Ticket.State == TicketState.Canceled;
            if (sink)
            {
                ShowCarrySprite(GetPlateSprite());
                CurrentState = KitchenMonsterState.ServerWalkToSink;
                ServerWalkToSink_Enter();
            }
            else
            {
                ShowCarryDish(c.Dish);
                CurrentState = KitchenMonsterState.ServerDeliver;
                ServerDeliver_Enter();
            }
        }

        private void ServerDeliver_Enter()
        {
            if (_carried.Count == 0)
            {
                CurrentState = KitchenMonsterState.ServerDecide;
                return;
            }
            var c = _carried[0];
            if (!TrySetPathToTileOrNeighbor(c.Ticket.TableTile))
                _mover.SetSingleTarget(TileCenter(c.Ticket.SeatTile));
        }

        private void ServerDeliver_Tick()
        {
            if (_carried.Count == 0)
            {
                CurrentState = KitchenMonsterState.ServerDecide;
                return;
            }
            var c = _carried[0];

            // Patron left mid-walk → divert this dish to the sink
            if (c.Ticket == null || c.Ticket.State == TicketState.Canceled)
            {
                BeginNextCarriedLeg();
                return;
            }

            if (_mover.IsMoving)
                return;

            // Arrived at the table — place the dish
            Entity dishEntity = null;
            if (TavernSeatConfig.TryGetPlateWorldPosition(c.Ticket.SeatTile, out var platePos))
                dishEntity = _coordinator.DishService?.SpawnDishAtWorldPos(c.Ticket.Dish, platePos);
            _coordinator.OnTicketDelivered(c.Ticket, dishEntity);
            _carried.RemoveAt(0);
            BeginNextCarriedLeg();
        }

        private void ServerWalkToSink_Enter()
        {
            TrySetPathTo(KitchenTaskCoordinator.SinkTile);
        }

        private void ServerWalkToSink_Tick()
        {
            if (_mover.IsMoving)
                return;
            if (_carried.Count > 0)
                _carried.RemoveAt(0); // dish disposed at the sink
            BeginNextCarriedLeg();
        }

        private void ServerBusPlate_Tick()
        {
            if (_mover.IsMoving)
                return;

            if (!_busPickedUp)
            {
                // First arrival trigger: walk to the plate
                if (!TrySetPathToTileOrNeighbor(WorldToTile(_busJob.WorldPos)))
                {
                    CurrentState = KitchenMonsterState.ServerDecide;
                    return;
                }
                _busPickedUp = true;
                return;
            }

            // Arrived at the plate or at the sink
            if (_busJob.DishEntity != null && !_busJob.DishEntity.IsDestroyed)
            {
                var sprite = _busJob.DishEntity.GetComponent<Nez.Sprites.SpriteRenderer>();
                ShowCarrySprite(sprite?.Sprite);
                _busJob.DishEntity.Destroy();
                _busJob.DishEntity = null;
                TrySetPathTo(KitchenTaskCoordinator.SinkTile);
                return;
            }

            HideCarryDish();
            CurrentState = KitchenMonsterState.ServerDecide;
        }

        private void ServerWander_Enter()
        {
            var zone = Zone;
            int minY, maxY;
            switch (zone)
            {
                case ServerZone.TopTables:
                    minY = GameConfig.TavernTopZoneMinTileY; maxY = GameConfig.TavernTopZoneMaxTileY; break;
                case ServerZone.BottomTables:
                    minY = GameConfig.TavernBottomZoneMinTileY; maxY = GameConfig.TavernBottomZoneMaxTileY; break;
                default:
                    minY = GameConfig.TavernTopZoneMinTileY; maxY = GameConfig.TavernBottomZoneMaxTileY; break;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                var tile = new Point(
                    Nez.Random.Range(GameConfig.TavernAreaMinTileX, GameConfig.TavernAreaMaxTileX + 1),
                    Nez.Random.Range(minY, maxY + 1));
                if (TrySetPathTo(tile))
                    return;
            }
            // No walkable pick — just idle in place this round
        }

        private void ServerWander_Tick()
        {
            // Interrupt wandering the moment real work appears
            if (_goHome || _coordinator.HasReadyDishForZone(Zone) || HasOrderWork(Zone))
            {
                _mover.Stop();
                CurrentState = KitchenMonsterState.ServerDecide;
                return;
            }
            if (_mover.IsMoving)
                return;
            if (elapsedTimeInState >= GameConfig.ServerWanderPauseSeconds)
                CurrentState = KitchenMonsterState.ServerDecide;
        }

        private bool HasOrderWork(ServerZone zone)
        {
            var partyTable = TavernSeatConfig.GetTableTile(KitchenTaskCoordinator.GetPartySeatTile(0));
            if (KitchenTaskCoordinator.ZoneContainsTable(zone, partyTable)
                && _coordinator.TryGetNextPartyOrder(out _, out _))
                return true;
            return FindPatronNeedingOrder(zone) != null;
        }

        private Entity FindPatronNeedingOrder(ServerZone zone)
        {
            var mercManager = Core.Services.GetService<MercenaryManager>();
            if (mercManager == null) return null;

            var unhired = mercManager.GetUnhiredMercenaries();
            Entity best = null;
            float bestDist = float.MaxValue;
            var myPos = Entity.Transform.Position;

            for (int i = 0; i < unhired.Count; i++)
            {
                var e = unhired[i];
                var comp = e.GetComponent<TavernPatronComponent>();
                if (comp == null || comp.State != PatronState.WaitingToOrder)
                    continue;
                if (comp.ActiveTicket != null)
                    continue;
                var table = TavernSeatConfig.GetTableTile(comp.SeatTile);
                if (!KitchenTaskCoordinator.ZoneContainsTable(zone, table))
                    continue;

                float dist = Vector2.Distance(myPos, e.Transform.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = e;
                }
            }
            return best;
        }

        // ═══════════════════════════════════ COOK ═════════════════════════════════

        private void CookWalkToBoard_Enter()
        {
            _cookTicketInHand = false;
            _cookCarryToSink = false;
            TrySetPathToTileOrNeighbor(KitchenTaskCoordinator.TicketBoardTile);
        }

        private void CookWalkToBoard_Tick()
        {
            if (_goHome)
            {
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }
            if (!_mover.IsMoving)
                CurrentState = KitchenMonsterState.CookAtBoard;
        }

        private void CookAtBoard_Tick()
        {
            if (_goHome && _cookTicket == null)
            {
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            if (_cookTicket == null)
            {
                if (elapsedTimeInState < GameConfig.FarmMonsterIdlePollInterval)
                    return;
                _cookTicket = _coordinator.TryReadNextTicket();
                _cookReadElapsed = 0f;
                return;
            }

            if (_cookTicket.State == TicketState.Canceled)
            {
                _cookTicket = null;
                return;
            }

            // Reading pause at the board
            _cookReadElapsed += Time.DeltaTime;
            if (_cookReadElapsed < GameConfig.TicketBoardPauseSeconds)
                return;

            if (_coordinator.TryClaimStation(_cookTicket, out _))
            {
                _cookTicketInHand = true;
                CurrentState = KitchenMonsterState.CookWalkToFridge;
            }
            // else: all stations busy (more cooks than stations shouldn't happen) — keep waiting
        }

        private void CookWalkToFridge_Enter()
        {
            _facing?.SetFacing(Direction.Right);
            TrySetPathToTileOrNeighbor(KitchenTaskCoordinator.FridgeTile);
        }

        private void CookWalkToFridge_Tick()
        {
            if (CookAbandonIfCanceled())
                return;
            if (!_mover.IsMoving)
                CurrentState = KitchenMonsterState.CookWaitIngredients;
        }

        private void CookWaitIngredients_Tick()
        {
            if (CookAbandonIfCanceled())
                return;
            if (_goHome)
            {
                _coordinator.ReleaseCookTicket(_cookTicket);
                _cookTicket = null;
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }
            // Wait for the runner to finish stocking the fridge for this ticket
            if (_cookTicket.IngredientsFetched)
                CurrentState = KitchenMonsterState.CookWalkToStation;
        }

        private void CookWalkToStation_Enter()
        {
            TrySetPathTo(KitchenTaskCoordinator.GetStationTile(_cookTicket.StationIndex));
        }

        private void CookWalkToStation_Tick()
        {
            if (CookAbandonIfCanceled())
                return;
            if (_mover.IsMoving)
                return;
            _coordinator.BeginCookingAtStation(_cookTicket, _monster.CookingProficiency);
            _cookElapsed = 0f;
            _facing?.SetFacing(Direction.Up);
            CurrentState = KitchenMonsterState.CookCooking;
        }

        private void CookCooking_Tick()
        {
            if (CookAbandonIfCanceled())
                return;
            if (_goHome)
            {
                // Shift over mid-cook: put the ticket back on the board for the next cook
                _coordinator.ReleaseCookTicket(_cookTicket);
                _cookTicket = null;
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            _cookElapsed += Time.DeltaTime;
            float duration = DishConfig.GetCookDuration(_cookTicket.Dish, _monster.CookingProficiency);
            if (_cookElapsed < duration)
                return;

            // Done — pick up the dish and head for a serving table
            _coordinator.FinishCooking(_cookTicket);
            ShowCarryDish(_cookTicket.Dish);
            if (_coordinator.TryReserveServingSlot(_cookTicket, out _))
                CurrentState = KitchenMonsterState.CookWalkToServing;
            else
                CurrentState = KitchenMonsterState.CookWaitServingSlot;
        }

        private void CookWaitServingSlot_Tick()
        {
            if (_cookTicket == null || _cookTicket.State == TicketState.Canceled)
            {
                // Patron left while we hold their dish — sink it
                _cookCarryToSink = true;
                CurrentState = KitchenMonsterState.CookWalkToServing;
                return;
            }
            if (_goHome)
            {
                // Never get stuck holding a dish at shift end — overflow onto a table
                _coordinator.ForceReserveServingSlot(_cookTicket);
                CurrentState = KitchenMonsterState.CookWalkToServing;
                return;
            }
            if (_coordinator.TryReserveServingSlot(_cookTicket, out _))
                CurrentState = KitchenMonsterState.CookWalkToServing;
        }

        private void CookWalkToServing_Enter()
        {
            if (_cookCarryToSink)
                TrySetPathTo(KitchenTaskCoordinator.SinkTile);
            else
                TrySetPathToTileOrNeighbor(KitchenTaskCoordinator.GetServingTile(_cookTicket.ServingSlot));
        }

        private void CookWalkToServing_Tick()
        {
            if (!_cookCarryToSink && (_cookTicket == null || _cookTicket.State == TicketState.Canceled))
            {
                _cookCarryToSink = true;
                CookWalkToServing_Enter();
                return;
            }
            if (_mover.IsMoving)
                return;

            if (_cookCarryToSink)
            {
                HideCarryDish();
                _cookCarryToSink = false;
                _cookTicket = null;
            }
            else
            {
                var tile = KitchenTaskCoordinator.GetServingTile(_cookTicket.ServingSlot);
                var entity = _coordinator.DishService?.SpawnDishAtTile(_cookTicket.Dish, tile);
                _coordinator.PlaceDishOnServing(_cookTicket, entity);
                HideCarryDish();
                _cookTicket = null;
            }

            CurrentState = _goHome ? KitchenMonsterState.ReturnHome : KitchenMonsterState.CookWalkToBoard;
        }

        /// <summary>Cook's ticket was canceled out from under it — drop everything, back to the board.</summary>
        private bool CookAbandonIfCanceled()
        {
            if (_cookTicket != null && _cookTicket.State != TicketState.Canceled)
                return false;
            _cookTicket = null;
            HideCarryDish();
            CurrentState = _goHome ? KitchenMonsterState.ReturnHome : KitchenMonsterState.CookWalkToBoard;
            return true;
        }

        // ═══════════════════════════════════ RUNNER ═══════════════════════════════

        /// <summary>Runners sprint at 2× while on an ingredient run (out to storage AND back).</summary>
        private void SetSprinting(bool sprinting)
        {
            if (_mover != null)
                _mover.MoveSpeed = GameConfig.HeroMovementSpeed
                    * (sprinting ? GameConfig.KitchenRunnerSprintMultiplier : 1f);
        }

        private void RunnerIdle_Enter()
        {
            SetSprinting(false);
            TrySetPathTo(KitchenTaskCoordinator.RunnerPostTile);
        }

        private void RunnerIdle_Tick()
        {
            if (_goHome)
            {
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }
            if (_mover.IsMoving)
                return;
            if (elapsedTimeInState < GameConfig.FarmMonsterIdlePollInterval)
                return;

            _fetchTicket = _coordinator.TryClaimFetchJob();
            if (_fetchTicket != null)
                CurrentState = KitchenMonsterState.RunnerWalkToStorage;
        }

        private void RunnerWalkToStorage_Enter()
        {
            SetSprinting(true);
            Point myTile = WorldToTile(Entity.Transform.Position);
            if (!_coordinator.TryFindNearestStorageDoor(myTile, out var door) || !TrySetPathTo(door))
            {
                // Storage vanished mid-order — the crops were reserved at order time, so the
                // ticket can proceed without the trip.
                _coordinator.CompleteFetch(_fetchTicket);
                _fetchTicket = null;
                CurrentState = KitchenMonsterState.RunnerIdle;
            }
        }

        private void RunnerWalkToStorage_Tick()
        {
            if (_fetchTicket == null || _fetchTicket.State == TicketState.Canceled)
            {
                _fetchTicket = null;
                CurrentState = KitchenMonsterState.RunnerIdle;
                return;
            }
            if (_goHome)
            {
                _coordinator.ReleaseFetchJob(_fetchTicket);
                _fetchTicket = null;
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }
            if (!_mover.IsMoving)
                CurrentState = KitchenMonsterState.RunnerCollect;
        }

        private void RunnerCollect_Tick()
        {
            if (elapsedTimeInState < 1f)
                return;
            // Atomic top-up into the fridge happens here; the walk back is cosmetic
            _coordinator.RunnerCollectAtStorage(_fetchTicket);
            CurrentState = KitchenMonsterState.RunnerWalkToFridge;
        }

        private void RunnerWalkToFridge_Enter()
        {
            TrySetPathToTileOrNeighbor(KitchenTaskCoordinator.FridgeTile);
        }

        private void RunnerWalkToFridge_Tick()
        {
            if (_mover.IsMoving)
                return;
            _coordinator.CompleteFetch(_fetchTicket);
            _fetchTicket = null;
            CurrentState = _goHome ? KitchenMonsterState.ReturnHome : KitchenMonsterState.RunnerIdle;
        }

        // ─────────────────────────────────── ReturnHome

        private void ReturnHome_Enter()
        {
            HideCarryDish();
            SetSprinting(false);
            _returnReachedExit = false;
            if (!TrySetPathTo(ExitTile))
                Entity.Destroy();
        }

        private void ReturnHome_Tick()
        {
            if (_mover.IsMoving)
                return;
            if (!_returnReachedExit)
            {
                _returnReachedExit = true;
                _facing?.SetFacing(Direction.Up);
                _mover.SetSingleTarget(TileCenter(DoorTile));
                return;
            }
            Entity.Destroy();
        }

        // ─────────────────────────────────── Helpers

        private void ShowCarryDish(DishType dish)
        {
            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var def = DishConfig.GetDefinition(dish);
            ShowCarrySprite(atlas?.GetSprite(def.BaseSpriteName + "_Small"));
        }

        private Nez.Textures.Sprite GetPlateSprite()
        {
            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            return atlas?.GetSprite("EmptyPlate");
        }

        private void ShowCarrySprite(Nez.Textures.Sprite sprite)
        {
            if (CarryRenderer == null || sprite == null) return;
            CarryRenderer.Sprite = sprite;
            CarryRenderer.SetLocalOffset(Vector2.Zero);
            if (BodyAnimator != null)
                CarryRenderer.SetLayerDepth(BodyAnimator.LayerDepth - 0.0001f);
            CarryRenderer.SetEnabled(true);
        }

        private void HideCarryDish() => CarryRenderer?.SetEnabled(false);

        /// <summary>
        /// Paths to the goal, or — when the goal itself is impassable (e.g. a table tile) —
        /// to its nearest passable 4-neighbor.
        /// </summary>
        private bool TrySetPathToTileOrNeighbor(Point goal)
        {
            if (TrySetPathTo(goal))
                return true;
            return _coordinator.Pathfinder.TryFindPassableNeighbor(goal, _mover.CurrentTile, out var neighbor)
                && TrySetPathTo(neighbor);
        }

        private bool TrySetPathTo(Point goal)
        {
            if (!_coordinator.Pathfinder.IsPassable(goal))
                return false;
            var start = _mover.CurrentTile;
            if (start == goal)
            {
                _mover.Stop();
                return true;
            }
            var path = _coordinator.Pathfinder.Search(start, goal);
            if (path == null) return false;
            _mover.SetPath(_coordinator.Pathfinder.SmoothPath(start, path));
            return true;
        }

        private static Vector2 TileCenter(Point tile) =>
            new Vector2(tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                        tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);

        private static Point WorldToTile(Vector2 worldPos) =>
            new Point((int)(worldPos.X / GameConfig.TileSize),
                      (int)(worldPos.Y / GameConfig.TileSize));
    }
}
