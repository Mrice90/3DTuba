// Port of game-core/src/main/java/com/infiniteconquest/core/CardAbilityRules.java @ Desolate-Tuba e0e565b
using System.Collections.Generic;
using System.Linq;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Queries and resolves ACTIVATED (and PASSIVE) card abilities.
    /// Package-private in Java; public here because GameState also resolves
    /// PASSIVE abilities through it.
    /// </summary>
    public sealed class CardAbilityRules
    {
        public IReadOnlyList<CardAbility> Abilities(CardInstance source, AbilityTrigger trigger) =>
            source.Definition.Abilities.Where(ability => ability.Trigger == trigger).ToList();

        public void Resolve(GameState state, CardInstance source, AbilityTrigger trigger)
        {
            foreach (var ability in Abilities(source, trigger)) Resolve(state, source, ability);
        }

        public void Resolve(GameState state, CardInstance source, CardAbility ability)
        {
            switch (ability.Effect)
            {
                case AbilityEffectType.DRAW_CARD:
                    state.DrawCards(source.Owner, ability.Amount);
                    break;
                case AbilityEffectType.DRAW_CHARACTER:
                    state.DrawCardsOfType(source.Owner, CardType.CHARACTER, ability.Amount);
                    break;
                case AbilityEffectType.DRAW_STRUCTURE:
                    state.DrawCardsOfType(source.Owner, CardType.STRUCTURE, ability.Amount);
                    break;
                case AbilityEffectType.GAIN_GP:
                    state.Player(source.Owner).RestoreGp(ability.Amount);
                    break;
                case AbilityEffectType.HEAL_SELF:
                    if (source.Definition.IsPermanent()) source.HealDamage(ability.Amount);
                    break;
                case AbilityEffectType.HEAL_CAPITAL:
                    state.BattlefieldCards(source.Owner)
                        .FirstOrDefault(card => card.Definition.Type == CardType.CAPITAL)
                        ?.HealDamage(ability.Amount);
                    break;
                case AbilityEffectType.BUFF_SELF_ATTACK:
                    if (source.Zone == Zone.BATTLEFIELD && source.Definition.Type == CardType.CHARACTER)
                        source.AddAttackBonus(ability.Amount);
                    break;
                case AbilityEffectType.BUFF_SELF_DEFENSE:
                    if (source.Zone == Zone.BATTLEFIELD && source.Definition.Type == CardType.CHARACTER)
                        source.AddDefenseBonus(ability.Amount);
                    break;
                case AbilityEffectType.DAMAGE_ENEMY_CAPITAL:
                    var capital = state.BattlefieldCards(1 - source.Owner)
                        .FirstOrDefault(card => card.Definition.Type == CardType.CAPITAL);
                    if (capital is not null)
                    {
                        capital.AddDamage(ability.Amount);
                        var capitalPos = state.Board.PositionOf(capital.InstanceId);
                        var topId = capitalPos is null ? (long?)null : state.Board.TopAt(capitalPos);
                        if (capital.Damage >= capital.Definition.HitPoints
                            && topId.HasValue && topId.Value.Equals(capital.InstanceId))
                            state.Destroy(capital);
                    }
                    break;
            }
            state.RecordCardAbility(source, ability);
        }
    }
}
