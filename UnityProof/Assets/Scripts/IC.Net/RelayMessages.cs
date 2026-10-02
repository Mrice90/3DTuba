using System;
using System.Collections.Generic;

namespace IC.Net
{
    /// <summary>
    /// relay-1 message DTOs. See prototypes/lobby-lab/docs/relay-design.md.
    /// All parsing is total: malformed messages throw RelayProtocolException,
    /// never half-parsed state.
    /// </summary>

    public sealed class RelayProtocolException : Exception
    {
        public RelayProtocolException(string message) : base(message) { }
        public RelayProtocolException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class RelayWelcome
    {
        public string Seat { get; }
        public string RoomId { get; }
        public string Phase { get; }
        public long Turn { get; }
        public string? Seed { get; }
        public IReadOnlyList<RelayIntent> Log { get; }

        internal RelayWelcome(string seat, string roomId, string phase, long turn, string? seed, IReadOnlyList<RelayIntent> log)
        {
            Seat = seat; RoomId = roomId; Phase = phase; Turn = turn; Seed = seed; Log = log;
        }
    }

    public sealed class RelayIntent
    {
        public long Seq { get; }
        public string Seat { get; }
        public string ActionId { get; }

        internal RelayIntent(long seq, string seat, string actionId)
        {
            Seq = seq; Seat = seat; ActionId = actionId;
        }
    }

    public sealed class RelayHashMismatch
    {
        public long Turn { get; }
        public IReadOnlyDictionary<string, string> Hashes { get; }

        internal RelayHashMismatch(long turn, IReadOnlyDictionary<string, string> hashes)
        {
            Turn = turn; Hashes = hashes;
        }
    }

    public sealed class RelayForfeit
    {
        public string? Seat { get; }
        public string Reason { get; }

        internal RelayForfeit(string? seat, string reason)
        {
            Seat = seat; Reason = reason;
        }
    }

    public sealed class RelayTimer
    {
        public string Seat { get; }
        public long MsRemaining { get; }

        internal RelayTimer(string seat, long msRemaining)
        {
            Seat = seat; MsRemaining = msRemaining;
        }
    }

    /// <summary>Parses inbound relay-1 server messages into DTOs.</summary>
    internal static class RelayMessages
    {
        public static string MessageType(Dictionary<string, object?> msg)
        {
            return JsonReq.Str(msg, "type");
        }

        public static RelayWelcome ParseWelcome(Dictionary<string, object?> msg)
        {
            var log = new List<RelayIntent>();
            if (msg.TryGetValue("log", out var lv) && lv is List<object?> arr)
            {
                foreach (var e in arr)
                {
                    if (e is Dictionary<string, object?> eo)
                        log.Add(ParseIntent(eo));
                }
            }
            return new RelayWelcome(
                JsonReq.Str(msg, "seat"),
                JsonReq.Str(msg, "roomId"),
                JsonReq.Str(msg, "phase"),
                JsonReq.OptLong(msg, "turn"),
                JsonReq.OptStr(msg, "seed"),
                log);
        }

        public static RelayIntent ParseIntent(Dictionary<string, object?> msg)
        {
            return new RelayIntent(
                JsonReq.Long(msg, "seq"),
                JsonReq.Str(msg, "seat"),
                JsonReq.Str(msg, "actionId"));
        }

        public static RelayHashMismatch ParseHashMismatch(Dictionary<string, object?> msg)
        {
            var hashes = new Dictionary<string, string>();
            if (msg.TryGetValue("hashes", out var hv) && hv is Dictionary<string, object?> ho)
            {
                foreach (var kv in ho)
                    if (kv.Value is string s) hashes[kv.Key] = s;
            }
            return new RelayHashMismatch(JsonReq.Long(msg, "turn"), hashes);
        }

        public static RelayForfeit ParseForfeit(Dictionary<string, object?> msg)
        {
            return new RelayForfeit(JsonReq.OptStr(msg, "seat"), JsonReq.Str(msg, "reason"));
        }

        public static RelayTimer ParseTimer(Dictionary<string, object?> msg)
        {
            return new RelayTimer(JsonReq.Str(msg, "seat"), JsonReq.Long(msg, "msRemaining"));
        }

        // --- outbound builders ---

        public static string Hello(string seat, long lastSeq) =>
            JsonLite.Stringify(JsonLite.Obj(("type", "hello"), ("seat", seat), ("lastSeq", lastSeq)));

        public static string SeedCommit(string commitHex) =>
            JsonLite.Stringify(JsonLite.Obj(("type", "seed-commit"), ("commit", commitHex)));

        public static string SeedReveal(string seed, string salt) =>
            JsonLite.Stringify(JsonLite.Obj(("type", "seed-reveal"), ("seed", seed), ("salt", salt)));

        public static string Intent(string actionId) =>
            JsonLite.Stringify(JsonLite.Obj(("type", "intent"), ("actionId", actionId)));

        public static string Turn(long turn, string activeSeat) =>
            JsonLite.Stringify(JsonLite.Obj(("type", "turn"), ("turn", turn), ("activeSeat", activeSeat)));

        public static string Hash(long turn, string stateHash) =>
            JsonLite.Stringify(JsonLite.Obj(("type", "hash"), ("turn", turn), ("hash", stateHash)));
    }
}
