// Port of game-core/src/main/java/com/infiniteconquest/core/PlayerState.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Java UUID card IDs → long (see CardInstance). Java package-private
    /// members → internal (same assembly).
    /// </summary>
    public sealed class PlayerState
    {
        private readonly List<long> _deck = new List<long>();
        private readonly List<long> _hand = new List<long>();
        private readonly List<long> _discard = new List<long>();

        public int Id { get; }
        public IReadOnlyList<long> Deck => _deck;
        public IReadOnlyList<long> Hand => _hand;
        public IReadOnlyList<long> Discard => _discard;
        public int CurrentGp { get; private set; }
        public int MaximumGp { get; private set; }

        public PlayerState(int id)
        {
            if (id < 0 || id > 1) throw new ArgumentException("Player ID must be 0 or 1");
            Id = id;
        }

        /// <summary>
        /// Deep copy: zone lists and GP counters are duplicated; card IDs are
        /// immutable values.
        /// </summary>
        internal PlayerState(PlayerState source) : this(source.Id)
        {
            _deck.AddRange(source._deck);
            _hand.AddRange(source._hand);
            _discard.AddRange(source._discard);
            CurrentGp = source.CurrentGp;
            MaximumGp = source.MaximumGp;
        }

        internal void LoadDeck(IReadOnlyList<long> cardIds)
        {
            if (_deck.Count != 0 || _hand.Count != 0) throw new InvalidOperationException("Deck already loaded");
            _deck.AddRange(cardIds);
        }

        internal long? DrawOne()
        {
            if (_deck.Count == 0) return null;
            long card = _deck[0];
            _deck.RemoveAt(0);
            _hand.Add(card);
            return card;
        }

        internal long? DrawFirst(Func<long, bool> predicate)
        {
            for (int index = 0; index < _deck.Count; index++)
            {
                long card = _deck[index];
                if (predicate(card))
                {
                    _deck.RemoveAt(index);
                    _hand.Add(card);
                    return card;
                }
            }
            return null;
        }

        public void AddToHand(long id) { _hand.Add(id); }

        public bool HasInHand(long id) => _hand.Contains(id);

        public void RemoveFromHand(long id)
        {
            if (!_hand.Remove(id)) throw new InvalidOperationException("Card is not in hand");
        }

        internal void AddToDiscard(long id) { _discard.Add(id); }

        internal long? RemoveMostRecentDiscard(Func<long, bool> predicate)
        {
            for (int index = _discard.Count - 1; index >= 0; index--)
            {
                long id = _discard[index];
                if (predicate(id))
                {
                    _discard.RemoveAt(index);
                    return id;
                }
            }
            return null;
        }

        public void RestoreGp(int amount)
        {
            if (amount < 0) throw new ArgumentException("GP restoration cannot be negative");
            CurrentGp += amount;
            MaximumGp = Math.Max(MaximumGp, CurrentGp);
        }

        public void SpendGp(int amount)
        {
            if (amount < 0 || amount > CurrentGp) throw new ArgumentException("Insufficient GP");
            CurrentGp -= amount;
        }

        internal void InitializeGp(int availableGp)
        {
            if (availableGp < 0) throw new ArgumentException("Available GP cannot be negative");
            MaximumGp = availableGp;
            CurrentGp = availableGp;
        }

        public void BeginTurn() { RestoreGp(1); }
    }
}
