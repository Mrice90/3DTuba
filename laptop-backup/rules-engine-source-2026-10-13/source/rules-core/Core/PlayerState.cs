// Port of game-core/src/main/java/com/infiniteconquest/core/PlayerState.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Linq;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// Java UUID card IDs are full Guids (see CardInstance). Java package-private
    /// members → internal (same assembly).
    /// </summary>
    public sealed class PlayerState
    {
        private readonly List<Guid> _deck = new List<Guid>();
        private readonly List<Guid> _hand = new List<Guid>();
        private readonly List<Guid> _discard = new List<Guid>();

        public int Id { get; }
        public IReadOnlyList<Guid> Deck => _deck;
        public IReadOnlyList<Guid> Hand => _hand;
        public IReadOnlyList<Guid> Discard => _discard;
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

        internal void LoadDeck(IReadOnlyList<Guid> cardIds)
        {
            if (_deck.Count != 0 || _hand.Count != 0) throw new InvalidOperationException("Deck already loaded");
            _deck.AddRange(cardIds);
        }

        internal Guid? DrawOne()
        {
            if (_deck.Count == 0) return null;
            Guid card = _deck[0];
            _deck.RemoveAt(0);
            _hand.Add(card);
            return card;
        }

        internal Guid? DrawFirst(Func<Guid, bool> predicate)
        {
            for (int index = 0; index < _deck.Count; index++)
            {
                Guid card = _deck[index];
                if (predicate(card))
                {
                    _deck.RemoveAt(index);
                    _hand.Add(card);
                    return card;
                }
            }
            return null;
        }

        internal void Purchase(Guid id) {
            if (!_deck.Remove(id)) throw new InvalidOperationException("Card is not in deck");
            _hand.Add(id);
        }
        public void AddToHand(Guid id) { _hand.Add(id); }

        public bool HasInHand(Guid id) => _hand.Contains(id);

        public void RemoveFromHand(Guid id)
        {
            if (!_hand.Remove(id)) throw new InvalidOperationException("Card is not in hand");
        }

        internal void AddToDiscard(Guid id) { _discard.Add(id); }

        internal Guid? RemoveMostRecentDiscard(Func<Guid, bool> predicate)
        {
            for (int index = _discard.Count - 1; index >= 0; index--)
            {
                Guid id = _discard[index];
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
