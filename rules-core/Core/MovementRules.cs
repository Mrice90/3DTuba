// Port of game-core/src/main/java/com/infiniteconquest/core/MovementRules.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    public sealed class MovementRules
    {
        public IReadOnlySet<BoardPosition> LegalDestinations(GameState state, CardInstance character)
        {
            if (character.Definition.Type != CardType.CHARACTER || character.Zone != Zone.BATTLEFIELD)
                return new HashSet<BoardPosition>();
            var originResult = state.Board.PositionOf(character.InstanceId);
            if (!originResult.HasValue || !state.Board.TopAt(originResult.Value).Equals(character.InstanceId))
                return new HashSet<BoardPosition>();

            int allowance = character.MovementRemaining();
            if (allowance == 0) return new HashSet<BoardPosition>();
            var origin = originResult.Value;
            // Invading enemy territory is slow going: halve movement, minimum 1.
            if (origin.IsOnEnemySide(character.Owner)) allowance = Math.Max(1, allowance / 2);

            // distance is lookup-only; reached carries the result set so the
            // dictionary itself is never iterated (per the porting contract).
            var distance = new Dictionary<BoardPosition, int>();
            var reached = new HashSet<BoardPosition>();
            var queue = new Queue<BoardPosition>();
            distance[origin] = 0;
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int nextDistance = distance[current] + 1;
                if (nextDistance > allowance) continue;
                foreach (var next in state.Rules.Geometry.Neighbors(current))
                {
                    if (distance.ContainsKey(next)) continue;
                    var pass = PassabilityOf(state, character, next);
                    if (pass == Passability.BLOCKED) continue;
                    distance[next] = nextDistance;
                    reached.Add(next);
                    if (pass == Passability.OPEN) queue.Enqueue(next);
                }
            }
            return reached;
        }

        public int ShortestLegalDistance(GameState state, CardInstance character, BoardPosition destination)
        {
            var path = ShortestLegalPath(state, character, destination);
            return path.Count == 0 ? -1 : path.Count;
        }

        /// <summary>Returns each entered cell, excluding the origin and including the destination.</summary>
        public IReadOnlyList<BoardPosition> ShortestLegalPath(GameState state, CardInstance character, BoardPosition destination)
        {
            var origin = state.Board.PositionOf(character.InstanceId)
                ?? throw new InvalidOperationException("Character is not on battlefield");
            if (!LegalDestinations(state, character).Contains(destination)) return new List<BoardPosition>();

            var distance = new Dictionary<BoardPosition, int>();
            var previous = new Dictionary<BoardPosition, BoardPosition>();
            var queue = new Queue<BoardPosition>();
            distance[origin] = 0;
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Equals(destination))
                {
                    var path = new LinkedList<BoardPosition>();
                    for (var step = destination; !step.Equals(origin); step = previous[step])
                        path.AddFirst(step);
                    return new List<BoardPosition>(path);
                }
                foreach (var next in state.Rules.Geometry.Neighbors(current))
                {
                    if (distance.ContainsKey(next)) continue;
                    // The destination was validated against legalDestinations, so it is
                    // enterable by construction; every other step must be open ground.
                    var pass = next.Equals(destination) ? Passability.OPEN : PassabilityOf(state, character, next);
                    if (pass == Passability.BLOCKED) continue;
                    distance[next] = distance[current] + 1;
                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }
            return new List<BoardPosition>();
        }

        /**
         * How a moving character may treat a hex.
         * - OPEN — empty or open ground: the character may enter, pass through, and end its move here.
         * - ENTER_ONLY — a friendly stack: the character may end its move here but not pass through.
         * - BLOCKED — solid: an enemy structure, the enemy Capital, or an enemy Character.
         * Enemy land is open ground — a character marches straight through it — but
         * enemy structures are solid and cannot be entered or passed through.
         *
         * (Named PassabilityOf because the nested enum already owns the name Passability.)
         */
        private enum Passability { OPEN, ENTER_ONLY, BLOCKED }

        private Passability PassabilityOf(GameState state, CardInstance character, BoardPosition position)
        {
            if (state.Board.IsEmpty(position)) return Passability.OPEN;
            var stack = state.Board.StackAt(position)
                .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card not on battlefield: " + id))
                .ToList();
            if (stack.All(card => card.Definition.Type == CardType.LAND)) return Passability.OPEN;
            bool friendly = stack.Where(card => card.Definition.Type != CardType.LAND)
                .All(card => card.Owner == character.Owner);
            return friendly ? Passability.ENTER_ONLY : Passability.BLOCKED;
        }
    }
}
