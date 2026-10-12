using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-087 in-process rules client, preserving the AI-079 JSON envelope and AI-062 events.
    // Each client owns a serialized worker queue; Unity consumes responses on its main thread.
    [Serializable] public sealed class BridgeAction {
        public string id, type, command, instance_id, card_id, card_name, target_instance_id, target_card_id;
        public WireHex to, from, at, target, destination;
        public bool has_destination;
        public int price; public string category;
    }
    [Serializable] public sealed class BridgeMarketCard { public string card_id, name, category; public int price, remaining, bought; }
    [Serializable] public sealed class BridgeHandCard { public string instance_id, card_id; public int slot_cost, slot_supply; }
    [Serializable] public sealed class BridgePlayer { public int gp, personal_turn, hand_count, deck_count, discard_count, seat, slots_total, slots_used, slots_available; public string faction, controller; public BridgeHandCard[] hand; }
    [Serializable] public sealed class BridgeStackCard { public string instance_id, card_id; public int owner, slot_cost, slot_supply, damage; }
    // AI-080-RESTORATION-R3 opt-in metadata (client_metadata:true on new).
    [Serializable] public sealed class BridgeCapital { public int seat, hp, max_hp, damage, x, y; public string instance_id, card_id; public bool destroyed; }
    [Serializable] public sealed class BridgeAbility {
        public string instance_id, card_id, name, type, effect, effect_type, status, reason, action_id;
        public int x, y, gp_cost; public bool buried;
        public bool Ready => status == "READY" && !string.IsNullOrEmpty(action_id);
    }
    [Serializable] public sealed class BridgeHex { public int x, y; public BridgeStackCard[] stack; }
    [Serializable] public sealed class BridgeState {
        public int turn, active_player, winner, acting_player;
        public string phase;
        public BridgePlayer[] players;
        public BridgeHex[] board;
        public bool slots_enabled, reaction_pending, multiplayer, purchase_economy;
        public string rules_version;
        public BridgeMarketCard[] market;
        public int starting_player;
        public BridgeCapital[] capitals;
        public BridgeAbility[] abilities;
        public bool GameOver => phase == "GAME_OVER";
        public bool HasCapitals => capitals != null && capitals.Length == 2 && capitals.Any(c => !string.IsNullOrEmpty(c.instance_id) || c.destroyed);
    }
    [Serializable] public sealed class BridgeResponse {
        public bool ok;
        public string id, error, error_code, state_hash;
        public int revision;
        public WireEvent[] events;
        public BridgeAction[] legal;
        public BridgeState state;
        [NonSerialized] public string raw;
    }

    public sealed class BridgeClient : IDisposable {
        readonly InfiniteConquest.Native.NativeRulesSession native;
        readonly BlockingCollection<string> nativeRequests;
        volatile bool disposed;
        readonly TextWriter input;
        readonly Process process;
        readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();
        readonly TextWriter transcript;
        readonly bool ownsTranscript;
        readonly object transcriptLock = new object();
        Thread reader, errReader;
        int requestId, transcriptSequence;
        public string LastError;
        public bool Alive => !disposed && (process == null || !process.HasExited);

        // Compatibility entry point for the existing UI. Runs the native rules session;
        // commandLine/workingDir are retained for call-site compatibility.
        public static BridgeClient Spawn(string commandLine, string workingDir, string transcriptPath = null) {
            TextWriter log = null;
            if (!string.IsNullOrEmpty(transcriptPath)) {
                var fullPath = Path.GetFullPath(transcriptPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                log = new StreamWriter(fullPath, false) { AutoFlush = true };
            }
            try { return new BridgeClient(new InfiniteConquest.Native.NativeRulesSession(Path.Combine(Application.streamingAssetsPath, "NativeRules")), log); }
            catch { log?.Dispose(); throw; }
        }

        BridgeClient(InfiniteConquest.Native.NativeRulesSession native, TextWriter transcript) {
            this.native = native; this.transcript = transcript; ownsTranscript = transcript != null;
            nativeRequests = new BlockingCollection<string>();
            reader = new Thread(() => {
                foreach (var request in nativeRequests.GetConsumingEnumerable()) {
                    if (disposed) break;
                    var line = native.Handle(request);
                    if (disposed) break;
                    WriteTranscript("response", line); lines.Enqueue(line);
                }
            }) { IsBackground = true, Name = "Infinite Conquest native rules" };
            reader.Start();
        }

        // Also used by the editor self-test with in-memory streams.
        public BridgeClient(Process process, TextWriter input, TextReader output, TextReader error = null,
                            TextWriter transcript = null, bool ownsTranscript = false) {
            this.process = process; this.input = input; this.transcript = transcript; this.ownsTranscript = ownsTranscript;
            reader = new Thread(() => { try { string l; while ((l = output.ReadLine()) != null) if (l.Trim().StartsWith("{")) { WriteTranscript("response", l); lines.Enqueue(l); } } catch { } }) { IsBackground = true };
            reader.Start();
            if (error != null) { errReader = new Thread(() => { try { string l; while ((l = error.ReadLine()) != null) errors.Enqueue(l); } catch { } }) { IsBackground = true }; errReader.Start(); }
        }

        public void Send(string json) {
            if (disposed) throw new ObjectDisposedException(nameof(BridgeClient));
            WriteTranscript("request", json);
            if (native != null) nativeRequests.Add(json);
            else { input.WriteLine(json); input.Flush(); }
        }
        string NextId() => "unity-" + Interlocked.Increment(ref requestId);
        // Latest r3 supports metadata for both modes; the older r3Solo named argument
        // remains compatible with the retained editor fixtures.
        public static string[] SelectedDeck;
        static string DeckRequest(bool multiplayer) => multiplayer || SelectedDeck == null ? "" : ",\"human_deck\":[" + string.Join(",", SelectedDeck.Select(id => "\"" + id + "\"")) + "]";
        public void New(int seed, int humanSeat, string difficulty = "HERO", int capitalX = -1, int capitalY = -1, bool multiplayer = false, bool clientMetadata = false, bool r3Solo = false) => Send("{\"id\":\"" + NextId() + "\",\"op\":\"new\",\"seed\":" + seed
            + DeckRequest(multiplayer) + ",\"human_player\":" + humanSeat + ",\"human_faction\":\"" + (humanSeat == 0 ? "ZEUS" : "POSEIDON")
            + "\",\"bot_faction\":\"" + (humanSeat == 0 ? "POSEIDON" : "ZEUS") + "\",\"difficulty\":\"" + difficulty + "\",\"interactive_reactions\":true"
            + (multiplayer ? ",\"multiplayer\":true" : "")
            + (clientMetadata || (r3Solo && !multiplayer) ? ",\"client_metadata\":true" : "")
            + (capitalX >= 0 && capitalY >= 0 ? ",\"capital_x\":" + capitalX + ",\"capital_y\":" + capitalY : "") + "}");
        public void Legal() => Send("{\"id\":\"" + NextId() + "\",\"op\":\"legal\"}");
        public void Act(string actionId, int seat = -1) => Send("{\"id\":\"" + NextId() + "\",\"op\":\"act\",\"action_id\":\"" + actionId.Replace("\"", "") + "\"" + (seat >= 0 ? ",\"seat\":" + seat : "") + "}");
        public void Hash() => Send("{\"id\":\"" + NextId() + "\",\"op\":\"hash\"}");

        public bool TryReceive(out BridgeResponse response) {
            response = null;
            while (errors.TryDequeue(out var e)) { LastError = e; UnityEngine.Debug.Log("BRIDGE stderr: " + e); }
            if (!lines.TryDequeue(out var line)) return false;
            try {
                response = DecodeResponse(line);
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

        // Unity materializes omitted nested serializable objects as zero-valued objects.
        // Keep only coordinates defined by this action's protocol schema.
        public static BridgeResponse DecodeResponse(string line) {
            var response = JsonUtility.FromJson<BridgeResponse>(line);
            if (response?.legal != null) foreach (var action in response.legal) {
                if (action == null) continue;
                bool spatialMove = action.type == "play" || action.type == "burrow" || action.type == "move" || action.type == "blink" || action.type == "attack";
                if (!spatialMove) action.to = null;
                if (action.type != "move" && action.type != "blink" && action.type != "attack") action.from = null;
                if (action.type != "activate") action.at = null;
                if (action.type != "cast") action.target = null;
                if (action.type != "cast" || !action.has_destination) action.destination = null;
            }
            return response;
        }
        public void Dispose() {
            if (disposed) return; disposed = true;
            if (nativeRequests != null) { nativeRequests.CompleteAdding(); reader?.Join(2000); }
            try { if (process != null && !process.HasExited) { input.Close(); if (!process.WaitForExit(1500)) process.Kill(); } } catch { }
            if (ownsTranscript) { try { lock (transcriptLock) transcript.Dispose(); } catch { } }
        }

        // Ordered JSONL envelope containing the exact AI-079 line in each direction. Logging is
        // deliberately outside the protocol streams and never changes requests or responses.
        void WriteTranscript(string direction, string line) {
            if (transcript == null) return;
            lock (transcriptLock) {
                int sequence = ++transcriptSequence;
                transcript.WriteLine("{\"sequence\":" + sequence + ",\"direction\":\"" + direction
                    + "\",\"line\":\"" + EscapeJson(line) + "\"}");
                transcript.Flush();
            }
        }

        static string EscapeJson(string value) {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

    }
}
