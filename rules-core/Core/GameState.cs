// Port of game-core/src/main/java/com/infiniteconquest/core/GameState.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Authoritative match state.
    /// DEVIATIONS (per task contract / noted for AI-084):
    /// - Java UUID card IDs → long, issued from a GameState-owned monotonic
    ///   counter seeded through the RNG stream. No Guid anywhere.
    /// - Java LinkedHashMap (insertion order) → SortedDictionary (ascending ID
    ///   order). Identical whenever cards register in ID-creation order (true
    ///   for live matches); see PORT_NOTES.md.
    /// - GameState owns the SeededRng (CONVENTIONS.md §3). The copy constructor
    ///   cannot clone System.Random's stream, so the copy derives a fresh seed
    ///   from (seed XOR nextEventSequence) — deterministic, documented.
    /// - Event detail strings embed BoardPosition/ID ToStrings, which differ
    ///   from Java's (UUID hex, Java record toString). Conformance on detail
    ///   strings needs normalization — flagged in PORT_NOTES.md.
    /// </summary>
    public sealed class GameState
    {
        private readonly long _seed;
        private readonly MatchRules _rules;
        private readonly BoardState _board;
        private readonly List<PlayerState> _players;
        private readonly SortedDictionary<long, CardInstance> _cards;
        private readonly List<GameEvent> _events;
        private readonly int[] _personalTurns;
        private readonly bool[] _mulliganCompleted;
        private readonly int[] _landsPlayedThisTurn;
        private readonly int[] _structuresPlayedThisTurn;
        private readonly HashSet<string> _terrainTriggersUsed;
        private readonly HashSet<string> _capitalPassivesUsedThisTurn;
        private readonly CapitalPassiveRules _capitalPassiveRules;
        private readonly CardAbilityRules _cardAbilityRules;
        private int _activePlayer;
        private int _startingPlayer;
        private int _turnNumber;
        private Phase _phase = Phase.START;
        private long _nextEventSequence;
        private int? _winner;
        private bool _started;
        private bool _mulliganWindowOpen = true;
        private bool _initialCapitalPassiveActivated;

        /// <summary>All game-logic randomness flows through this instance.</summary>
        public SeededRng Rng { get; }
        private long _nextInstanceId;

        /// <summary>Issues the next deterministic instance ID.</summary>
        public long NextInstanceId() => _nextInstanceId++;

        public GameState(long seed) : this(seed, MatchRules.Current(), true) { }

        internal GameState(long seed, MatchRules rules, bool startImmediately)
        {
            _seed = seed;
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rng = new SeededRng(seed);
            // Counter seeded through the RNG stream (no Guid).
            _nextInstanceId = 1 + Rng.Next(int.MaxValue - 1);
            _board = new BoardState();
            _players = new List<PlayerState> { new PlayerState(0), new PlayerState(1) };
            _cards = new SortedDictionary<long, CardInstance>();
            _events = new List<GameEvent>();
            _personalTurns = new int[2];
            _mulliganCompleted = new bool[2];
            _landsPlayedThisTurn = new int[2];
            _structuresPlayedThisTurn = new int[2];
            _terrainTriggersUsed = new HashSet<string>();
            _capitalPassivesUsedThisTurn = new HashSet<string>();
            _capitalPassiveRules = new CapitalPassiveRules();
            _cardAbilityRules = new CardAbilityRules();
            if (startImmediately) InitializeMatch();
        }

        /// <summary>
        /// Deep copy of the full authoritative state, used for AI lookahead
        /// simulations. The rules objects are stateless and recreated; everything
        /// else is duplicated, so applying engine actions to the copy can never
        /// affect the original.
        /// </summary>
        public GameState Copy() => new GameState(this);

        private GameState(GameState source)
        {
            _seed = source._seed;
            _rules = source._rules;
            // System.Random's stream cannot be cloned; derive a deterministic
            // stream for the copy without touching the source's stream.
            Rng = new SeededRng(source._seed ^ source._nextEventSequence);
            _nextInstanceId = source._nextInstanceId;
            _board = new BoardState(source._board);
            _players = new List<PlayerState> { new PlayerState(source._players[0]), new PlayerState(source._players[1]) };
            _cards = new SortedDictionary<long, CardInstance>();
            foreach (var card in source._cards.Values) _cards.Add(card.InstanceId, new CardInstance(card));
            _events = new List<GameEvent>(source._events);
            _personalTurns = (int[])source._personalTurns.Clone();
            _mulliganCompleted = (bool[])source._mulliganCompleted.Clone();
            _landsPlayedThisTurn = (int[])source._landsPlayedThisTurn.Clone();
            _structuresPlayedThisTurn = (int[])source._structuresPlayedThisTurn.Clone();
            _terrainTriggersUsed = new HashSet<string>(source._terrainTriggersUsed);
            _capitalPassivesUsedThisTurn = new HashSet<string>(source._capitalPassivesUsedThisTurn);
            _capitalPassiveRules = new CapitalPassiveRules();
            _cardAbilityRules = new CardAbilityRules();
            _activePlayer = source._activePlayer;
            _startingPlayer = source._startingPlayer;
            _turnNumber = source._turnNumber;
            _phase = source._phase;
            _nextEventSequence = source._nextEventSequence;
            _winner = source._winner;
            _started = source._started;
            _mulliganWindowOpen = source._mulliganWindowOpen;
            _initialCapitalPassiveActivated = source._initialCapitalPassiveActivated;
        }

        public long Seed() => _seed;
        public MatchRules Rules() => _rules;
        public BoardState Board => _board;
        public PlayerState Player(int id) => _players[id];
        public int ActivePlayer => _activePlayer;
        public int StartingPlayer() => _startingPlayer;
        public int TurnNumber() => _turnNumber;
        public int PersonalTurnNumber(int id) => _personalTurns[id];

        public bool CanPlayDevelopment(int playerId, CardType type)
        {
            switch (type)
            {
                case CardType.LAND: return _landsPlayedThisTurn[playerId] == 0;
                case CardType.STRUCTURE: return _structuresPlayedThisTurn[playerId] == 0;
                default: return true;
            }
        }

        /// <summary>Lands played this turn, for client-side legality hints in net snapshots.</summary>
        public int LandsPlayedThisTurn(int playerId) => _landsPlayedThisTurn[playerId];
        /// <summary>Structures played this turn, for client-side legality hints in net snapshots.</summary>
        public int StructuresPlayedThisTurn(int playerId) => _structuresPlayedThisTurn[playerId];
        public Phase Phase() => _phase;
        /// <summary>True while either player may still submit a mulligan decision.</summary>
        public bool IsMulliganWindowOpen() => _mulliganWindowOpen;
        public int? Winner() => _winner;
        public IReadOnlyList<GameEvent> Events() => _events;
        public CardInstance? Card(long id) => _cards.TryGetValue(id, out var card) ? card : null;

        public IReadOnlyList<CardInstance> BattlefieldCards(int playerId)
        {
            return _cards.Values
                .Where(card => card.Owner == playerId && card.Zone == Zone.BATTLEFIELD)
                .OrderBy(card => card.InstanceId)
                .ToList();
        }

        public int GpIncomePerTurn(int playerId)
        {
            return BattlefieldCards(playerId)
                .Where(card => card.Definition.Type == CardType.LAND
                    || card.Definition.Type == CardType.STRUCTURE
                    || card.Definition.Type == CardType.CAPITAL)
                .Sum(card => card.Definition.Type == CardType.CAPITAL ? 1 : card.Definition.Income());
        }

        public void Mulligan(int playerId, IReadOnlyCollection<long> discardedCardIds)
        {
            if (playerId < 0 || playerId > 1) throw new ArgumentException("Player must be 0 or 1");
            if (!_mulliganWindowOpen || _turnNumber != 1 || _phase == Phase.GAME_OVER)
                throw new InvalidOperationException("Mulligan window has closed");
            if (_mulliganCompleted[playerId]) throw new InvalidOperationException("Player already completed a mulligan");
            var discarded = new HashSet<long>(discardedCardIds);
            if (discarded.Count != discardedCardIds.Count) throw new ArgumentException("Discarded cards must be unique");
            if (discarded.Count > 3) throw new ArgumentException("You may discard at most 3 cards");
            var openingHand = new List<long>(Player(playerId).Hand);
            if (!discarded.All(openingHand.Contains)) throw new ArgumentException("Discarded cards must be in the opening hand");
            int replaced = 0;
            foreach (var id in openingHand)
            {
                if (!discarded.Contains(id)) continue;
                Player(playerId).RemoveFromHand(id);
                var card = Card(id) ?? throw new InvalidOperationException("Mulliganed card is not registered: " + id);
                card.MoveTo(Zone.DISCARD);
                Player(playerId).AddToDiscard(id);
                replaced++;
            }
            for (int i = 0; i < replaced; i++) DrawCard(playerId);
            _mulliganCompleted[playerId] = true;
            if (_mulliganCompleted[0] && _mulliganCompleted[1]) _mulliganWindowOpen = false;
            Emit(GameEvent.Type.MULLIGAN_COMPLETED, playerId, "Discarded and redrew " + replaced);
        }

        public CapitalPassive? CapitalPassiveFor(int playerId)
        {
            var capital = BattlefieldCards(playerId).FirstOrDefault(card => card.Definition.Type == CardType.CAPITAL);
            return capital == null ? null : _capitalPassiveRules.PassiveFor(capital.Definition);
        }

        public void Register(CardInstance card)
        {
            if (!_cards.TryAdd(card.InstanceId, card)) throw new ArgumentException("Duplicate card instance ID");
        }

        internal void SetStartingPlayer(int playerId)
        {
            if (_started) throw new InvalidOperationException("Starting player is already locked");
            if (playerId < 0 || playerId > 1) throw new ArgumentException("Player must be 0 or 1");
            _startingPlayer = playerId;
        }

        internal void InitializeMatch()
        {
            if (_started) throw new InvalidOperationException("Match already started");
            _started = true;
            _activePlayer = _startingPlayer;
            _turnNumber = 1;
            _personalTurns[_activePlayer] = 1;
            foreach (var player in _players)
                player.InitializeGp(player.Id == 0 ? _rules.StartingGp : _rules.SecondPlayerStartingGp);
            Emit(GameEvent.Type.MATCH_STARTED, _activePlayer,
                $"Match seed {_seed}; coin flip: Player {_activePlayer + 1} starts");
            StartTurn();
        }

        public void ActivateInitialCapitalPassive()
        {
            if (!_started || _turnNumber != 1) throw new InvalidOperationException("Initial Capital passive timing has passed");
            if (_initialCapitalPassiveActivated) throw new InvalidOperationException("Initial Capital passive already activated");
            _initialCapitalPassiveActivated = true;
            GenerateCapitalGp(_activePlayer);
            _capitalPassiveRules.OnTurnStarted(this, _activePlayer);
        }

        internal void DrawInitialHands()
        {
            for (int playerId = 0; playerId < 2; playerId++)
                for (int i = 0; i < _rules.InitialHandSizeFor(playerId != _startingPlayer); i++)
                    DrawCard(playerId);
        }

        internal void AdvanceTurn()
        {
            _mulliganWindowOpen = false;
            _phase = Phase.END;
            Emit(GameEvent.Type.PHASE_CHANGED, _activePlayer, "END");
            Emit(GameEvent.Type.TURN_ENDED, _activePlayer, "Turn ended");
            _activePlayer = 1 - _activePlayer;
            _turnNumber++;
            _personalTurns[_activePlayer]++;
            StartTurn();
        }

        internal void RecordCardPlayed(CardInstance card)
        {
            _mulliganWindowOpen = false;
            if (card.Definition.Type == CardType.LAND) _landsPlayedThisTurn[card.Owner]++;
            if (card.Definition.Type == CardType.STRUCTURE) _structuresPlayedThisTurn[card.Owner]++;
            Emit(GameEvent.Type.CARD_PLAYED, card.Owner, card.InstanceId.ToString());
            _capitalPassiveRules.OnCardPlayed(this, card);
            ApplyDevelopmentDeployPassive(card);
            _cardAbilityRules.Resolve(this, card, AbilityTrigger.ENTERS_PLAY);
        }

        internal void SpendGp(int playerId, int amount, string reason)
        {
            Player(playerId).SpendGp(amount);
            if (amount > 0) Emit(GameEvent.Type.GP_SPENT, playerId, $"{amount} for {reason}");
        }

        internal void RecordCharacterMoved(CardInstance card, BoardPosition from, BoardPosition to, int distance)
        {
            _mulliganWindowOpen = false;
            Emit(GameEvent.Type.CHARACTER_MOVED, card.Owner, $"{card.InstanceId} {from} -> {to} cost {distance}");
        }

        internal void RecordAttack(CardInstance attacker, CardInstance target)
        {
            _mulliganWindowOpen = false;
            Emit(GameEvent.Type.ATTACK_RESOLVED, attacker.Owner, $"{attacker.InstanceId} -> {target.InstanceId}");
        }

        internal void RecordOpportunityAttack(CardInstance attacker, CardInstance target, BoardPosition trigger)
        {
            Emit(GameEvent.Type.OPPORTUNITY_ATTACK, attacker.Owner,
                $"{attacker.InstanceId} -> {target.InstanceId} at {trigger.X},{trigger.Y}");
        }

        internal void DrawCards(int playerId, int amount)
        {
            for (int i = 0; i < amount; i++) DrawCard(playerId);
        }

        internal void DrawCardsOfType(int playerId, CardType type, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                long? drawn = Player(playerId).DrawFirst(id =>
                {
                    var candidate = Card(id);
                    return candidate != null && candidate.Definition.Type == type;
                });
                if (!drawn.HasValue)
                {
                    Emit(GameEvent.Type.DRAW_FAILED, playerId, "No " + type + " remains in deck");
                    return;
                }
                var instance = Card(drawn.Value) ?? throw new InvalidOperationException("Deck references unregistered card");
                instance.MoveTo(Zone.HAND);
                Emit(GameEvent.Type.CARD_DRAWN, playerId, instance.InstanceId.ToString());
            }
        }

        internal void ReturnCharacterToHand(CardInstance card)
        {
            _board.Remove(card.InstanceId);
            card.MoveTo(Zone.HAND);
            Player(card.Owner).AddToHand(card.InstanceId);
        }

        internal void Destroy(CardInstance card)
        {
            if (card.Zone != Zone.BATTLEFIELD) return;
            bool permanent = card.Definition.IsPermanent();
            var formerPosition = _board.PositionOf(card.InstanceId);
            _board.Remove(card.InstanceId);
            card.MoveTo(Zone.DISCARD);
            Player(card.Owner).AddToDiscard(card.InstanceId);
            Emit(GameEvent.Type.CARD_DESTROYED, card.Owner, card.InstanceId.ToString());
            _cardAbilityRules.Resolve(this, card, AbilityTrigger.DESTROYED);
            if (card.Definition.HasKeyword(Keyword.ARCHIVE))
            {
                int amount = card.Definition.KeywordValue(Keyword.ARCHIVE).Amount;
                RecordTerrain(card, card, Keyword.ARCHIVE, amount);
                DrawCards(card.Owner, amount);
            }
            if (permanent) _capitalPassiveRules.OnPermanentDestroyed(this, card);
            if (permanent && _phase != Phase.GAME_OVER)
            {
                int result = new VictoryEvaluator().WinnerAfterCapitalLoss(this, card.Owner);
                if (result >= 0)
                {
                    FinishGame(result, "Player " + result + " destroyed the enemy Capital");
                }
            }
            if (_phase != Phase.GAME_OVER && formerPosition != null)
            {
                var topId = _board.TopAt(formerPosition);
                if (topId.HasValue)
                {
                    var revealed = Card(topId.Value);
                    if (revealed != null && revealed.Definition.IsPermanent() && revealed.Damage >= revealed.Definition.HitPoints)
                        Destroy(revealed);
                }
            }
        }

        private void StartTurn()
        {
            _capitalPassivesUsedThisTurn.Clear();
            _terrainTriggersUsed.Clear();
            _landsPlayedThisTurn[_activePlayer] = 0;
            _structuresPlayedThisTurn[_activePlayer] = 0;
            foreach (var card in _cards.Values) card.ClearCombatDamage();
            _phase = Phase.START;
            Emit(GameEvent.Type.PHASE_CHANGED, _activePlayer, "START");
            GeneratePermanentGp(_activePlayer);
            ApplyDevelopmentStartPassives(_activePlayer);
            ResetControlledCards(_activePlayer);
            TerrainRules.StartTurn(this, _activePlayer);
            foreach (var card in BattlefieldCards(_activePlayer))
                _cardAbilityRules.Resolve(this, card, AbilityTrigger.PASSIVE);
            if (_turnNumber > 1)
            {
                for (int i = 0; i < _rules.CardsDrawnAtTurnStart; i++) DrawCard(_activePlayer);
            }
            if (_phase == Phase.GAME_OVER) return;
            _capitalPassiveRules.OnTurnStarted(this, _activePlayer);
            if (_phase == Phase.GAME_OVER) return;
            Emit(GameEvent.Type.TURN_STARTED, _activePlayer, "Personal turn " + _personalTurns[_activePlayer]);
            _phase = Phase.PLAY;
            Emit(GameEvent.Type.PHASE_CHANGED, _activePlayer, "PLAY");
        }

        private void ResetControlledCards(int playerId)
        {
            int untapped = 0;
            foreach (var card in _cards.Values)
            {
                if (card.Owner == playerId && card.Zone == Zone.BATTLEFIELD)
                {
                    if (card.Tapped) untapped++;
                    card.ResetTurnActions();
                }
            }
            Emit(GameEvent.Type.CARDS_UNTAPPED, playerId, untapped.ToString());
        }

        private void DrawCard(int playerId)
        {
            long? drawn = Player(playerId).DrawOne();
            if (!drawn.HasValue)
            {
                Emit(GameEvent.Type.DRAW_FAILED, playerId, "Deck is empty");
                foreach (var card in _cards.Values)
                {
                    if (card.Owner == playerId && card.Zone == Zone.BATTLEFIELD && card.Definition.IsPermanent())
                    {
                        card.AddDamage(1);
                        Emit(GameEvent.Type.EXHAUSTION_DAMAGE, playerId, card.InstanceId.ToString());
                        var position = _board.PositionOf(card.InstanceId);
                        long? top = position == null ? null : _board.TopAt(position);
                        if (card.Damage >= card.Definition.HitPoints && top == card.InstanceId) Destroy(card);
                        if (_phase == Phase.GAME_OVER) break;
                    }
                }
                return;
            }
            var instance = Card(drawn.Value) ?? throw new InvalidOperationException("Deck references unregistered card");
            instance.MoveTo(Zone.HAND);
            Emit(GameEvent.Type.CARD_DRAWN, playerId, instance.InstanceId.ToString());
        }

        internal CardInstance? ReturnMostRecentDiscardedCharacter(int playerId)
        {
            long? id = Player(playerId).RemoveMostRecentDiscard(value =>
            {
                var candidate = Card(value);
                return candidate != null && candidate.Definition.Type == CardType.CHARACTER;
            });
            if (!id.HasValue) return null;
            var returned = Card(id.Value) ?? throw new InvalidOperationException("Discarded card is not registered: " + id.Value);
            returned.MoveTo(Zone.HAND);
            Player(playerId).AddToHand(returned.InstanceId);
            return returned;
        }

        internal bool TryUseCapitalPassive(int playerId, CapitalPassive passive)
        {
            return _capitalPassivesUsedThisTurn.Add(playerId + ":" + passive);
        }

        internal void MarkCapitalPassiveUsed(int playerId, CapitalPassive passive)
        {
            _capitalPassivesUsedThisTurn.Add(playerId + ":" + passive);
        }

        internal void RecordCapitalPassive(int playerId, CapitalPassive passive, string detail)
        {
            Emit(GameEvent.Type.CAPITAL_PASSIVE_TRIGGERED, playerId, passive + ": " + detail);
        }

        private void GeneratePermanentGp(int playerId)
        {
            int generated = GpIncomePerTurn(playerId);
            if (generated > 0) Player(playerId).RestoreGp(generated);
            Emit(GameEvent.Type.GP_GENERATED, playerId, generated + " GP from Capital, Lands and Structures");
        }

        private void GenerateCapitalGp(int playerId)
        {
            bool controlsCapital = BattlefieldCards(playerId).Any(card => card.Definition.Type == CardType.CAPITAL);
            if (!controlsCapital) return;
            Player(playerId).RestoreGp(1);
            Emit(GameEvent.Type.GP_GENERATED, playerId, "1 GP from Capital");
        }

        private void ApplyDevelopmentDeployPassive(CardInstance card)
        {
            switch (card.Definition.DevelopmentPassive)
            {
                case DevelopmentPassive.DRAW_ON_DEPLOY:
                    DrawCards(card.Owner, 1);
                    RecordDevelopmentPassive(card);
                    break;
                case DevelopmentPassive.HEAL_CAPITAL_ON_DEPLOY:
                    var capital = BattlefieldCards(card.Owner).FirstOrDefault(value => value.Definition.Type == CardType.CAPITAL);
                    if (capital != null) capital.HealDamage(3);
                    RecordDevelopmentPassive(card);
                    break;
                default:
                    break;
            }
        }

        private void ApplyDevelopmentStartPassives(int playerId)
        {
            foreach (var card in BattlefieldCards(playerId)
                .Where(card => card.Definition.DevelopmentPassive == DevelopmentPassive.SELF_REPAIR && card.Damage > 0))
            {
                card.HealDamage(2);
                RecordDevelopmentPassive(card);
            }
        }

        private void RecordDevelopmentPassive(CardInstance card)
        {
            Emit(GameEvent.Type.DEVELOPMENT_PASSIVE_TRIGGERED, card.Owner,
                card.InstanceId + " " + DevelopmentRules.PassiveText(card.Definition.DevelopmentPassive));
        }

        internal void RecordCardAbility(CardInstance card, CardAbility ability)
        {
            Emit(GameEvent.Type.CARD_ABILITY_TRIGGERED, card.Owner,
                $"{card.InstanceId} {ability.Trigger} {ability.Effect} {ability.Amount}{(ability.GpCost > 0 ? " cost " + ability.GpCost : "")}");
        }

        public bool UseTerrainTrigger(CardInstance source, CardInstance target, Keyword keyword)
        {
            return _terrainTriggersUsed.Add($"{source.InstanceId}:{target.InstanceId}:{keyword}");
        }

        internal void RecordTerrain(CardInstance source, CardInstance target, Keyword keyword, int amount)
        {
            var position = _board.PositionOf(target.InstanceId);
            Emit(GameEvent.Type.TERRAIN_TRIGGERED, source.Owner,
                $"{source.InstanceId} {target.InstanceId} {keyword} {amount}{(position == null ? "" : $" {position.X},{position.Y}")}");
        }

        private void FinishGame(int? winningPlayer, string detail)
        {
            _winner = winningPlayer;
            _phase = Phase.GAME_OVER;
            Emit(GameEvent.Type.GAME_OVER, winningPlayer ?? -1, detail);
        }

        private void Emit(GameEvent.Type type, int playerId, string detail)
        {
            _events.Add(new GameEvent(_nextEventSequence++, _turnNumber, playerId, type, detail));
        }

        /// <summary>
        /// Placeholder definition for hidden opponent cards. Instances are inert:
        /// only hand/deck counts are ever read from them.
        /// Assumes Data.CardDefinition has a 9-arg constructor
        /// (id, name, type, faction, cost, attack, defense, range, movement).
        /// </summary>
        private static readonly CardDefinition HiddenCard =
            new CardDefinition("hidden_card", "Hidden Card", CardType.CHARACTER, "HIDDEN", 0, 0, 0, 0, 0);

        /// <summary>
        /// Deterministic placeholder IDs (negative range; live IDs are positive)
        /// so consecutive snapshots diff cleanly. Java used nameUUIDFromBytes;
        /// values differ by construction — flagged in PORT_NOTES.md.
        /// </summary>
        private static long HiddenCardId(int player, Zone zone, int index)
            => -(1_000_000L * player + 1_000L * (int)zone + index + 1);

        /// <summary>
        /// Rebuilds a client-side GameState from a redacted GameSnapshot.
        /// The result is a faithful copy of everything the viewing player may see:
        /// public zones in full, the viewer's own hand in full, and opponent
        /// hidden zones as counts only. It is used to render confirmed server state;
        /// commands are never applied to it locally. Unknown definition IDs fail fast.
        /// </summary>
        public static GameState FromSnapshot(GameSnapshot snapshot, Func<string, CardDefinition> definitions)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            MatchRules rules = snapshot.rulesId == "SQUARE" ? MatchRules.Current() : MatchRules.Hex();
            var state = new GameState(snapshot.seed, rules, false);
            state._started = true;
            state._activePlayer = snapshot.activePlayer;
            state._startingPlayer = snapshot.startingPlayer;
            state._turnNumber = snapshot.turnNumber;
            Array.Copy(snapshot.personalTurns, state._personalTurns, 2);
            state._phase = Enum.Parse<Phase>(snapshot.phase);
            state._winner = snapshot.winner;
            state._mulliganWindowOpen = false;
            state._mulliganCompleted[0] = true;
            state._mulliganCompleted[1] = true;
            state._initialCapitalPassiveActivated = true;
            Array.Copy(snapshot.landsPlayed, state._landsPlayedThisTurn, 2);
            Array.Copy(snapshot.structuresPlayed, state._structuresPlayedThisTurn, 2);

            var deckOrder = new List<List<long>> { new List<long>(), new List<long>() };
            var hands = new List<CardInstance>();
            var discards = new List<CardInstance>();
            var boardViews = new List<GameSnapshot.CardView>();
            foreach (var view in snapshot.cards)
            {
                var definition = definitions(view.definitionId)
                    ?? throw new ArgumentException("Unknown card definition: " + view.definitionId);
                var card = new CardInstance(view.instanceId, definition, view.owner, view.zone);
                card.AddDamage(view.damage);
                card.SetTapped(view.tapped);
                card.AddAttackBonus(view.attackBonus);
                card.AddDefenseBonus(view.defenseBonus);
                if (view.attackedThisTurn) card.MarkAttacked();
                if (view.blinkUsedThisTurn) card.MarkBlinkUsed();
                if (view.abilityUsedThisTurn) card.MarkAbilityUsed();
                card.SpendMovement(view.movementSpent);
                state.Register(card);
                switch (view.zone)
                {
                    case Zone.DECK: deckOrder[view.owner].Add(view.instanceId); break;
                    case Zone.HAND: hands.Add(card); break;
                    case Zone.DISCARD: discards.Add(card); break;
                    case Zone.BATTLEFIELD: boardViews.Add(view); break;
                }
            }
            for (int playerId = 0; playerId < 2; playerId++)
            {
                var order = new List<long>(deckOrder[playerId]);
                state._players[playerId].InitializeGp(snapshot.maxGp[playerId]);
                int excess = snapshot.maxGp[playerId] - snapshot.gp[playerId];
                if (excess > 0) state._players[playerId].SpendGp(excess);
                if (playerId != snapshot.viewingPlayer)
                {
                    // Hidden deck: pad with deterministic placeholders so deck size
                    // is exact. Placeholders are inert; only counts are ever read.
                    for (int i = order.Count; i < snapshot.deckCounts[playerId]; i++)
                    {
                        long id = HiddenCardId(playerId, Zone.DECK, i);
                        state.Register(new CardInstance(id, HiddenCard, playerId, Zone.DECK));
                        order.Add(id);
                    }
                }
                state._players[playerId].LoadDeck(order);
            }
            foreach (var card in hands) state._players[card.Owner].AddToHand(card.InstanceId);
            if (snapshot.viewingPlayer == 0 || snapshot.viewingPlayer == 1)
            {
                int opponent = 1 - snapshot.viewingPlayer;
                for (int i = 0; i < snapshot.handCounts[opponent]; i++)
                {
                    long id = HiddenCardId(opponent, Zone.HAND, i);
                    state.Register(new CardInstance(id, HiddenCard, opponent, Zone.HAND));
                    state._players[opponent].AddToHand(id);
                }
            }
            foreach (var card in discards) state._players[card.Owner].AddToDiscard(card.InstanceId);
            foreach (var view in boardViews) state._board.Push(view.position!, view.instanceId);

            state._events.AddRange(snapshot.events);
            long maxSequence = snapshot.events.Count == 0 ? -1L : snapshot.events.Max(e => e.sequence);
            state._nextEventSequence = maxSequence + 1;
            return state;
        }
    }
}
