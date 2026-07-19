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
    /// Drives a single kitchen monster (cook, server, or runner): emerge from the house door,
    /// walk to the assigned post, idle, claim and execute tasks, and return home when asked.
    /// </summary>
    public class KitchenMonsterStateMachine : SimpleStateMachine<KitchenMonsterState>, IPausableComponent
    {
        private readonly AlliedMonster _monster;
        private readonly KitchenTaskCoordinator _coordinator;
        private readonly KitchenRole _role;
        private readonly int _stoveIndex; // -1 for server/runner

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

        // Cook state
        private KitchenTicket _activeCookTicket;
        private float _cookElapsed;

        // Server state
        private KitchenTicket _deliveryTicket;
        private Vector2 _deliveryTargetPos; // plate world pos on the table
        private bool _hasDeliveryTarget;
        private KitchenTaskCoordinator.BusJob _busJob;
        private bool _hasBusJob;
        private Vector2 _busPickupPos;

        // Runner state
        private KitchenTicket _fetchTicket;
        private float _collectElapsed;

        public bool ShouldPause => true;

        /// <summary>True once the monster has been asked to walk home and despawn.</summary>
        public bool IsReturningHome => _goHome;

        private Point DoorTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 2);
        private Point ExitTile => new Point(_houseAnchorTile.X, _houseAnchorTile.Y + 3);

        public KitchenMonsterStateMachine(AlliedMonster monster, KitchenTaskCoordinator coordinator,
            Point houseAnchorTile, KitchenRole role, int stoveIndex)
        {
            _monster = monster;
            _coordinator = coordinator;
            _houseAnchorTile = houseAnchorTile;
            _role = role;
            _stoveIndex = stoveIndex;
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
                CurrentState = KitchenMonsterState.WalkToPost;
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
            if (!_mover.IsMoving)
                CurrentState = KitchenMonsterState.WalkToPost;
        }

        // ─────────────────────────────────── WalkToPost

        private void WalkToPost_Enter()
        {
            Point post = GetPostTile();
            TrySetPathTo(post);
        }

        private void WalkToPost_Tick()
        {
            if (_goHome)
            {
                _mover.Stop();
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }
            if (!_mover.IsMoving)
                CurrentState = KitchenMonsterState.IdleAtPost;
        }

        // ─────────────────────────────────── IdleAtPost

        private void IdleAtPost_Tick()
        {
            if (_goHome)
            {
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            if (elapsedTimeInState < GameConfig.FarmMonsterIdlePollInterval)
                return;

            switch (_role)
            {
                case KitchenRole.Cook:    IdleAsCook();   break;
                case KitchenRole.Server:  IdleAsServer(); break;
                case KitchenRole.Runner:  IdleAsRunner(); break;
            }
        }

        // ─────────────────────────────────── Cook logic

        private void IdleAsCook()
        {
            var ticket = _coordinator.BeginCooking(_stoveIndex, _monster.CookingProficiency);
            if (ticket == null)
                return; // nothing to cook

            _activeCookTicket = ticket;
            _cookElapsed = 0f;
            _facing?.SetFacing(Direction.Up);
            CurrentState = KitchenMonsterState.Cooking;
        }

        private void Cooking_Tick()
        {
            if (_goHome)
            {
                // Release the ticket back to ReadyToCook
                _coordinator.ReleaseTicket(_activeCookTicket);
                _activeCookTicket = null;
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            _cookElapsed += Time.DeltaTime;
            float duration = DishConfig.GetCookDuration(_activeCookTicket.Dish, _monster.CookingProficiency);
            if (_cookElapsed < duration)
                return;

            // Cooking complete — spawn dish entity above the stove
            var plateTile = KitchenTaskCoordinator.GetPlateTile(_stoveIndex);
            var dishEntity = _coordinator.DishService?.SpawnDishAtTile(_activeCookTicket.Dish, plateTile);
            _coordinator.OnDishPlated(_activeCookTicket, dishEntity);
            _activeCookTicket = null;
            CurrentState = KitchenMonsterState.IdleAtPost;
        }

        // ─────────────────────────────────── Server logic

        private void IdleAsServer()
        {
            // Priority 1: bus jobs
            if (_coordinator.TryClaimBusJob(out var busJob))
            {
                _busJob = busJob;
                _hasBusJob = true;
                _busPickupPos = busJob.WorldPos;
                Point targetTile = WorldToTile(_busPickupPos);
                if (!TrySetPathToTileOrNeighbor(targetTile))
                    _mover.SetSingleTarget(_busPickupPos);
                CurrentState = KitchenMonsterState.BusingPlate;
                return;
            }

            // Priority 2: deliver Plated tickets
            var delivery = _coordinator.TryClaimDeliveryTicket();
            if (delivery != null)
            {
                _deliveryTicket = delivery;
                // Walk to pick up from above stove
                if (delivery.StoveIndex >= 0)
                {
                    var pickupTile = KitchenTaskCoordinator.GetPlateTile(delivery.StoveIndex);
                    if (!TrySetPathToTileOrNeighbor(pickupTile))
                        _mover.SetSingleTarget(TileCenter(pickupTile));
                    CurrentState = KitchenMonsterState.WalkToPickUpDish;
                }
                else
                {
                    // Stove unknown (shouldn't happen), just walk to sink
                    if (!TrySetPathTo(KitchenTaskCoordinator.SinkTile))
                        _mover.SetSingleTarget(KitchenTaskCoordinator.SinkWorldPos);
                    CurrentState = KitchenMonsterState.WalkToPickUpDish;
                }
                return;
            }

            // Priority 3: party orders
            if (_coordinator.TryGetNextPartyOrder(out int partySlot, out var partyDish))
            {
                // CreateTicket for party — no patron entity
                var ticket = _coordinator.CreateTicket(partyDish, true, partySlot, null);
                if (ticket != null)
                {
                    _coordinator.NotifyPartyOrderTaken(partySlot, ticket);
                    // No walking needed for party order-taking (it's handled via the interface)
                }
                return;
            }

            // Priority 3b: patron orders — walk to nearest unseated patron without an order
            var patronToOrder = FindPatronNeedingOrder();
            if (patronToOrder != null)
            {
                var patronComp = patronToOrder.GetComponent<TavernPatronComponent>();
                Point patronTile = WorldToTile(patronToOrder.Transform.Position);
                if (TrySetPathTo(patronTile))
                {
                    _deliveryTicket = null; // reuse as "patron we're approaching" — store target in FSM field
                    _hasDeliveryTarget = false;
                    // Store patron reference in delivery slot temporarily
                    _deliveryTargetPos = patronToOrder.Transform.Position;
                    _hasDeliveryTarget = true;
                    CurrentState = KitchenMonsterState.WalkToTakeOrder;
                }
            }
        }

        private void WalkToPickUpDish_Tick()
        {
            if (_goHome || (_deliveryTicket != null && _deliveryTicket.State == TicketState.Canceled))
            {
                // Ticket canceled mid-walk — divert to sink
                DivertToSink();
                return;
            }

            if (!_mover.IsMoving)
            {
                // Arrived at the stove — pick up the dish
                if (_deliveryTicket != null && _deliveryTicket.PlatedDishEntity != null
                    && !_deliveryTicket.PlatedDishEntity.IsDestroyed)
                {
                    _deliveryTicket.PlatedDishEntity.Destroy();
                    _deliveryTicket.PlatedDishEntity = null;
                    ShowCarryDish(_deliveryTicket.Dish);
                }

                // Determine where to deliver
                if (_deliveryTicket != null)
                {
                    if (_deliveryTicket.IsPartyTicket)
                    {
                        Point seatTile = GetPartySeatTile(_deliveryTicket.PartySlot);
                        if (TavernSeatConfig.TryGetPlateWorldPosition(seatTile, out var pos))
                        {
                            _deliveryTargetPos = pos;
                            _hasDeliveryTarget = true;
                            Point tableTile = WorldToTile(pos);
                            if (!TrySetPathToTileOrNeighbor(tableTile))
                                _mover.SetSingleTarget(pos);
                        }
                        else
                        {
                            DivertToSink();
                            return;
                        }
                    }
                    else if (_deliveryTicket.PatronEntity != null && !_deliveryTicket.PatronEntity.IsDestroyed)
                    {
                        var patronComp = _deliveryTicket.PatronEntity.GetComponent<TavernPatronComponent>();
                        if (patronComp != null)
                        {
                            if (TavernSeatConfig.TryGetPlateWorldPosition(patronComp.SeatTile, out var pos))
                            {
                                _deliveryTargetPos = pos;
                                _hasDeliveryTarget = true;
                                Point tableTile = WorldToTile(pos);
                                if (!TrySetPathToTileOrNeighbor(tableTile))
                                    _mover.SetSingleTarget(pos);
                            }
                            else
                            {
                                DivertToSink();
                                return;
                            }
                        }
                        else
                        {
                            DivertToSink();
                            return;
                        }
                    }
                    else
                    {
                        DivertToSink();
                        return;
                    }
                }

                CurrentState = KitchenMonsterState.DeliveringDish;
            }
        }

        private void DeliveringDish_Tick()
        {
            if (_deliveryTicket != null && _deliveryTicket.State == TicketState.Canceled)
            {
                // Patron left mid-delivery — go to sink
                DivertToSink();
                return;
            }

            if (!_mover.IsMoving)
            {
                // Arrived at table — place dish
                HideCarryDish();
                Entity dishEntity = null;
                if (_hasDeliveryTarget && _deliveryTicket != null)
                {
                    dishEntity = _coordinator.DishService?.SpawnDishAtWorldPos(
                        _deliveryTicket.Dish, _deliveryTargetPos);
                }
                _coordinator.OnTicketDelivered(_deliveryTicket, dishEntity);
                _deliveryTicket = null;
                _hasDeliveryTarget = false;
                CurrentState = KitchenMonsterState.IdleAtPost;
            }
        }

        private void WalkToTakeOrder_Tick()
        {
            if (_goHome)
            {
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            if (!_mover.IsMoving)
            {
                // Arrived near patron — try to take order
                // Find the patron entity near our current position
                var patron = FindPatronNeedingOrder();
                if (patron != null)
                {
                    var patronComp = patron.GetComponent<TavernPatronComponent>();
                    // Pick a random orderable dish
                    var orderableDishes = new System.Collections.Generic.List<DishType>(DishTypeInfo.Count);
                    _coordinator.GetOrderableDishes(orderableDishes);
                    if (orderableDishes.Count > 0 && _coordinator.IsKitchenOpen)
                    {
                        int idx = Nez.Random.Range(0, orderableDishes.Count);
                        var dish = orderableDishes[idx];
                        var ticket = _coordinator.CreateTicket(dish, false, -1, patron);
                        if (ticket != null)
                        {
                            patronComp.OnOrderTaken(ticket);
                        }
                    }
                }
                CurrentState = KitchenMonsterState.IdleAtPost;
            }
        }

        private void BusingPlate_Tick()
        {
            if (!_mover.IsMoving)
            {
                if (_hasBusJob)
                {
                    // Arrived at pickup — despawn the plate entity, carrying its actual sprite
                    if (_busJob.DishEntity != null && !_busJob.DishEntity.IsDestroyed)
                    {
                        var busSprite = _busJob.DishEntity.GetComponent<Nez.Sprites.SpriteRenderer>();
                        ShowCarrySprite(busSprite?.Sprite);
                        _busJob.DishEntity.Destroy();
                    }
                    _hasBusJob = false;

                    // Walk to sink
                    TrySetPathTo(KitchenTaskCoordinator.SinkTile);
                }
                else
                {
                    // Arrived at sink — despawn carry and done
                    HideCarryDish();
                    CurrentState = KitchenMonsterState.IdleAtPost;
                }
            }
        }

        // ─────────────────────────────────── Runner logic

        private void IdleAsRunner()
        {
            var ticket = _coordinator.TryClaimFetchTicket();
            if (ticket == null) return;

            _fetchTicket = ticket;
            _collectElapsed = 0f;

            Point myTile = WorldToTile(Entity.Transform.Position);
            if (_coordinator.TryFindNearestStorageDoor(myTile, out var door))
            {
                TrySetPathTo(door);
                CurrentState = KitchenMonsterState.WalkToStorage;
            }
            else
            {
                // No storage — mark fetched immediately (ingredients are already deducted logically)
                _coordinator.OnIngredientsFetched(_fetchTicket);
                _fetchTicket = null;
            }
        }

        private void WalkToStorage_Tick()
        {
            if (_goHome)
            {
                // Release the runner claim
                _coordinator.ReleaseTicket(_fetchTicket);
                _fetchTicket = null;
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            if (!_mover.IsMoving)
            {
                _collectElapsed = 0f;
                CurrentState = KitchenMonsterState.CollectingIngredients;
            }
        }

        private void CollectingIngredients_Tick()
        {
            _collectElapsed += Time.DeltaTime;
            if (_collectElapsed < 1f)
                return;

            // Return to kitchen (sink area)
            var sinkTile = KitchenTaskCoordinator.SinkTile;
            TrySetPathTo(sinkTile);
            CurrentState = KitchenMonsterState.ReturnToKitchen;
        }

        private void ReturnToKitchen_Tick()
        {
            if (_goHome)
            {
                _coordinator.ReleaseTicket(_fetchTicket);
                _fetchTicket = null;
                CurrentState = KitchenMonsterState.ReturnHome;
                return;
            }

            if (!_mover.IsMoving)
            {
                _coordinator.OnIngredientsFetched(_fetchTicket);
                _fetchTicket = null;
                CurrentState = KitchenMonsterState.IdleAtPost;
            }
        }

        // ─────────────────────────────────── ReturnHome

        private void ReturnHome_Enter()
        {
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

        private Point GetPostTile()
        {
            switch (_role)
            {
                case KitchenRole.Cook:
                    return KitchenTaskCoordinator.GetStoveTile(_stoveIndex);
                case KitchenRole.Server:
                    return KitchenTaskCoordinator.SinkTile;
                case KitchenRole.Runner:
                    // Runners idle just east of the sink (west of it is stove 3)
                    return new Point(GameConfig.KitchenSinkTileX + 1, GameConfig.KitchenSinkTileY);
                default:
                    return ExitTile;
            }
        }

        private void DivertToSink()
        {
            HideCarryDish();
            if (_deliveryTicket != null)
            {
                _coordinator.AbortDelivery(_deliveryTicket);
                _deliveryTicket = null;
            }
            _hasDeliveryTarget = false;
            TrySetPathTo(KitchenTaskCoordinator.SinkTile);
            // Use BusingPlate state with no bus job so we just walk to sink and return to idle
            _hasBusJob = false;
            CurrentState = KitchenMonsterState.BusingPlate;
        }

        private Entity FindPatronNeedingOrder()
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

                float dist = Vector2.Distance(myPos, e.Transform.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = e;
                }
            }
            return best;
        }

        private static Point GetPartySeatTile(int partySlot)
        {
            switch (partySlot)
            {
                case 0: return new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
                case 1: return new Point(GameConfig.TavernMercenary1SeatTileX, GameConfig.TavernMercenary1SeatTileY);
                case 2: return new Point(GameConfig.TavernMercenary2SeatTileX, GameConfig.TavernMercenary2SeatTileY);
                default: return new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
            }
        }

        private void ShowCarryDish(DishType dish)
        {
            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var def = DishConfig.GetDefinition(dish);
            ShowCarrySprite(atlas?.GetSprite(def.BaseSpriteName + "_Small"));
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
