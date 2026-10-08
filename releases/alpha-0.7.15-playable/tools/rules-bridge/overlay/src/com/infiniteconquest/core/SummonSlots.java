package com.infiniteconquest.core;

import java.util.*;

/**
 * 3DTuba rules overlay (not in TubaExperiment): the structure summon-slot
 * rule (backlog AI-108), active when {@link HouseRules#summonSlots()} is on.
 *
 * <p>Every friendly Structure and Capital has summon slots. A Character can
 * only be summoned (or burrowed) on or next to one of them that has enough
 * free slots for it, and it occupies those slots for as long as it stays on
 * the battlefield. Slots free up when the Character is destroyed or returned
 * to hand. Losing the anchoring Structure does not remove its Characters.
 *
 * <p>The numbers live in this class only, so balancing is a one-file change.
 */
public final class SummonSlots {
    /** Slots a Capital provides. */
    public static final int CAPITAL_SLOTS = 3;

    private SummonSlots() {}

    /** Slots a Structure or Capital provides; 0 for anything else. Higher-tier (costlier) Structures hold more. */
    public static int capacity(CardDefinition definition) {
        return switch (definition.type()) {
            case CAPITAL -> CAPITAL_SLOTS;
            case STRUCTURE -> definition.cost() <= 1 ? 1
                    : definition.cost() <= 3 ? 2
                    : definition.cost() <= 5 ? 3 : 4;
            default -> 0;
        };
    }

    /** Slots a Character occupies: bigger (costlier) Characters take more. */
    public static int cost(CardDefinition definition) {
        if (definition.type() != CardType.CHARACTER) return 0;
        return definition.cost() <= 3 ? 1 : definition.cost() <= 6 ? 2 : 3;
    }

    /** Slots in use on an anchor: the slot cost of every Character it summoned that is still on the battlefield. */
    public static int used(GameState state, UUID anchorId) {
        int used = 0;
        for (Map.Entry<UUID, UUID> entry : state.summonAnchors().entrySet()) {
            if (!entry.getValue().equals(anchorId)) continue;
            CardInstance character = state.card(entry.getKey()).orElse(null);
            if (character != null && character.zone() == Zone.BATTLEFIELD) used += cost(character.definition());
        }
        return used;
    }

    public static int free(GameState state, CardInstance anchor) {
        return Math.max(0, capacity(anchor.definition()) - used(state, anchor.instanceId()));
    }

    /**
     * The friendly Structure or Capital that would pay for summoning
     * {@code character} at {@code destination}: one on the destination stack
     * or on an adjacent hex with enough free slots. Prefers the destination
     * stack, then the most free slots, then board order, so the choice is
     * deterministic (lockstep play depends on that).
     */
    public static Optional<CardInstance> anchorFor(GameState state, int player, BoardPosition destination,
                                                   CardDefinition character) {
        int need = cost(character);
        CardInstance best = null;
        int bestScore = Integer.MIN_VALUE;
        for (BoardPosition position : state.board().positions()) {
            boolean here = position.equals(destination);
            if (!here && !state.rules().geometry().adjacent(destination, position)) continue;
            for (UUID id : state.board().stackAt(position)) {
                CardInstance anchor = state.card(id).orElse(null);
                if (anchor == null || anchor.owner() != player || capacity(anchor.definition()) == 0) continue;
                int free = free(state, anchor);
                if (free < need) continue;
                int score = (here ? 1000 : 0) + free;
                if (score > bestScore) { best = anchor; bestScore = score; }
            }
        }
        return Optional.ofNullable(best);
    }

    /** True when the slot rule is off or a slot is available. */
    public static boolean allows(GameState state, int player, BoardPosition destination, CardDefinition character) {
        return !HouseRules.summonSlots() || anchorFor(state, player, destination, character).isPresent();
    }
}
