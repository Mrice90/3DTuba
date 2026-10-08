import com.infiniteconquest.cli.ActionHints;
import com.infiniteconquest.cli.BotDifficulty;
import com.infiniteconquest.cli.BotPlayer;
import com.infiniteconquest.cli.CapitalRoster;
import com.infiniteconquest.cli.CommandProcessor;
import com.infiniteconquest.cli.DemoMatchFactory;
import com.infiniteconquest.cli.FactionDecks;
import com.infiniteconquest.cli.PrototypeCardPool;
import com.infiniteconquest.core.*;

import java.util.*;

/**
 * Rules audit for the rules bridge engine (pinned alpha + 3DTuba overlay).
 *
 * Plays seeded bot-vs-bot HEX matches (both starter decks, all difficulties)
 * and checks, at sampled decisions, that the legal-action list and the engine
 * agree in both directions: every listed action is accepted, and every other
 * well-formed action in a broad candidate set (every play/burrow/move/attack/
 * blink/cast/activate on every hex and stack card) is rejected. After every
 * action it checks board invariants. With "ic3d" it also checks summon slots.
 *
 * Usage: java -cp <classes>:<jar> RulesAudit <alpha|ic3d> <matches> [sampleEvery]
 * Exit code 0 when no finding, 1 otherwise. Findings print one per line.
 */
public final class RulesAudit {
    private static final ActionHints HINTS = new ActionHints();
    private static final GameEngine ENGINE = new GameEngine();
    private static final Map<String, Integer> findings = new TreeMap<>();
    private static final Map<String, String> firstExample = new HashMap<>();
    private static long checkedCandidates;
    private static long checkedDecisions;
    private static long coveredActions, slotBlockedSummons, summons;
    /** Alpha-engine gaps the ic3d overlay fixes; reported but not failed in alpha mode. */
    private static final String KNOWN_ALPHA_GAP = "top Permanent at or below 0 HP is still in play";

    public static void main(String[] args) {
        boolean ic3d = args.length > 0 && args[0].equals("ic3d");
        int matches = args.length > 1 ? Integer.parseInt(args[1]) : 10;
        int sampleEvery = args.length > 2 ? Integer.parseInt(args[2]) : 4;
        HouseRules.set(ic3d, ic3d);
        String[][] pairs = {{"ZEUS", "POSEIDON"}, {"POSEIDON", "ZEUS"}};
        BotDifficulty[] levels = BotDifficulty.values();
        int[] wins = new int[3];
        long actions = 0;
        for (int m = 0; m < matches; m++) {
            long seed = 1000 + m;
            String[] f = pairs[m % 2];
            BotDifficulty level = levels[m % levels.length];
            GameState state = newMatch(seed, f[0], f[1]);
            CommandProcessor commands = new CommandProcessor(state);
            BotPlayer[] bots = {new BotPlayer(level, new Random(seed ^ 1)), new BotPlayer(level, new Random(seed ^ 2))};
            int decision = 0;
            while (state.phase() != Phase.GAME_OVER && state.turnNumber() <= 300 && decision < 20_000) {
                int p = state.activePlayer();
                if (decision % sampleEvery == 0) auditDecision(state, "match " + m + " decision " + decision);
                BotPlayer.Decision d = bots[p].takeNextAction(state, commands, p);
                actions++;
                if (d.command().matches("(activate|cast) .*-.*")) coveredActions++;
                if (d.command().startsWith("play ") && d.result().contains("Character summoned")) summons++;
                if (!d.result().startsWith("OK:")) finding("bot's listed action rejected", d.command() + " -> " + d.result());
                checkInvariants(state, "after " + d.command());
                if (!d.command().equals("end") && state.phase() != Phase.GAME_OVER && state.activePlayer() == p) {
                    BotPlayer.Decision r = bots[1 - p].react(state, commands, 1 - p);
                    if (r != null) {
                        actions++;
                        if (!r.result().startsWith("OK:")) finding("bot's listed reaction rejected", r.command() + " -> " + r.result());
                        checkInvariants(state, "after reaction " + r.command());
                    }
                }
                decision++;
            }
            int w = state.winner().isPresent() ? state.winner().getAsInt() : 2;
            wins[w]++;
            if (w == 2) finding("match did not finish", "seed " + seed + " turn " + state.turnNumber());
        }
        System.out.println("rules audit (" + (ic3d ? "ic3d" : "alpha") + "): " + matches + " matches, " + actions
                + " actions, " + checkedDecisions + " decisions cross-checked against " + checkedCandidates
                + " candidate actions; wins seat0=" + wins[0] + " seat1=" + wins[1] + " unfinished=" + wins[2]);
        if (ic3d) System.out.println("rules audit (ic3d): " + summons + " summons by bots, " + slotBlockedSummons
                + " candidate summons refused for lack of slots, " + coveredActions + " covered casts/activations by bots");
        Integer known = ic3d ? null : findings.remove(KNOWN_ALPHA_GAP);
        if (known != null) System.out.println("KNOWN alpha gap x" + known + " (fixed by rules ic3d): " + KNOWN_ALPHA_GAP + " | e.g. " + firstExample.get(KNOWN_ALPHA_GAP));
        if (findings.isEmpty()) {
            System.out.println("rules audit: PASS (no findings)");
            System.exit(0);
        }
        findings.forEach((k, v) -> System.out.println("FINDING x" + v + ": " + k + " | e.g. " + firstExample.get(k)));
        System.exit(1);
    }

    static GameState newMatch(long seed, String faction0, String faction1) {
        PrototypeCardPool pool = new PrototypeCardPool();
        FactionDecks decks = new FactionDecks(pool);
        CapitalRoster capitals = new CapitalRoster();
        List<CardDefinition> cards0 = decks.starter(faction0);
        List<CardDefinition> cards1 = decks.starter(faction1);
        DeckBuild b0 = new DeckBuild(faction0 + " starter", faction0, null, capitals.defaultForDeck(cards0).orElseThrow(), cards0);
        DeckBuild b1 = new DeckBuild(faction1 + " starter", faction1, null, capitals.defaultForDeck(cards1).orElseThrow(), cards1);
        return new DemoMatchFactory().create(seed, b0, b1, new BoardPosition(1, 0), new BoardPosition(2, 5), BoardGeometry.HEX);
    }

    /** Listed actions must all be accepted; every other candidate must be rejected. */
    static void auditDecision(GameState state, String where) {
        checkedDecisions++;
        int p = state.activePlayer();
        Set<String> listed = new LinkedHashSet<>(HINTS.forActivePlayer(state, ENGINE));
        for (String command : listed) {
            String result = tryOnCopy(state, command);
            if (!result.startsWith("OK:")) finding("listed action rejected by engine: " + verb(command) + " (" + reason(result) + ")", where + ": " + command);
        }
        for (String command : candidates(state, p)) {
            if (listed.contains(command)) continue;
            checkedCandidates++;
            String result = tryOnCopy(state, command);
            if (result.startsWith("OK:")) finding("engine accepts an action the legal list omits: " + verb(command), where + ": " + command + " " + describe(state, command));
            if (result.contains("free summon slot")) slotBlockedSummons++;
        }
        // Reactions for the waiting player.
        Set<String> reactions = new LinkedHashSet<>(HINTS.spellActionsForPlayer(state, 1 - p));
        for (String command : reactions) {
            String result = tryOnCopy(state, command);
            if (!result.startsWith("OK:")) finding("listed reaction rejected by engine (" + reason(result) + ")", where + ": " + command);
        }
    }

    static String tryOnCopy(GameState state, String command) {
        GameState copy = state.copy();
        try {
            return new CommandProcessor(copy).execute(command);
        } catch (RuntimeException e) {
            return "CRASH: " + e;
        }
    }

    static List<String> candidates(GameState state, int p) {
        List<String> out = new ArrayList<>();
        List<BoardPosition> cells = new ArrayList<>(state.board().positions());
        List<UUID> hand = state.player(p).hand();
        for (int h = 0; h < hand.size(); h++) {
            CardInstance card = state.card(hand.get(h)).orElseThrow();
            for (BoardPosition c : cells) {
                if (card.definition().type() == CardType.SPELL) {
                    List<UUID> stack = state.board().stackAt(c);
                    for (int i = 0; i < stack.size(); i++) {
                        boolean top = i == stack.size() - 1;
                        String base = "cast " + h + " " + c.x() + " " + c.y() + (top ? "" : " " + stack.get(i));
                        boolean teleport = card.definition().effects().stream().anyMatch(e -> e.type() == SpellEffectType.TELEPORT_CHARACTER);
                        if (teleport && top) {
                            for (BoardPosition d : cells) out.add(base + " " + d.x() + " " + d.y());
                        } else if (!teleport) out.add(base);
                    }
                } else {
                    out.add("play " + h + " " + c.x() + " " + c.y());
                    if (card.definition().type() == CardType.CHARACTER) out.add("burrow " + h + " " + c.x() + " " + c.y());
                }
            }
        }
        for (BoardPosition from : cells) {
            if (state.board().isEmpty(from)) continue;
            for (BoardPosition to : cells) {
                if (from.equals(to)) continue;
                out.add("move " + from.x() + " " + from.y() + " " + to.x() + " " + to.y());
                out.add("attack " + from.x() + " " + from.y() + " " + to.x() + " " + to.y());
                out.add("blink " + from.x() + " " + from.y() + " " + to.x() + " " + to.y());
            }
            List<UUID> stack = state.board().stackAt(from);
            for (int i = 0; i < stack.size(); i++) {
                boolean top = i == stack.size() - 1;
                out.add("activate " + from.x() + " " + from.y() + (top ? "" : " " + stack.get(i)));
            }
        }
        return out;
    }

    static void checkInvariants(GameState state, String where) {
        Map<UUID, Integer> seen = new HashMap<>();
        for (BoardPosition pos : state.board().positions()) {
            List<UUID> stack = state.board().stackAt(pos);
            Set<Integer> characterOwners = new HashSet<>();
            for (int i = 0; i < stack.size(); i++) {
                CardInstance c = state.card(stack.get(i)).orElse(null);
                if (c == null) { finding("board holds an unregistered card", where); continue; }
                seen.merge(c.instanceId(), 1, Integer::sum);
                if (c.zone() != Zone.BATTLEFIELD) finding("card on the board is not in the battlefield zone", where + ": " + c.definition().id() + " " + c.zone());
                if (c.definition().type() == CardType.CHARACTER) characterOwners.add(c.owner());
                if (c.definition().type() == CardType.CAPITAL && i != 0) finding("Capital is not at the bottom of its stack", where);
                if (c.definition().type() == CardType.SPELL) finding("Spell on the board", where);
            }
            if (characterOwners.size() > 1) finding("Characters of both players share a hex", where + " at " + pos);
            state.board().topAt(pos).flatMap(state::card).ifPresent(top -> {
                if (top.definition().isPermanent() && top.damage() >= top.definition().hitPoints() && state.phase() != Phase.GAME_OVER)
                    finding("top Permanent at or below 0 HP is still in play", where + ": " + top.definition().id());
            });
        }
        seen.forEach((id, n) -> { if (n > 1) finding("card on the board twice", where); });
        for (int p = 0; p < 2; p++) {
            for (CardInstance c : state.battlefieldCards(p)) if (!seen.containsKey(c.instanceId())) finding("battlefield card missing from the board", where + ": " + c.definition().id());
            if (state.player(p).currentGp() < 0) finding("negative GP", where);
            if (HouseRules.summonSlots()) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    int cap = SummonSlots.capacity(c.definition());
                    if (cap > 0 && SummonSlots.used(state, c.instanceId()) > cap) finding("summon slots over capacity", where + ": " + c.definition().id());
                }
                for (CardInstance c : state.battlefieldCards(p)) {
                    if (c.definition().type() != CardType.CHARACTER) continue;
                    if (!state.summonAnchors().containsKey(c.instanceId())) finding("Character on the battlefield without a summon anchor", where + ": " + c.definition().id());
                }
            }
        }
        if (state.winner().isPresent() != (state.phase() == Phase.GAME_OVER)) finding("winner and GAME_OVER disagree", where);
    }

    static String verb(String command) { return command.split(" ")[0] + (command.split(" ").length > (command.startsWith("activate") ? 3 : 4) && (command.startsWith("activate") || command.startsWith("cast")) && command.contains("-") ? " (covered card)" : ""); }
    static String reason(String result) { return result.replaceAll("[0-9a-f]{8}-[0-9a-f-]{27}", "<id>"); }

    static String describe(GameState state, String command) {
        String[] parts = command.split(" ");
        try {
            BoardPosition a = new BoardPosition(Integer.parseInt(parts[parts[0].equals("play") || parts[0].equals("burrow") || parts[0].equals("cast") ? 2 : 1]),
                    Integer.parseInt(parts[parts[0].equals("play") || parts[0].equals("burrow") || parts[0].equals("cast") ? 3 : 2]));
            StringBuilder sb = new StringBuilder("[stack at " + a.x() + "," + a.y() + ":");
            for (UUID id : state.board().stackAt(a)) state.card(id).ifPresent(c -> sb.append(" ").append(c.definition().id()).append("/p").append(c.owner()));
            if (parts[0].equals("play") || parts[0].equals("burrow") || parts[0].equals("cast")) {
                state.card(state.player(state.activePlayer()).hand().get(Integer.parseInt(parts[1]))).ifPresent(c -> sb.append("; card ").append(c.definition().id()));
            }
            return sb.append("]").toString();
        } catch (RuntimeException e) {
            return "";
        }
    }

    static void finding(String kind, String example) {
        findings.merge(kind, 1, Integer::sum);
        firstExample.putIfAbsent(kind, example);
    }
}
