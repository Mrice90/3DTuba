/**
 * referee-engine.js — turn an engine (engine/README.md) into the `replay`
 * function referee-runner.js expects (AI-010).
 *
 * The replay starts the engine from the transcript's seed and applies every
 * relay intent in sequence order as the seat that sent it. The engine is the
 * judge: an intent it refuses was illegal, and only a modified client sends
 * one, so that seat loses. Intents after the game is over change nothing and
 * are ignored.
 *
 * A game the engine finished is decided by the engine, however the relay
 * session ended (including a turn timeout after game over, which is what a
 * client that never sends `result` produces). A game that is not over is
 * decided by a relay turn timeout (the timed-out seat loses) or a concession:
 * a seat whose result claim names its opponent as the winner has conceded (a
 * claim against its own interest). Otherwise the verdict is `unfinished` and
 * the ledger voids the match without rating it.
 */

export function replayWithEngine(makeEngine, { engineName = "engine" } = {}) {
    return async function replay({ seed, seats, intents, transcript = {} }) {
        const engine = await makeEngine();
        try {
            await engine.start({ seed, seats });
            const ordered = [...intents].sort((a, b) => a.seq - b.seq);
            let applied = 0;
            let ignored = 0;
            for (const it of ordered) {
                const st = await engine.status();
                if (st.over) { ignored++; continue; }
                const player = seats.indexOf(it.seat);
                if (player < 0) return { legal: false, offendingSeat: it.seat, failedSeq: it.seq, engine: engineName };
                const r = await engine.act(player, it.actionId);
                if (!r.ok) return { legal: false, offendingSeat: it.seat, failedSeq: it.seq, reason: String(r.error || "").slice(0, 200), engine: engineName };
                applied++;
            }
            const st = await engine.status();
            const finalStateHash = await engine.hash();
            // The native op:hash leaves out the revision, so the verdict carries it too.
            const base = {
                legal: true, finalStateHash, intentsApplied: applied, intentsIgnored: ignored, engine: engineName,
                ...(st.revision != null ? { finalRevision: st.revision } : {}),
                ...(st.rulesVersion ? { rulesVersion: st.rulesVersion } : {}),
            };
            if (st.over && st.winnerSeat != null) return { ...base, winnerSeat: seats[st.winnerSeat] };
            if (st.over) return { ...base, unfinished: true, draw: true };
            const conceders = Object.entries(transcript.results || {})
                .filter(([seat, claim]) => seats.includes(seat) && claim.winnerSeat !== seat)
                .map(([seat]) => seat);
            if (transcript.endReason === "timeout" && seats.includes(transcript.forfeitSeat)) {
                // The relay timed the seat out mid-game: abandoning is losing.
                return { ...base, winnerSeat: seats.find((s) => s !== transcript.forfeitSeat), forfeitedBy: transcript.forfeitSeat };
            }
            if (conceders.length === 1) {
                return { ...base, winnerSeat: seats.find((s) => s !== conceders[0]), concededBy: conceders[0] };
            }
            return { ...base, unfinished: true };
        } finally {
            await engine.close();
        }
    };
}
