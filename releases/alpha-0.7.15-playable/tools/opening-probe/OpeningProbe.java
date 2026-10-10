import com.infiniteconquest.cli.ActionHints;
import com.infiniteconquest.cli.BotDifficulty;
import com.infiniteconquest.cli.BotPlayer;
import com.infiniteconquest.cli.CapitalRoster;
import com.infiniteconquest.cli.CommandProcessor;
import com.infiniteconquest.cli.DemoMatchFactory;
import com.infiniteconquest.cli.FactionDecks;
import com.infiniteconquest.cli.PrototypeCardPool;
import com.infiniteconquest.core.BoardGeometry;
import com.infiniteconquest.core.BoardPosition;
import com.infiniteconquest.core.CardDefinition;
import com.infiniteconquest.core.CardInstance;
import com.infiniteconquest.core.CardType;
import com.infiniteconquest.core.DeckBuild;
import com.infiniteconquest.core.GameEngine;
import com.infiniteconquest.core.GameState;
import com.infiniteconquest.core.Phase;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import java.util.UUID;

/**
 * AI-108-OPENING probe. Runs bot-vs-bot HEX matches set up exactly like the
 * rules bridge (alpha starter decks, capitals at (1,0)/(2,5), seeded bot
 * RNGs) and records how each player's opening plays out, with or without
 * the provisional structure-slot policy.
 *
 * The slot policy here is a re-implementation of the documented SP2 rule
 * (docs/muse/sprint-03/2026-10-08-slot-qa-report.md section 1), NOT the SP2
 * overlay classes, which live only in the local Unity package:
 *   supply = 2 x living controlled structures (pooled); capitals/lands 0;
 *   ordinary character costs 1; Olympian Storm Titan and Trident Core cost 2;
 *   a play/burrow is offered only if used + cost <= supply.
 * The bot sees the filtered legal list, so it never attempts a blocked summon.
 *
 * Output: one CSV row per player per personal turn (first PROBE_TURNS turns)
 * plus one match row, on stdout.
 *
 * Usage: java -cp <alpha jar>:. OpeningProbe <slots:on|off> <difficulty>
 *        <faction0> <faction1> <seedFrom> <seedTo>
 */
public final class OpeningProbe {
    static final int PROBE_TURNS = 10;
    static final int SUPPLY_PER_STRUCTURE = 2;
    static final int MAX_TURNS = 300;

    static boolean slots;
    static String variant;
    static Method choose;

    public static void main(String[] args) throws Exception {
        slots = args[0].equals("on");
        BotDifficulty difficulty = BotDifficulty.valueOf(args[1]);
        String[] factions = {args[2], args[3]};
        long from = Long.parseLong(args[4]);
        long to = Long.parseLong(args[5]);
        variant = args.length > 6 ? args[6] : "base";
        choose = BotPlayer.class.getDeclaredMethod("choose", GameState.class, List.class, int.class);
        choose.setAccessible(true);
        System.out.println("kind,slots,difficulty,seed,seat,faction,starter,pturn,"
                + "hand_structs,usable_struct,structs_board,supply,used,actions,summons,"
                + "blocked_offers,chars_end,lands_board,chars_hand,struct_offered,summon_offered,gp,winner,turns,first_struct,first_summon,never_summoned,variant");
        for (long seed = from; seed <= to; seed++) run(seed, difficulty, factions);
    }

    /**
     * Deck lever "deck16": swap the four most expensive characters for extra
     * copies of the two cheapest structures (12 -> 16 structures, 60 cards).
     */
    static List<CardDefinition> deck(List<CardDefinition> starter) {
        if (!variant.contains("deck16")) return starter;
        List<CardDefinition> out = new ArrayList<>(starter);
        List<CardDefinition> characters = out.stream()
                .filter(c -> c.type() == CardType.CHARACTER)
                .sorted(java.util.Comparator.comparingInt(CardDefinition::cost).reversed()).toList();
        for (int i = 0; i < 4; i++) out.remove(characters.get(i));
        List<CardDefinition> cheap = starter.stream().filter(c -> c.type() == CardType.STRUCTURE)
                .distinct().sorted(java.util.Comparator.comparingInt(CardDefinition::cost)).limit(2).toList();
        for (CardDefinition c : cheap) { out.add(c); out.add(c); }
        return out;
    }

    /**
     * Opening-hand lever "mull": using the engine's existing mulligan (up to
     * 3 cards), a hand missing a Land or a Structure throws back its most
     * expensive non-development cards. No rule change; it is advice a
     * tutorial/HUD could give.
     */
    static void mulligan(GameState s, int p) {
        boolean land = false, struct = false;
        List<CardInstance> others = new ArrayList<>();
        for (UUID id : s.player(p).hand()) {
            CardInstance c = s.card(id).orElseThrow();
            if (c.definition().type() == CardType.LAND) land = true;
            else if (c.definition().type() == CardType.STRUCTURE) struct = true;
            else others.add(c);
        }
        if (land && struct) { s.mulligan(p, List.of()); return; }
        others.sort(java.util.Comparator.comparingInt((CardInstance c) -> c.definition().cost()).reversed());
        s.mulligan(p, others.stream().limit(3).map(CardInstance::instanceId).toList());
    }

    static int slotCost(CardDefinition d) {
        String id = d.id();
        return id.contains("storm_titan") || id.contains("trident_core") ? 2 : 1;
    }

    static int supply(GameState s, int p) {
        return SUPPLY_PER_STRUCTURE * (int) s.battlefieldCards(p).stream()
                .filter(c -> c.definition().type() == CardType.STRUCTURE).count();
    }

    static int used(GameState s, int p) {
        return s.battlefieldCards(p).stream()
                .filter(c -> c.definition().type() == CardType.CHARACTER)
                .mapToInt(c -> slotCost(c.definition())).sum();
    }

    static int chars(GameState s, int p) {
        return (int) s.battlefieldCards(p).stream()
                .filter(c -> c.definition().type() == CardType.CHARACTER).count();
    }

    static int lands(GameState s, int p) {
        return (int) s.battlefieldCards(p).stream()
                .filter(c -> c.definition().type() == CardType.LAND).count();
    }

    static int structs(GameState s, int p) {
        return (int) s.battlefieldCards(p).stream()
                .filter(c -> c.definition().type() == CardType.STRUCTURE).count();
    }

    static CardInstance handCard(GameState s, int p, String command) {
        String[] parts = command.split("\\s+");
        UUID id = s.player(p).hand().get(Integer.parseInt(parts[1]));
        return s.card(id).orElseThrow();
    }

    static boolean isSummon(GameState s, int p, String command) {
        if (!command.startsWith("play ") && !command.startsWith("burrow ")) return false;
        return handCard(s, p, command).definition().type() == CardType.CHARACTER;
    }

    static boolean isStructurePlay(GameState s, int p, String command) {
        return command.startsWith("play ")
                && handCard(s, p, command).definition().type() == CardType.STRUCTURE;
    }

    /** Offered summons removed by the slot cap; the remaining legal list. */
    static int[] blocked = new int[1];
    static List<String> filter(GameState s, int p, List<String> legal) {
        blocked[0] = 0;
        if (!slots) return legal;
        int free = supply(s, p) - used(s, p);
        List<String> out = new ArrayList<>();
        for (String c : legal) {
            if (isSummon(s, p, c) && slotCost(handCard(s, p, c).definition()) > free) {
                blocked[0]++;
                continue;
            }
            out.add(c);
        }
        return out;
    }

    static void run(long seed, BotDifficulty difficulty, String[] factions) throws Exception {
        PrototypeCardPool pool = new PrototypeCardPool();
        FactionDecks decks = new FactionDecks(pool);
        CapitalRoster capitals = new CapitalRoster();
        List<CardDefinition> cards0 = deck(decks.starter(factions[0]));
        List<CardDefinition> cards1 = deck(decks.starter(factions[1]));
        DeckBuild b0 = new DeckBuild(factions[0] + " starter", factions[0], null,
                capitals.defaultForDeck(cards0).orElseThrow(), cards0);
        DeckBuild b1 = new DeckBuild(factions[1] + " starter", factions[1], null,
                capitals.defaultForDeck(cards1).orElseThrow(), cards1);
        GameState state = new DemoMatchFactory().create(seed, b0, b1,
                new BoardPosition(1, 0), new BoardPosition(2, 5), BoardGeometry.HEX);
        if (variant.contains("mull")) for (int p = 0; p < 2; p++) mulligan(state, p);
        CommandProcessor commands = new CommandProcessor(state);
        ActionHints hints = new ActionHints();
        BotPlayer[] bots = {
                new BotPlayer(difficulty, new Random(seed ^ 0xC0FFEE1L)),
                new BotPlayer(difficulty, new Random(seed ^ 0xC0FFEE2L))};
        int starter = state.activePlayer();
        int[] firstStruct = {-1, -1};
        int[] firstSummon = {-1, -1};
        StringBuilder rows = new StringBuilder();
        int decisions = 0;

        while (state.phase() != Phase.GAME_OVER && state.turnNumber() <= MAX_TURNS) {
            int p = state.activePlayer();
            int pturn = state.personalTurnNumber(p);
            int handStructs = 0;
            boolean usableStruct = false;
            for (UUID id : state.player(p).hand()) {
                CardDefinition d = state.card(id).orElseThrow().definition();
                if (d.type() == CardType.STRUCTURE) {
                    handStructs++;
                    if (d.cost() <= pturn) usableStruct = true;
                }
            }
            int actions = 0, summons = 0, blockedOffers = 0;
            int charsHand = 0;
            for (UUID id : state.player(p).hand())
                if (state.card(id).orElseThrow().definition().type() == CardType.CHARACTER) charsHand++;
            int gpStart = state.player(p).currentGp();
            boolean structOffered = false, summonOffered = false;
            while (state.phase() != Phase.GAME_OVER && state.activePlayer() == p
                    && state.personalTurnNumber(p) == pturn) {
                if (++decisions > 200_000) throw new IllegalStateException("decision cap, seed " + seed);
                List<String> raw = hints.forActivePlayer(state, new GameEngine());
                for (String c : raw) {
                    if (isStructurePlay(state, p, c)) structOffered = true;
                    if (isSummon(state, p, c)) summonOffered = true;
                }
                List<String> legal = filter(state, p, raw);
                blockedOffers = Math.max(blockedOffers, blocked[0]);
                String cmd = (String) choose.invoke(bots[p], state, legal, p);
                boolean summon = !cmd.equals("end") && isSummon(state, p, cmd);
                boolean struct = !cmd.equals("end") && isStructurePlay(state, p, cmd);
                commands.execute(cmd);
                if (cmd.equals("end")) break;
                actions++;
                if (summon) { summons++; if (firstSummon[p] < 0) firstSummon[p] = pturn; }
                if (struct && firstStruct[p] < 0) firstStruct[p] = pturn;
                if (state.phase() != Phase.GAME_OVER && state.activePlayer() == p)
                    bots[1 - p].react(state, commands, 1 - p);
            }
            if (pturn <= PROBE_TURNS) {
                rows.append(String.join(",", "turn", slots ? "on" : "off", difficulty.name(),
                        Long.toString(seed), Integer.toString(p), factions[p],
                        Integer.toString(p == starter ? 1 : 0), Integer.toString(pturn),
                        Integer.toString(handStructs), usableStruct ? "1" : "0",
                        Integer.toString(structs(state, p)), Integer.toString(supply(state, p)),
                        Integer.toString(used(state, p)), Integer.toString(actions),
                        Integer.toString(summons), Integer.toString(blockedOffers),
                        Integer.toString(chars(state, p)), Integer.toString(lands(state, p)),
                        Integer.toString(charsHand), structOffered ? "1" : "0", summonOffered ? "1" : "0",
                        Integer.toString(gpStart), "", "", "", "", "", variant)).append('\n');
            }
        }
        System.out.print(rows);
        String winner = state.winner().isPresent() ? Integer.toString(state.winner().getAsInt()) : "none";
        for (int p = 0; p < 2; p++) {
            System.out.println(String.join(",", "match", slots ? "on" : "off", difficulty.name(),
                    Long.toString(seed), Integer.toString(p), factions[p],
                    Integer.toString(p == starter ? 1 : 0), "", "", "", "", "", "", "", "", "", "",
                    "", "", "", "", "",
                    winner, Integer.toString(state.turnNumber()),
                    Integer.toString(firstStruct[p]), Integer.toString(firstSummon[p]),
                    firstSummon[p] < 0 ? "1" : "0", variant));
        }
    }
}
