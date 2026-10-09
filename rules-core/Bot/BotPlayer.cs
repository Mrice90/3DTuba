// Port of game-cli/src/main/java/com/infiniteconquest/cli/BotPlayer.java @ Desolate-Tuba e0e565b
//
// Decision logic only — presentation pacing and CLI scaffolding are out of
// scope per CONVENTIONS.md §6.
//
// API alignment: this port follows the member shapes already established in
// the draft's Core files (notably Core/GameEngine.cs): state queries are
// properties (ActivePlayer, Board, CurrentGp), parameterized lookups are
// methods (Card, Player, BattlefieldCards), and card data reads are properties
// (Definition.Type, Damage, InstanceId, HasKeyword). Members not yet ported
// anywhere in the draft are flagged in PORT_NOTES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InfiniteConquest.RulesCore.Core;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Bot
{
    public sealed class BotPlayer
    {
        public const int BotId = 1;

        /**
         * MORTAL mistake model, documented for players: on each decision the bot
         * misjudges like a human beginner. It is a cautious novice — it would
         * rather clear the board than strike the enemy Capital, so it filters out
         * capital attacks except for obvious lethals (even a beginner takes the
         * winning hit) and except when the Capital is the only enemy target left.
         * Independently, 25% of the time it explores, picking uniformly among
         * its top 3 heuristic actions instead of the best one. The misplaced
         * priorities are the systematic weakness; the exploration is ordinary
         * epsilon-greedy noise.
         */
        /// <summary>Chance MORTAL explores among its top actions instead of taking the best.</summary>
        public const double MortalMistakeRate = 0.25;
        /// <summary>How many top heuristic actions MORTAL samples from when it explores.</summary>
        public const int MortalExplorationWidth = 3;
        /// <summary>
        /// DEMIGOD prescreen width: the heuristic score narrows the field to the
        /// top 12 actions before the 1-ply simulation, keeping a turn well under
        /// a second even on modest hardware.
        /// </summary>
        public const int DemigodPrescreen = 12;

        private readonly BotDifficulty _difficulty;
        private readonly SeededRng _rng;
        private readonly ActionHints _hints = new ActionHints();
        private long _ghostIdCounter = -1;

        /// <summary>Classic behavior: HERO difficulty.</summary>
        public BotPlayer(SeededRng rng) : this(BotDifficulty.HERO, rng)
        {
        }

        /// <param name="difficulty">which skill level to play at</param>
        /// <param name="rng">drives MORTAL exploration; pass the GameState-owned
        /// SeededRng for fully deterministic games (same seed + same decks =
        /// identical game). Never null; the core never news up its own RNG.</param>
        public BotPlayer(BotDifficulty difficulty, SeededRng rng)
        {
            _difficulty = difficulty;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public BotDifficulty Difficulty() => _difficulty;

        public Decision TakeNextAction(GameState state, CommandProcessor commands)
        {
            return TakeNextAction(state, commands, BotId);
        }

        public Decision TakeNextAction(GameState state, CommandProcessor commands, int playerId)
        {
            if (state.ActivePlayer != playerId)
                throw new InvalidOperationException("It is not player " + playerId + "'s turn");
            IReadOnlyList<string> legal = _hints.ForActivePlayer(state, new GameEngine());
            string command = Choose(state, legal, playerId);
            return new Decision(command, commands.Execute(command));
        }

        public Decision? React(GameState state, CommandProcessor commands)
        {
            return React(state, commands, BotId);
        }

        public Decision? React(GameState state, CommandProcessor commands, int playerId)
        {
            if (state.ActivePlayer == playerId) return null;
            IReadOnlyList<string> legal = _hints.SpellActionsForPlayer(state, playerId);
            if (legal.Count == 0) return null;
            string command = Choose(state, legal, playerId);
            return new Decision(command, commands.Execute(command));
        }

        private string Choose(GameState state, IReadOnlyList<string> legal, int playerId)
        {
            if (legal.Count == 0) return "end";
            return _difficulty switch
            {
                BotDifficulty.MORTAL => MortalChoice(state, legal, playerId),
                BotDifficulty.HERO => HeroChoice(state, legal, playerId),
                BotDifficulty.DEMIGOD => DemigodChoice(state, legal, playerId),
                _ => throw new ArgumentOutOfRangeException(nameof(_difficulty)),
            };
        }

        /// <summary>Original behavior: argmax on the heuristic score, largest command string wins ties.</summary>
        private string HeroChoice(GameState state, IReadOnlyList<string> legal, int playerId)
        {
            return Ranked(state, legal, playerId)[0];
        }

        /// <summary>
        /// Human-like mistakes: MORTAL plays like a cautious beginner. It would
        /// rather clear the board than strike the enemy Capital, so non-lethal
        /// capital attacks are filtered out of its options — but it still takes
        /// an obvious lethal on the Capital, and it still attacks the Capital
        /// when nothing else is left to hit. On top of that, MortalMistakeRate
        /// of the time it explores, picking uniformly among its top
        /// MortalExplorationWidth heuristic actions instead of the best. RNG draws
        /// happen in a fixed order per decision, so a seeded SeededRng reproduces
        /// the same game exactly.
        /// </summary>
        private string MortalChoice(GameState state, IReadOnlyList<string> legal, int playerId)
        {
            List<string> pool = FilterCapitalAttacks(state, legal, playerId);
            List<string> ranked = Ranked(state, pool, playerId);
            if (ranked.Count > 1 && _rng.NextDouble() < MortalMistakeRate)
            {
                return ranked[_rng.Next(Math.Min(MortalExplorationWidth, ranked.Count))];
            }
            return ranked[0];
        }

        /// <summary>
        /// The novice's misplaced priorities: drop strikes on the enemy Capital —
        /// both attacks and activated damage abilities — unless the strike would
        /// obviously destroy it, or unless the Capital is the only enemy target
        /// available. Never returns an empty pool — if every legal command was a
        /// filtered capital strike, the full list is kept.
        /// </summary>
        private List<string> FilterCapitalAttacks(GameState state, IReadOnlyList<string> legal, int playerId)
        {
            var capitalStrikes = new List<string>();
            var others = new List<string>(legal.Count);
            foreach (string command in legal)
            {
                if (IsCapitalStrike(state, command)) capitalStrikes.Add(command);
                else others.Add(command);
            }
            if (capitalStrikes.Count == 0) return new List<string>(legal);
            bool anyOtherAttackTarget = others.Any(command => command.StartsWith("attack", StringComparison.Ordinal));
            if (!anyOtherAttackTarget) return new List<string>(legal); // nothing else to hit: even a novice swings at the Capital
            foreach (string command in capitalStrikes)
            {
                if (IsLethalCapitalStrike(state, command, playerId)) others.Add(command); // even a novice takes lethal
            }
            return others.Count == 0 ? new List<string>(legal) : others;
        }

        /// <summary>True if the command strikes the enemy Capital: an attack on it, or an
        /// activated ability whose every effect damages it.</summary>
        private bool IsCapitalStrike(GameState state, string command)
        {
            return IsCapitalAttack(state, command) || IsCapitalPinger(state, command);
        }

        /// <summary>True if the command activates an ability that does nothing but damage the enemy Capital.</summary>
        private bool IsCapitalPinger(GameState state, string command)
        {
            if (!command.StartsWith("activate", StringComparison.Ordinal)) return false;
            string[] parts = SplitCommand(command);
            CardInstance? card = TopCard(state, new BoardPosition(ParseInt(parts[1]), ParseInt(parts[2])));
            if (card == null) return false;
            List<CardAbility> abilities = card.Definition.Abilities
                .Where(ability => ability.Trigger == AbilityTrigger.ACTIVATED)
                .ToList();
            return abilities.Count > 0
                && abilities.All(ability => ability.Effect == AbilityEffectType.DAMAGE_ENEMY_CAPITAL);
        }

        /// <summary>True if the capital strike would obviously destroy the enemy Capital.</summary>
        private bool IsLethalCapitalStrike(GameState state, string command, int playerId)
        {
            if (command.StartsWith("attack", StringComparison.Ordinal)) return IsLethalCapitalAttack(state, command);
            string[] parts = SplitCommand(command);
            var position = new BoardPosition(ParseInt(parts[1]), ParseInt(parts[2]));
            int ping = 0;
            CardInstance? card = TopCard(state, position);
            if (card != null)
            {
                ping = card.Definition.Abilities
                    .Where(ability => ability.Trigger == AbilityTrigger.ACTIVATED)
                    .Sum(ability => ability.Amount);
            }
            return ping >= EnemyCapitalRemaining(state, playerId);
        }

        /// <summary>True if the command attacks a Capital.</summary>
        private bool IsCapitalAttack(GameState state, string command)
        {
            if (!command.StartsWith("attack", StringComparison.Ordinal)) return false;
            string[] parts = SplitCommand(command);
            var target = new BoardPosition(ParseInt(parts[3]), ParseInt(parts[4]));
            CardInstance? card = TopCard(state, target);
            return card != null && card.Definition.Type == CardType.CAPITAL;
        }

        /// <summary>
        /// True if the attack would obviously destroy the enemy Capital: the
        /// attacker's power (doubled by SIEGE, as the engine applies it) meets or
        /// beats the Capital's remaining hit points. Terrain damage reduction is
        /// ignored on purpose — a novice misjudges that, and erring toward
        /// attacking the Capital is the desired direction.
        /// </summary>
        private bool IsLethalCapitalAttack(GameState state, string command)
        {
            string[] parts = SplitCommand(command);
            var from = new BoardPosition(ParseInt(parts[1]), ParseInt(parts[2]));
            var target = new BoardPosition(ParseInt(parts[3]), ParseInt(parts[4]));
            CardInstance? attacker = TopCard(state, from);
            CardInstance? capital = TopCard(state, target);
            if (attacker == null || capital == null || capital.Definition.Type != CardType.CAPITAL) return false;
            int power = attacker.EffectiveAttack;
            if (attacker.Definition.HasKeyword(Keyword.SIEGE)) power *= 2;
            return power >= capital.Definition.HitPoints - capital.Damage;
        }

        /// <summary>
        /// 1-ply lookahead: heuristic-prescreen to the top DemigodPrescreen
        /// actions, simulate each on a deep copy of the state, and take the move
        /// with the best resulting position. Simulation never touches the real
        /// game; the copy is discarded after evaluation.
        /// </summary>
        private string DemigodChoice(GameState state, IReadOnlyList<string> legal, int playerId)
        {
            List<string> ranked = Ranked(state, legal, playerId);
            List<string> candidates = ranked.GetRange(0, Math.Min(DemigodPrescreen, ranked.Count));
            string best = candidates[0];
            double bestValue = double.NegativeInfinity;
            foreach (string command in candidates)
            {
                GameState simulated = state.Copy();
                string result = new CommandProcessor(simulated).Execute(command);
                if (!result.StartsWith("OK:", StringComparison.Ordinal)) continue; // legal list came from this engine; stay safe anyway
                double value = Evaluate(simulated, playerId);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = command;
                }
            }
            return best;
        }

        /// <summary>
        /// Position evaluation from <paramref name="playerId"/>'s perspective. An immediate
        /// win/loss dwarfs everything — a simulated move that destroys the enemy
        /// Capital scores near-infinite, so lethal-on-capital detection dominates
        /// the score automatically. Otherwise the Capital-health differential is
        /// the primary axis: dealing Capital damage and preventing own-Capital
        /// damage outrank material, GP, and hand-size differentials by an order
        /// of magnitude, because the Capital is the win condition.
        /// </summary>
        private double Evaluate(GameState state, int playerId)
        {
            int foe = 1 - playerId;
            int? winner = state.Winner;
            if (winner.HasValue) return winner.Value == playerId ? 1e9 : -1e9;
            double capitals = CapitalHealth(state, playerId) - CapitalHealth(state, foe);
            double material = MaterialValue(state, playerId) - MaterialValue(state, foe);
            double gp = state.Player(playerId).CurrentGp - state.Player(foe).CurrentGp;
            double cards = state.Player(playerId).Hand.Count - state.Player(foe).Hand.Count;
            // A screened Capital (few enemy attackers with a sight line to it)
            // and an exposed enemy Capital are both worth real, if modest, value:
            // screens buy the turns that win games.
            double exposure = CapitalExposure(state, foe) - CapitalExposure(state, playerId);
            return 100.0 * capitals + 10.0 * material + 1.5 * gp + 2.0 * cards + 8.0 * exposure;
        }

        private double MaterialValue(GameState state, int playerId)
        {
            double total = 0;
            foreach (CardInstance card in state.BattlefieldCards(playerId))
            {
                if (card.Definition.Type == CardType.CAPITAL) continue;
                total += card.EffectiveAttack + Math.Max(0, card.DefenseRemaining);
            }
            return total;
        }

        private double CapitalHealth(GameState state, int playerId)
        {
            return state.BattlefieldCards(playerId)
                .Where(card => card.Definition.Type == CardType.CAPITAL)
                .Sum(card => (double)Math.Max(0, card.Definition.HitPoints - card.Damage));
        }

        /// <summary>
        /// Heuristic ranking, best first. Sort order reproduces the original
        /// max(comparingInt(score).thenComparing(naturalOrder)) exactly:
        /// highest score wins, and the largest command string wins score ties
        /// (ordinal comparison matches Java's UTF-16 natural ordering).
        /// </summary>
        private List<string> Ranked(GameState state, IReadOnlyList<string> legal, int playerId)
        {
            return legal
                .OrderByDescending(command => Score(state, command, playerId))
                .ThenByDescending(command => command, StringComparer.Ordinal)
                .ToList();
        }

        private int Score(GameState state, string command, int playerId)
        {
            string[] parts = SplitCommand(command);
            int b = parts[0] switch
            {
                "cast" or "react" => SpellScore(state, parts, playerId),
                "attack" => AttackScore(state, parts),
                "play" => PlayScore(state, parts, playerId),
                "burrow" => 82,
                "blink" => 45,
                "move" => 35,
                "activate" => ActivateScore(state, parts, playerId),
                "end" => 0,
                _ => 1,
            };
            return b + CapitalSynergy(state, playerId, parts[0]);
        }

        private int AttackScore(GameState state, string[] parts)
        {
            var from = new BoardPosition(ParseInt(parts[1]), ParseInt(parts[2]));
            var target = new BoardPosition(ParseInt(parts[3]), ParseInt(parts[4]));
            CardInstance attacker = TopCardRequired(state, from);
            CardInstance card = TopCardRequired(state, target);
            if (card.Definition.Type == CardType.CAPITAL)
            {
                // Destroying the Capital wins the game. A lethal strike is
                // the best possible action; chip damage is worth dealing,
                // but killing enemy Characters that threaten our own
                // Capital comes first.
                int power = attacker.EffectiveAttack;
                if (attacker.Definition.HasKeyword(Keyword.SIEGE)) power *= 2;
                int remaining = card.Definition.HitPoints - card.Damage;
                return power >= remaining ? 150 : 108;
            }
            // Characters are threats to the Capital; other permanents are
            // support pieces — worth hitting, but not before the real war.
            return card.Definition.Type == CardType.CHARACTER ? 110 : 95;
        }

        private int PlayScore(GameState state, string[] parts, int playerId)
        {
            int index = ParseInt(parts[1]);
            CardInstance handCard = RequireCard(state, state.Player(playerId).Hand[index]);
            return handCard.Definition.Type switch
            {
                CardType.LAND => 90,
                // A structure that screens the Capital — standing on a
                // sight line between an exposed enemy attacker and home —
                // is worth far more than a bare stat play. The bonus
                // fades on its own: once attackers are screened they no
                // longer count as exposed.
                CardType.STRUCTURE => 85 + ScreenBonus(state, parts, playerId),
                CardType.CHARACTER => 80,
                _ => 0,
            };
        }

        private int SpellScore(GameState state, string[] parts, int playerId)
        {
            int handIndex = ParseInt(parts[0] == "react" ? parts[2] : parts[1]);
            CardInstance spell = RequireCard(state, state.Player(playerId).Hand[handIndex]);
            SpellEffect effect = spell.Definition.Effects[0];
            return effect.Type switch
            {
                SpellEffectType.DAMAGE_PERMANENT => 140 + effect.Amount,
                SpellEffectType.STRIKE_CHARACTER => 135 + effect.Amount,
                SpellEffectType.RETURN_CHARACTER => 125,
                SpellEffectType.HEAL_PERMANENT => 115 + effect.Amount,
                SpellEffectType.BUFF_ATTACK => 105 + effect.Amount,
                SpellEffectType.BUFF_DEFENSE => 100 + effect.Amount,
                SpellEffectType.TELEPORT_CHARACTER => 60,
                _ => throw new ArgumentOutOfRangeException(nameof(effect)),
            };
        }

        /// <summary>
        /// Values an activated ability by what it actually does instead of a flat
        /// score. Lethal damage on the enemy Capital wins the game, so it scores
        /// like a lethal attack; chip damage, card draw, healing, and buffs score
        /// on the same scale as the rest of the heuristic, minus the GP the
        /// ability costs to fire. Firing a heal with nothing to heal scores below
        /// "end", so the bot holds its GP instead of wasting it.
        /// </summary>
        private int ActivateScore(GameState state, string[] parts, int playerId)
        {
            var position = new BoardPosition(ParseInt(parts[1]), ParseInt(parts[2]));
            CardInstance? card = TopCard(state, position);
            if (card == null) return 1;
            int total = 0;
            int gpCost = 0;
            foreach (CardAbility ability in card.Definition.Abilities)
            {
                if (ability.Trigger != AbilityTrigger.ACTIVATED) continue;
                gpCost += ability.GpCost;
                total += ability.Effect switch
                {
                    AbilityEffectType.DAMAGE_ENEMY_CAPITAL => ActivateDamageScore(state, playerId, ability),
                    AbilityEffectType.DRAW_CARD => 66 + 6 * ability.Amount,
                    AbilityEffectType.DRAW_CHARACTER => 66 + 6 * ability.Amount,
                    AbilityEffectType.DRAW_STRUCTURE => 66 + 6 * ability.Amount,
                    AbilityEffectType.GAIN_GP => 48 + 2 * ability.Amount,
                    AbilityEffectType.HEAL_SELF => HealSelfScore(card, ability),
                    AbilityEffectType.HEAL_CAPITAL => HealCapitalScore(state, playerId, ability),
                    AbilityEffectType.BUFF_SELF_ATTACK => 58 + 4 * ability.Amount,
                    AbilityEffectType.BUFF_SELF_DEFENSE => 58 + 3 * ability.Amount,
                    _ => throw new ArgumentOutOfRangeException(nameof(ability)),
                };
            }
            return total - gpCost;
        }

        private int ActivateDamageScore(GameState state, int playerId, CardAbility ability)
        {
            int remaining = EnemyCapitalRemaining(state, playerId);
            if (ability.Amount >= remaining) return 150;
            return 104 + 2 * ability.Amount;
        }

        private int HealSelfScore(CardInstance card, CardAbility ability)
        {
            int missing = card.Damage;
            if (missing == 0) return 0;
            return 40 + 4 * Math.Min(ability.Amount, missing);
        }

        private int HealCapitalScore(GameState state, int playerId, CardAbility ability)
        {
            int missing = OwnCapitalMissing(state, playerId);
            if (missing == 0) return 0;
            return 50 + 5 * Math.Min(ability.Amount, missing);
        }

        /// <summary>Least remaining hit points across the enemy Capitals.</summary>
        private int EnemyCapitalRemaining(GameState state, int playerId)
        {
            int min = int.MaxValue;
            foreach (CardInstance card in state.BattlefieldCards(1 - playerId))
            {
                if (card.Definition.Type != CardType.CAPITAL) continue;
                min = Math.Min(min, Math.Max(0, card.Definition.HitPoints - card.Damage));
            }
            return min;
        }

        /// <summary>Total missing hit points across the player's own Capitals.</summary>
        private int OwnCapitalMissing(GameState state, int playerId)
        {
            return state.BattlefieldCards(playerId)
                .Where(card => card.Definition.Type == CardType.CAPITAL)
                .Sum(card => card.Damage);
        }

        /// <summary>
        /// Bonus for playing a structure on a hex that would screen the Capital:
        /// +15 per enemy attacker whose currently-clear sight line to our Capital
        /// the new structure would block, capped at +45. Evaluated against the
        /// live board, so the bonus naturally disappears once the Capital is
        /// already screened.
        /// </summary>
        private int ScreenBonus(GameState state, string[] parts, int playerId)
        {
            var at = new BoardPosition(ParseInt(parts[2]), ParseInt(parts[3]));
            BoardPosition? capital = CapitalPosition(state, playerId);
            if (capital == null) return 0;
            int foe = 1 - playerId;
            var sight = new LineOfSightRules();
            var exposed = new List<BoardPosition>();
            foreach (CardInstance enemy in state.BattlefieldCards(foe))
            {
                if (enemy.Definition.Type != CardType.CHARACTER) continue;
                BoardPosition? from = state.Board.PositionOf(enemy.InstanceId);
                if (from == null) continue;
                long? top = state.Board.TopAt(from);
                if (!top.HasValue || top.Value != enemy.InstanceId) continue;
                if (sight.HasLineOfSight(state, from, capital)) exposed.Add(from);
            }
            if (exposed.Count == 0) return 0;
            // Ghost the structure onto the candidate hex and re-check the sight lines.
            GameState probe = state.Copy();
            int index = ParseInt(parts[1]);
            CardDefinition definition = RequireCard(state, state.Player(playerId).Hand[index]).Definition;
            var ghost = new CardInstance(NextGhostId(), definition, playerId, Zone.BATTLEFIELD);
            probe.Register(ghost);
            probe.Board.Push(at, ghost.InstanceId);
            int screened = 0;
            foreach (BoardPosition from in exposed)
            {
                if (!sight.HasLineOfSight(probe, from, capital)) screened++;
            }
            return Math.Min(45, 15 * screened);
        }

        private BoardPosition? CapitalPosition(GameState state, int playerId)
        {
            foreach (CardInstance card in state.BattlefieldCards(playerId))
            {
                if (card.Definition.Type != CardType.CAPITAL) continue;
                BoardPosition? position = state.Board.PositionOf(card.InstanceId);
                if (position != null) return position;
            }
            return null;
        }

        /// <summary>How many of <paramref name="owner"/>'s enemies can currently see their Capital.</summary>
        private int CapitalExposure(GameState state, int owner)
        {
            BoardPosition? capital = CapitalPosition(state, owner);
            if (capital == null) return 0;
            int foe = 1 - owner;
            var sight = new LineOfSightRules();
            int exposed = 0;
            foreach (CardInstance enemy in state.BattlefieldCards(foe))
            {
                if (enemy.Definition.Type != CardType.CHARACTER) continue;
                BoardPosition? from = state.Board.PositionOf(enemy.InstanceId);
                if (from != null && sight.HasLineOfSight(state, from, capital)) exposed++;
            }
            return exposed;
        }

        private int CapitalSynergy(GameState state, int playerId, string action)
        {
            CapitalPassive? passive = state.CapitalPassiveFor(playerId);
            if (!passive.HasValue) return 0;
            return passive.Value switch
            {
                CapitalPassive.STORM_TITHE => action == "cast" || action == "react" ? 8 : 0,
                CapitalPassive.TRIDENT_RESTORATION => action == "play" ? 3 : 0,
                CapitalPassive.DEEP_RESERVES => action == "burrow" ? 8 : 0,
                CapitalPassive.CLOUDWARD => action == "blink" ? 5 : 0,
                _ => 0,
            };
        }

        /// <summary>
        /// Deterministic stand-in for Java's UUID.randomUUID() in ScreenBonus.
        /// Ghost ids are negative longs, so they can never collide with real
        /// instance ids and never consume the SeededRng stream — Java's
        /// randomUUID doesn't touch the game RNG either, and spending stream
        /// draws here would shift every later MORTAL exploration draw out of
        /// parity. See PORT_NOTES.md.
        /// </summary>
        private long NextGhostId() => _ghostIdCounter--;

        /// <summary>Mirrors board.topAt(position).flatMap(state::card): the card on top of a stack, if any.</summary>
        private static CardInstance? TopCard(GameState state, BoardPosition position)
        {
            long? top = state.Board.TopAt(position);
            return top.HasValue ? state.Card(top.Value) : null;
        }

        private static CardInstance TopCardRequired(GameState state, BoardPosition position)
        {
            return TopCard(state, position)
                ?? throw new InvalidOperationException("Expected a card on top of " + position);
        }

        private static CardInstance RequireCard(GameState state, long id)
        {
            return state.Card(id)
                ?? throw new InvalidOperationException("Expected card instance " + id);
        }

        /// <summary>Mirrors Java's command.split("\\s+") — splits on runs of whitespace, no empties.</summary>
        private static string[] SplitCommand(string command)
        {
            return command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        public record Decision(string Command, string Result);
    }
}
