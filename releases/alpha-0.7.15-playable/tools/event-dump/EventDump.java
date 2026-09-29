import com.fasterxml.jackson.databind.ObjectMapper;
import com.infiniteconquest.cli.BotPlayer;
import com.infiniteconquest.cli.BotDifficulty;
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
import com.infiniteconquest.core.GameEvent;
import com.infiniteconquest.core.GameState;
import com.infiniteconquest.core.Phase;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.UUID;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * AI-066: headless seeded Zeus-vs-Poseidon AI match runner that dumps the
 * real engine event stream as JSONL in the AI-062 board-events wire format
 * (docs/muse/sprint-02/board-events/event-schema.json).
 *
 * <p>Usage: {@code EventDump <seed> <out.jsonl> [--mode base|swap|mirror]}
 *
 * <p>Modes (AI-072): {@code base} (default) seats Zeus at player 0 and
 * Poseidon at player 1; {@code swap} reverses the seats (Poseidon at 0,
 * Zeus at 1), which separates "which deck" from "which seat goes first";
 * {@code mirror} seats the Zeus starter at both players, a control for
 * hidden seat bias. All three run the same engine, bots and rules —
 * only the harness deck assignment changes. Same seed and mode →
 * byte-identical dump.
 *
 * <p>Compiled against the pinned alpha fat JAR
 * (releases/alpha-0.7.15-playable/infinite-conquest-alpha-0.7.15.jar) and run
 * with it on the classpath. Both players are HERO bots with seeded RNGs, so
 * a given seed reproduces the same match and the same dump byte-for-byte.
 *
 * <p>Adaptation notes (see board-events.md):
 * <ul>
 *   <li>Unstructured Java detail strings are parsed into {@code card_id},
 *       {@code from}/{@code to} hexes and {@code amount} (see PATTERNS).
 *   <li>{@code seq} is renumbered 0..N-1 over the final transcript (the
 *       validator requires seq == 0-based index).
 *   <li>Synthetic {@code DAMAGE_DEALT}/{@code CAPITAL_HIT} events are derived
 *       per board-events.md §22: after an {@code ATTACK_RESOLVED},
 *       {@code OPPORTUNITY_ATTACK}, {@code TERRAIN_TRIGGERED}, or spell
 *       {@code CARD_PLAYED}, the target's {@code damage+combatDamage} delta
 *       is emitted. Exhaustion damage is <em>not</em> diffed — it already has
 *       its own {@code EXHAUSTION_DAMAGE} event with amount 1
 *       (GameState.java:304).
 *   <li>Spells carry no board position in the Java event; the adapter uses
 *       the casting player's capital hex as {@code to} (documented choice —
 *       the schema requires {@code to} on {@code CARD_PLAYED}).
 *   <li>Setup events (initial hand draws) fire with the engine's turnNumber 0;
 *       the adapter clamps the wire {@code turn} to a minimum of 1.
 * </ul>
 */
public final class EventDump {
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

    private static final int MAX_TURNS = 300;
    private static final int MAX_DECISIONS = 200_000;

    public static void main(String[] args) throws Exception {
        if (args.length < 2) {
            System.err.println("usage: EventDump <seed> <out.jsonl> [--mode base|swap|mirror]");
            System.exit(2);
        }
        long seed = Long.parseLong(args[0]);
        Path out = Path.of(args[1]);
        String mode = "base";
        for (int i = 2; i < args.length; i++) {
            if (args[i].startsWith("--mode=")) {
                mode = args[i].substring("--mode=".length());
            } else {
                System.err.println("unknown argument: " + args[i]);
                System.exit(2);
            }
        }

        PrototypeCardPool pool = new PrototypeCardPool();
        FactionDecks factions = new FactionDecks(pool);
        CapitalRoster capitals = new CapitalRoster();
        List<CardDefinition> zeus = factions.starter("ZEUS");
        List<CardDefinition> poseidon = factions.starter("POSEIDON");

        // AI-072: seat/deck arrangements. The engine, bots and rules are the
        // same in every mode — only the harness deck assignment changes.
        List<CardDefinition> deck0, deck1;
        String name0, name1, faction0, faction1;
        switch (mode) {
            case "base" -> {
                deck0 = zeus; name0 = "Zeus starter"; faction0 = "ZEUS";
                deck1 = poseidon; name1 = "Poseidon starter"; faction1 = "POSEIDON";
            }
            case "swap" -> {
                deck0 = poseidon; name0 = "Poseidon starter"; faction0 = "POSEIDON";
                deck1 = zeus; name1 = "Zeus starter"; faction1 = "ZEUS";
            }
            case "mirror" -> {
                deck0 = zeus; name0 = "Zeus starter"; faction0 = "ZEUS";
                deck1 = zeus; name1 = "Zeus starter"; faction1 = "ZEUS";
            }
            default -> {
                System.err.println("unknown mode: " + mode + " (want base|swap|mirror)");
                System.exit(2);
                return;
            }
        }
        DeckBuild build0 = new DeckBuild(name0, faction0, null,
                capitals.defaultForDeck(deck0).orElseThrow(), deck0);
        DeckBuild build1 = new DeckBuild(name1, faction1, null,
                capitals.defaultForDeck(deck1).orElseThrow(), deck1);

        GameState state = new DemoMatchFactory().create(seed, build0, build1,
                new BoardPosition(1, 0), new BoardPosition(2, 5), BoardGeometry.HEX);
        CommandProcessor commands = new CommandProcessor(state);
        BotPlayer[] bots = {
                new BotPlayer(BotDifficulty.HERO, new Random(seed ^ 0xC0FFEE1L)),
                new BotPlayer(BotDifficulty.HERO, new Random(seed ^ 0xC0FFEE2L)),
        };

        Adapter adapter = new Adapter(state);
        int decisions = 0;
        while (state.phase() != Phase.GAME_OVER) {
            if (state.turnNumber() > MAX_TURNS) {
                throw new IllegalStateException("match did not finish within " + MAX_TURNS + " turns");
            }
            if (++decisions > MAX_DECISIONS) {
                throw new IllegalStateException("match did not finish within " + MAX_DECISIONS + " decisions");
            }
            int p = state.activePlayer();
            adapter.snapshotDamage();
            BotPlayer.Decision d = bots[p].takeNextAction(state, commands, p);
            if (!d.result().startsWith("OK:") && !d.command().equals("end")) {
                System.err.println("warning: bot command not OK: " + d.command() + " -> " + d.result());
            }
            adapter.drain();
            // Opponent spell reaction, mirroring the CLI loop.
            if (!d.command().equals("end") && state.phase() != Phase.GAME_OVER && state.activePlayer() == p) {
                adapter.snapshotDamage();
                BotPlayer.Decision r = bots[1 - p].react(state, commands, 1 - p);
                if (r != null) {
                    adapter.drain();
                }
            }
        }
        adapter.drain();

        List<Map<String, Object>> transcript = adapter.transcript();
        ObjectMapper json = new ObjectMapper();
        StringBuilder sb = new StringBuilder();
        for (Map<String, Object> e : transcript) {
            sb.append(json.writeValueAsString(e)).append('\n');
        }
        Files.writeString(out, sb.toString());
        System.out.println("wrote " + transcript.size() + " events to " + out
                + " (mode: " + mode + ", winner: " + state.winner().stream().mapToObj(String::valueOf).findFirst().orElse("draw") + ")");
    }

    /** Adapts GameEvents to wire-format maps as the match runs. */
    private static final class Adapter {
        private final GameState state;
        private final List<Map<String, Object>> out = new ArrayList<>();
        private int consumed = 0;
        private final Map<UUID, int[]> damageBefore = new HashMap<>();
        private final Map<UUID, BoardPosition> lastPos = new HashMap<>();
        private final Map<UUID, String> cardIdCache = new HashMap<>();

        Adapter(GameState state) {
            this.state = state;
            // Seed last-known positions (capitals deploy with no CARD_PLAYED event).
            for (int p = 0; p < 2; p++) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    state.board().positionOf(c.instanceId()).ifPresent(pos -> lastPos.put(c.instanceId(), pos));
                }
            }
        }

        void snapshotDamage() {
            damageBefore.clear();
            for (int p = 0; p < 2; p++) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    damageBefore.put(c.instanceId(), new int[]{c.damage(), c.combatDamage()});
                }
            }
        }

        /** Adapt all new Java events since the last drain, plus synthetic damage. */
        void drain() {
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
                    case ATTACK_RESOLVED, OPPORTUNITY_ATTACK, TERRAIN_TRIGGERED -> damageTrigger = true;
                    default -> {}
                }
                if (e.type() == GameEvent.Type.CARD_PLAYED && isSpell(e)) {
                    spellPlayed = true;
                }
            }
            if (damageTrigger || spellPlayed) {
                deriveDamage(gameOverIdx);
            }
        }

        List<Map<String, Object>> transcript() {
            for (int i = 0; i < out.size(); i++) {
                out.get(i).put("seq", i);
            }
            return out;
        }

        private boolean isSpell(GameEvent e) {
            Matcher m = SINGLE_UUID.matcher(e.detail().trim());
            if (!m.matches()) {
                return false;
            }
            return cardOf(m.group(1)).map(c -> c.definition().type() == CardType.SPELL).orElse(false);
        }

        /** §22: diff damage+combatDamage per target; emit DAMAGE_DEALT / CAPITAL_HIT.
         *  Synthetics go before GAME_OVER when the same drain ended the match. */
        private void deriveDamage(int gameOverIdx) {
            List<Map<String, Object>> synth = new ArrayList<>();
            java.util.Set<UUID> seen = new java.util.HashSet<>();
            for (int p = 0; p < 2; p++) {
                for (CardInstance c : state.battlefieldCards(p)) {
                    seen.add(c.instanceId());
                    syntheticFor(c, synth);
                }
            }
            // Lethal damage: the target left the battlefield this decision but
            // is still registered (DISCARD) with its final damage totals.
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

        private void syntheticFor(CardInstance c, List<Map<String, Object>> synth) {
            int[] before = damageBefore.get(c.instanceId());
            int beforeTotal = before == null ? 0 : before[0] + before[1];
            int delta = (c.damage() + c.combatDamage()) - beforeTotal;
            if (delta > 0) {
                Map<String, Object> wire = base(c.owner());
                boolean capital = c.definition().type() == CardType.CAPITAL;
                wire.put("event", capital ? "CAPITAL_HIT" : "DAMAGE_DEALT");
                wire.put("instance_id", c.instanceId().toString());
                wire.put("amount", delta);
                wire.put("detail", delta + (capital ? " capital damage to " : " damage to ")
                        + c.definition().id());
                wire.put("turn", state.turnNumber());
                synth.add(wire);
            }
        }

        private Map<String, Object> base(int player) {
            Map<String, Object> wire = new LinkedHashMap<>();
            wire.put("turn", state.turnNumber());
            wire.put("player", player);
            return wire;
        }

        private java.util.Optional<CardInstance> cardOf(String uuid) {
            try {
                return state.card(UUID.fromString(uuid));
            } catch (IllegalArgumentException ex) {
                return java.util.Optional.empty();
            }
        }

        private String cardId(UUID id) {
            return cardIdCache.computeIfAbsent(id,
                    u -> state.card(u).map(c -> c.definition().id()).orElse("unknown:" + u));
        }

        private Map<String, Object> hex(String name, BoardPosition pos, Map<String, Object> wire) {
            Map<String, Object> h = new LinkedHashMap<>();
            h.put("x", pos.x());
            h.put("y", pos.y());
            wire.put(name, h);
            return wire;
        }

        /** Capital hex for a player — fallback {@code to} for spells. */
        private BoardPosition capitalPos(int player) {
            return state.battlefieldCards(player).stream()
                    .filter(c -> c.definition().type() == CardType.CAPITAL)
                    .findFirst()
                    .flatMap(c -> state.board().positionOf(c.instanceId()))
                    .orElseThrow(() -> new IllegalStateException("no capital on board for player " + player));
        }

        private Map<String, Object> adapt(GameEvent e) {
            Map<String, Object> wire = base(e.playerId());
            wire.put("event", e.type().name());
            // Setup events (initial draws) fire with turnNumber 0; the wire
            // format is 1-based, so clamp.
            wire.put("turn", Math.max(1, e.turnNumber()));
            wire.put("detail", e.detail());
            String detail = e.detail().trim();
            switch (e.type()) {
                case CARD_PLAYED -> {
                    Matcher m = SINGLE_UUID.matcher(detail);
                    if (!m.matches()) {
                        throw new IllegalStateException("unparseable CARD_PLAYED detail: " + detail);
                    }
                    UUID id = UUID.fromString(m.group(1));
                    CardInstance card = cardOf(m.group(1)).orElseThrow(
                            () -> new IllegalStateException("CARD_PLAYED unknown instance " + id));
                    wire.put("card_id", card.definition().id());
                    wire.put("instance_id", id.toString());
                    BoardPosition pos = state.board().positionOf(id).orElse(null);
                    if (pos != null) {
                        hex("to", pos, wire);
                        wire.put("stack_index", state.board().stackAt(pos).size());
                        lastPos.put(id, pos);
                    } else {
                        // Spells resolve to DISCARD with no board position; the
                        // schema requires `to`, so use the caster's capital hex.
                        hex("to", capitalPos(card.owner()), wire);
                    }
                }
                case CHARACTER_MOVED -> {
                    Matcher m = MOVED.matcher(detail);
                    if (!m.matches()) {
                        throw new IllegalStateException("unparseable CHARACTER_MOVED detail: " + detail);
                    }
                    UUID id = UUID.fromString(m.group(1));
                    wire.put("instance_id", id.toString());
                    wire.put("card_id", cardId(id));
                    hex("from", new BoardPosition(Integer.parseInt(m.group(2)), Integer.parseInt(m.group(3))), wire);
                    BoardPosition to = new BoardPosition(Integer.parseInt(m.group(4)), Integer.parseInt(m.group(5)));
                    hex("to", to, wire);
                    wire.put("amount", Integer.parseInt(m.group(6)));
                    lastPos.put(id, to);
                }
                case ATTACK_RESOLVED, OPPORTUNITY_ATTACK -> {
                    Matcher m = UUID_PAIR.matcher(detail);
                    if (!m.matches()) {
                        throw new IllegalStateException("unparseable attack detail: " + detail);
                    }
                    UUID attacker = UUID.fromString(m.group(1));
                    UUID target = UUID.fromString(m.group(2));
                    wire.put("instance_id", attacker.toString());
                    wire.put("card_id", cardId(attacker));
                    if (m.group(3) != null) {
                        hex("to", new BoardPosition(Integer.parseInt(m.group(4)), Integer.parseInt(m.group(5))), wire);
                    } else {
                        BoardPosition tp = lastPos.get(target);
                        if (tp == null) {
                            tp = state.board().positionOf(target).orElseThrow(
                                    () -> new IllegalStateException("no position for attack target " + target));
                        }
                        hex("to", tp, wire);
                    }
                }
                case CARD_DESTROYED -> {
                    Matcher m = SINGLE_UUID.matcher(detail);
                    if (!m.matches()) {
                        throw new IllegalStateException("unparseable CARD_DESTROYED detail: " + detail);
                    }
                    wire.put("instance_id", m.group(1));
                    wire.put("card_id", cardId(UUID.fromString(m.group(1))));
                }
                case CARD_DRAWN -> {
                    Matcher m = SINGLE_UUID.matcher(detail);
                    if (m.matches()) {
                        wire.put("instance_id", m.group(1));
                    }
                }
                case GP_GENERATED, GP_SPENT -> {
                    Matcher m = LEADING_INT.matcher(detail);
                    if (!m.find()) {
                        throw new IllegalStateException("unparseable GP detail: " + detail);
                    }
                    wire.put("amount", Integer.parseInt(m.group(1)));
                }
                case EXHAUSTION_DAMAGE -> {
                    Matcher m = SINGLE_UUID.matcher(detail);
                    if (!m.matches()) {
                        throw new IllegalStateException("unparseable EXHAUSTION_DAMAGE detail: " + detail);
                    }
                    wire.put("instance_id", m.group(1));
                    wire.put("amount", 1); // GameState.java:304 — addDamage(1)
                }
                case CARD_ABILITY_TRIGGERED -> {
                    Matcher m = ABILITY.matcher(detail);
                    if (m.find()) {
                        wire.put("instance_id", m.group(1));
                        wire.put("amount", Integer.parseInt(m.group(2)));
                    }
                }
                case TERRAIN_TRIGGERED -> {
                    Matcher m = TERRAIN.matcher(detail);
                    if (m.matches()) {
                        wire.put("instance_id", m.group(1));
                        wire.put("amount", Integer.parseInt(m.group(3)));
                        if (m.group(4) != null) {
                            hex("to", new BoardPosition(Integer.parseInt(m.group(5)), Integer.parseInt(m.group(6))), wire);
                        }
                    }
                }
                default -> {}
            }
            return wire;
        }
    }
}
