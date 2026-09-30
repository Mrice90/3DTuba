using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-079 rules-bridge v1.0.0 client (behind the -bridgeCmd flag). The bridge is Muse's
    // headless Java process against the pinned alpha jar, speaking line-delimited JSON over
    // stdin/stdout. Requests use id/op and responses carry legal/state/events.
    [Serializable] public sealed class BridgeAction {
        public string id, type, command, instance_id, card_id, card_name;
        public WireHex to, from, at, target, destination;
    }
    [Serializable] public sealed class BridgeHandCard { public string instance_id, card_id; }
    [Serializable] public sealed class BridgePlayer { public int gp, hand_count, deck_count, discard_count, seat; public string faction, controller; public BridgeHandCard[] hand; }
    [Serializable] public sealed class BridgeStackCard { public string instance_id, card_id; public int owner; }
    [Serializable] public sealed class BridgeHex { public int x, y; public BridgeStackCard[] stack; }
    [Serializable] public sealed class BridgeState {
        public int turn, active_player, winner;
        public string phase;
        public BridgePlayer[] players;
        public BridgeHex[] board;
        public bool GameOver => phase == "GAME_OVER";
    }
    [Serializable] public sealed class BridgeResponse {
        public bool ok;
        public string id, error, error_code;
        public int revision;
        public WireEvent[] events;
        public BridgeAction[] legal;
        public BridgeState state;
        [NonSerialized] public string raw;
    }

    public sealed class BridgeClient : IDisposable {
        readonly TextWriter input;
        readonly Process process;
        readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        Thread reader, errReader;
        int requestId;
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
        string NextId() => "unity-" + Interlocked.Increment(ref requestId);
        public void New(int seed, int humanSeat) => Send("{\"id\":\"" + NextId() + "\",\"op\":\"new\",\"seed\":" + seed
            + ",\"human_player\":" + humanSeat + ",\"human_faction\":\"" + (humanSeat == 0 ? "ZEUS" : "POSEIDON")
            + "\",\"bot_faction\":\"" + (humanSeat == 0 ? "POSEIDON" : "ZEUS") + "\",\"difficulty\":\"HERO\"}");
        public void Legal() => Send("{\"id\":\"" + NextId() + "\",\"op\":\"legal\"}");
        public void Act(string actionId) => Send("{\"id\":\"" + NextId() + "\",\"op\":\"act\",\"action_id\":\"" + actionId.Replace("\"", "") + "\"}");

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
