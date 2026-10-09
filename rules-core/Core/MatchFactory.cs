// Port of game-core/src/main/java/com/infiniteconquest/core/MatchFactory.java @ Desolate-Tuba e0e565b
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace InfiniteConquest.RulesCore.Core
{
    public sealed class MatchFactory
    {
        private readonly DeckValidator _deckValidator = new DeckValidator();

        public GameState Create(long seed, MatchRules rules,
                                IReadOnlyList<CardDefinition> playerZeroDeck,
                                IReadOnlyList<CardDefinition> playerOneDeck)
        {
            ValidateDeck(playerZeroDeck, 0);
            ValidateDeck(playerOneDeck, 1);

            // The boolean flag's meaning lives in GameState's constructor (not ported yet).
            var state = new GameState(seed, rules, false);
            state.SetStartingPlayer(CoinFlipWinner(seed));
            LoadPlayerDeck(state, seed, 0, playerZeroDeck);
            LoadPlayerDeck(state, seed, 1, playerOneDeck);
            state.DrawInitialHands();
            state.InitializeMatch();
            return state;
        }

        private void ValidateDeck(IReadOnlyList<CardDefinition> deck, int playerId)
        {
            var errors = _deckValidator.Validate(deck);
            if (errors.Count > 0)
                throw new ArgumentException("Player " + playerId + " deck is invalid: " + string.Join("; ", errors));
        }

        private void LoadPlayerDeck(GameState state, long seed, int playerId, IReadOnlyList<CardDefinition> definitions)
        {
            // Java: Collections.shuffle(shuffled, new Random(derivedSeed(seed, playerId))).
            // SeededRng wraps System.Random, whose stream differs from java.util.Random
            // (CONVENTIONS.md §4) — shuffle ORDER algorithm matches, stream does not.
            var shuffled = new List<CardDefinition>(definitions);
            new SeededRng(DerivedSeed(seed, playerId)).Shuffle(shuffled);

            var instanceIds = new List<long>();
            for (int index = 0; index < shuffled.Count; index++)
            {
                var definition = shuffled[index];
                // Deterministic id = Java UUID.nameUUIDFromBytes(...).getMostSignificantBits().
                var instanceId = NameSeededId(seed, playerId, index, definition.Id);
                var instance = new CardInstance(instanceId, definition, playerId, Zone.DECK);
                state.Register(instance);
                instanceIds.Add(instanceId);
            }
            state.Player(playerId).LoadDeck(instanceIds);
        }

        private static long DerivedSeed(long seed, int playerId) =>
            seed ^ unchecked((long)0x9E3779B97F4A7C15UL * (playerId + 1L));

        private static int CoinFlipWinner(long seed)
        {
            long mixed = seed + unchecked((long)0x9E3779B97F4A7C15UL);
            mixed = (mixed ^ (long)((ulong)mixed >> 30)) * unchecked((long)0xBF58476D1CE4E5B9UL);
            mixed = (mixed ^ (long)((ulong)mixed >> 27)) * unchecked((long)0x94D049BB133111EBUL);
            mixed ^= (long)((ulong)mixed >> 31);
            return (int)(mixed & 1L);
        }

        /// <summary>
        /// Mirrors java.util.UUID.nameUUIDFromBytes(name).getMostSignificantBits():
        /// MD5 digest with the version (3) and IETF variant bits set, first 8
        /// bytes read big-endian. Returns a long (no Guid per CONVENTIONS.md §3);
        /// the value equals Java's UUID.getMostSignificantBits().
        /// </summary>
        private static long NameSeededId(long seed, int playerId, int index, string definitionId)
        {
            byte[] hash;
            using (var md5 = MD5.Create())
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(
                    seed + ":" + playerId + ":" + index + ":" + definitionId));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
            long msb = 0;
            for (int i = 0; i < 8; i++) msb = (msb << 8) | hash[i];
            return msb;
        }
    }
}
