// Port of game-core/src/main/java/com/infiniteconquest/core/CapitalPassiveRules.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Capital passive triggers. Maps are lookup-only (never iterated), so
    /// plain Dictionary is fine under CONVENTIONS.md §2.
    /// DEVIATION (forced by the no-Guid contract): Java breaks ties with
    /// UUID.toString() lexicographic order; C# instance IDs are monotonic
    /// longs, so ties break on numeric ID order. Deterministic either way,
    /// but the tiebreak values differ from Java — flagged for AI-084.
    /// </summary>
    public sealed class CapitalPassiveRules
    {
        private static readonly Dictionary<string, CapitalPassive> ByCapital = new Dictionary<string, CapitalPassive>
        {
            ["zeus_capital_olympus_citadel"] = CapitalPassive.OLYMPIAN_MUSTER,
            ["zeus_capital_keraunos_spire"] = CapitalPassive.STORM_TITHE,
            ["zeus_capital_cloud_throne"] = CapitalPassive.CLOUDWARD,
            ["poseidon_capital_atlantis_nexus"] = CapitalPassive.TIDAL_RENEWAL,
            ["poseidon_capital_trident_bastion"] = CapitalPassive.TRIDENT_RESTORATION,
            ["poseidon_capital_abyssal_court"] = CapitalPassive.DEEP_RESERVES,
        };

        private static readonly Dictionary<CapitalPassive, string> Descriptions = new Dictionary<CapitalPassive, string>
        {
            [CapitalPassive.OLYMPIAN_MUSTER] = "Start of your turn: your first Blink Character gains +2 Attack this turn.",
            [CapitalPassive.STORM_TITHE] = "The first Spell you cast each turn refunds 1 GP.",
            [CapitalPassive.CLOUDWARD] = "The first Character you Blink each turn gains +2 Defense until your next turn.",
            [CapitalPassive.TIDAL_RENEWAL] = "Start of your turn: heal 3 damage from your most damaged Land.",
            [CapitalPassive.TRIDENT_RESTORATION] = "The first Land you play each turn heals your Capital for 2.",
            [CapitalPassive.DEEP_RESERVES] = "The first Mole you burrow each turn refunds 1 GP.",
        };

        public CapitalPassive? PassiveFor(CardDefinition capital)
        {
            if (capital.Type != CardType.CAPITAL) return null;
            return ByCapital.TryGetValue(capital.Id, out var passive) ? passive : (CapitalPassive?)null;
        }

        public string Description(CardDefinition capital)
        {
            var passive = PassiveFor(capital);
            return passive.HasValue ? Descriptions[passive.Value] : "No passive ability.";
        }

        public int SupportedCapitalCount() => ByCapital.Count;

        internal void OnTurnStarted(GameState state, int playerId)
        {
            var activePassive = Passive(state, playerId);
            if (activePassive == null) return;
            switch (activePassive.Value)
            {
                case CapitalPassive.OLYMPIAN_MUSTER:
                    var blinker = FirstBattlefieldCard(state, playerId,
                        card => card.Definition.Type == CardType.CHARACTER && card.Definition.HasKeyword(Keyword.BLINK));
                    if (blinker != null)
                    {
                        blinker.AddAttackBonus(2);
                        Trigger(state, playerId, CapitalPassive.OLYMPIAN_MUSTER);
                    }
                    break;
                case CapitalPassive.TIDAL_RENEWAL:
                    var damaged = MostDamaged(state, playerId, CardType.LAND);
                    if (damaged != null)
                    {
                        damaged.HealDamage(3);
                        Trigger(state, playerId, CapitalPassive.TIDAL_RENEWAL);
                    }
                    break;
                default:
                    break;
            }
        }

        internal void OnCardPlayed(GameState state, CardInstance card)
        {
            var passive = Passive(state, card.Owner);
            if (passive == null) return;
            if (passive == CapitalPassive.STORM_TITHE && card.Definition.Type == CardType.SPELL)
                Refund(state, card.Owner, passive.Value);
            if (passive == CapitalPassive.TRIDENT_RESTORATION && card.Definition.Type == CardType.LAND
                && Use(state, card.Owner, passive.Value))
            {
                var capital = Capital(state, card.Owner);
                if (capital != null) capital.HealDamage(2);
                Emit(state, card.Owner, passive.Value);
            }
        }

        internal void OnBurrowed(GameState state, CardInstance card)
        {
            if (Passive(state, card.Owner) == CapitalPassive.DEEP_RESERVES)
                Refund(state, card.Owner, CapitalPassive.DEEP_RESERVES, 1);
        }

        internal void OnBlinked(GameState state, CardInstance card)
        {
            if (Passive(state, card.Owner) == CapitalPassive.CLOUDWARD
                && Use(state, card.Owner, CapitalPassive.CLOUDWARD))
            {
                card.AddDefenseBonus(2);
                Emit(state, card.Owner, CapitalPassive.CLOUDWARD);
            }
        }

        internal void OnMoved(GameState state, CardInstance card)
        {
        }

        internal void BeforeAttack(GameState state, CardInstance attacker, CardInstance target)
        {
        }

        internal void OnCharacterReturnedBySpell(GameState state, int casterId, CardInstance target)
        {
        }

        internal void OnPermanentDestroyed(GameState state, CardInstance destroyed)
        {
        }

        private CapitalPassive? Passive(GameState state, int playerId)
        {
            var capital = Capital(state, playerId);
            return capital == null ? null : PassiveFor(capital.Definition);
        }

        private CardInstance? Capital(GameState state, int playerId)
        {
            return FirstBattlefieldCard(state, playerId, card => card.Definition.Type == CardType.CAPITAL);
        }

        private CardInstance? FirstBattlefieldCard(GameState state, int playerId, Func<CardInstance, bool> predicate)
        {
            return state.BattlefieldCards(playerId).Where(predicate).OrderBy(card => card.InstanceId).FirstOrDefault();
        }

        private CardInstance? MostDamaged(GameState state, int playerId, CardType type)
        {
            return state.BattlefieldCards(playerId)
                .Where(card => card.Definition.Type == type && card.Damage > 0)
                .OrderByDescending(card => card.Damage)
                .ThenByDescending(card => card.InstanceId)
                .FirstOrDefault();
        }

        // Dead code in the Java source too (never called); ported to mirror exactly.
        private CardInstance? MostDamagedPermanent(GameState state, int playerId)
        {
            return state.BattlefieldCards(playerId)
                .Where(card => card.Definition.IsPermanent() && card.Damage > 0)
                .OrderByDescending(card => card.Damage)
                .ThenByDescending(card => card.InstanceId)
                .FirstOrDefault();
        }

        private void Refund(GameState state, int playerId, CapitalPassive passive)
        {
            Refund(state, playerId, passive, 1);
        }

        private void Refund(GameState state, int playerId, CapitalPassive passive, int amount)
        {
            if (Use(state, playerId, passive))
            {
                state.Player(playerId).RestoreGp(amount);
                Emit(state, playerId, passive);
            }
        }

        private bool Use(GameState state, int playerId, CapitalPassive passive)
        {
            return state.TryUseCapitalPassive(playerId, passive);
        }

        private void Trigger(GameState state, int playerId, CapitalPassive passive)
        {
            state.MarkCapitalPassiveUsed(playerId, passive);
            Emit(state, playerId, passive);
        }

        private void Emit(GameState state, int playerId, CapitalPassive passive)
        {
            state.RecordCapitalPassive(playerId, passive, Descriptions[passive]);
        }
    }
}
