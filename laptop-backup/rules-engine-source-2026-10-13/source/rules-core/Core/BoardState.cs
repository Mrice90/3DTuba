// Port of game-core/src/main/java/com/infiniteconquest/core/BoardState.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Battlefield stacks keyed by position. Iteration order reproduces the Java
    /// LinkedHashMap insertion order (row-major: y outer, x inner). Lookups use a
    /// dictionary, but game logic never iterates the dictionary itself —
    /// iteration always goes through the explicit order list.
    /// </summary>
    public sealed class BoardState
    {
        private readonly List<BoardPosition> _order = new List<BoardPosition>();
        private readonly Dictionary<BoardPosition, List<Guid>> _cells = new Dictionary<BoardPosition, List<Guid>>();

        public BoardState()
        {
            for (int y = 0; y < BoardPosition.Height; y++)
                for (int x = 0; x < BoardPosition.Width; x++)
                {
                    if (!BoardPosition.Contains(x,y)) continue;
                    var position = new BoardPosition(x, y);
                    _order.Add(position);
                    _cells[position] = new List<Guid>();
                }
        }

        /// <summary>Deep copy: positions are immutable records, stacks are duplicated.</summary>
        internal BoardState(BoardState source)
        {
            foreach (var position in source._order)
            {
                _order.Add(position);
                _cells[position] = new List<Guid>(source._cells[position]);
            }
        }

        public IReadOnlyList<BoardPosition> Positions() => _order;

        public IReadOnlyList<Guid> StackAt(BoardPosition position)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            return _cells[position];
        }

        public Guid? TopAt(BoardPosition position)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            var stack = _cells[position];
            return stack.Count == 0 ? (Guid?)null : stack[stack.Count - 1];
        }

        public BoardPosition? PositionOf(Guid id)
        {
            foreach (var position in _order)
                if (_cells[position].Contains(id)) return position;
            return null;
        }

        public bool IsEmpty(BoardPosition position)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            return _cells[position].Count == 0;
        }

        public void Push(BoardPosition position, Guid id)
        {
            if (PositionOf(id) != null) throw new InvalidOperationException("Card is already on battlefield");
            if (position == null) throw new ArgumentNullException(nameof(position));
            _cells[position].Add(id);
        }

        public void InsertBelowTop(BoardPosition position, Guid id)
        {
            if (PositionOf(id) != null) throw new InvalidOperationException("Card is already on battlefield");
            if (position == null) throw new ArgumentNullException(nameof(position));
            var stack = _cells[position];
            if (stack.Count == 0) throw new InvalidOperationException("Cannot insert beneath an empty stack");
            stack.Insert(stack.Count - 1, id);
        }

        public Guid Pop(BoardPosition position)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            var stack = _cells[position];
            if (stack.Count == 0) throw new InvalidOperationException("Cannot pop empty cell");
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return top;
        }

        public void Remove(Guid id)
        {
            var position = PositionOf(id) ?? throw new InvalidOperationException("Card is not on battlefield");
            if (!TopAt(position).Equals(id)) throw new InvalidOperationException("Only top card can be removed");
            Pop(position);
        }

        public void MoveTop(BoardPosition from, BoardPosition to, Guid expected)
        {
            if (!TopAt(from).Equals(expected)) throw new InvalidOperationException("Only top card can move");
            Pop(from);
            Push(to, expected);
        }
    }
}
