using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    [Serializable] public sealed class WireHex { public int x, y; }
    // One AI-062 board-events wire record (AI-066 JSONL line). `event` is a C# keyword, hence @event.
    [Serializable] public sealed class WireEvent {
        public int turn, player, seq, amount, stack_index;
        public string @event, detail, card_id, instance_id;
        public WireHex to, from;
        [NonSerialized] public bool hasTo, hasFrom, hasAmount;
        public Vector2Int To => new Vector2Int(to.x, to.y);
        public Vector2Int From => new Vector2Int(from.x, from.y);

        public static WireEvent Parse(string line) {
            var e = JsonUtility.FromJson<WireEvent>(line);
            e.hasTo = line.Contains("\"to\"") && e.to != null;
            e.hasFrom = line.Contains("\"from\"") && e.from != null;
            e.hasAmount = line.Contains("\"amount\"");
            return e;
        }
        public static List<WireEvent> ParseJsonl(string text) {
            var list = new List<WireEvent>();
            foreach (var raw in text.Split('\n')) {
                var line = raw.Trim();
                if (line.Length > 0) list.Add(Parse(line));
            }
            return list;
        }
        // The engine's damage detail names the card: "2 damage to zeus_x" / "6 capital damage to poseidon_capital_y".
        public string DetailCardId() {
            if (detail == null) return null;
            int i = detail.LastIndexOf(" to ", StringComparison.Ordinal);
            return i >= 0 ? detail.Substring(i + 4).Trim() : null;
        }
        // "attacker -> target ..." for ATTACK_RESOLVED / OPPORTUNITY_ATTACK
        public string DetailTargetInstance() {
            if (detail == null) return null;
            int i = detail.IndexOf("-> ", StringComparison.Ordinal);
            if (i < 0) return null;
            var rest = detail.Substring(i + 3).Trim();
            int sp = rest.IndexOf(' ');
            return sp > 0 ? rest.Substring(0, sp) : rest;
        }
    }

    // Presentation-side match model built only from events (plus bridge state in live play).
    public sealed class MatchModel {
        public sealed class Inst { public string id, cardId; public int owner; public Vector2Int pos = new Vector2Int(-1, -1); public bool onBoard; }
        public readonly Dictionary<string, Inst> Instances = new Dictionary<string, Inst>();
        public int[] Gp = new int[2], Hand = new int[2], CapitalHp = { 20, 20 }, Played = new int[2], Destroyed = new int[2];
        public int Turn, Active, Winner = -1;
        public string Phase = "", Banner = "", CapitalId0 = "zeus_capital_olympus_citadel", CapitalId1 = "poseidon_capital_atlantis_nexus";
        public string[] Faction = { "ZEUS", "POSEIDON" };
        public readonly List<string> Log = new List<string>();

        public Inst Get(string id) { if (id != null && Instances.TryGetValue(id, out var i)) return i; return null; }
        public Inst Ensure(string id, string cardId, int owner) {
            if (id == null) id = Guid.NewGuid().ToString();
            if (!Instances.TryGetValue(id, out var i)) { i = new Inst { id = id, cardId = cardId, owner = owner }; Instances[id] = i; }
            if (cardId != null) i.cardId = cardId;
            return i;
        }
        public void AddLog(string s) { Log.Add(s); if (Log.Count > 200) Log.RemoveAt(0); }
    }
}
