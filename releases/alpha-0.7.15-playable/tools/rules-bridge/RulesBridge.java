import com.fasterxml.jackson.databind.ObjectMapper;
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
import com.infiniteconquest.core.GameEvent;
import com.infiniteconquest.core.GameState;
import com.infiniteconquest.core.Phase;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Random;
import java.util.TreeMap;
import java.util.UUID;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * AI-079: headless rules bridge for the 3D playable (protocol v1.1.0).
 *
 * <p>A small Java program against the pinned alpha JAR that runs a
 * Zeus-vs-Poseidon HEX match and talks line-delimited JSON over
 * stdin/stdout. The Claude Unity thread spawns this process from
 * UnityProof to make the board playable — the protocol documented in
 * README.md is stable.
 *
 * <p>Protocol (one JSON object per line, each direction). Every request
 * carries {@code id} and {@code op}; every response carries {@code id},
 * {@code ok} and {@code revision}. Success responses include
 * {@code events} (AI-062 wire format), {@code state} (redacted) and
 * {@code legal}. Error responses carry {@code error} and
 * {@code error_code}; they never mutate state or revision.
 *
 * <ul>
 *   <li>{@code {"id":"r1","op":"new","seed":42,"human_player":0,
 *       "human_faction":"ZEUS","bot_faction":"POSEIDON",
 *       "difficulty":"HERO"}}
 *   <li>{@code {"id":"r2","op":"legal"}}
 *   <li>{@code {"id":"r3","op":"act","action_id":"r0-a5"}}
 *   <li>{@code {"id":"r4","op":"hash"}} — AI-097: canonical SHA-256 of the
 *       full <em>unredacted</em> game state for the relay lockstep hash
 *       exchange. Read-only: never mutates state or revision.
 * </ul>
 *
 * <p>Action ids are deterministic and revision-scoped
 * ({@code r<revision>-a<index>}). Stale or fabricated ids are rejected
 * with {@code INVALID_ACTION} and no state/revision mutation.
 *
 * <p>State is redacted: the opponent's hand and deck expose counts only,
 * never identities. The {@code hash} op is the exception by design —
 * lockstep compares it across two clients that both legitimately hold
 * the full state (same seed + same intents ⇒ same hash). Bot turns run
 * automatically until the next human decision or GAME_OVER. Stdout is
 * JSONL only; diagnostics go to stderr.
 */
public final class RulesBridge {
    private static final int MAX_TURNS = 300;
    private static final int MAX_DECISIONS = 200_000;

    private final ObjectMapper json = new ObjectMapper();
    private Session session;

    public static void main(String[] args) throws Exception {
        new RulesBridge().run();
    }

    private void run() throws Exception {
        BufferedReader in = new BufferedReader(
                new InputStreamReader(System.in, StandardCharsets.UTF_8));
        PrintWriter out = new PrintWriter(
                new OutputStreamWriter(System.out, StandardCharsets.UTF_8), true);
        String line;
        while ((line = in.readLine()) != null) {
            line = line.trim();
            if (line.isEmpty()) {
                continue;
            }
            Map<String, Object> response;
            String reqId = "";
            try {
                @SuppressWarnings("unchecked")
                Map<String, Object> req = json.readValue(line, Map.class);
                reqId = String.valueOf(req.getOrDefault("id", ""));
                response = handle(reqId, req);
            } catch (Exception e) {
                response = error(reqId, -1, "BAD_REQUEST",
                        "bad request: " + e.getMessage());
            }
            response.putIfAbsent("id", reqId);
            try {
                out.println(json.writeValueAsString(response));
            } catch (Exception e) {
                out.println("{\"id\":" + json.writeValueAsString(reqId)
                        + ",\"ok\":false,\"revision\":-1,"
                        + "\"error_code\":\"BAD_REQUEST\","
                        + "\"error\":\"response encoding failed\"}");
            }
        }
    }

    private Map<String, Object> handle(String reqId, Map<String, Object> req) {
        String op = String.valueOf(req.getOrDefault("op", ""));
        try {
            return switch (op) {
                case "new" -> doNew(reqId, req);
                case "legal" -> doLegal(reqId);
                case "act" -> doAct(reqId, req);
                case "hash" -> doHash(reqId);
                default -> error(reqId, revision(),
                        "BAD_REQUEST",
                        "unknown op: " + op + " (want new|legal|act|hash)");
            };
        } catch (Exception e) {
            return error(reqId, revision(), "INTERNAL",
                    e.getClass().getSimpleName() + ": " + e.getMessage());
        }
    }

    private int revision() {
        return session == null ? -1 : session.revision;
    }

    private Map<String, Object> doNew(String reqId, Map<String, Object> req) {
        long seed = req.get("seed") instanceof Number n
                ? n.longValue() : System.currentTimeMillis();
        int humanPlayer = req.get("human_player") instanceof Number n
                ? n.intValue() : 0;
        String humanFaction = String.valueOf(
                req.getOrDefault("human_faction", "ZEUS")).toUpperCase();
        String botFaction = String.valueOf(
                req.getOrDefault("bot_faction", "POSEIDON")).toUpperCase();
        String difficulty = String.valueOf(
                req.getOrDefault("difficulty", "HERO")).toUpperCase();
        if (humanPlayer != 0 && humanPlayer != 1) {
            return error(reqId, -1, "BAD_REQUEST",
                    "human_player must be 0 or 1");
        }
        if (!isFaction(humanFaction) || !isFaction(botFaction)) {
            return error(reqId, -1, "BAD_REQUEST",
                    "factions must be ZEUS or POSEIDON");
        }
        BotDifficulty botDifficulty;
        try {
            botDifficulty = BotDifficulty.valueOf(difficulty);
        } catch (IllegalArgumentException e) {
            return error(reqId, -1, "BAD_REQUEST",
                    "difficulty must be MORTAL, HERO or DEMIGOD");
        }
        session = new Session(seed, humanPlayer, humanFaction, botFaction,
                botDifficulty);
        List<Map<String, Object>> events = new ArrayList<>();
        session.runBotTurns(events);
        session.refreshLegal();
        return success(reqId, session.revision, events);
    }

    private static boolean isFaction(String f) {
        return "ZEUS".equals(f) || "POSEIDON".equals(f);
    }

    private Map<String, Object> doLegal(String reqId) {
        if (session == null) {
            return error(reqId, -1, "NO_MATCH",
                    "no match started (send op:new first)");
        }
        return success(reqId, session.revision, List.of());
    }

    private Map<String, Object> doAct(String reqId, Map<String, Object> req) {
        if (session == null) {
            return error(reqId, -1, "NO_MATCH",
                    "no match started (send op:new first)");
        }
        String actionId = String.valueOf(req.getOrDefault("action_id", ""));
        Session.ActResolution resolution = session.resolveAction(actionId);
        if (!resolution.valid()) {
            return error(reqId, session.revision, "INVALID_ACTION",
                    resolution.error());
        }
        List<Map<String, Object>> events = new ArrayList<>();
        String result = session.executeHuman(resolution.command(), events);
        if (!result.startsWith("OK:")) {
            return error(reqId, session.revision, "INVALID_ACTION",
                    "engine rejected action: " + result);
        }
        session.revision++;
        session.runBotTurns(events);
        session.refreshLegal();
        return success(reqId, session.revision, events);
    }

    /**
     * AI-097: canonical state hash for the relay lockstep hash exchange.
     * Returns SHA-256 over the canonical JSON of the full <em>unredacted</em>
     * game state (both hands/decks/discard, full board stacks with mutable
     * card state, GP, turn, phase). Read-only: never mutates state or
     * revision. Two bridges that started from the same {@code new} seed
     * and applied the same intents in the same order report the same hash.
     */
    private Map<String, Object> doHash(String reqId) {
        if (session == null) {
            return error(reqId, -1, "NO_MATCH",
                    "no match started (send op:new first)");
        }
        Map<String, Object> m = new LinkedHashMap<>();
        m.put("id", reqId);
        m.put("ok", true);
        m.put("revision", session.revision);
        m.put("turn", Math.max(1, session.state.turnNumber()));
        m.put("state_hash", sha256Hex(session.canonicalState()));
        return m;
    }

    private static String sha256Hex(String input) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] digest = md.digest(
                    input.getBytes(StandardCharsets.UTF_8));
            StringBuilder sb = new StringBuilder(digest.length * 2);
            for (byte b : digest) {
                sb.append(Character.forDigit((b >> 4) & 0xF, 16));
                sb.append(Character.forDigit(b & 0xF, 16));
            }
            return sb.toString();
        } catch (Exception e) {
            throw new IllegalStateException("SHA-256 unavailable", e);
        }
    }

    /**
     * Canonical JSON: object keys sorted, arrays in encounter order,
     * compact separators, standard string escaping. Deterministic for
     * equal logical state regardless of the map implementation used.
     */
    private static String canonicalJson(Object value) {
        StringBuilder sb = new StringBuilder();
        appendCanonical(sb, value);
        return sb.toString();
    }

    @SuppressWarnings("unchecked")
    private static void appendCanonical(StringBuilder sb, Object value) {
        if (value == null) {
            sb.append("null");
        } else if (value instanceof Map<?, ?> map) {
            TreeMap<String, Object> sorted = new TreeMap<>();
            for (Map.Entry<?, ?> e : map.entrySet()) {
                sorted.put(String.valueOf(e.getKey()), e.getValue());
            }
            sb.append('{');
            boolean first = true;
            for (Map.Entry<String, Object> e : sorted.entrySet()) {
                if (!first) {
                    sb.append(',');
                }
                first = false;
                appendString(sb, e.getKey());
                sb.append(':');
                appendCanonical(sb, e.getValue());
            }
            sb.append('}');
        } else if (value instanceof List<?> list) {
            sb.append('[');
            boolean first = true;
            for (Object item : list) {
                if (!first) {
                    sb.append(',');
                }
                first = false;
                appendCanonical(sb, item);
            }
            sb.append(']');
        } else if (value instanceof String s) {
            appendString(sb, s);
        } else if (value instanceof Boolean || value instanceof Number) {
            sb.append(value);
        } else {
            throw new IllegalArgumentException(
                    "non-canonical value type: " + value.getClass());
        }
    }

    private static void appendString(StringBuilder sb, String s) {
        sb.append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"' -> sb.append("\\\"");
                case '\\' -> sb.append("\\\\");
                case '\b' -> sb.append("\\b");
                case '\f' -> sb.append("\\f");
                case '\n' -> sb.append("\\n");
                case '\r' -> sb.append("\\r");
                case '\t' -> sb.append("\\t");
                default -> {
                    if (c < 0x20) {
                        sb.append(String.format("\\u%04x", (int) c));
                    } else {
                        sb.append(c);
                    }
                }
            }
        }
        sb.append('"');
    }

    private Map<String, Object> success(String reqId, int revision,
                                       List<Map<String, Object>> events) {
        Map<String, Object> m = new LinkedHashMap<>();
        m.put("id", reqId);
        m.put("ok", true);
        m.put("revision", revision);
        m.put("events", events);
        m.put("state", session.snapshot());
        m.put("legal", session.currentLegal());
        return m;
    }

    private static Map<String, Object> error(String reqId, int revision,
                                            String code, String message) {
        Map<String, Object> m = new LinkedHashMap<>();
        m.put("id", reqId);
        m.put("ok", false);
        m.put("revision", revision);
        m.put("error_code", code);
        m.put("error", message);
        return m;
    }

    private final class Session {
        private final GameState state;
        private final CommandProcessor commands;
        private final GameEngine engine;
        private final ActionHints hints;
        private final BotPlayer[] bots;
        private final int humanPlayer;
        private final String[] factions = new String[2];
        private final Adapter adapter;
        private int revision;
        private int decisions;
        private Map<String, String> actionCommands = new HashMap<>();
        private List<Map<String, Object>> legalCache = new ArrayList<>();

        Session(long seed, int humanPlayer, String humanFaction,
                String botFaction, BotDifficulty difficulty) {
            this.humanPlayer = humanPlayer;
            int botPlayer = 1 - humanPlayer;
            factions[humanPlayer] = humanFaction;
            factions[botPlayer] = botFaction;

            PrototypeCardPool pool = new PrototypeCardPool();
            FactionDecks decks = new FactionDecks(pool);
            CapitalRoster capitals = new CapitalRoster();
            List<CardDefinition> cards0 = decks.starter(factions[0]);
            List<CardDefinition> cards1 = decks.starter(factions[1]);
            DeckBuild build0 = new DeckBuild(factions[0] + " starter",
                    factions[0], null,
                    capitals.defaultForDeck(cards0).orElseThrow(), cards0);
            DeckBuild build1 = new DeckBuild(factions[1] + " starter",
                    factions[1], null,
                    capitals.defaultForDeck(cards1).orElseThrow(), cards1);
            this.state = new DemoMatchFactory().create(seed, build0, build1,
                    new BoardPosition(1, 0), new BoardPosition(2, 5),
                    BoardGeometry.HEX);
            this.commands = new CommandProcessor(state);
            this.engine = new GameEngine();
            this.hints = new ActionHints();
            Random botRng0 = new Random(seed ^ 0xC0FFEE1L);
            Random botRng1 = new Random(seed ^ 0xC0FFEE2L);
            this.bots = new BotPlayer[]{
                    new BotPlayer(difficulty, botRng0),
                    new BotPlayer(difficulty, botRng1),
            };
            this.adapter = new Adapter(state);
            this.revision = 0;
            this.decisions = 0;
            refreshLegal();
        }

        boolean isGameOver() {
            return state.phase() == Phase.GAME_OVER;
        }

        boolean isHumanTurn() {
            return !isGameOver() && state.activePlayer() == humanPlayer;
        }

        void refreshLegal() {
            actionCommands = new HashMap<>();
            legalCache = new ArrayList<>();
            if (!isHumanTurn()) {
                return;
            }
            List<String> forms = hints.forActivePlayer(state, engine);
            for (int i = 0; i < forms.size(); i++) {
                Map<String, Object> action = describeAction(forms.get(i));
                if (action == null) {
                    continue;
                }
                String id = "r" + revision + "-a" + i;
                action.put("id", id);
                action.put("command", forms.get(i));
                actionCommands.put(id, forms.get(i));
                legalCache.add(action);
            }
        }

        List<Map<String, Object>> currentLegal() {
            return legalCache;
        }

        ActResolution resolveAction(String actionId) {
            if (isGameOver()) {
                return ActResolution.invalid("match is over");
            }
            if (!isHumanTurn()) {
                return ActResolution.invalid("not the human turn");
            }
            String command = actionCommands.get(actionId);
            if (command == null) {
                return ActResolution.invalid(
                        "unknown or stale action id: " + actionId
                                + " (revision " + revision + ")");
            }
            return ActResolution.valid(command);
        }

        String executeHuman(String command,
                           List<Map<String, Object>> events) {
            int p = state.activePlayer();
            adapter.snapshotDamage();
            String result = commands.execute(command);
            adapter.drainInto(events);
            if (!command.equals("end") && !isGameOver()
                    && state.activePlayer() == p && p == humanPlayer) {
                adapter.snapshotDamage();
                BotPlayer.Decision r =
                        bots[1 - p].react(state, commands, 1 - p);
                if (r != null) {
                    adapter.drainInto(events);
                }
            }
            return result;
        }

        void runBotTurns(List<Map<String, Object>> events) {
            while (!isGameOver() && !isHumanTurn()) {
                if (state.turnNumber() > MAX_TURNS
                        || ++decisions > MAX_DECISIONS) {
                    throw new IllegalStateException(
                            "match did not finish within limits");
                }
                int p = state.activePlayer();
                adapter.snapshotDamage();
                BotPlayer.Decision d =
                        bots[p].takeNextAction(state, commands, p);
                adapter.drainInto(events);
                if (!d.command().equals("end") && !isGameOver()
                        && state.activePlayer() == p) {
                    int opp = 1 - p;
                    if (opp != humanPlayer) {
                        adapter.snapshotDamage();
                        BotPlayer.Decision r =
                                bots[opp].react(state, commands, opp);
                        if (r != null) {
                            adapter.drainInto(events);
                        }
                    }
                }
            }
        }

        private Map<String, Object> describeAction(String form) {
            String[] parts = form.split("\\s+");
            if (parts.length == 0) {
                return null;
            }
            Map<String, Object> action = new LinkedHashMap<>();
            int p = state.activePlayer();
            try {
                switch (parts[0]) {
                    case "play", "burrow" -> {
                        int handIndex = Integer.parseInt(parts[1]);
                        UUID instanceId = state.player(p).hand().get(handIndex);
                        CardInstance card = state.card(instanceId).orElseThrow();
                        action.put("type", parts[0]);
                        action.put("hand_index", handIndex);
                        action.put("card_id", card.definition().id());
                        action.put("card_name", card.definition().name());
                        action.put("instance_id", instanceId.toString());
                        action.put("to", hex(Integer.parseInt(parts[2]),
                                Integer.parseInt(parts[3])));
                    }
                    case "move", "blink", "attack" -> {
                        BoardPosition from = new BoardPosition(
                                Integer.parseInt(parts[1]),
                                Integer.parseInt(parts[2]));
                        BoardPosition to = new BoardPosition(
                                Integer.parseInt(parts[3]),
                                Integer.parseInt(parts[4]));
                        UUID sourceId = state.board().topAt(from).orElseThrow();
                        CardInstance source =
                                state.card(sourceId).orElseThrow();
                        action.put("type", parts[0]);
                        action.put("from", hex(from.x(), from.y()));
                        action.put("to", hex(to.x(), to.y()));
                        action.put("instance_id", sourceId.toString());
                        action.put("card_id", source.definition().id());
                        if (parts[0].equals("attack")) {
                            UUID targetId =
                                    state.board().topAt(to).orElseThrow();
                            CardInstance target =
                                    state.card(targetId).orElseThrow();
                            action.put("target_instance_id",
                                    targetId.toString());
                            action.put("target_card_id",
                                    target.definition().id());
                        }
                    }
                    case "activate" -> {
                        BoardPosition at = new BoardPosition(
                                Integer.parseInt(parts[1]),
                                Integer.parseInt(parts[2]));
                        UUID sourceId = state.board().topAt(at).orElseThrow();
                        CardInstance source =
                                state.card(sourceId).orElseThrow();
                        action.put("type", "activate");
                        action.put("at", hex(at.x(), at.y()));
                        action.put("instance_id", sourceId.toString());
                        action.put("card_id", source.definition().id());
                    }
                    case "cast" -> {
                        int handIndex = Integer.parseInt(parts[1]);
                        UUID instanceId =
                                state.player(p).hand().get(handIndex);
                        CardInstance card =
                                state.card(instanceId).orElseThrow();
                        BoardPosition target = new BoardPosition(
                                Integer.parseInt(parts[2]),
                                Integer.parseInt(parts[3]));
                        UUID targetId =
                                state.board().topAt(target).orElseThrow();
                        action.put("type", "cast");
                        action.put("hand_index", handIndex);
                        action.put("card_id", card.definition().id());
                        action.put("card_name", card.definition().name());
                        action.put("instance_id", instanceId.toString());
                        action.put("target", hex(target.x(), target.y()));
                        action.put("target_instance_id",
                                targetId.toString());
                        if (parts.length == 6) {
                            action.put("destination",
                                    hex(Integer.parseInt(parts[4]),
                                            Integer.parseInt(parts[5])));
                        }
                    }
                    case "end" -> action.put("type", "end_turn");
                    default -> {
                        return null;
                    }
                }
            } catch (RuntimeException e) {
                return null;
            }
            return action;
        }

        private static Map<String, Object> hex(int x, int y) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("x", x);
            m.put("y", y);
            return m;
        }

        Map<String, Object> snapshot() {
            Map<String, Object> snap = new LinkedHashMap<>();
            snap.put("seed", state.seed());
            snap.put("turn", Math.max(1, state.turnNumber()));
            snap.put("phase", state.phase().name());
            snap.put("active_player", state.activePlayer());
            snap.put("winner", state.winner().isPresent()
                    ? state.winner().getAsInt() : null);
            snap.put("you", humanPlayer);
            snap.put("revision", revision);
            List<Map<String, Object>> players = new ArrayList<>();
            for (int p = 0; p < 2; p++) {
                Map<String, Object> pl = new LinkedHashMap<>();
                pl.put("seat", p);
                pl.put("faction", factions[p]);
                pl.put("controller", p == humanPlayer ? "human" : "bot");
                pl.put("gp", state.player(p).currentGp());
                List<UUID> handIds = state.player(p).hand();
                pl.put("hand_count", handIds.size());
                List<Map<String, Object>> hand = new ArrayList<>();
                if (p == humanPlayer) {
                    for (UUID id : handIds) {
                        CardInstance card = state.card(id).orElse(null);
                        if (card == null) {
                            continue;
                        }
                        Map<String, Object> c = new LinkedHashMap<>();
                        c.put("card_id", card.definition().id());
                        c.put("instance_id", id.toString());
                        hand.add(c);
                    }
                }
                pl.put("hand", hand);
                pl.put("deck_count", state.player(p).deck().size());
                pl.put("discard_count", state.player(p).discard().size());
                players.add(pl);
            }
            snap.put("players", players);
            List<Map<String, Object>> board = new ArrayList<>();
            for (BoardPosition pos : state.board().positions()) {
                List<UUID> stack = state.board().stackAt(pos);
                if (stack.isEmpty()) {
                    continue;
                }
                List<Map<String, Object>> cards = new ArrayList<>();
                for (UUID id : stack) {
                    CardInstance card = state.card(id).orElse(null);
                    if (card == null) {
                        continue;
                    }
                    Map<String, Object> c = new LinkedHashMap<>();
                    c.put("card_id", card.definition().id());
                    c.put("instance_id", id.toString());
                    c.put("owner", card.owner());
                    cards.add(c);
                }
                Map<String, Object> cell = new LinkedHashMap<>();
                cell.put("x", pos.x());
                cell.put("y", pos.y());
                cell.put("stack", cards);
                board.add(cell);
            }
            snap.put("board", board);
            return snap;
        }

        /**
         * AI-097: canonical serialization of the full <em>unredacted</em>
         * game state for the relay lockstep hash exchange. Both players'
         * hands, decks and discard piles carry full card identities and
         * mutable per-card state; board stacks are ordered bottom-to-top;
         * cells are sorted by (x, y). Deterministic for a given engine
         * state — same seed + same intents ⇒ byte-identical output.
         */
        String canonicalState() {
            Map<String, Object> root = new LinkedHashMap<>();
            root.put("seed", state.seed());
            root.put("turn", Math.max(1, state.turnNumber()));
            root.put("phase", state.phase().name());
            root.put("active_player", state.activePlayer());
            root.put("winner", state.winner().isPresent()
                    ? state.winner().getAsInt() : null);
            List<Object> players = new ArrayList<>();
            for (int p = 0; p < 2; p++) {
                Map<String, Object> pl = new LinkedHashMap<>();
                pl.put("seat", p);
                pl.put("faction", factions[p]);
                pl.put("gp", state.player(p).currentGp());
                pl.put("max_gp", state.player(p).maximumGp());
                pl.put("hand", encodeCards(state.player(p).hand()));
                pl.put("deck", encodeCards(state.player(p).deck()));
                pl.put("discard", encodeCards(state.player(p).discard()));
                players.add(pl);
            }
            root.put("players", players);
            List<Object> board = new ArrayList<>();
            List<BoardPosition> positions =
                    new ArrayList<>(state.board().positions());
            positions.sort(Comparator.comparingInt(BoardPosition::x)
                    .thenComparingInt(BoardPosition::y));
            for (BoardPosition pos : positions) {
                List<UUID> stack = state.board().stackAt(pos);
                if (stack.isEmpty()) {
                    continue;
                }
                Map<String, Object> cell = new LinkedHashMap<>();
                cell.put("x", pos.x());
                cell.put("y", pos.y());
                cell.put("stack", encodeCards(stack));
                board.add(cell);
            }
            root.put("board", board);
            return RulesBridge.canonicalJson(root);
        }

        private List<Object> encodeCards(List<UUID> ids) {
            List<Object> out = new ArrayList<>(ids.size());
            for (UUID id : ids) {
                out.add(encodeCard(id));
            }
            return out;
        }

        private Map<String, Object> encodeCard(UUID id) {
            Map<String, Object> c = new LinkedHashMap<>();
            c.put("instance_id", id.toString());
            CardInstance card = state.card(id).orElse(null);
            if (card == null) {
                c.put("missing", true);
                return c;
            }
            c.put("card_id", card.definition().id());
            c.put("owner", card.owner());
            c.put("zone", card.zone().name());
            c.put("damage", card.damage());
            c.put("combat_damage", card.combatDamage());
            c.put("tapped", card.tapped());
            c.put("movement_spent", card.movementSpent());
            c.put("attacked", card.attackedThisTurn());
            c.put("blink_used", card.blinkUsedThisTurn());
            c.put("attack_bonus", card.attackBonus());
            c.put("defense_bonus", card.defenseBonus());
            c.put("ability_used", card.abilityUsedThisTurn());
            return c;
        }

        record ActResolution(boolean valid, String command, String error) {
            static ActResolution valid(String command) {
                return new ActResolution(true, command, null);
            }

            static ActResolution invalid(String error) {
                return new ActResolution(false, null, error);
            }
        }
    }

    private static final class Adapter {
        private final GameState state;
        private final List<Map<String, Object>> out = new ArrayList<>();
        private int consumed = 0;
        private final Map<UUID, int[]> damageBefore = new HashMap<>();
        private final Map<UUID, BoardPosition> lastPos = new HashMap<>();
        private final Map<UUID, String> cardIdCache = new HashMap<>();

        private static final Pattern MOVED =
                Pattern.compile("^([0-9a-fA-F-]{36}) BoardPosition\\[x=(\\d+), ?y=(\\d+)\\] -> BoardPosition\\[x=(\\d+), ?y=(\\d+)\\] cost (\\d+)$");
        private static final Pattern UUID_PAIR =
                Pattern.compile("^([0-9a-fA-F-]{36}) -> ([0-9a-fA-F-]{36})( at (\\d+),(\\d+))?$");
        private static final Pattern LEADING_INT = Pattern.compile("^(\\d+)\\b");
        private static final Pattern SINGLE_UUID = Pattern.compile("^([0-9a-fA-F-]{36})$");
        private static final Pattern ABILITY =
                Pattern.compile("^([0-9a-fA-F-]{36}) \\S+ \\S+ (\\d+)");
        private static final Pattern TERRAIN =
                Pattern.compile("^([0-9a-fA-F-]{36}) ([0-9a-fA-F-]{36}) \\S+ (\\d+)( (\\d+),(\\d+))?$");

        Adapter(GameState state) {
            this.state = state;
            for (int p = 0; p < 2; p++) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    state.board().positionOf(c.instanceId())
                            .ifPresent(pos -> lastPos.put(c.instanceId(), pos));
                }
            }
        }

        void snapshotDamage() {
            damageBefore.clear();
            for (int p = 0; p < 2; p++) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    damageBefore.put(c.instanceId(),
                            new int[]{c.damage(), c.combatDamage()});
                }
            }
        }

        void drainInto(List<Map<String, Object>> sink) {
            int mark = out.size();
            drain();
            for (int i = mark; i < out.size(); i++) {
                sink.add(out.get(i));
            }
            renumber(sink);
        }

        private void renumber(List<Map<String, Object>> sink) {
            for (int i = 0; i < sink.size(); i++) {
                sink.get(i).put("seq", i);
            }
        }

        private void drain() {
            List<GameEvent> events = state.events();
            int gameOverIdx = -1;
            boolean damageTrigger = false;
            boolean spellPlayed = false;
            while (consumed < events.size()) {
                GameEvent e = events.get(consumed++);
                Map<String, Object> wire = adapt(e);
                if (wire != null) {
                    if (e.type() == GameEvent.Type.GAME_OVER) {
                        gameOverIdx = out.size();
                    }
                    out.add(wire);
                }
                switch (e.type()) {
                    case ATTACK_RESOLVED, OPPORTUNITY_ATTACK, TERRAIN_TRIGGERED ->
                            damageTrigger = true;
                    default -> {
                    }
                }
                if (e.type() == GameEvent.Type.CARD_PLAYED && isSpell(e)) {
                    spellPlayed = true;
                }
            }
            if (damageTrigger || spellPlayed) {
                deriveDamage(gameOverIdx);
            }
        }

        private boolean isSpell(GameEvent e) {
            Matcher m = SINGLE_UUID.matcher(e.detail().trim());
            if (!m.matches()) {
                return false;
            }
            return cardOf(m.group(1))
                    .map(c -> c.definition().type() == CardType.SPELL)
                    .orElse(false);
        }

        private void deriveDamage(int gameOverIdx) {
            List<Map<String, Object>> synth = new ArrayList<>();
            java.util.Set<UUID> seen = new java.util.HashSet<>();
            for (int p = 0; p < 2; p++) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    seen.add(c.instanceId());
                    syntheticFor(c, synth);
                }
            }
            for (UUID id : damageBefore.keySet()) {
                if (!seen.contains(id)) {
                    cardOf(id.toString()).ifPresent(c -> syntheticFor(c, synth));
                }
            }
            if (gameOverIdx >= 0) {
                out.addAll(gameOverIdx, synth);
            } else {
                out.addAll(synth);
            }
        }

        private void syntheticFor(CardInstance c,
                                 List<Map<String, Object>> synth) {
            int[] before = damageBefore.get(c.instanceId());
            int beforeTotal = before == null ? 0 : before[0] + before[1];
            int delta = (c.damage() + c.combatDamage()) - beforeTotal;
            if (delta > 0) {
                Map<String, Object> wire = base(c.owner());
                boolean capital = c.definition().type() == CardType.CAPITAL;
                wire.put("event", capital ? "CAPITAL_HIT" : "DAMAGE_DEALT");
                wire.put("instance_id", c.instanceId().toString());
                wire.put("amount", delta);
                wire.put("detail", delta + (capital ? " capital damage to "
                        : " damage to ") + c.definition().id());
                wire.put("turn", Math.max(1, state.turnNumber()));
                synth.add(wire);
            }
        }

        private Map<String, Object> base(int player) {
            Map<String, Object> wire = new LinkedHashMap<>();
            wire.put("turn", Math.max(1, state.turnNumber()));
            wire.put("player", player);
            return wire;
        }

        private Optional<CardInstance> cardOf(String uuid) {
            try {
                UUID id = UUID.fromString(uuid);
                Optional<CardInstance> card = state.card(id);
                card.ifPresent(c ->
                        cardIdCache.putIfAbsent(id, c.definition().id()));
                return card;
            } catch (IllegalArgumentException e) {
                return Optional.empty();
            }
        }

        private String cardId(UUID id) {
            return cardIdCache.computeIfAbsent(id, u -> state.card(u)
                    .map(c -> c.definition().id()).orElse("unknown:" + u));
        }

        private Map<String, Object> adapt(GameEvent e) {
            Map<String, Object> wire = base(e.playerId());
            String detail = e.detail();
            Matcher m;
            switch (e.type()) {
                case MATCH_STARTED -> {
                    wire.put("event", "MATCH_STARTED");
                    wire.put("detail", detail);
                }
                case TURN_STARTED -> {
                    wire.put("event", "TURN_STARTED");
                    wire.put("detail", detail);
                }
                case TURN_ENDED -> {
                    wire.put("event", "TURN_ENDED");
                    wire.put("detail", detail);
                }
                case PHASE_CHANGED -> {
                    wire.put("event", "PHASE_CHANGED");
                    wire.put("detail", detail);
                }
                case CARD_DRAWN -> {
                    m = SINGLE_UUID.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "CARD_DRAWN");
                    wire.put("instance_id", m.group(1));
                    wire.put("detail", detail);
                }
                case CARD_PLAYED -> {
                    m = SINGLE_UUID.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "CARD_PLAYED");
                    wire.put("instance_id", m.group(1));
                    cardOf(m.group(1)).ifPresent(c -> {
                        wire.put("card_id", c.definition().id());
                        state.board().positionOf(c.instanceId()).ifPresent(pos -> {
                            wire.put("to", Map.of("x", pos.x(), "y", pos.y()));
                            lastPos.put(c.instanceId(), pos);
                        });
                    });
                    if (!wire.containsKey("to")) {
                        capitalHex(e.playerId()).ifPresent(pos ->
                                wire.put("to", Map.of("x", pos.x(), "y", pos.y())));
                    }
                    wire.put("detail", detail);
                }
                case CHARACTER_MOVED -> {
                    m = MOVED.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "CHARACTER_MOVED");
                    wire.put("instance_id", m.group(1));
                    wire.put("from", Map.of("x", Integer.parseInt(m.group(2)),
                            "y", Integer.parseInt(m.group(3))));
                    wire.put("to", Map.of("x", Integer.parseInt(m.group(4)),
                            "y", Integer.parseInt(m.group(5))));
                    wire.put("amount", Integer.parseInt(m.group(6)));
                    lastPos.put(UUID.fromString(m.group(1)), new BoardPosition(
                            Integer.parseInt(m.group(4)),
                            Integer.parseInt(m.group(5))));
                    wire.put("detail", detail);
                }
                case ATTACK_RESOLVED, OPPORTUNITY_ATTACK -> {
                    m = UUID_PAIR.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", e.type() == GameEvent.Type.ATTACK_RESOLVED
                            ? "ATTACK_RESOLVED" : "OPPORTUNITY_ATTACK");
                    wire.put("instance_id", m.group(1));
                    wire.put("target_instance_id", m.group(2));
                    UUID targetId = UUID.fromString(m.group(2));
                    Optional<BoardPosition> targetPos = cardOf(m.group(2))
                            .flatMap(c -> state.board().positionOf(c.instanceId()));
                    if (targetPos.isEmpty() && lastPos.containsKey(targetId)) {
                        targetPos = Optional.of(lastPos.get(targetId));
                    }
                    targetPos.ifPresent(pos -> wire.put("to",
                            Map.of("x", pos.x(), "y", pos.y())));
                    wire.put("detail", detail);
                }
                case CARD_DESTROYED -> {
                    m = SINGLE_UUID.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "CARD_DESTROYED");
                    wire.put("instance_id", m.group(1));
                    wire.put("card_id",
                            cardId(UUID.fromString(m.group(1))));
                    wire.put("detail", detail);
                }
                case GP_GENERATED, GP_SPENT -> {
                    m = LEADING_INT.matcher(detail.trim());
                    wire.put("event", e.type() == GameEvent.Type.GP_GENERATED
                            ? "GP_GENERATED" : "GP_SPENT");
                    if (m.find()) {
                        wire.put("amount", Integer.parseInt(m.group(1)));
                    }
                    wire.put("detail", detail);
                }
                case CARD_ABILITY_TRIGGERED -> {
                    m = ABILITY.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "CARD_ABILITY_TRIGGERED");
                    wire.put("instance_id", m.group(1));
                    wire.put("amount", Integer.parseInt(m.group(2)));
                    wire.put("detail", detail);
                }
                case TERRAIN_TRIGGERED -> {
                    m = TERRAIN.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "TERRAIN_TRIGGERED");
                    wire.put("instance_id", m.group(1));
                    wire.put("target_instance_id", m.group(2));
                    wire.put("amount", Integer.parseInt(m.group(3)));
                    wire.put("detail", detail);
                }
                case EXHAUSTION_DAMAGE -> {
                    m = SINGLE_UUID.matcher(detail.trim());
                    if (!m.matches()) {
                        return null;
                    }
                    wire.put("event", "EXHAUSTION_DAMAGE");
                    wire.put("instance_id", m.group(1));
                    wire.put("amount", 1);
                    wire.put("detail", detail);
                }
                case GAME_OVER -> {
                    wire.put("event", "GAME_OVER");
                    wire.put("detail", detail);
                }
                default -> {
                    return null;
                }
            }
            return wire;
        }

        private Optional<BoardPosition> capitalHex(int player) {
            for (CardInstance c : state.battlefieldCards(player)) {
                if (c.definition().type() == CardType.CAPITAL) {
                    Optional<BoardPosition> pos =
                            state.board().positionOf(c.instanceId());
                    if (pos.isPresent()) {
                        return pos;
                    }
                }
            }
            return Optional.empty();
        }
    }
}
