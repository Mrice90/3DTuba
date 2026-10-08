package com.infiniteconquest.core;

/**
 * 3DTuba rules overlay (not in TubaExperiment). Switches for the Infinite
 * Conquest 3D rule changes layered over the pinned alpha engine
 * (TubaExperiment 992bc95). Both default to off, so an engine built with the
 * overlay plays exactly like the alpha until a match opts in (the rules
 * bridge sets them from the {@code new} request's {@code rules} field).
 *
 * <ul>
 *   <li>{@link #summonSlots}: Characters must be summoned through a friendly
 *       Structure or Capital with free summon slots ({@link SummonSlots}).</li>
 *   <li>{@link #coveredStructures}: Structures and Capitals stay in play
 *       under the cards stacked on them. Permanent-damage and repair spells
 *       can target a covered Structure or Capital, and a Land, Structure or
 *       Capital covered only by its owner's cards can still use its
 *       activated ability.</li>
 * </ul>
 *
 * The bridge runs one match per process, so process-wide switches are enough.
 */
public final class HouseRules {
    private static volatile boolean summonSlots;
    private static volatile boolean coveredStructures;

    private HouseRules() {}

    public static boolean summonSlots() { return summonSlots; }
    public static boolean coveredStructures() { return coveredStructures; }

    public static void set(boolean slots, boolean covered) {
        summonSlots = slots;
        coveredStructures = covered;
    }

    public static void reset() { set(false, false); }
}
