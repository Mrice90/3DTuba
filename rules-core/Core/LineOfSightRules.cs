// Port of game-core/src/main/java/com/infiniteconquest/core/LineOfSightRules.java @ Desolate-Tuba e0e565b
using System;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    public sealed class LineOfSightRules
    {
        /// <summary>
        /// Activated abilities that strike the enemy Capital are aimed shots: they
        /// need a clear sight line from the source to that Capital. An enemy
        /// structure between the two works as cover and blocks the ability, unless
        /// the source is aiming from a higher height — the height-aware trace lets
        /// a higher eye level shoot over lower cover. Vacuously true when no enemy
        /// Capital stands on the battlefield.
        /// </summary>
        public bool HasLineToEnemyCapital(GameState state, CardInstance source, BoardPosition from)
        {
            return state.BattlefieldCards(1 - source.Owner)
                .Where(card => card.Definition.Type == CardType.CAPITAL)
                .Select(card => state.Board.PositionOf(card.InstanceId))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .Any(capital => HasLineOfSight(state, from, capital));
        }

        public bool HasLineOfSight(GameState state, BoardPosition from, BoardPosition to)
        {
            if (state.Rules.Geometry == BoardGeometry.HEX)
            {
                return new[] { 0.000001, -0.000001 }
                    .Any(nudge => BoardGeometry.HEX.HexTrace(from, to, nudge)
                        .All(p => !BlocksHeightSight(state, from, to, p)));
            }
            int x = from.X;
            int y = from.Y;
            int dx = Math.Abs(to.X - x);
            int dy = Math.Abs(to.Y - y);
            int stepX = Math.Sign(to.X - x);
            int stepY = Math.Sign(to.Y - y);
            int error = dx - dy;

            while (x != to.X || y != to.Y)
            {
                int doubledError = error * 2;
                if (doubledError > -dy)
                {
                    error -= dy;
                    x += stepX;
                }
                if (doubledError < dx)
                {
                    error += dx;
                    y += stepY;
                }
                var position = new BoardPosition(x, y);
                if (!position.Equals(to) && BlocksSight(state, position)) return false;
            }
            return true;
        }

        private bool BlocksHeightSight(GameState state, BoardPosition from, BoardPosition to, BoardPosition middle)
        {
            int obstacle = TerrainRules.ObstacleHeight(state, middle);
            if (obstacle == 0) return false;
            int distance = state.Rules.Geometry.Distance(from, to);
            double fraction = (double)state.Rules.Geometry.Distance(from, middle) / Math.Max(1, distance);
            double ray = TerrainRules.EyeLevel(state, from) * (1 - fraction) + TerrainRules.EyeLevel(state, to) * fraction;
            return obstacle + 1e-9 >= ray;
        }

        private bool BlocksSight(GameState state, BoardPosition position)
        {
            var topId = state.Board.TopAt(position);
            if (!topId.HasValue) return false;
            var card = state.Card(topId.Value);
            if (card == null) return false;
            return card.Definition.Type == CardType.STRUCTURE
                || card.Definition.Type == CardType.CAPITAL
                || card.Definition.HasKeyword(Keyword.VANGUARD);
        }
    }
}
