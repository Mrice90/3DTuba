// Port of game-core/src/main/java/com/infiniteconquest/core/DeckValidator.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfiniteConquest.RulesCore.Core
{
    public sealed class DeckValidator
    {
        public const int MinimumSize = 40;
        public const int RequiredSize = MinimumSize;
        public const int MaxCopies = 4;
        public const int MinDistinct = 10;

        public IReadOnlyList<string> Validate(IReadOnlyList<CardDefinition> cards)
        {
            if (cards is null) throw new ArgumentNullException(nameof(cards));
            var errors = new List<string>();
            if (cards.Count < MinimumSize) errors.Add("Deck must contain at least 40 cards");

            // Java uses Collectors.groupingBy (HashMap, unspecified iteration order).
            // SortedDictionary keeps multi-error output deterministic (alphabetical by card id).
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var card in cards)
            {
                counts.TryGetValue(card.Id, out long n);
                counts[card.Id] = n + 1;
            }
            foreach (var kv in counts)
            {
                if (kv.Value > MaxCopies) errors.Add(kv.Key + " exceeds the four-copy limit");
            }
            if (counts.Count < MinDistinct) errors.Add("Deck must contain at least 10 distinct card IDs");
            return errors;
        }

        public bool IsValid(IReadOnlyList<CardDefinition> cards) => Validate(cards).Count == 0;
    }
}
