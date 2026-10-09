// Port of game-core/src/main/java/com/infiniteconquest/core/DeckBuild.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Explicit deck identity. Capital is separate from the draw pile.
    /// Mirrors the Java record, including compact-constructor normalization
    /// (blank name → "Custom Deck") and validation.
    /// </summary>
    public record DeckBuild
    {
        public static IReadOnlySet<string> Factions { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "ZEUS", "POSEIDON" };

        public string Name { get; init; }
        public string PrimaryFaction { get; init; }
        public string? AllyFaction { get; init; }
        public CardDefinition Capital { get; init; }
        public IReadOnlyList<CardDefinition> Cards { get; init; }

        public DeckBuild(string? name, string? primaryFaction, string? allyFaction,
                         CardDefinition? capital, List<CardDefinition> cards)
        {
            if (capital is null) throw new ArgumentNullException(nameof(capital), "Choose a Capital");
            if (cards is null) throw new ArgumentNullException(nameof(cards));
            var errors = Errors(primaryFaction, allyFaction, capital, cards);
            if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors));
            Name = string.IsNullOrWhiteSpace(name) ? "Custom Deck" : name!;
            // Errors() guarantees primaryFaction is a known faction here.
            PrimaryFaction = primaryFaction!;
            AllyFaction = allyFaction;
            Capital = capital;
            Cards = new List<CardDefinition>(cards);
        }

        public static bool Eligible(CardDefinition card, string? primary, string? ally) =>
            card.Type != CardType.CAPITAL
            && (card.Faction == primary || card.Faction == ally || card.Faction == "NEUTRAL");

        public static IReadOnlyList<string> Errors(string? primary, string? ally,
            CardDefinition? capital, IReadOnlyList<CardDefinition> cards)
        {
            var errors = new List<string>(new DeckValidator().Validate(cards));
            if (primary is null || !Factions.Contains(primary)) errors.Add("Choose a primary faction");
            if (ally is not null && (!Factions.Contains(ally) || ally == primary))
                errors.Add("Choose at most one different ally faction");
            if (capital is null || capital.Type != CardType.CAPITAL || capital.Faction != primary)
                errors.Add("Capital must belong to the primary faction");
            if (cards.Any(c => !Eligible(c, primary, ally)))
                errors.Add("Cards must belong to your primary faction, optional ally, or Neutral; Capitals stay outside the deck");
            return errors;
        }
    }
}
