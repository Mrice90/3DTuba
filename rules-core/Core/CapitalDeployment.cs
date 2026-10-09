// Port of game-core/src/main/java/com/infiniteconquest/core/CapitalDeployment.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Commit-reveal capital placement. Java IllegalStateException →
    /// InvalidOperationException, IllegalArgumentException → ArgumentException.
    /// </summary>
    public sealed class CapitalDeployment
    {
        private readonly CardInstance?[] _capitals = new CardInstance?[2];
        private readonly BoardPosition?[] _positions = new BoardPosition?[2];
        private bool _revealed;

        public void Commit(int playerId, CardInstance capital, BoardPosition position)
        {
            if (_revealed) throw new InvalidOperationException("Capitals already revealed");
            if (playerId < 0 || playerId > 1 || capital.Owner != playerId) throw new ArgumentException("Wrong owner");
            if (capital.Definition.Type != CardType.CAPITAL) throw new ArgumentException("Card must be a Capital");
            if (!position.IsOnPlayerSide(playerId)) throw new ArgumentException("Capital must be on its owner's plot");
            _capitals[playerId] = capital;
            _positions[playerId] = position;
        }

        public bool Ready() => _capitals[0] != null && _capitals[1] != null;

        public IReadOnlyDictionary<int, BoardPosition> Reveal(BoardState board)
        {
            if (!Ready()) throw new InvalidOperationException("Both players must commit first");
            if (_revealed) throw new InvalidOperationException("Capitals already revealed");
            for (int player = 0; player < 2; player++)
            {
                if (!board.IsEmpty(_positions[player]!)) throw new InvalidOperationException("Capital position is occupied");
            }
            for (int player = 0; player < 2; player++)
            {
                _capitals[player]!.MoveTo(Zone.BATTLEFIELD);
                board.Push(_positions[player]!, _capitals[player]!.InstanceId);
            }
            _revealed = true;
            return new Dictionary<int, BoardPosition> { [0] = _positions[0]!, [1] = _positions[1]! };
        }
    }
}
