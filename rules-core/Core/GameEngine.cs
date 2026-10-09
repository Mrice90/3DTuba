// Port of game-core/src/main/java/com/infiniteconquest/core/GameEngine.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    public sealed class GameEngine
    {
        private readonly MovementRules _movementRules = new MovementRules();
        private readonly LineOfSightRules _lineOfSightRules = new LineOfSightRules();
        private readonly CardAbilityRules _cardAbilityRules = new CardAbilityRules();

        public ActionResult Apply(GameState state, GameAction action)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (action is null) throw new ArgumentNullException(nameof(action));
            if (state.Phase != Phase.PLAY) return ActionResult.Rejected("Actions require Play phase");
            if (action is GameAction.CastSpell castSpell) return CastSpell(state, castSpell);
            if (action.PlayerId != state.ActivePlayer) return ActionResult.Rejected("Not active player");
            if (action is GameAction.EndTurn) { state.AdvanceTurn(); return ActionResult.Accepted("Turn ended"); }
            if (action is GameAction.PlayLand playLand) return PlayLand(state, playLand);
            if (action is GameAction.PlayStructure playStructure) return PlayStructure(state, playStructure);
            if (action is GameAction.SummonCharacter summon) return SummonCharacter(state, summon);
            if (action is GameAction.BurrowCharacter burrow) return BurrowCharacter(state, burrow);
            if (action is GameAction.MoveCharacter move) return MoveCharacter(state, move);
            if (action is GameAction.BlinkCharacter blink) return BlinkCharacter(state, blink);
            if (action is GameAction.Attack attack) return Attack(state, attack);
            if (action is GameAction.ActivateAbility activate) return ActivateAbility(state, activate);
            return ActionResult.Rejected("Unsupported action");
        }

        private ActionResult ActivateAbility(GameState state, GameAction.ActivateAbility action)
        {
            var source = state.Card(action.CardId);
            if (source is null || source.Owner != action.PlayerId || source.Zone != Zone.BATTLEFIELD)
                return ActionResult.Rejected("Ability source must be your battlefield card");
            var position = state.Board.PositionOf(source.InstanceId);
            if (position is null || !state.Board.TopAt(position).Value.Equals(source.InstanceId))
                return ActionResult.Rejected("Only the top card of a stack can activate an ability");
            var abilities = _cardAbilityRules.Abilities(source, AbilityTrigger.ACTIVATED);
            if (abilities.Count == 0) return ActionResult.Rejected("Card has no activated ability");
            if (source.AbilityUsedThisTurn) return ActionResult.Rejected("Ability already used this turn");
            if (abilities.Any(ability => ability.Effect == AbilityEffectType.DAMAGE_ENEMY_CAPITAL)
                    && !_lineOfSightRules.HasLineToEnemyCapital(state, source, position))
                return ActionResult.Rejected("No line of sight to the enemy Capital — blocked by cover");
            int totalCost = abilities.Sum(ability => ability.GpCost);
            if (state.Player(action.PlayerId).CurrentGp < totalCost) return ActionResult.Rejected("Not enough GP");
            state.SpendGp(action.PlayerId, totalCost, source.Definition.Name + " ability");
            source.MarkAbilityUsed();
            foreach (var ability in abilities) _cardAbilityRules.Resolve(state, source, ability);
            return ActionResult.Accepted("Activated ability resolved");
        }

        public IReadOnlyCollection<BoardPosition> LegalMovementDestinations(GameState state, long id) =>
            state.Card(id) is CardInstance c
                ? _movementRules.LegalDestinations(state, c)
                : new HashSet<BoardPosition>();

        public IReadOnlyCollection<BoardPosition> LegalAttackDestinations(GameState state, long attackerId)
        {
            var attacker = state.Card(attackerId);
            if (attacker is null || attacker.Owner != state.ActivePlayer
                    || attacker.Definition.Type != CardType.CHARACTER || attacker.AttackedThisTurn)
                return new HashSet<BoardPosition>();
            var from = state.Board.PositionOf(attacker.InstanceId);
            if (from is null || !state.Board.TopAt(from).Value.Equals(attacker.InstanceId))
                return new HashSet<BoardPosition>();
            var legal = new HashSet<BoardPosition>();
            foreach (var to in state.Board.Positions())
            {
                var targetId = state.Board.TopAt(to);
                if (!targetId.HasValue) continue;
                var target = state.Card(targetId.Value) ?? throw new InvalidOperationException("Card not registered");
                if (target.Owner != attacker.Owner
                        && (target.Definition.Type == CardType.CHARACTER || target.Definition.IsPermanent())
                        && state.Rules.Geometry.Distance(from, to) <= EffectiveRange(state, attacker)
                        && _lineOfSightRules.HasLineOfSight(state, from, to))
                    legal.Add(to);
            }
            return legal;
        }

        private ActionResult CastSpell(GameState state, GameAction.CastSpell action)
        {
            var spell = PlayableFromHand(state, action.PlayerId, action.CardId, CardType.SPELL);
            if (spell is null) return ActionResult.Rejected("Spell must be owned, affordable, and in hand");

            var target = action.TargetId is null ? null : state.Card(action.TargetId.Value);
            foreach (var effect in spell.Definition.Effects)
            {
                var error = ValidateSpellEffect(state, action.PlayerId, effect, target, action.Destination);
                if (error is not null) return ActionResult.Rejected(error);
            }

            PayAndRemoveFromHand(state, spell);
            spell.MoveTo(Zone.DISCARD);
            state.Player(action.PlayerId).AddToDiscard(spell.InstanceId);
            state.RecordCardPlayed(spell);
            foreach (var effect in spell.Definition.Effects)
            {
                ApplySpellEffect(state, action.PlayerId, effect, target, action.Destination);
                if (state.Phase == Phase.GAME_OVER) break;
            }
            return ActionResult.Accepted(action.PlayerId == state.ActivePlayer
                    ? "Spell resolved" : "Reaction spell resolved");
        }

        private string? ValidateSpellEffect(GameState state, int caster, SpellEffect effect,
                                           CardInstance? target, BoardPosition? destination)
        {
            // Java declares an unused `targetRequired` local here; omitted (no semantic effect).
            if (target is null) return "Spell requires a target";
            {
                var position = state.Board.PositionOf(target.InstanceId);
                if (position is null || !state.Board.TopAt(position).Value.Equals(target.InstanceId))
                    return "Spell can target only the top battlefield card";
                if (effect.Target == SpellTarget.FRIENDLY && target.Owner != caster) return "Spell requires a friendly target";
                if (effect.Target == SpellTarget.ENEMY && target.Owner == caster) return "Spell requires an enemy target";
            }
            return effect.Type switch
            {
                SpellEffectType.STRIKE_CHARACTER or SpellEffectType.RETURN_CHARACTER
                    or SpellEffectType.BUFF_ATTACK or SpellEffectType.BUFF_DEFENSE
                    or SpellEffectType.TELEPORT_CHARACTER =>
                    target.Definition.Type != CardType.CHARACTER ? "Spell requires a Character target"
                        : effect.Type == SpellEffectType.TELEPORT_CHARACTER
                          && (destination is null || !state.Board.IsEmpty(destination))
                        ? "Teleport requires an empty destination" : null,
                SpellEffectType.DAMAGE_PERMANENT or SpellEffectType.HEAL_PERMANENT =>
                    !target.Definition.IsPermanent() ? "Spell requires a Permanent target" : null,
                _ => null,
            };
        }

        private void ApplySpellEffect(GameState state, int casterId, SpellEffect effect,
                                      CardInstance? target, BoardPosition? destination)
        {
            // target is non-null here: ValidateSpellEffect rejects null targets before any effect applies.
            switch (effect.Type)
            {
                case SpellEffectType.STRIKE_CHARACTER:
                    if (effect.Amount >= target!.EffectiveDefense) state.Destroy(target);
                    break;
                case SpellEffectType.DAMAGE_PERMANENT:
                    target!.AddDamage(effect.Amount);
                    if (target.Damage >= target.Definition.HitPoints) state.Destroy(target);
                    break;
                case SpellEffectType.HEAL_PERMANENT:
                    target!.HealDamage(effect.Amount);
                    break;
                case SpellEffectType.TELEPORT_CHARACTER:
                    var origin = state.Board.PositionOf(target!.InstanceId)
                        ?? throw new InvalidOperationException("Card not on battlefield");
                    state.Board.MoveTop(origin, destination!, target.InstanceId);
                    state.RecordCharacterMoved(target, origin, destination!, 0);
                    TerrainRules.Entered(state, target, origin, destination!, false);
                    break;
                case SpellEffectType.RETURN_CHARACTER:
                    state.ReturnCharacterToHand(target!);
                    new CapitalPassiveRules().OnCharacterReturnedBySpell(state, casterId, target!);
                    break;
                case SpellEffectType.BUFF_ATTACK:
                    target!.AddAttackBonus(effect.Amount);
                    break;
                case SpellEffectType.BUFF_DEFENSE:
                    target!.AddDefenseBonus(effect.Amount);
                    break;
            }
        }

        private ActionResult SummonCharacter(GameState state, GameAction.SummonCharacter action)
        {
            var card = PlayableFromHand(state, action.PlayerId, action.CardId, CardType.CHARACTER);
            if (card is null) return ActionResult.Rejected("Character must be owned, affordable, and in hand");
            var destination = action.Destination;
            // Note: All() is true on an empty stack, exactly like Java's allMatch.
            bool onFriendlyCard = state.Board.StackAt(destination)
                    .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card not registered"))
                    .All(c => c.Owner == action.PlayerId);
            bool adjacentToFriendlyPermanent = state.Board.Positions()
                    .Where(p => state.Rules.Geometry.Adjacent(destination, p))
                    .SelectMany(p => state.Board.StackAt(p))
                    .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card not registered"))
                    .Any(c => c.Owner == action.PlayerId && c.Definition.IsPermanent());
            if ((!state.Board.IsEmpty(destination) && !onFriendlyCard)
                    || (state.Board.IsEmpty(destination) && !adjacentToFriendlyPermanent))
                return ActionResult.Rejected("Character must join a friendly stack or deploy within one space of a friendly Permanent");
            PayAndRemoveFromHand(state, card);
            card.MoveTo(Zone.BATTLEFIELD);
            state.Board.Push(destination, card.InstanceId);
            state.RecordCardPlayed(card);
            TerrainRules.Entered(state, card, null, destination, true);
            return ActionResult.Accepted("Character summoned");
        }

        private ActionResult BurrowCharacter(GameState state, GameAction.BurrowCharacter action)
        {
            var card = PlayableFromHand(state, action.PlayerId, action.CardId, CardType.CHARACTER);
            if (card is null || !card.Definition.HasKeyword(Keyword.MOLE))
                return ActionResult.Rejected("Only an affordable Mole Character in hand can burrow");
            var top = state.Board.TopAt(action.Destination);
            if (!top.HasValue) return ActionResult.Rejected("Mole requires a controlled Land");
            var land = state.Card(top.Value) ?? throw new InvalidOperationException("Card not registered");
            if (land.Owner != action.PlayerId || land.Definition.Type != CardType.LAND)
                return ActionResult.Rejected("Mole requires a controlled Land on top of the stack");
            PayAndRemoveFromHand(state, card);
            card.MoveTo(Zone.BATTLEFIELD);
            state.Board.InsertBelowTop(action.Destination, card.InstanceId);
            state.RecordCardPlayed(card);
            new CapitalPassiveRules().OnBurrowed(state, card);
            return ActionResult.Accepted("Mole burrowed beneath Land");
        }

        private ActionResult PlayStructure(GameState state, GameAction.PlayStructure action)
        {
            if (!state.CanPlayDevelopment(action.PlayerId, CardType.STRUCTURE))
                return ActionResult.Rejected("Only one Structure may be played per turn");
            var card = DevelopableFromHand(state, action.PlayerId, action.CardId, CardType.STRUCTURE);
            if (card is null) return ActionResult.Rejected("Structure must be in hand and its turn value must be reached and its gold cost affordable");
            var top = state.Board.TopAt(action.Destination);
            if (!top.HasValue) return ActionResult.Rejected("Structure requires a controlled Land");
            var foundation = state.Card(top.Value) ?? throw new InvalidOperationException("Card not registered");
            if (foundation.Owner != action.PlayerId || foundation.Definition.Type != CardType.LAND)
                return ActionResult.Rejected("Structure requires a controlled Land on top of the stack");
            RemoveDevelopmentFromHand(state, card);
            card.MoveTo(Zone.BATTLEFIELD);
            state.Board.Push(action.Destination, card.InstanceId);
            state.RecordCardPlayed(card);
            return ActionResult.Accepted("Structure played");
        }

        private ActionResult BlinkCharacter(GameState state, GameAction.BlinkCharacter action)
        {
            var card = state.Card(action.CardId);
            if (card is null || card.Owner != action.PlayerId || card.Definition.Type != CardType.CHARACTER
                    || !card.Definition.HasKeyword(Keyword.BLINK))
                return ActionResult.Rejected("Invalid Blink Character");
            if (card.BlinkUsedThisTurn) return ActionResult.Rejected("Blink already used this turn");
            var origin = state.Board.PositionOf(card.InstanceId);
            if (origin is null || !state.Board.TopAt(origin).Value.Equals(card.InstanceId))
                return ActionResult.Rejected("Only the top Character can Blink");
            if (!state.Board.IsEmpty(action.Destination))
                return ActionResult.Rejected("Blink destination must be empty");
            state.Board.MoveTop(origin, action.Destination, card.InstanceId);
            card.MarkBlinkUsed();
            state.RecordCharacterMoved(card, origin, action.Destination, 0);
            TerrainRules.Entered(state, card, origin, action.Destination, false);
            if (card.Zone == Zone.BATTLEFIELD) new CapitalPassiveRules().OnBlinked(state, card);
            return ActionResult.Accepted("Character Blinked");
        }

        private ActionResult Attack(GameState state, GameAction.Attack action)
        {
            var attacker = state.Card(action.AttackerId);
            var target = state.Card(action.TargetId);
            if (attacker is null || target is null) return ActionResult.Rejected("Unknown attacker or target");
            if (attacker.Owner != action.PlayerId || target.Owner == action.PlayerId) return ActionResult.Rejected("Invalid ownership");
            if (attacker.Definition.Type != CardType.CHARACTER || attacker.AttackedThisTurn) return ActionResult.Rejected("Attacker cannot attack");
            var from = state.Board.PositionOf(attacker.InstanceId);
            var to = state.Board.PositionOf(target.InstanceId);
            if (from is null || to is null) return ActionResult.Rejected("Attacker and target must be on battlefield");
            if (!state.Board.TopAt(from).Value.Equals(attacker.InstanceId)
                    || !state.Board.TopAt(to).Value.Equals(target.InstanceId)) return ActionResult.Rejected("Only top cards interact");
            if (state.Rules.Geometry.Distance(from, to) > EffectiveRange(state, attacker)) return ActionResult.Rejected("Target out of range");
            if (!_lineOfSightRules.HasLineOfSight(state, from, to)) return ActionResult.Rejected("Line of sight blocked");

            new CapitalPassiveRules().BeforeAttack(state, attacker, target);
            attacker.MarkAttacked();
            state.RecordAttack(attacker, target);
            if (target.Definition.Type == CardType.CHARACTER)
            {
                int attackerPower = EffectiveAttack(state, attacker);
                int defenderPower = EffectiveAttack(state, target);
                target.AddCombatDamage(TerrainRules.ReduceDamage(state, target, attackerPower, state.Rules.Geometry.Distance(from, to) > 1));
                bool targetDies = target.CombatDamage >= target.EffectiveDefense;
                bool fastStrikeStopsRetaliation = attacker.Definition.HasKeyword(Keyword.FAST_STRIKE)
                        && attackerPower > target.EffectiveDefense;
                bool canRetaliate = !fastStrikeStopsRetaliation && defenderPower > 0
                        && state.Rules.Geometry.Distance(to, from) <= EffectiveRange(state, target)
                        && _lineOfSightRules.HasLineOfSight(state, to, from);
                if (canRetaliate) attacker.AddCombatDamage(TerrainRules.ReduceDamage(state, attacker, defenderPower, state.Rules.Geometry.Distance(to, from) > 1));
                bool attackerDies = canRetaliate && attacker.CombatDamage >= attacker.EffectiveDefense;
                if (targetDies) state.Destroy(target);
                if (attackerDies) state.Destroy(attacker);
                if (targetDies && attackerDies) return ActionResult.Accepted("Both Characters destroyed in simultaneous combat");
                if (targetDies) return ActionResult.Accepted("Defender destroyed");
                if (attackerDies) return ActionResult.Accepted("Attacker destroyed by retaliation");
                return ActionResult.Accepted(canRetaliate
                        ? "Combat damage marked until end of turn"
                        : "Combat damage marked; defender could not retaliate at this range");
            }
            else if (target.Definition.IsPermanent())
            {
                int damage = EffectiveAttack(state, attacker);
                if (attacker.Definition.HasKeyword(Keyword.SIEGE)) damage *= 2;
                target.AddDamage(TerrainRules.ReduceDamage(state, target, damage, state.Rules.Geometry.Distance(from, to) > 1));
                if (target.Damage >= target.Definition.HitPoints) state.Destroy(target);
            }
            else return ActionResult.Rejected("Target cannot be attacked");
            return ActionResult.Accepted("Attack resolved");
        }

        private ActionResult MoveCharacter(GameState state, GameAction.MoveCharacter action)
        {
            var card = state.Card(action.CardId);
            if (card is null || card.Owner != action.PlayerId || card.Definition.Type != CardType.CHARACTER)
                return ActionResult.Rejected("Invalid Character");
            var path = _movementRules.ShortestLegalPath(state, card, action.Destination);
            if (path.Count == 0) return ActionResult.Rejected("Destination unreachable");
            var origin = state.Board.PositionOf(card.InstanceId)
                ?? throw new InvalidOperationException("Card not on battlefield");
            var current = origin;
            var reacted = new HashSet<long>();
            int traveled = 0;
            int opportunityAttacks = 0;
            foreach (var threat in OpportunityThreatsAt(state, card, origin, reacted))
            {
                var enemy = state.Card(threat.AttackerId) ?? throw new InvalidOperationException("Card not registered");
                reacted.Add(enemy.InstanceId);
                opportunityAttacks++;
                state.RecordOpportunityAttack(enemy, card, origin);
                card.AddCombatDamage(TerrainRules.ReduceDamage(state, card, EffectiveAttack(state, enemy), state.Rules.Geometry.Distance(threat.AttackerPosition, origin) > 1));
                if (card.CombatDamage >= card.EffectiveDefense)
                {
                    state.Destroy(card);
                    break;
                }
            }
            foreach (var step in path)
            {
                if (card.Zone != Zone.BATTLEFIELD) break;
                state.Board.MoveTop(current, step, card.InstanceId);
                var previous = current;
                current = step;
                traveled++;
                card.SpendMovement(1);
                TerrainRules.Entered(state, card, previous, step, false);
                if (card.Zone != Zone.BATTLEFIELD) break;
                foreach (var threat in OpportunityThreatsAt(state, card, step, reacted))
                {
                    var enemy = state.Card(threat.AttackerId) ?? throw new InvalidOperationException("Card not registered");
                    reacted.Add(enemy.InstanceId);
                    opportunityAttacks++;
                    state.RecordOpportunityAttack(enemy, card, step);
                    card.AddCombatDamage(TerrainRules.ReduceDamage(state, card, EffectiveAttack(state, enemy), state.Rules.Geometry.Distance(threat.AttackerPosition, step) > 1));
                    if (card.CombatDamage >= card.EffectiveDefense)
                    {
                        state.Destroy(card);
                        break;
                    }
                }
                if (card.Zone != Zone.BATTLEFIELD) break;
            }
            state.RecordCharacterMoved(card, origin, current, traveled);
            if (card.Zone == Zone.BATTLEFIELD) new CapitalPassiveRules().OnMoved(state, card);
            if (card.Zone != Zone.BATTLEFIELD)
                return ActionResult.Accepted("Movement stopped: Character destroyed by an entry effect or opportunity attack");
            return ActionResult.Accepted(opportunityAttacks == 0 ? "Character moved"
                    : "Character moved through " + opportunityAttacks + " opportunity attack" + (opportunityAttacks == 1 ? "" : "s"));
        }

        public IReadOnlyList<OpportunityThreat> OpportunityThreats(GameState state, long moverId, BoardPosition destination)
        {
            var mover = state.Card(moverId);
            if (mover is null) return new List<OpportunityThreat>();
            var path = _movementRules.ShortestLegalPath(state, mover, destination);
            if (path.Count == 0) return new List<OpportunityThreat>();
            var found = new HashSet<long>();
            var threats = new List<OpportunityThreat>();
            var origin = state.Board.PositionOf(mover.InstanceId)
                ?? throw new InvalidOperationException("Card not on battlefield");
            var threatenedSteps = new List<BoardPosition> { origin };
            threatenedSteps.AddRange(path);
            foreach (var step in threatenedSteps)
            {
                foreach (var threat in OpportunityThreatsAt(state, mover, step, found))
                {
                    found.Add(threat.AttackerId);
                    threats.Add(threat);
                }
            }
            return threats;
        }

        private List<OpportunityThreat> OpportunityThreatsAt(GameState state, CardInstance mover,
                                                            BoardPosition step, HashSet<long> excluded)
        {
            var threats = new List<OpportunityThreat>();
            foreach (var enemyPosition in state.Board.Positions())
            {
                var top = state.Board.TopAt(enemyPosition);
                if (!top.HasValue || excluded.Contains(top.Value) || top.Value.Equals(mover.InstanceId)) continue;
                var enemy = state.Card(top.Value) ?? throw new InvalidOperationException("Card not registered");
                if (enemy.Owner == mover.Owner || enemy.Definition.Type != CardType.CHARACTER
                        || EffectiveAttack(state, enemy) <= 0
                        || state.Rules.Geometry.Distance(enemyPosition, step) > EffectiveRange(state, enemy)) continue;
                if (_lineOfSightRules.HasLineOfSight(state, enemyPosition, step))
                    threats.Add(new OpportunityThreat(enemy.InstanceId, enemyPosition, step,
                            enemy.Definition.Name, EffectiveAttack(state, enemy), mover.DefenseRemaining));
            }
            return threats;
        }

        public record OpportunityThreat(long AttackerId, BoardPosition AttackerPosition, BoardPosition TriggerPosition,
                                        string AttackerName, int Attack, int MoverDefense)
        {
            public bool Lethal() => Attack >= MoverDefense;
        }

        public int EffectiveAttack(GameState state, CardInstance card) =>
            card.EffectiveAttack + (SharpShotActive(state, card) ? 1 : 0);

        public int EffectiveRange(GameState state, CardInstance card) =>
            card.Definition.Range + (SharpShotActive(state, card) ? 1 : 0) + TerrainRules.RangeBonus(state, card);

        private bool SharpShotActive(GameState state, CardInstance card)
        {
            if (!card.Definition.HasKeyword(Keyword.SHARP_SHOT)) return false;
            var position = state.Board.PositionOf(card.InstanceId);
            var top = position is null ? (long?)null : state.Board.TopAt(position);
            if (position is null || !(top?.Equals(card.InstanceId) ?? false)) return false;
            return state.Board.StackAt(position)
                    .TakeWhile(id => !id.Equals(card.InstanceId))
                    .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card not registered"))
                    .Any(under => under.Owner == card.Owner
                            && (under.Definition.Type == CardType.STRUCTURE
                            || under.Definition.Type == CardType.CAPITAL));
        }

        private ActionResult PlayLand(GameState state, GameAction.PlayLand action)
        {
            if (!state.CanPlayDevelopment(action.PlayerId, CardType.LAND))
                return ActionResult.Rejected("Only one Land may be played per turn");
            var card = DevelopableFromHand(state, action.PlayerId, action.CardId, CardType.LAND);
            if (card is null) return ActionResult.Rejected("Land must be in hand and its turn value must be reached and its gold cost affordable");
            if (!LegalLandDestination(state, action.PlayerId, action.Destination))
                return ActionResult.Rejected("Land requires an empty hex within one space of a land you control or your Capital");
            RemoveDevelopmentFromHand(state, card);
            card.MoveTo(Zone.BATTLEFIELD);
            state.Board.Push(action.Destination, card.InstanceId);
            state.RecordCardPlayed(card);
            return ActionResult.Accepted("Land played");
        }

        /// <summary>
        /// Territory grows outward: a land may only be played on an empty hex
        /// adjacent to a land its owner controls or its owner's Capital.
        /// </summary>
        public static bool LegalLandDestination(GameState state, int playerId, BoardPosition destination)
        {
            if (!state.Board.IsEmpty(destination)) return false;
            return state.Board.Positions()
                    .Where(p => state.Rules.Geometry.Adjacent(destination, p))
                    .SelectMany(p => state.Board.StackAt(p))
                    .Select(id => state.Card(id) ?? throw new InvalidOperationException("Card not registered"))
                    .Any(c => c.Owner == playerId
                            && (c.Definition.Type == CardType.LAND || c.Definition.Type == CardType.CAPITAL));
        }

        private CardInstance? PlayableFromHand(GameState state, int playerId, long id, CardType type)
        {
            var card = state.Card(id);
            if (card is null || card.Owner != playerId || card.Definition.Type != type
                    || card.Zone != Zone.HAND || !state.Player(playerId).HasInHand(id)
                    || card.Definition.GoldCost > state.Player(playerId).CurrentGp) return null;
            return card;
        }

        private CardInstance? DevelopableFromHand(GameState state, int playerId, long id, CardType type)
        {
            var card = state.Card(id);
            if (card is null || card.Owner != playerId || card.Definition.Type != type
                    || card.Zone != Zone.HAND || !state.Player(playerId).HasInHand(id)
                    || state.PersonalTurnNumber(playerId) < card.Definition.Cost
                    || state.Player(playerId).CurrentGp < card.Definition.DevelopmentGoldCost) return null;
            return card;
        }

        private void RemoveDevelopmentFromHand(GameState state, CardInstance card)
        {
            state.SpendGp(card.Owner, card.Definition.DevelopmentGoldCost, card.Definition.Name);
            state.Player(card.Owner).RemoveFromHand(card.InstanceId);
        }

        private void PayAndRemoveFromHand(GameState state, CardInstance card)
        {
            state.SpendGp(card.Owner, card.Definition.Cost, card.Definition.Name);
            state.Player(card.Owner).RemoveFromHand(card.InstanceId);
        }
    }
}
