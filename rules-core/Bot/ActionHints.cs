// Port of game-cli/src/main/java/com/infiniteconquest/cli/ActionHints.java @ Desolate-Tuba e0e565b
// Pure logic — no CLI scaffolding. Enumerates legal command strings for the active player.
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Core;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Bot
{
    public sealed class ActionHints
    {
        public List<string> ForActivePlayer(GameState state, GameEngine engine)
        {
            int player = state.ActivePlayer;
            var hints = new List<string>();
            var hand = state.Player(player).Hand;

            for (int index = 0; index < hand.Count; index++)
            {
                var card = state.Card(hand[index]) ?? throw new InvalidOperationException("Card missing from hand");
                if (!state.CanPlayDevelopment(player, card.Definition.Type)) continue;
                bool development = card.Definition.Type == CardType.LAND
                        || card.Definition.Type == CardType.STRUCTURE;
                if ((development && card.Definition.Cost > state.PersonalTurnNumber(player))
                        || card.Definition.GoldCost() > state.Player(player).CurrentGp) continue;
                foreach (var position in state.Board.Positions())
                {
                    if (card.Definition.Type == CardType.LAND
                            && GameEngine.LegalLandDestination(state, player, position))
                    {
                        hints.Add($"play {index} {position.X} {position.Y}");
                    }
                    else if (card.Definition.Type == CardType.STRUCTURE && IsControlledTopLand(state, player, position))
                    {
                        hints.Add($"play {index} {position.X} {position.Y}");
                    }
                    else if (card.Definition.Type == CardType.CHARACTER && LegalSummonCell(state, player, position))
                    {
                        hints.Add($"play {index} {position.X} {position.Y}");
                    }
                    if (card.Definition.Type == CardType.CHARACTER
                            && card.Definition.HasKeyword(Keyword.MOLE)
                            && IsControlledTopLand(state, player, position))
                    {
                        hints.Add($"burrow {index} {position.X} {position.Y}");
                    }
                }
            }
            hints.AddRange(SpellActionsForPlayer(state, player));

            foreach (var from in state.Board.Positions())
            {
                var top = state.Board.TopAt(from);
                if (!top.HasValue) continue;
                var card = state.Card(top.Value) ?? throw new InvalidOperationException("Card missing from board");
                if (card.Owner != player || card.Definition.Type != CardType.CHARACTER) continue;
                foreach (var to in engine.LegalMovementDestinations(state, card.InstanceId))
                {
                    hints.Add($"move {from.X} {from.Y} {to.X} {to.Y}");
                }
                if (card.Definition.HasKeyword(Keyword.BLINK) && !card.BlinkUsedThisTurn)
                {
                    foreach (var to in state.Board.Positions())
                        if (state.Board.IsEmpty(to))
                            hints.Add($"blink {from.X} {from.Y} {to.X} {to.Y}");
                }
                foreach (var to in engine.LegalAttackDestinations(state, card.InstanceId))
                {
                    hints.Add($"attack {from.X} {from.Y} {to.X} {to.Y}");
                }
            }
            foreach (var position in state.Board.Positions())
            {
                var topId = state.Board.TopAt(position);
                if (!topId.HasValue) continue;
                var card = state.Card(topId.Value);
                if (card is null || card.Owner != player || card.AbilityUsedThisTurn) continue;
                var activated = card.Definition.Abilities
                    .Where(a => a.Trigger == AbilityTrigger.ACTIVATED).ToList();
                if (activated.Count == 0) continue;
                if (activated.Sum(a => a.GpCost) > state.Player(player).CurrentGp) continue;
                if (AimsAtEnemyCapital(card)
                        && !new LineOfSightRules().HasLineToEnemyCapital(state, card, position)) continue;
                hints.Add($"activate {position.X} {position.Y}");
            }
            hints.Add("end");
            return hints;
        }

        /// <summary>
        /// True when one of the card's activated abilities strikes the enemy
        /// Capital — an aimed shot that needs a clear sight line.
        /// </summary>
        private bool AimsAtEnemyCapital(CardInstance card)
        {
            return card.Definition.Abilities.Any(a =>
                a.Trigger == AbilityTrigger.ACTIVATED
                && a.Effect == AbilityEffectType.DAMAGE_ENEMY_CAPITAL);
        }

        public List<string> SpellActionsForPlayer(GameState state, int player)
        {
            var result = new List<string>();
            var hand = state.Player(player).Hand;
            string prefix = player == state.ActivePlayer ? "cast " : "react " + player + " ";
            for (int index = 0; index < hand.Count; index++)
            {
                var spell = state.Card(hand[index]) ?? throw new InvalidOperationException("Card missing from hand");
                if (spell.Definition.Type != CardType.SPELL
                        || spell.Definition.Cost > state.Player(player).CurrentGp) continue;
                var effect = spell.Definition.Effects[0];
                foreach (var targetPosition in state.Board.Positions())
                {
                    var targetId = state.Board.TopAt(targetPosition);
                    if (!targetId.HasValue) continue;
                    var target = state.Card(targetId.Value) ?? throw new InvalidOperationException("Card missing from board");
                    if (!ValidSpellTarget(effect, player, target)) continue;
                    string cmd = prefix + index + " " + targetPosition.X + " " + targetPosition.Y;
                    if (effect.Type == SpellEffectType.TELEPORT_CHARACTER)
                    {
                        foreach (var destination in state.Board.Positions())
                            if (state.Board.IsEmpty(destination))
                                result.Add(cmd + " " + destination.X + " " + destination.Y);
                    }
                    else result.Add(cmd);
                }
            }
            return result;
        }

        private bool ValidSpellTarget(SpellEffect effect, int player, CardInstance target)
        {
            if (effect.Target == SpellTarget.FRIENDLY && target.Owner != player) return false;
            if (effect.Target == SpellTarget.ENEMY && target.Owner == player) return false;
            return effect.Type switch
            {
                SpellEffectType.STRIKE_CHARACTER or SpellEffectType.TELEPORT_CHARACTER
                    or SpellEffectType.RETURN_CHARACTER or SpellEffectType.BUFF_ATTACK
                    or SpellEffectType.BUFF_DEFENSE =>
                    target.Definition.Type == CardType.CHARACTER,
                SpellEffectType.DAMAGE_PERMANENT or SpellEffectType.HEAL_PERMANENT =>
                    target.Definition.IsPermanent(),
                _ => false,
            };
        }

        private bool IsControlledTopLand(GameState state, int player, BoardPosition position)
        {
            var topId = state.Board.TopAt(position);
            if (!topId.HasValue) return false;
            var card = state.Card(topId.Value);
            return card is not null && card.Owner == player && card.Definition.Type == CardType.LAND;
        }

        private bool LegalSummonCell(GameState state, int player, BoardPosition destination)
        {
            bool onFriendlyStack = !state.Board.IsEmpty(destination)
                && state.Board.StackAt(destination)
                    .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card missing from board"))
                    .All(card => card.Owner == player);
            bool besidePermanent = state.Board.IsEmpty(destination)
                && state.Board.Positions()
                    .Where(p => state.Rules().Geometry.Adjacent(destination, p))
                    .SelectMany(p => state.Board.StackAt(p))
                    .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card missing from board"))
                    .Any(card => card.Owner == player && card.Definition.IsPermanent());
            return onFriendlyStack || besidePermanent;
        }
    }
}
