using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IC.Net;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        RelayClient onlineRelay;
        readonly ConcurrentQueue<Action> onlineCallbacks = new ConcurrentQueue<Action>();
        readonly Queue<RelayIntent> onlineIntents = new Queue<RelayIntent>();
        readonly Queue<long> onlineHashes = new Queue<long>();
        string[] onlineSeats;
        bool onlineBridgeBusy, onlineFailed, onlineHashBusy, onlineSubmitting, onlineHashBarrier;
        long onlineHashTurn, onlineAppliedSequence;
        int onlineAnnouncedTurn, onlineActions, onlineHpChecks, onlineCapitalChanges, onlineLastHpSum = -1;
        float onlineDeadline;

        [Serializable] sealed class OnlineResult {
            public bool passed; public int actions, winner, turn, hpChecks, capitalChanges; public long sequence;
            public string state_hash, error;
        }

        async void BeginOnline() {
            OpenMenu(); waiting = true; liveStatus = "Connecting to the match...";
            onlineDeadline = Time.realtimeSinceStartup + 240;
            try {
                onlineSeats = new[] { Arg("-relaySeat0"), Arg("-relaySeat1") };
                if (onlineSeats.Any(string.IsNullOrEmpty) || onlineSeats[0] == onlineSeats[1]) throw new ArgumentException("Two distinct room seats are required");
                humanSeat = int.TryParse(Arg("-humanSeat"), out var seat) ? seat : 0;
                if (humanSeat < 0 || humanSeat > 1) throw new ArgumentException("Seat must be 0 or 1");
                var endpoint = new Uri(Arg("-relayBase"));
                if (endpoint.Scheme != "wss" && !(endpoint.Scheme == "ws" && endpoint.IsLoopback)) throw new ArgumentException("Online matches require a secure connection");
                var room = Arg("-relayRoom");
                if (string.IsNullOrEmpty(room)) throw new ArgumentException("Room is required");
                var uri = new Uri(endpoint, "/rooms/" + Uri.EscapeDataString(room) + "/ws?dataVersion=lab-2&seat=" + Uri.EscapeDataString(onlineSeats[humanSeat]));
                var client = await RelayClient.ConnectAsync(uri, onlineSeats[humanSeat], room);
                onlineRelay = client;
                client.SeedReceived += seed => onlineCallbacks.Enqueue(() => {
                    int engineSeed = unchecked((int)Convert.ToUInt32(seed.Substring(0, 8), 16));
                    onlineBridgeBusy = true;
                    StartLive(resolvedBridgeCmd, Arg("-bridgeCwd"), Arg("-bridgeTranscript"), engineSeed, humanSeat, "HERO", multiplayer:true);
                    if (Flag("-onlineAutoSmoke")) { speed = 200; sfx.Muted = true; }
                });
                client.IntentReceived += intent => onlineCallbacks.Enqueue(() => onlineIntents.Enqueue(intent));
                client.HashRequested += turn => onlineCallbacks.Enqueue(() => onlineHashes.Enqueue(turn));
                client.HashMatched += turn => onlineCallbacks.Enqueue(() => {
                    onlineHashBarrier = false;
                    if (!onlineBridgeBusy && !onlineSubmitting) waiting = false;
                });
                client.HashMismatched += mismatch => onlineCallbacks.Enqueue(() => FailOnline("The match states diverged at turn " + mismatch.Turn));
                client.ErrorReceived += error => onlineCallbacks.Enqueue(() => FailOnline(error));
                client.Forfeited += forfeit => onlineCallbacks.Enqueue(() => FailOnline("Match forfeited: " + forfeit.Reason));
                client.Closed += () => onlineCallbacks.Enqueue(() => { if (!MatchOver) FailOnline("Connection lost. This candidate does not yet support reconnect."); });
                await client.SendHelloAsync();
                var seedPart = Guid.NewGuid().ToString("N"); var salt = Guid.NewGuid().ToString("N");
                await client.CommitSeedAsync(seedPart, salt);
                await client.RevealSeedAsync(seedPart, salt);
            } catch (Exception ex) { onlineCallbacks.Enqueue(() => FailOnline(ex.Message)); }
        }

        async void OnlineSend(Task operation) {
            try { await operation; }
            catch (Exception ex) { onlineCallbacks.Enqueue(() => FailOnline(ex.Message)); }
        }

        void PumpOnline() {
            while (onlineCallbacks.TryDequeue(out var callback)) callback();
            if (onlineFailed || onlineRelay == null) return;
            if (Flag("-onlineAutoSmoke") && Time.realtimeSinceStartup > onlineDeadline) { FailOnline("Online match timed out"); return; }
            if (bridge == null || onlineBridgeBusy || onlineHashBusy) return;
            if (onlineHashes.Count > 0) {
                onlineHashTurn = onlineHashes.Dequeue(); onlineHashBusy = true; bridge.Hash(); return;
            }
            if (onlineIntents.Count > 0) {
                var intent = onlineIntents.Dequeue();
                if (intent.Seq <= onlineAppliedSequence) return;
                if (intent.Seq != onlineAppliedSequence + 1) { FailOnline("Missing match action sequence"); return; }
                int actor = Array.IndexOf(onlineSeats, intent.Seat);
                if (actor < 0 || liveState == null || actor != liveState.acting_player) { FailOnline("Unexpected acting player"); return; }
                onlineAppliedSequence = intent.Seq; onlineBridgeBusy = true; onlineSubmitting = false;
                waiting = true; liveActions = new BridgeAction[0]; ClearSelection();
                bridge.Act(intent.ActionId, actor); onlineActions++; return;
            }
            if (Flag("-onlineAutoSmoke") && liveState != null && !liveState.GameOver && !waiting && !onlineSubmitting && liveActions.Length > 0) {
                var options = liveActions.OrderBy(ValidationPriority).ToArray();
                int best = ValidationPriority(options[0]);
                options = options.Where(a => ValidationPriority(a) == best).ToArray();
                ActLive(options[(onlineActions * 17 + humanSeat) % options.Length]);
            }
        }

        void SendOnlineAction(BridgeAction action) {
            if (onlineFailed || onlineSubmitting || onlineHashBarrier || liveState == null || liveState.acting_player != humanSeat) return;
            onlineSubmitting = true; waiting = true;
            liveStatus = "Waiting for the match to confirm your action...";
            liveActions = new BridgeAction[0]; ClearSelection();
            OnlineSend(onlineRelay.SendIntentAsync(action.id));
        }

        void OnlineStateReceived(BridgeResponse response) {
            if (onlineRelay == null) return;
            onlineBridgeBusy = false;
            if (!response.ok) { FailOnline(response.error ?? "Rules rejected an online intent"); return; }
            if (liveState == null) return;
            // Automated online smoke only: the HUD must equal the bridge's capital HP for both seats after every state.
            if (!liveState.GameOver && Flag("-onlineAutoSmoke")) {
                if (!liveState.HasCapitals) { FailOnline("Online bridge sent no capital HP metadata"); return; }
                for (int s = 0; s < 2; s++) if (model.CapitalHp[s] != liveState.capitals[s].hp) { FailOnline($"HUD capital HP for seat {s} ({model.CapitalHp[s]}) differs from the bridge ({liveState.capitals[s].hp})"); return; }
                int sum = liveState.capitals[0].hp + liveState.capitals[1].hp;
                if (onlineLastHpSum >= 0 && sum != onlineLastHpSum) onlineCapitalChanges++;
                onlineLastHpSum = sum; onlineHpChecks++;
            }
            if (liveState.acting_player != humanSeat) {
                liveActions = new BridgeAction[0];
                liveStatus = liveState.reaction_pending ? "Opponent is choosing a reaction." : "Opponent's turn.";
            }
            if (liveState.turn > onlineAnnouncedTurn) {
                onlineHashBarrier = onlineAnnouncedTurn > 0;
                onlineAnnouncedTurn = liveState.turn;
                if (onlineHashBarrier) waiting = true;
                OnlineSend(onlineRelay.AnnounceTurnAsync(liveState.turn, onlineSeats[liveState.acting_player]));
            }
            if (liveState.GameOver && Flag("-onlineAutoSmoke")) onlineHashes.Enqueue(-1);
        }

        bool OnlineHashResponse(BridgeResponse response) {
            if (onlineRelay == null || string.IsNullOrEmpty(response.state_hash)) return false;
            onlineHashBusy = false;
            if (onlineHashTurn == -1) {
                var result = new OnlineResult { passed = liveState != null && liveState.GameOver && !onlineFailed,
                    actions = onlineActions, sequence = onlineAppliedSequence, state_hash = response.state_hash, hpChecks = onlineHpChecks, capitalChanges = onlineCapitalChanges,
                    winner = liveState?.winner ?? -1, turn = liveState?.turn ?? 0 };
                FinishOnlineSmoke(result);
            } else OnlineSend(onlineRelay.SendHashAsync(onlineHashTurn, response.state_hash));
            return true;
        }

        void FailOnline(string reason) {
            if (onlineFailed) return;
            onlineFailed = true; waiting = true; liveActions = new BridgeAction[0]; liveStatus = reason;
            Debug.LogError("ONLINE_MATCH_FAIL " + reason);
            if (Flag("-onlineAutoSmoke")) FinishOnlineSmoke(new OnlineResult { error = reason, actions = onlineActions, sequence = onlineAppliedSequence });
        }
        void FinishOnlineSmoke(OnlineResult result) {
            var output = Arg("-playtestResult"); if (output != null) File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log("ONLINE_MATCH_SMOKE " + (result.passed ? "PASS" : "FAIL"));
            Application.Quit(result.passed ? 0 : 1);
        }
        void CloseOnline() { var relay = onlineRelay; onlineRelay = null; if (relay != null) OnlineSend(relay.DisposeAsync().AsTask()); }
    }
}
