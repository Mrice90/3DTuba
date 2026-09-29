using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-079 rules-bridge client (behind the -bridgeCmd flag). The bridge is Muse's headless Java process
    // against the pinned alpha jar, speaking line-delimited JSON over stdin/stdout. Until it lands this
    // client follows the protocol as described in PRODUCT_BACKLOG.md (AI-079):
    //   -> {"cmd":"new","seed":42,"human":0}      <- {"ok":true,"events":[...],"state":{...}}
    //   -> {"cmd":"legal"}                         <- {"ok":true,"actions":[{"id":"a3","kind":"MOVE",...}]}
    //   -> {"cmd":"act","id":"a3"}                 <- {"ok":true,"events":[...],"state":{...}}  (bot seat auto-plays)
    //   illegal ids                                <- {"ok":false,"error":"..."}
    // Events are AI-062 wire records; state carries hands, GP and per-hex stacks. Field names here are the
    // expected ones; adjust BridgeResponse if the landed bridge differs.
    [Serializable] public sealed class BridgeAction {
        public string id, kind, label, instance_id, card_id;
        public WireHex to, from;
    }
    [Serializable] public sealed class BridgeHandCard { public string instance_id, card_id; }
    [Serializable] public sealed class BridgePlayer { public int gp, capital_hp, hand_count, deck_count; public BridgeHandCard[] hand; }
    [Serializable] public sealed class BridgeStackCard { public string instance_id, card_id; public int owner; }
    [Serializable] public sealed class BridgeHex { public int x, y; public BridgeStackCard[] stack; }
    [Serializable] public sealed class BridgeState {
        public int turn, active, winner = -1;
        public string phase;
        public BridgePlayer[] players;
        public BridgeHex[] board;
    }
    [Serializable] public sealed class BridgeResponse {
        public bool ok;
        public string error;
        public WireEvent[] events;
        public BridgeAction[] actions;
        public BridgeState state;
        [NonSerialized] public string raw;
    }

    public sealed class BridgeClient : IDisposable {
        readonly TextWriter input;
        readonly Process process;
        readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        Thread reader, errReader;
        public string LastError;
        public bool Alive => process == null || !process.HasExited;

        // Spawns the bridge. commandLine = "java -cp ... RulesBridge" (the first token is the executable).
        public static BridgeClient Spawn(string commandLine, string workingDir) {
            SplitCommand(commandLine, out var exe, out var args);
            var psi = new ProcessStartInfo(exe, args) {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, WorkingDirectory = string.IsNullOrEmpty(workingDir) ? Environment.CurrentDirectory : workingDir,
            };
            var p = Process.Start(psi);
            return new BridgeClient(p, p.StandardInput, p.StandardOutput, p.StandardError);
        }

        // Also used by the editor self-test with in-memory streams.
        public BridgeClient(Process process, TextWriter input, TextReader output, TextReader error = null) {
            this.process = process; this.input = input;
            reader = new Thread(() => { try { string l; while ((l = output.ReadLine()) != null) if (l.Trim().StartsWith("{")) lines.Enqueue(l); } catch { } }) { IsBackground = true };
            reader.Start();
            if (error != null) { errReader = new Thread(() => { try { string l; while ((l = error.ReadLine()) != null) errors.Enqueue(l); } catch { } }) { IsBackground = true }; errReader.Start(); }
        }

        public void Send(string json) { input.WriteLine(json); input.Flush(); }
        public void New(int seed, int humanSeat) => Send("{\"cmd\":\"new\",\"seed\":" + seed + ",\"human\":" + humanSeat + "}");
        public void Legal() => Send("{\"cmd\":\"legal\"}");
        public void Act(string id) => Send("{\"cmd\":\"act\",\"id\":\"" + id.Replace("\"", "") + "\"}");

        public bool TryReceive(out BridgeResponse response) {
            response = null;
            while (errors.TryDequeue(out var e)) { LastError = e; UnityEngine.Debug.Log("BRIDGE stderr: " + e); }
            if (!lines.TryDequeue(out var line)) return false;
            try {
                response = JsonUtility.FromJson<BridgeResponse>(line);
                response.raw = line;
                if (response.events != null) foreach (var ev in response.events) {
                    ev.hasTo = ev.to != null && (ev.@event == "CARD_PLAYED" || ev.@event == "CHARACTER_MOVED" || ev.@event == "ATTACK_RESOLVED" || ev.@event == "OPPORTUNITY_ATTACK");
                    ev.hasFrom = ev.from != null && ev.@event == "CHARACTER_MOVED";
                    ev.hasAmount = true;
                }
            } catch (Exception ex) {
                response = new BridgeResponse { ok = false, error = "unparseable bridge line: " + ex.Message, raw = line };
            }
            if (!response.ok && response.error != null) LastError = response.error;
            return true;
        }

        public void Dispose() {
            try { if (process != null && !process.HasExited) { input.Close(); if (!process.WaitForExit(1500)) process.Kill(); } } catch { }
        }

        static void SplitCommand(string cmd, out string exe, out string args) {
            cmd = cmd.Trim();
            if (cmd.StartsWith("\"")) { int end = cmd.IndexOf('"', 1); exe = cmd.Substring(1, end - 1); args = cmd.Substring(end + 1).Trim(); }
            else { int sp = cmd.IndexOf(' '); exe = sp < 0 ? cmd : cmd.Substring(0, sp); args = sp < 0 ? "" : cmd.Substring(sp + 1); }
        }
    }
}
