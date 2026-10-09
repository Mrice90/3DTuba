// Port of game-core/src/main/java/com/infiniteconquest/core/GameSnapshot.java @ Desolate-Tuba e0e565b
using System.Collections.Generic;
using InfiniteConquest.RulesCore.Data;

namespace InfiniteConquest.RulesCore.Core
{
    /// <summary>
    /// A redacted, serializable view of a GameState for exactly one viewing player.
    /// This is the ONLY match state that ever crosses the network.
    /// Property names stay camelCase to match the wire JSON (CONVENTIONS.md §5
    /// applied to the snapshot as well as events — it is the same wire surface).
    ///
    /// Visibility contract:
    /// - Battlefield and discard piles: full card detail for both players (public zones).
    /// - Viewing player's own hand: full card detail.
    /// - Both decks: counts only (deckCounts); no card IDs and no deck order ever
    ///   appear in cards.
    /// - Opponent's hand: count only (handCounts); no card IDs.
    /// - Events: a redacted recent tail; event types that reference hidden-zone
    ///   card IDs (for example CARD_DRAWN) are dropped server-side before transmission.
    /// </summary>
    public record GameSnapshot(
        long seed,
        /// <summary>BoardGeometry name: "HEX" or "SQUARE".</summary>
        string rulesId,
        int viewingPlayer,
        int activePlayer,
        int startingPlayer,
        int turnNumber,
        /// <summary>Personal turn counters, indexed by player.</summary>
        int[] personalTurns,
        /// <summary>Phase name.</summary>
        string phase,
        /// <summary>Null until the game ends.</summary>
        int? winner,
        /// <summary>Current GP, indexed by player.</summary>
        int[] gp,
        /// <summary>Maximum GP, indexed by player.</summary>
        int[] maxGp,
        /// <summary>Hand sizes, indexed by player. Opponent entries are counts only.</summary>
        int[] handCounts,
        /// <summary>Deck sizes, indexed by player. Opponent entries are counts only.</summary>
        int[] deckCounts,
        /// <summary>Lands played this turn, indexed by player (drives play legality hints).</summary>
        int[] landsPlayed,
        /// <summary>Structures played this turn, indexed by player.</summary>
        int[] structuresPlayed,
        /// <summary>
        /// Every visible card: all battlefield and discard cards, plus the viewing
        /// player's own hand. Battlefield cards are listed bottom-to-top per stack
        /// so the board rebuilds exactly.
        /// </summary>
        IReadOnlyList<CardView> cards,
        /// <summary>Redacted recent events, oldest first.</summary>
        IReadOnlyList<GameEvent> events,
        /// <summary>
        /// True while either player may still submit a mulligan decision.
        /// Clients use this to tell a pre-mulligan snapshot from a live one.
        /// </summary>
        bool mulliganOpen)
    {
        /// <summary>
        /// Full detail for one visible card.
        /// DEVIATION (per task contract): Java UUID instanceId → long.
        /// </summary>
        public record CardView(
            long instanceId,
            string definitionId,
            int owner,
            Zone zone,
            /// <summary>Null unless zone == BATTLEFIELD.</summary>
            BoardPosition? position,
            int damage,
            bool tapped,
            int attackBonus,
            int defenseBonus,
            bool attackedThisTurn,
            bool blinkUsedThisTurn,
            bool abilityUsedThisTurn,
            int movementSpent) { }
    }
}
