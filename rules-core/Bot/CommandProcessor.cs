// Port of game-cli/src/main/java/com/infiniteconquest/cli/CommandProcessor.java @ Desolate-Tuba e0e565b
// Command-string → GameAction dispatch. The BattlefieldRenderer (presentation)
// is intentionally omitted: board/hand/inspect return a placeholder.
// (Deviation recorded in PORT_NOTES.md.)
using System;
using System.Globalization;
using InfiniteConquest.RulesCore.Core;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Bot
{
    public sealed class CommandProcessor
    {
        private readonly GameState _state;
        private readonly GameEngine _engine;
        private readonly ActionHints _hints;
        private bool _quit;

        public CommandProcessor(GameState state) : this(state, new GameEngine(), new ActionHints()) { }

        internal CommandProcessor(GameState state, GameEngine engine, ActionHints hints)
        {
            _state = state;
            _engine = engine;
            _hints = hints;
        }

        public bool QuitRequested => _quit;

        public string Execute(string? input)
        {
            if (input is null) { _quit = true; return "Input closed."; }
            string trimmed = input.Trim();
            if (trimmed.Length == 0) return "";
            string[] parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();
            try
            {
                return command switch
                {
                    "help" => Help(),
                    "board" or "hand" => "[board render omitted in core draft]",
                    "inspect" => Inspect(parts),
                    "actions" => string.Join(Environment.NewLine, _hints.ForActivePlayer(_state, _engine)),
                    "play" => Apply(Play(parts, false)),
                    "burrow" => Apply(Play(parts, true)),
                    "move" => Apply(BoardAction(parts, "move")),
                    "blink" => Apply(BoardAction(parts, "blink")),
                    "attack" => Apply(BoardAction(parts, "attack")),
                    "activate" => Apply(Activate(parts)),
                    "cast" => Apply(Spell(parts, _state.ActivePlayer, 1)),
                    "react" => React(parts),
                    "end" => Apply(new GameAction.EndTurn(_state.ActivePlayer)),
                    "quit" or "exit" => Quit(),
                    _ => "Unknown command. Type help.",
                };
            }
            catch (ArgumentException ex)
            {
                return "Invalid command: " + ex.Message;
            }
            catch (IndexOutOfRangeException ex)
            {
                return "Invalid command: " + ex.Message;
            }
        }

        private string Quit() { _quit = true; return "Match closed."; }

        private string React(string[] parts)
        {
            if (parts.Length < 2) throw new ArgumentException("Reaction requires a player number");
            int player = Number(parts[1]);
            if (player < 0 || player > 1 || player == _state.ActivePlayer)
                throw new ArgumentException("Reaction player must be the inactive player");
            return Apply(Spell(parts, player, 2));
        }

        private string Inspect(string[] parts)
        {
            // Presentation-layer inspection omitted in the core draft.
            return "[inspect omitted in core draft]";
        }

        private GameAction Play(string[] parts, bool burrow)
        {
            RequireLength(parts, 4);
            int handIndex = Number(parts[1]);
            var destination = Position(parts[2], parts[3]);
            var hand = _state.Player(_state.ActivePlayer).Hand;
            long cardId = hand[handIndex];
            if (burrow) return new GameAction.BurrowCharacter(_state.ActivePlayer, cardId, destination);
            var type = (_state.Card(cardId) ?? throw new InvalidOperationException("Card missing")).Definition.Type;
            return type switch
            {
                CardType.LAND => new GameAction.PlayLand(_state.ActivePlayer, cardId, destination),
                CardType.STRUCTURE => new GameAction.PlayStructure(_state.ActivePlayer, cardId, destination),
                CardType.CHARACTER => new GameAction.SummonCharacter(_state.ActivePlayer, cardId, destination),
                _ => throw new ArgumentException("That card type is not playable yet"),
            };
        }

        private GameAction Spell(string[] parts, int player, int handOffset)
        {
            int remaining = parts.Length - handOffset;
            if (remaining != 3 && remaining != 5)
                throw new ArgumentException("Use target coordinates and optional teleport destination");
            int handIndex = Number(parts[handOffset]);
            long spellId = _state.Player(player).Hand[handIndex];
            var targetPosition = Position(parts[handOffset + 1], parts[handOffset + 2]);
            long targetId = _state.Board.TopAt(targetPosition)
                ?? throw new ArgumentException("No spell target at coordinates");
            BoardPosition? destination = remaining == 5
                ? Position(parts[handOffset + 3], parts[handOffset + 4]) : null;
            return new GameAction.CastSpell(player, spellId, targetId, destination);
        }

        private GameAction BoardAction(string[] parts, string command)
        {
            RequireLength(parts, 5);
            var from = Position(parts[1], parts[2]);
            var to = Position(parts[3], parts[4]);
            long source = _state.Board.TopAt(from)
                ?? throw new ArgumentException("No card at source");
            return command switch
            {
                "move" => new GameAction.MoveCharacter(_state.ActivePlayer, source, to),
                "blink" => new GameAction.BlinkCharacter(_state.ActivePlayer, source, to),
                "attack" => new GameAction.Attack(
                    _state.ActivePlayer, source,
                    _state.Board.TopAt(to) ?? throw new ArgumentException("No target at destination")),
                _ => throw new ArgumentException("Unsupported board command"),
            };
        }

        private GameAction Activate(string[] parts)
        {
            RequireLength(parts, 3);
            long source = _state.Board.TopAt(Position(parts[1], parts[2]))
                ?? throw new ArgumentException("No card at source");
            return new GameAction.ActivateAbility(_state.ActivePlayer, source);
        }

        private string Apply(GameAction action)
        {
            var result = _engine.Apply(_state, action);
            return (result.accepted ? "OK: " : "REJECTED: ") + result.message;
        }

        private BoardPosition Position(string x, string y) => new BoardPosition(Number(x), Number(y));

        private int Number(string text)
        {
            try { return int.Parse(text, CultureInfo.InvariantCulture); }
            catch (FormatException ex) { throw new ArgumentException("Expected a number", ex); }
        }

        private void RequireLength(string[] parts, int expected)
        {
            if (parts.Length != expected) throw new ArgumentException("Wrong number of arguments");
        }

        public static string Help() => """
                Commands:
                  board                         show battlefield and active hand
                  actions                       list currently legal command forms
                  inspect <hand#>               inspect a card in your hand
                  inspect <x> <y>               inspect a battlefield stack
                  play <hand#> <x> <y>          play Land, Structure, or Character
                  burrow <hand#> <x> <y>        place a Mole beneath your top Land
                  move <fromX> <fromY> <x> <y>  move the top Character
                  blink <fromX> <fromY> <x> <y> teleport a Blink Character
                  attack <fromX> <fromY> <x> <y> attack the top enemy card
                  activate <x> <y>              pay GP to use a top card's ability
                  cast <hand#> <x> <y> [toX toY] cast during your turn
                  react <player#> <hand#> <x> <y> [toX toY]
                                                cast using saved GP on the enemy turn
                  end                           end the active player's turn
                  help                          show commands
                  quit                          close the match
                """.Trim();
    }
}
