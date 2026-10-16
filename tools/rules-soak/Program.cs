// Rules-core soak + determinism harness (Claude, rules soak track).
// Drives the shipping NativeRulesSession JSON protocol headless, checks invariants after every
// step, and proves that the same seed + intents always land on the same state hash.
// It never changes rules: everything here is read-only against ChatGPT's engine.
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfiniteConquest.Native;
using InfiniteConquest.RulesCore.Bot;
using InfiniteConquest.RulesCore.Core;
using InfiniteConquest.RulesCore.Data;

static class Program
{
    static int Main(string[] args)
    {
        var o = Opts.Parse(args);
        try
        {
            return o.Cmd switch
            {
                "soak" => Soak.Run(o),
                "replay" => Replay.RunFile(o),
                "shrink" => Shrink.Run(o),
                "suite" => Suite.Run(o),
                "trace" => Trace.Run(o),
                _ => Usage(),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FATAL " + e);
            return 3;
        }
    }

    static int Usage()
    {
        Console.Error.WriteLine(@"rules-soak <cmd> [options]
  soak    --data DIR --mode mp|bots --seeds A-B [--out DIR] [--difficulty HERO] [--policy aggro|random]
          [--extra '{json merged into op:new}'] [--max-actions N] [--max-turns N]
  replay  --data DIR --case FILE          replay one case and check hash trail / expected failure
  shrink  --data DIR --case FILE [--out FILE]  minimise an mp failure case
  suite   --data DIR --dir DIR            replay every case in DIR (regression suite)");
        return 2;
    }
}

sealed class Opts
{
    public string Cmd = "";
    public Dictionary<string, string> Map = new();
    public static Opts Parse(string[] a)
    {
        var o = new Opts { Cmd = a.Length > 0 ? a[0] : "" };
        for (int i = 1; i < a.Length; i++)
            if (a[i].StartsWith("--")) o.Map[a[i][2..]] = (i + 1 < a.Length && !a[i + 1].StartsWith("--")) ? a[++i] : "true";
        return o;
    }
    public string S(string k, string d = null) => Map.TryGetValue(k, out var v) ? v : d;
    public int I(string k, int d) => Map.TryGetValue(k, out var v) ? int.Parse(v) : d;
    public (long, long) Range(string k, string d)
    {
        var s = S(k, d).Split('-');
        long a = long.Parse(s[0]);
        return (a, s.Length > 1 ? long.Parse(s[1]) : a);
    }
}

/// Wraps one NativeRulesSession; exposes the engine objects via reflection for invariant checks.
sealed class Engine
{
    static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public readonly NativeRulesSession Session;
    public JsonNode Last;
    int seq;

    public Engine(string dataDir) { Session = new NativeRulesSession(dataDir); }

    public JsonNode Call(JsonObject req)
    {
        req["id"] = "s" + (seq++);
        var line = req.ToJsonString();
        var resp = JsonNode.Parse(Session.Handle(line));
        return resp;
    }

    public JsonNode New(JsonObject req) { Last = Call(req); return Last; }
    public JsonNode Act(string actionId, int seat) { Last = Call(new JsonObject { ["op"] = "act", ["action_id"] = actionId, ["seat"] = seat }); return Last; }
    public string Hash() => (string)Call(new JsonObject { ["op"] = "hash" })["state_hash"];

    T Field<T>(string name) => (T)typeof(NativeRulesSession).GetField(name, F)?.GetValue(Session);
    public GameState State => Field<GameState>("state");
    public CommandProcessor Commands => Field<CommandProcessor>("commands");
    public PlaytestBot[] Bots => Field<PlaytestBot[]>("bots");
    public bool Over => State.Phase() == Phase.GAME_OVER;
}

record Violation(string Kind, string Detail);

static class Invariants
{
    /// Checks state-level invariants that must hold after every accepted step.
    public static List<Violation> Check(GameState s, int baselineCards, JsonNode snapshot)
    {
        var v = new List<Violation>();
        var seen = new Dictionary<Guid, string>();
        void Place(Guid id, string where, Zone want)
        {
            if (seen.TryGetValue(id, out var prev)) v.Add(new("DUP_INSTANCE", $"{id} in {prev} and {where}"));
            else seen[id] = where;
            var c = s.Card(id);
            if (c == null) { v.Add(new("UNREGISTERED", $"{id} in {where}")); return; }
            if (c.Zone != want) v.Add(new("ZONE_MISMATCH", $"{c.Definition.Id} {id} listed in {where} but zone={c.Zone}"));
        }
        for (int p = 0; p < 2; p++)
        {
            var ps = s.Player(p);
            foreach (var id in ps.Hand) Place(id, $"p{p}.hand", Zone.HAND);
            foreach (var id in ps.Deck) Place(id, $"p{p}.deck", Zone.DECK);
            foreach (var id in ps.Discard) Place(id, $"p{p}.discard", Zone.DISCARD);
            if (ps.CurrentGp < 0) v.Add(new("NEGATIVE_GP", $"p{p} gp={ps.CurrentGp}"));
        }
        var valid = new HashSet<BoardPosition>(s.Board.Positions());
        int capitals0 = 0, capitals1 = 0;
        foreach (var pos in s.Board.Positions())
        {
            var stack = s.Board.StackAt(pos);
            for (int i = 0; i < stack.Count; i++)
            {
                var id = stack[i];
                Place(id, $"board({pos.X},{pos.Y})#{i}", Zone.BATTLEFIELD);
                var c = s.Card(id);
                if (c == null) continue;
                var where = s.Board.PositionOf(id);
                if (where == null || where != pos) v.Add(new("POSITION_INDEX", $"{c.Definition.Id} in stack ({pos.X},{pos.Y}) but PositionOf={where}"));
                if (c.Definition.Type == CardType.CAPITAL) { if (c.Owner == 0) capitals0++; else capitals1++; }
                bool top = i == stack.Count - 1;
                if (!s.Phase().Equals(Phase.GAME_OVER) && c.Definition.Type == CardType.CAPITAL && c.Definition.HitPoints > 0 && c.Damage >= c.Definition.HitPoints)
                    v.Add(new("CAPITAL_AT_ZERO_HP", $"p{c.Owner} {c.Definition.Id} damage {c.Damage} >= hp {c.Definition.HitPoints} at ({pos.X},{pos.Y}) depth {i}/{stack.Count - 1}, match still live"));
                else if (!s.Phase().Equals(Phase.GAME_OVER) && top && c.Definition.IsPermanent() && c.Definition.HitPoints > 0 && c.Damage >= c.Definition.HitPoints)
                    v.Add(new("ZOMBIE_PERMANENT", $"{c.Definition.Id} damage {c.Damage} >= hp {c.Definition.HitPoints} still on board at ({pos.X},{pos.Y})"));
                if (!s.Phase().Equals(Phase.GAME_OVER) && c.Definition.Type == CardType.CHARACTER && c.EffectiveDefense() > 0 && c.CombatDamage >= c.EffectiveDefense())
                    v.Add(new("ZOMBIE_CHARACTER", $"{c.Definition.Id} combat damage {c.CombatDamage} >= defense {c.EffectiveDefense()} at ({pos.X},{pos.Y})"));
            }
        }
        foreach (var id in seen.Keys)
        {
            var pos = s.Board.PositionOf(id);
            if (pos != null && !valid.Contains(pos)) v.Add(new("OFF_BOARD", $"{id} at ({pos.X},{pos.Y})"));
        }
        bool over = s.Phase() == Phase.GAME_OVER;
        if (!over && (capitals0 != 1 || capitals1 != 1)) v.Add(new("CAPITAL_COUNT", $"live match with capitals p0={capitals0} p1={capitals1}"));
        if (over && s.Winner() == null) v.Add(new("OVER_NO_WINNER", "GAME_OVER with no winner"));
        if (!over && s.Winner() != null) v.Add(new("WINNER_NOT_OVER", $"winner={s.Winner()} but phase={s.Phase()}"));
        if (over && s.Winner() is int w && (capitals0 + capitals1) == 2) v.Add(new("WIN_WITH_BOTH_CAPITALS", $"winner {w} while both capitals stand"));
        if (seen.Count < baselineCards) v.Add(new("CARD_LOST", $"tracked cards {seen.Count} < starting {baselineCards}"));
        if (snapshot != null)
        {
            foreach (var p in snapshot["players"]?.AsArray() ?? new JsonArray())
            {
                int used = (int)p["slots_used"], total = (int)p["slots_total"];
                if (used > total) v.Add(new("SLOT_OVERFLOW", $"seat {p["seat"]} slots {used}/{total}"));
            }
        }
        return v;
    }

    public static int CardCount(GameState s)
    {
        int n = 0;
        for (int p = 0; p < 2; p++) n += s.Player(p).Hand.Count + s.Player(p).Deck.Count + s.Player(p).Discard.Count;
        foreach (var pos in s.Board.Positions()) n += s.Board.StackAt(pos).Count;
        return n;
    }
}

/// One recorded intent: what the referee would receive.
sealed class Intent
{
    public int seat { get; set; }
    public string action_id { get; set; }
    public string command { get; set; }
}

sealed class Case
{
    public string name { get; set; }
    public string note { get; set; }
    public JsonObject @new { get; set; }
    public List<Intent> intents { get; set; } = new();
    public List<string> hashes { get; set; } = new();   // hash after op:new, then after each intent
    public string final_hash { get; set; }
    public string expect_kind { get; set; }             // null = clean replay expected
    public int expect_step { get; set; } = -1;
    public string detail { get; set; }
}

record Outcome(string Kind, int Step, string Detail, List<string> Hashes, string FinalHash, int? Winner, int Turns, int Actions);

static class Json
{
    public static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
}

/// Replays a case through a fresh multiplayer session: the referee's path.
static class Replay
{
    public static Outcome Run(string data, Case c, bool checkInvariants = true, int stopAt = int.MaxValue)
    {
        var e = new Engine(data);
        var r = e.New((JsonObject)c.@new.DeepClone());
        var hashes = new List<string>();
        if (r["ok"]?.GetValue<bool>() != true) return new("NEW_REJECTED", -1, r.ToJsonString(), hashes, null, null, 0, 0);
        hashes.Add(e.Hash());
        int baseline = Invariants.CardCount(e.State);
        for (int i = 0; i < c.intents.Count && i < stopAt; i++)
        {
            var it = c.intents[i];
            var legal = r["legal"].AsArray();
            var offered = legal.FirstOrDefault(a => (string)a["id"] == it.action_id);
            if (offered == null) return new("INTENT_NOT_OFFERED", i, $"{it.action_id} '{it.command}' not in legal set (rev {r["revision"]})", hashes, null, null, e.State.TurnNumber(), i);
            if ((string)offered["command"] != it.command) return new("LEGAL_ORDER_DIVERGES", i, $"{it.action_id} is '{offered["command"]}' now, was '{it.command}'", hashes, null, null, e.State.TurnNumber(), i);
            JsonNode next;
            try { next = e.Act(it.action_id, it.seat); }
            catch (Exception ex) { return new("CRASH", i, ex.ToString(), hashes, null, null, e.State.TurnNumber(), i); }
            if (next["ok"]?.GetValue<bool>() != true)
            {
                var code = (string)next["error_code"];
                return new(code == "INTERNAL" ? "CRASH" : "LEGAL_REJECTED", i, $"'{it.command}' -> {next["error_code"]}: {next["error"]}", hashes, null, null, e.State.TurnNumber(), i);
            }
            r = next;
            hashes.Add(e.Hash());
            if (checkInvariants)
            {
                var v = Invariants.Check(e.State, baseline, r["state"]);
                if (v.Count > 0) return new(v[0].Kind, i, string.Join(" | ", v.Select(x => x.Kind + ": " + x.Detail)), hashes, hashes[^1], e.State.Winner(), e.State.TurnNumber(), i + 1);
            }
        }
        return new(null, -1, null, hashes, hashes[^1], e.State.Winner(), e.State.TurnNumber(), c.intents.Count);
    }

    public static int RunFile(Opts o)
    {
        var c = JsonSerializer.Deserialize<Case>(File.ReadAllText(o.S("case")));
        var (ok, msg) = Verify(o.S("data"), c);
        Console.WriteLine((ok ? "PASS " : "FAIL ") + c.name + " " + msg);
        return ok ? 0 : 1;
    }

    /// A case passes when it reproduces exactly what it recorded: the same failure at the same step,
    /// or (for clean cases) the same hash after every step.
    public static (bool, string) Verify(string data, Case c)
    {
        var a = Run(data, c);
        var b = Run(data, c); // in-process twice: catches hidden static state between matches
        if (a.Kind != b.Kind || a.Step != b.Step || !a.Hashes.SequenceEqual(b.Hashes)) return (false, $"replay not repeatable in-process: {a.Kind}@{a.Step} vs {b.Kind}@{b.Step}");
        if (c.expect_kind != null)
        {
            if (a.Kind == c.expect_kind && a.Step == c.expect_step) return (true, $"reproduces {a.Kind} at intent {a.Step}");
            return (false, $"expected {c.expect_kind}@{c.expect_step}, got {a.Kind ?? "clean"}@{a.Step}: {a.Detail}");
        }
        if (a.Kind != null) return (false, $"{a.Kind} at intent {a.Step}: {a.Detail}");
        if (c.hashes.Count > 0)
        {
            for (int i = 0; i < Math.Min(c.hashes.Count, a.Hashes.Count); i++)
                if (c.hashes[i] != a.Hashes[i]) return (false, $"hash diverges at step {i}: recorded {c.hashes[i][..12]} now {a.Hashes[i][..12]}");
            if (c.hashes.Count != a.Hashes.Count) return (false, $"hash trail length {a.Hashes.Count} != recorded {c.hashes.Count}");
        }
        return (true, $"{c.intents.Count} intents, final {a.FinalHash?[..12]}, winner {a.Winner}");
    }
}

static class Soak
{
    public static readonly string[][] Matchups = { new[] { "ZEUS", "POSEIDON" }, new[] { "POSEIDON", "ZEUS" }, new[] { "ZEUS", "ZEUS" }, new[] { "POSEIDON", "POSEIDON" } };

    public static int Run(Opts o)
    {
        string data = o.S("data"), mode = o.S("mode", "mp"), outDir = o.S("out", "soak-out");
        var (a, b) = o.Range("seeds", "1-20");
        Directory.CreateDirectory(Path.Combine(outDir, "cases"));
        var summary = new StreamWriter(Path.Combine(outDir, $"summary-{mode}-{a}-{b}.tsv"));
        summary.WriteLine("seed\tmatchup\tconfig\tresult\tstep\twinner\tturns\tactions\tms\tfinal_hash\ttrail_digest\tdetail");
        var counts = new SortedDictionary<string, int>();
        var sw = Stopwatch.StartNew();
        for (long seed = a; seed <= b; seed++)
        {
            foreach (var mu in Matchups)
            {
                var configs = mode == "bots" ? new[] { o.S("difficulty", "HERO") } : new[] { "aggro-vs-random", "random-vs-aggro", "aggro-vs-aggro" };
                if (mode == "bots" && o.S("difficulty") == "all") configs = new[] { "MORTAL", "HERO", "DEMIGOD" };
                foreach (var cfg in configs)
                {
                    var t = Stopwatch.StartNew();
                    var (res, c) = mode == "bots" ? Bots.Play(data, seed, mu, cfg, o) : Mp.Play(data, seed, mu, cfg, o);
                    t.Stop();
                    string kind = res.Kind ?? (res.Winner != null ? "WIN" : "NO_RESULT");
                    counts[kind] = counts.GetValueOrDefault(kind) + 1;
                    string digest = Digest(res.Hashes);
                    summary.WriteLine($"{seed}\t{mu[0]}-{mu[1]}\t{cfg}\t{kind}\t{res.Step}\t{res.Winner}\t{res.Turns}\t{res.Actions}\t{t.ElapsedMilliseconds}\t{res.FinalHash}\t{digest}\t{Trim(res.Detail)}");
                    if (res.Kind != null && c != null)
                    {
                        c.name = $"{mode}-{res.Kind}-s{seed}-{mu[0]}-{mu[1]}-{cfg}";
                        c.expect_kind = res.Kind; c.expect_step = res.Step; c.detail = res.Detail;
                        File.WriteAllText(Path.Combine(outDir, "cases", c.name + ".json"), JsonSerializer.Serialize(c, Json.Opt));
                    }
                }
            }
            summary.Flush();
        }
        summary.Close();
        Console.WriteLine($"{mode} seeds {a}-{b}: " + string.Join(", ", counts.Select(kv => kv.Key + "=" + kv.Value)) + $" in {sw.Elapsed.TotalSeconds:F0}s");
        return 0;
    }

    public static string Digest(List<string> h)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join(",", h))))[..16].ToLowerInvariant();
    }
    static string Trim(string s) => s == null ? "" : (s.Length > 300 ? s[..300] : s).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
}

/// Multiplayer (referee protocol): the harness picks an offered action id for whichever seat must act.
static class Mp
{
    public static JsonObject NewRequest(long seed, string[] mu, Opts o)
    {
        var req = new JsonObject { ["op"] = "new", ["seed"] = seed, ["multiplayer"] = true, ["human_player"] = 0, ["human_faction"] = mu[0], ["bot_faction"] = mu[1] };
        Merge(req, o.S("extra"));
        return req;
    }

    public static void Merge(JsonObject req, string extra)
    {
        if (string.IsNullOrEmpty(extra)) return;
        foreach (var kv in JsonNode.Parse(extra).AsObject()) req[kv.Key] = kv.Value?.DeepClone();
    }

    public static (Outcome, Case) Play(string data, long seed, string[] mu, string cfg, Opts o)
    {
        int maxActions = o.I("max-actions", 6000), maxTurns = o.I("max-turns", 300);
        var policies = cfg.Split("-vs-");
        var rng = new Random((int)(seed * 7919 + cfg.GetHashCode32()));
        var e = new Engine(data);
        var c = new Case { @new = NewRequest(seed, mu, o) };
        JsonNode r;
        try { r = e.New((JsonObject)c.@new.DeepClone()); }
        catch (Exception ex) { return (new("CRASH", -1, ex.ToString(), new(), null, null, 0, 0), c); }
        if (r["ok"]?.GetValue<bool>() != true) return (new(((string)r["error_code"]) == "INTERNAL" ? "CRASH" : "NEW_REJECTED", -1, r.ToJsonString(), new(), null, null, 0, 0), c);
        var hashes = new List<string> { e.Hash() };
        int baseline = Invariants.CardCount(e.State);
        var v0 = Invariants.Check(e.State, baseline, r["state"]);
        if (v0.Count > 0) return (new(v0[0].Kind, -1, string.Join(" | ", v0.Select(x => x.Kind + ": " + x.Detail)), hashes, hashes[^1], null, 0, 0), Done(c, hashes));
        for (int i = 0; i < maxActions; i++)
        {
            if (e.Over) return (new(null, -1, null, hashes, hashes[^1], e.State.Winner(), e.State.TurnNumber(), i), Done(c, hashes));
            if (e.State.TurnNumber() > maxTurns) return (new("NO_FINISH", i, $"turn {e.State.TurnNumber()} > {maxTurns}", hashes, hashes[^1], null, e.State.TurnNumber(), i), Done(c, hashes));
            var legal = r["legal"].AsArray();
            int seat = (int)r["state"]["acting_player"];
            if (legal.Count == 0) return (new("STALL", i, $"no legal actions for seat {seat}, phase {e.State.Phase()}", hashes, hashes[^1], null, e.State.TurnNumber(), i), Done(c, hashes));
            var pick = Choose(legal, policies[seat], e.State, seat, rng);
            var it = new Intent { seat = seat, action_id = (string)pick["id"], command = (string)pick["command"] };
            c.intents.Add(it);
            var t = Stopwatch.StartNew();
            JsonNode next;
            try { next = e.Act(it.action_id, seat); }
            catch (Exception ex) { return (new("CRASH", i, ex.ToString(), hashes, null, null, e.State.TurnNumber(), i), Done(c, hashes)); }
            if (t.ElapsedMilliseconds > 5000) return (new("SLOW_ACTION", i, $"'{it.command}' took {t.ElapsedMilliseconds} ms", hashes, null, null, e.State.TurnNumber(), i), Done(c, hashes));
            if (next["ok"]?.GetValue<bool>() != true)
            {
                var code = (string)next["error_code"];
                return (new(code == "INTERNAL" ? "CRASH" : "LEGAL_REJECTED", i, $"'{it.command}' -> {code}: {next["error"]}", hashes, null, null, e.State.TurnNumber(), i), Done(c, hashes));
            }
            r = next;
            string h = e.Hash();
            if (h != e.Hash()) return (new("HASH_UNSTABLE", i, "two hash calls disagree with no action between", hashes, null, null, e.State.TurnNumber(), i), Done(c, hashes));
            hashes.Add(h);
            var v = Invariants.Check(e.State, baseline, r["state"]);
            if (v.Count > 0) return (new(v[0].Kind, i, string.Join(" | ", v.Select(x => x.Kind + ": " + x.Detail)), hashes, h, e.State.Winner(), e.State.TurnNumber(), i + 1), Done(c, hashes));
        }
        return (new("HANG", maxActions, $"no result after {maxActions} actions, turn {e.State.TurnNumber()}", hashes, hashes[^1], null, e.State.TurnNumber(), maxActions), Done(c, hashes));
    }

    static Case Done(Case c, List<string> hashes) { c.hashes = new List<string>(hashes); c.final_hash = hashes.Count > 0 ? hashes[^1] : null; return c; }

    static JsonNode Choose(JsonArray legal, string policy, GameState s, int seat, Random rng)
    {
        if (policy == "random")
        {
            // End turn is rare so random play still develops the board.
            var nonEnd = legal.Where(a => (string)a["type"] != "end_turn").ToList();
            if (nonEnd.Count > 0 && rng.NextDouble() < 0.9) return nonEnd[rng.Next(nonEnd.Count)];
            return legal[rng.Next(legal.Count)];
        }
        if (rng.NextDouble() < 0.15) return legal[rng.Next(legal.Count)];
        var enemyCap = s.BattlefieldCards(1 - seat).FirstOrDefault(c => c.Definition.Type == CardType.CAPITAL);
        var capPos = enemyCap == null ? null : s.Board.PositionOf(enemyCap.InstanceId);
        double Score(JsonNode a)
        {
            string type = (string)a["type"];
            double sc = type switch
            {
                "attack" => 60, "activate" => 50, "cast" => 40, "play" => 30, "burrow" => 25, "buy" => 20,
                "move" => 10, "blink" => 10, "pass_reaction" => 5, "end_turn" => 1, _ => 3,
            };
            if (type == "attack" && enemyCap != null && (string)a["target_instance_id"] == enemyCap.InstanceId.ToString()) sc += 100;
            if ((type == "move" || type == "blink") && capPos != null && a["from"] != null && a["to"] != null)
            {
                var from = new BoardPosition((int)a["from"]["x"], (int)a["from"]["y"]);
                var to = new BoardPosition((int)a["to"]["x"], (int)a["to"]["y"]);
                sc += 6 * (from.DistanceTo(capPos) - to.DistanceTo(capPos));
            }
            return sc + rng.NextDouble();
        }
        return legal.OrderByDescending(Score).First();
    }

    static int GetHashCode32(this string s) { unchecked { int h = 17; foreach (char ch in s) h = h * 31 + ch; return h; } }
}

/// Bot-vs-bot: the shipping PlaytestBot plays both seats inside one solo session (RunBots for two bots),
/// then the bot transcript is replayed through the multiplayer protocol to prove the referee lands on the
/// same state hash.
static class Bots
{
    sealed class Step { public int seat; public string cmd; public bool react; public bool ok; public string hash; }

    public static (Outcome, Case) Play(string data, long seed, string[] mu, string difficulty, Opts o)
    {
        int maxTurns = o.I("max-turns", 300), maxDecisions = o.I("max-actions", 20000);
        // The session runs its own bot for the non-human seat as soon as the match opens. Seat the
        // "human" on the starting player so no decisions happen before this harness takes over.
        var probe = new Engine(data);
        var preq = SoloRequest(seed, mu, 0, difficulty, o);
        var pr = probe.New(preq);
        if (pr["ok"]?.GetValue<bool>() != true) return (new("NEW_REJECTED", -1, pr.ToJsonString(), new(), null, null, 0, 0), null);
        int start = probe.State.StartingPlayer();
        var e = new Engine(data);
        var r = e.New(SoloRequest(seed, mu, start, difficulty, o));
        if (r["ok"]?.GetValue<bool>() != true) return (new("NEW_REJECTED", -1, r.ToJsonString(), new(), null, null, 0, 0), null);
        if ((int)r["revision"] != 0 || e.State.ActivePlayer != start) return (new("SETUP_DIVERGES", -1, $"starting player differs between seatings (probe {start}, now {e.State.ActivePlayer})", new(), null, null, 0, 0), null);
        var state = e.State; var commands = e.Commands; var bots = e.Bots;
        int baseline = Invariants.CardCount(state);
        var steps = new List<Step>();
        var hashes = new List<string> { e.Hash() };
        int rejected = 0;
        string firstReject = null;
        try
        {
            while (!e.Over)
            {
                if (state.TurnNumber() > maxTurns) return (new("NO_FINISH", steps.Count, $"turn {state.TurnNumber()} > {maxTurns}", hashes, hashes[^1], null, state.TurnNumber(), steps.Count), null);
                if (steps.Count > maxDecisions) return (new("HANG", steps.Count, $"{steps.Count} decisions without a result", hashes, hashes[^1], null, state.TurnNumber(), steps.Count), null);
                int active = state.ActivePlayer;
                var d = bots[active].TakeNextAction(state, commands, active);
                bool ok = d.Result.StartsWith("OK:");
                if (!ok) { rejected++; firstReject ??= $"seat {active} '{d.Command}' -> {d.Result}"; }
                steps.Add(new Step { seat = active, cmd = d.Command, ok = ok, hash = e.Hash() });
                hashes.Add(steps[^1].hash);
                var v = Invariants.Check(state, baseline, null);
                if (v.Count > 0) return (new(v[0].Kind, steps.Count - 1, string.Join(" | ", v.Select(x => x.Kind + ": " + x.Detail)), hashes, hashes[^1], state.Winner(), state.TurnNumber(), steps.Count), null);
                if (d.Command == "end" || e.Over || state.ActivePlayer != active) continue;
                int other = 1 - active;
                var rd = bots[other].React(state, commands, other);
                if (rd != null)
                {
                    bool rok = rd.Result.StartsWith("OK:");
                    if (!rok) { rejected++; firstReject ??= $"seat {other} react '{rd.Command}' -> {rd.Result}"; }
                    steps.Add(new Step { seat = other, cmd = rd.Command, react = true, ok = rok, hash = e.Hash() });
                    hashes.Add(steps[^1].hash);
                    var v2 = Invariants.Check(state, baseline, null);
                    if (v2.Count > 0) return (new(v2[0].Kind, steps.Count - 1, string.Join(" | ", v2.Select(x => x.Kind + ": " + x.Detail)), hashes, hashes[^1], state.Winner(), state.TurnNumber(), steps.Count), null);
                }
            }
        }
        catch (Exception ex)
        {
            return (new("CRASH", steps.Count, ex.ToString(), hashes, null, null, state.TurnNumber(), steps.Count), null);
        }
        var result = new Outcome(null, -1, null, hashes, hashes[^1], state.Winner(), state.TurnNumber(), steps.Count);
        if (o.S("no-referee") == "true") return (result, null);
        // Referee cross-check: the same game as multiplayer intents must reach the same hash.
        var (mpKind, mpStep, mpDetail, mpCase) = ToReferee(data, seed, mu, steps, o);
        if (mpKind != null) return (result with { Kind = mpKind, Step = mpStep, Detail = mpDetail + (firstReject != null ? $" (bot had {rejected} rejected commands, first: {firstReject})" : "") }, mpCase);
        if (rejected > 0) return (result with { Kind = "BOT_REJECTED_COMMAND", Step = -1, Detail = $"{rejected} bot commands rejected by the engine; first: {firstReject}" }, mpCase);
        return (result, mpCase);
    }

    static JsonObject SoloRequest(long seed, string[] mu, int human, string difficulty, Opts o)
    {
        var req = new JsonObject { ["op"] = "new", ["seed"] = seed, ["human_player"] = human, ["human_faction"] = mu[human], ["bot_faction"] = mu[1 - human], ["difficulty"] = difficulty };
        Mp.Merge(req, o.S("extra"));
        return req;
    }

    static (string, int, string, Case) ToReferee(string data, long seed, string[] mu, List<Step> steps, Opts o)
    {
        var c = new Case { @new = Mp.NewRequest(seed, mu, o) };
        var e = new Engine(data);
        var r = e.New((JsonObject)c.@new.DeepClone());
        if (r["ok"]?.GetValue<bool>() != true) return ("REFEREE_NEW_REJECTED", -1, r.ToJsonString(), c);
        var hashes = new List<string> { e.Hash() };
        int si = 0;
        string Submit(int seat, string command)
        {
            var hit = r["legal"].AsArray().FirstOrDefault(a => (string)a["command"] == command);
            if (hit == null) return $"'{command}' (seat {seat}) is not offered by the multiplayer legal set";
            c.intents.Add(new Intent { seat = seat, action_id = (string)hit["id"], command = command });
            var n = e.Act((string)hit["id"], seat);
            if (n["ok"]?.GetValue<bool>() != true) return $"'{command}' rejected: {n["error"]}";
            r = n;
            hashes.Add(e.Hash());
            return null;
        }
        foreach (var st in steps)
        {
            if (!st.ok) { si++; continue; } // engine rejected it; it changed nothing
            bool window = r["state"]["reaction_pending"]?.GetValue<bool>() == true;
            if (window && !st.react)
            {
                var err = Submit((int)r["state"]["acting_player"], "pass");
                if (err != null) return ("REFEREE_DIVERGES", si, err, Fin(c, hashes));
            }
            else if (!window && st.react) return ("REFEREE_DIVERGES", si, $"bot reacted with '{st.cmd}' but multiplayer opened no reaction window", Fin(c, hashes));
            var e2 = Submit(st.seat, st.cmd);
            if (e2 != null) return ("REFEREE_DIVERGES", si, e2, Fin(c, hashes));
            if (hashes[^1] != st.hash) return ("REFEREE_HASH_MISMATCH", si, $"after '{st.cmd}' solo hash {st.hash[..12]} != referee {hashes[^1][..12]}", Fin(c, hashes));
            si++;
        }
        return (null, -1, null, Fin(c, hashes));
    }

    static Case Fin(Case c, List<string> h) { c.hashes = new(h); c.final_hash = h[^1]; return c; }
}

/// ddmin over the intent list of an mp case, keeping only replays that stay fully legal and reproduce
/// the same failure kind.
static class Shrink
{
    public static int Run(Opts o)
    {
        string data = o.S("data");
        var c = JsonSerializer.Deserialize<Case>(File.ReadAllText(o.S("case")));
        var first = Replay.Run(data, c);
        if (first.Kind == null) { Console.WriteLine("case does not fail; nothing to shrink"); return 1; }
        string kind = first.Kind;
        var list = c.intents.Take(first.Step + 1).ToList();
        // Intent ids are revision-scoped, so re-resolve each kept intent by its command at replay time.
        bool Fails(List<Intent> cand, out Outcome res)
        {
            var e = new Engine(data);
            var r = e.New((JsonObject)c.@new.DeepClone());
            int baseline = Invariants.CardCount(e.State);
            var resolved = new List<Intent>();
            res = null;
            for (int i = 0; i < cand.Count; i++)
            {
                var hit = r["legal"].AsArray().FirstOrDefault(a => (string)a["command"] == cand[i].command && (int)r["state"]["acting_player"] == cand[i].seat);
                if (hit == null) return false;
                resolved.Add(new Intent { seat = cand[i].seat, action_id = (string)hit["id"], command = cand[i].command });
                JsonNode n;
                try { n = e.Act((string)hit["id"], cand[i].seat); }
                catch { if (kind == "CRASH") { res = new("CRASH", i, "", null, null, null, 0, 0); cand.Clear(); cand.AddRange(resolved); return true; } return false; }
                if (n["ok"]?.GetValue<bool>() != true)
                {
                    string k = (string)n["error_code"] == "INTERNAL" ? "CRASH" : "LEGAL_REJECTED";
                    if (k == kind && i == cand.Count - 1) { cand.Clear(); cand.AddRange(resolved); return true; }
                    return false;
                }
                r = n;
                var v = Invariants.Check(e.State, baseline, r["state"]);
                if (v.Count > 0) { if (v[0].Kind == kind && i == cand.Count - 1) { cand.Clear(); cand.AddRange(resolved); return true; } return false; }
            }
            return false;
        }
        int granularity = 2;
        while (list.Count >= 2)
        {
            int chunk = (int)Math.Ceiling(list.Count / (double)granularity);
            bool reduced = false;
            for (int start = 0; start < list.Count - 1; start += chunk)
            {
                var cand = list.Take(start).Concat(list.Skip(Math.Min(start + chunk, list.Count - 1))).ToList();
                if (cand.Count == 0 || cand[^1].command != list[^1].command) continue;
                if (Fails(cand, out _)) { list = cand; granularity = Math.Max(granularity - 1, 2); reduced = true; break; }
            }
            if (!reduced) { if (granularity >= list.Count) break; granularity = Math.Min(list.Count, granularity * 2); }
        }
        var outCase = new Case { name = c.name + "-min", @new = c.@new, intents = list, note = $"shrunk from {c.intents.Count} to {list.Count} intents" };
        var check = Replay.Run(data, outCase);
        outCase.expect_kind = check.Kind; outCase.expect_step = check.Step; outCase.detail = check.Detail;
        outCase.hashes = check.Hashes; outCase.final_hash = check.FinalHash;
        string path = o.S("out", Path.ChangeExtension(o.S("case"), ".min.json"));
        File.WriteAllText(path, JsonSerializer.Serialize(outCase, Json.Opt));
        Console.WriteLine($"{c.intents.Count} -> {list.Count} intents; replays {check.Kind}@{check.Step}; wrote {path}");
        return 0;
    }
}

static class Suite
{
    public static int Run(Opts o)
    {
        int pass = 0, fail = 0;
        foreach (var f in Directory.GetFiles(o.S("dir"), "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
        {
            var c = JsonSerializer.Deserialize<Case>(File.ReadAllText(f));
            var (ok, msg) = Replay.Verify(o.S("data"), c);
            Console.WriteLine((ok ? "PASS " : "FAIL ") + Path.GetFileName(f) + " " + msg);
            if (ok) pass++; else fail++;
        }
        Console.WriteLine($"suite: {pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}

/// Prints capitals, stacks and engine events step by step for a case (for writing handoffs).
static class Trace
{
    public static int Run(Opts o)
    {
        string data = o.S("data");
        var c = JsonSerializer.Deserialize<Case>(File.ReadAllText(o.S("case")));
        int from = o.I("from", 0), to = o.I("to", int.MaxValue);
        var e = new Engine(data);
        var r = e.New((JsonObject)c.@new.DeepClone());
        for (int i = 0; i < c.intents.Count && i <= to; i++)
        {
            var it = c.intents[i];
            r = e.Act(it.action_id, it.seat);
            if (i < from) continue;
            var s = e.State;
            Console.WriteLine($"#{i} T{s.TurnNumber()} seat{it.seat} {it.command} -> ok={r["ok"]} phase={s.Phase()} winner={s.Winner()}");
            foreach (var ev in r["events"]?.AsArray() ?? new JsonArray())
                Console.WriteLine($"     ev {ev["event"]} {ev["detail"]}");
            for (int p = 0; p < 2; p++)
            {
                var cap = s.Board.Positions().SelectMany(pos => s.Board.StackAt(pos).Select((id, k) => (pos, id, k))).Where(x => s.Card(x.id).Owner == p && s.Card(x.id).Definition.Type == CardType.CAPITAL).ToList();
                foreach (var (pos, id, k) in cap)
                {
                    var stack = s.Board.StackAt(pos);
                    Console.WriteLine($"     cap p{p} {s.Card(id).Definition.Id} dmg {s.Card(id).Damage}/{s.Card(id).Definition.HitPoints} at ({pos.X},{pos.Y}) depth {k}/{stack.Count - 1} stack=[{string.Join(", ", stack.Select(g => s.Card(g).Definition.Id + "(" + s.Card(g).Owner + ")"))}]");
                }
                if (cap.Count == 0) Console.WriteLine($"     cap p{p} NONE");
            }
        }
        return 0;
    }
}
