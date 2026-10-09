// Port of game-core/src/main/java/com/infiniteconquest/core/TerrainRules.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Development passives remain active under friendly occupants.
    /// Characters do not add height.
    /// </summary>
    public static class TerrainRules
    {
        private static readonly HashSet<Keyword> LandKeywordSet = new HashSet<Keyword>
            { Keyword.HIGH_GROUND, Keyword.COVER, Keyword.WAYSTATION, Keyword.FERTILE, Keyword.SANCTUARY, Keyword.ARCHIVE };
        private static readonly HashSet<Keyword> StructureKeywordSet = new HashSet<Keyword>
            { Keyword.TURRET, Keyword.MEDIC_TENT, Keyword.WATCHTOWER, Keyword.BULWARK, Keyword.WORKSHOP, Keyword.BEACON };
        private static readonly Keyword[] EnteredKeywords =
            { Keyword.TURRET, Keyword.MEDIC_TENT, Keyword.WAYSTATION, Keyword.BEACON };
        private static readonly Keyword[] StartTurnKeywords = { Keyword.SANCTUARY, Keyword.WORKSHOP };

        public static IReadOnlySet<Keyword> LandKeywords() => LandKeywordSet;
        public static IReadOnlySet<Keyword> StructureKeywords() => StructureKeywordSet;

        public static KeywordValue DefaultValue(Keyword k) => k switch
        {
            Keyword.TURRET => new KeywordValue(2, 1),
            Keyword.MEDIC_TENT or Keyword.WORKSHOP => new KeywordValue(1, 2),
            Keyword.BEACON => new KeywordValue(1, 1),
            _ => new KeywordValue(0, 1),
        };

        public static IReadOnlyList<CardInstance> Stack(GameState state, BoardPosition position) =>
            state.Board.StackAt(position)
                .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card not on battlefield: " + id))
                .ToList();

        public static int Height(GameState state, BoardPosition position) =>
            Stack(state, position).Sum(c =>
            {
                int value = c.Definition.Type == CardType.STRUCTURE || c.Definition.Type == CardType.CAPITAL ? 1 : 0;
                return value + (c.Definition.HasKeyword(Keyword.HIGH_GROUND) ? c.Definition.KeywordValue(Keyword.HIGH_GROUND).Amount : 0);
            });

        public static int EyeLevel(GameState state, BoardPosition position)
        {
            var topId = state.Board.TopAt(position);
            var top = topId.HasValue ? state.Card(topId.Value) : null;
            int topBody = top != null && (top.Definition.Type == CardType.STRUCTURE || top.Definition.Type == CardType.CAPITAL) ? 1 : 0;
            return Height(state, position) - topBody + 1;
        }

        public static int ObstacleHeight(GameState state, BoardPosition position)
        {
            var cards = Stack(state, position);
            bool building = cards.Any(c => c.Definition.Type == CardType.STRUCTURE || c.Definition.Type == CardType.CAPITAL);
            bool guard = cards.Count != 0 && cards[cards.Count - 1].Definition.HasKeyword(Keyword.VANGUARD);
            return guard ? Height(state, position) + 1 : building ? Height(state, position) : 0;
        }

        private static bool Active(GameState state, CardInstance source)
        {
            var p = state.Board.PositionOf(source.InstanceId);
            return source.Zone == Zone.BATTLEFIELD && p.HasValue
                && Stack(state, p.Value).All(c => c.Owner == source.Owner);
        }

        private static List<CardInstance> Sources(GameState state)
        {
            var result = new List<CardInstance>();
            // Positions() preserves the Java LinkedHashMap insertion order.
            foreach (var p in state.Board.Positions())
                foreach (var c in Stack(state, p))
                    if ((c.Definition.Type == CardType.LAND || c.Definition.Type == CardType.STRUCTURE) && Active(state, c))
                        result.Add(c);
            return result;
        }

        public static int RangeBonus(GameState state, CardInstance card)
        {
            var p = state.Board.PositionOf(card.InstanceId);
            if (!p.HasValue) return 0;
            return Stack(state, p.Value)
                .Where(c => c.Owner == card.Owner && c.Definition.HasKeyword(Keyword.WATCHTOWER))
                .Select(c => c.Definition.KeywordValue(Keyword.WATCHTOWER).Amount)
                .DefaultIfEmpty(0).Max();
        }

        public static int ReduceDamage(GameState state, CardInstance target, int amount, bool ranged)
        {
            var p = state.Board.PositionOf(target.InstanceId);
            if (!p.HasValue) return amount;
            int reduction = 0;
            foreach (var source in Stack(state, p.Value))
            {
                if (source.Owner != target.Owner) continue;
                if (ranged && target.Definition.Type == CardType.CHARACTER && source.Definition.HasKeyword(Keyword.COVER))
                    reduction = Math.Max(reduction, source.Definition.KeywordValue(Keyword.COVER).Amount);
                if (source.Definition.HasKeyword(Keyword.BULWARK))
                    reduction = Math.Max(reduction, source.Definition.KeywordValue(Keyword.BULWARK).Amount);
            }
            return Math.Max(0, amount - reduction);
        }

        public static void Entered(GameState state, CardInstance entrant, BoardPosition from, BoardPosition to, bool summoned)
        {
            if (entrant.Definition.Type != CardType.CHARACTER || entrant.Zone != Zone.BATTLEFIELD) return;
            var topTo = state.Board.TopAt(to);
            if (!topTo.HasValue || !topTo.Value.Equals(entrant.InstanceId)) return;
            foreach (var source in Sources(state))
            {
                if (state.Phase == Phase.GAME_OVER || entrant.Zone != Zone.BATTLEFIELD) break;
                var origin = state.Board.PositionOf(source.InstanceId)
                    ?? throw new InvalidOperationException("Terrain source not on battlefield");
                foreach (var keyword in EnteredKeywords)
                {
                    if (!source.Definition.HasKeyword(keyword)) continue;
                    var value = source.Definition.KeywordValue(keyword);
                    bool friendly = source.Owner == entrant.Owner;
                    if (keyword == Keyword.TURRET ? friendly : !friendly) continue;
                    if (keyword == Keyword.BEACON && !summoned) continue;
                    if (keyword == Keyword.WAYSTATION && summoned) continue;
                    if (state.Rules.Geometry.Distance(origin, to) > value.Range) continue;
                    if (from != null && state.Rules.Geometry.Distance(origin, from) <= value.Range) continue;
                    if (!new LineOfSightRules().HasLineOfSight(state, origin, to)) continue;
                    if (keyword == Keyword.MEDIC_TENT && entrant.CombatDamage == 0) continue;
                    if (!state.UseTerrainTrigger(source, entrant, keyword)) continue;
                    switch (keyword)
                    {
                        case Keyword.TURRET:
                        {
                            int damage = ReduceDamage(state, entrant, value.Amount,
                                state.Rules.Geometry.Distance(origin, to) > 1);
                            entrant.AddCombatDamage(damage);
                            state.RecordTerrain(source, entrant, keyword, damage);
                            if (entrant.CombatDamage >= entrant.EffectiveDefense()) state.Destroy(entrant);
                            break;
                        }
                        case Keyword.MEDIC_TENT:
                        {
                            int healed = Math.Min(value.Amount, entrant.CombatDamage);
                            entrant.HealCombatDamage(healed);
                            state.RecordTerrain(source, entrant, keyword, healed);
                            break;
                        }
                        case Keyword.WAYSTATION:
                        {
                            entrant.RestoreMovement(value.Amount);
                            state.RecordTerrain(source, entrant, keyword, value.Amount);
                            break;
                        }
                        case Keyword.BEACON:
                        {
                            entrant.AddAttackBonus(value.Amount);
                            state.RecordTerrain(source, entrant, keyword, value.Amount);
                            break;
                        }
                    }
                }
            }
        }

        public static void StartTurn(GameState state, int player)
        {
            foreach (var source in Sources(state))
            {
                if (source.Owner != player) continue;
                var origin = state.Board.PositionOf(source.InstanceId)
                    ?? throw new InvalidOperationException("Terrain source not on battlefield");
                foreach (var keyword in StartTurnKeywords)
                {
                    if (!source.Definition.HasKeyword(keyword)) continue;
                    var value = source.Definition.KeywordValue(keyword);
                    // Max by damage, tie-break by instanceId string — mirrors
                    // Comparator.comparingInt(damage).thenComparing(instanceId.toString()).
                    // Ordinal comparison of the string forms; see PORT_NOTES.md
                    // for the cross-engine tie-break caveat under the long-ID scheme.
                    CardInstance? best = null;
                    foreach (var c in state.BattlefieldCards(player))
                    {
                        if (c.Damage <= 0) continue;
                        if (keyword == Keyword.WORKSHOP ? c.Definition.Type != CardType.STRUCTURE : !c.Definition.IsPermanent()) continue;
                        var pos = state.Board.PositionOf(c.InstanceId);
                        if (!pos.HasValue || state.Rules.Geometry.Distance(origin, pos.Value) > value.Range) continue;
                        if (best == null || c.Damage > best.Damage ||
                            (c.Damage == best.Damage &&
                             string.Compare(c.InstanceId.ToString(), best.InstanceId.ToString(), StringComparison.Ordinal) > 0))
                            best = c;
                    }
                    if (best != null)
                    {
                        int healed = Math.Min(value.Amount, best.Damage);
                        best.HealDamage(healed);
                        state.RecordTerrain(source, best, keyword, healed);
                    }
                }
            }
        }

        public static string Describe(CardDefinition card, Keyword keyword)
        {
            var v = card.KeywordValue(keyword);
            return keyword switch
            {
                Keyword.HIGH_GROUND => $"Adds {v.Amount} height level to this stack. Land itself does not block sight.",
                Keyword.COVER => $"Friendly Characters on this stack take {v.Amount} less ranged attack or Turret damage. Uses the strongest protection, not a sum.",
                Keyword.WAYSTATION => $"A friendly Character entering this hex by movement recovers {v.Amount} movement, once per Character per turn.",
                Keyword.FERTILE => $"Generates {v.Amount} extra gold each owner turn (included in displayed income).",
                Keyword.SANCTUARY => $"Start of your turn: repair {v.Amount} damage on the most damaged friendly Permanent in this hex.",
                Keyword.ARCHIVE => $"When destroyed, draw {v.Amount} card(s).",
                Keyword.TURRET => $"An enemy Character entering visible range {v.Range} takes {v.Amount} damage. Once per entrant per turn; moving within the area does not retrigger.",
                Keyword.MEDIC_TENT => $"A friendly Character entering visible range {v.Range} heals {v.Amount} marked combat damage. Once per entrant per turn.",
                Keyword.WATCHTOWER => $"Friendly Characters on this stack gain +{v.Amount} Range.",
                Keyword.BULWARK => $"This stack's friendly occupant takes {v.Amount} less attack or Turret damage. Uses the strongest protection, not a sum.",
                Keyword.WORKSHOP => $"Start of your turn: repair {v.Amount} damage on the most damaged friendly Structure within {v.Range} space(s).",
                Keyword.BEACON => $"A friendly Character summoned within visible range {v.Range} gains +{v.Amount} Attack until its next turn.",
                _ => "",
            };
        }
    }
}
