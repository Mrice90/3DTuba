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
        readonly Queue<RelayHashCheckpoint> onlineHashes = new Queue<RelayHashCheckpoint>();
        string[] onlineSeats;
        bool onlineBridgeBusy, onlineFailed, onlineHashBusy, onlineSubmitting, onlineHashBarrier;
        bool onlineCosmeticsReady;
        string onlineLocalTile,onlineLocalCoin;
        long onlineHashTurn, onlineAppliedSequence;
        int onlineAnnouncedTurn, onlineActions, onlineHpChecks, onlineCapitalChanges, onlineLastHpSum = -1;
        float onlineDeadline;
        float onlineNextApply,onlineTestApplyDelay;int onlineCheckpointWaits;

        [Serializable] sealed class OnlineResult {
            public bool passed; public int actions, winner, turn, hpChecks, capitalChanges; public long sequence;
            public string state_hash, error;
            public string tile0,tile1,localCoin; public bool cosmeticsReady;
            public int checkpointWaits;
        }

        async void BeginOnline() {
            OpenMenu(); waiting = true; liveStatus = "Connecting to the match...";
            onlineDeadline = Time.realtimeSinceStartup + 240;
            try {
                onlineSeats = new[] { Arg("-relaySeat0"), Arg("-relaySeat1") };
                if (onlineSeats.Any(string.IsNullOrEmpty) || onlineSeats[0] == onlineSeats[1]) throw new ArgumentException("Two distinct room seats are required");
                humanSeat = int.TryParse(Arg("-humanSeat"), out var seat) ? seat : 0;
                if (humanSeat < 0 || humanSeat > 1) throw new ArgumentException("Seat must be 0 or 1");
                onlineLocalTile=MatchCosmetics.Validate(Arg("-onlineTile")??BoardSkin.SelectedName(humanSeat),humanSeat);
                onlineLocalCoin=CoinSkin.Load(Arg("-coinSkin")??CoinSkin.SelectedName).name;
                onlineTestApplyDelay=Flag("-onlineAutoSmoke")&&int.TryParse(Arg("-onlineApplyDelayMs"),out var delay)?Mathf.Clamp(delay,0,250)/1000f:0;
                var endpoint = new Uri(Arg("-relayBase"));
                if (endpoint.Scheme != "wss" && !(endpoint.Scheme == "ws" && endpoint.IsLoopback)) throw new ArgumentException("Online matches require a secure connection");
                var room = Arg("-relayRoom");
                if (string.IsNullOrEmpty(room)) throw new ArgumentException("Room is required");
                var uri = new Uri(endpoint, "/rooms/" + Uri.EscapeDataString(room) + "/ws?dataVersion=lab-2&seat=" + Uri.EscapeDataString(onlineSeats[humanSeat]));
                var client = await RelayClient.ConnectAsync(uri, onlineSeats[humanSeat], room);
                onlineRelay = client;
                client.CosmeticsReceived += cosmetics=>QueueOnline(client,()=>{
                    if(cosmetics.RulesVersion!="purchase-v3-hex37"||cosmetics.Seat0!=onlineSeats[0]||cosmetics.Seat1!=onlineSeats[1]
                        ||MatchCosmetics.Validate(cosmetics.Tile0,0)!=cosmetics.Tile0||MatchCosmetics.Validate(cosmetics.Tile1,1)!=cosmetics.Tile1
                        ||(humanSeat==0?cosmetics.Tile0:cosmetics.Tile1)!=onlineLocalTile){FailOnline("The room supplied incompatible player appearances.");return;}
                    MatchCosmetics.SetSeats(cosmetics.Tile0,cosmetics.Tile1);board.ApplySkins();
                    if(!onlineCosmeticsReady){onlineCosmeticsReady=true;OnlineSend(CommitOnlineSeed(client));}
                });
                client.SeedReceived += seed => QueueOnline(client,() => {
                    if(!onlineCosmeticsReady){FailOnline("The room did not negotiate player appearances.");return;}
                    int engineSeed = unchecked((int)Convert.ToUInt32(seed.Substring(0, 8), 16));
                    onlineBridgeBusy = true;
                    StartLive(resolvedBridgeCmd, Arg("-bridgeCwd"), Arg("-bridgeTranscript"), engineSeed, humanSeat, "HERO", multiplayer:true);
                    if (Flag("-onlineAutoSmoke")) { speed = 200; sfx.Muted = true; }
                });
                client.IntentReceived += intent => QueueOnline(client,() => onlineIntents.Enqueue(intent));
                client.HashCheckpointRequested += checkpoint => QueueOnline(client,() => {onlineHashes.Enqueue(checkpoint);onlineHashBarrier=true;waiting=true;});
                client.HashMatched += turn => QueueOnline(client,() => {
                    onlineHashBarrier = false;
                    if (!onlineBridgeBusy && !onlineSubmitting) waiting = false;
                });
                client.HashMismatched += mismatch => QueueOnline(client,() => FailOnline("The match states diverged at turn " + mismatch.Turn));
                client.ErrorReceived += error => QueueOnline(client,() => FailOnline(error));
                client.Forfeited += forfeit => QueueOnline(client,() => FailOnline("Match forfeited: " + forfeit.Reason));
                client.Closed += () => QueueOnline(client,() => { if (!MatchOver) FailOnline("Connection lost. This candidate does not yet support reconnect."); });
                await client.SendHelloAsync();
                await client.SendCosmeticsAsync(onlineLocalTile);
            } catch (Exception ex) { onlineCallbacks.Enqueue(() => FailOnline(ex.Message)); }
        }

        async Task CommitOnlineSeed(RelayClient client){
            var seedPart=Guid.NewGuid().ToString("N");var salt=Guid.NewGuid().ToString("N");
            await client.CommitSeedAsync(seedPart,salt);await client.RevealSeedAsync(seedPart,salt);
        }
        void QueueOnline(RelayClient client,Action action)=>onlineCallbacks.Enqueue(()=>{if(onlineRelay==client)action();});

        async void OnlineSend(Task operation) {
            var client=onlineRelay;
            try { await operation; }
            catch (Exception ex) { if(client!=null)QueueOnline(client,() => FailOnline(ex.Message)); }
        }

        void PumpOnline() {
            while (onlineCallbacks.TryDequeue(out var callback)) callback();
            if (onlineFailed || onlineRelay == null) return;
            if (Flag("-onlineAutoSmoke") && Time.realtimeSinceStartup > onlineDeadline) { FailOnline("Online match timed out"); return; }
            if (bridge == null || onlineBridgeBusy || onlineHashBusy) return;
            if (onlineHashes.Count > 0 && onlineHashes.Peek().Sequence < onlineAppliedSequence) {FailOnline("The match passed a required synchronization checkpoint.");return;}
            if (onlineHashes.Count > 0 && onlineHashes.Peek().Sequence == onlineAppliedSequence) {
                onlineHashTurn = onlineHashes.Dequeue().Turn; onlineHashBusy = true; bridge.Hash(); return;
            }
            if(onlineHashes.Count>0)onlineCheckpointWaits++;
            if(Time.realtimeSinceStartup<onlineNextApply)return;
            if (onlineIntents.Count > 0) {
                var intent = onlineIntents.Dequeue();
                if (intent.Seq <= onlineAppliedSequence) return;
                if (intent.Seq != onlineAppliedSequence + 1) { FailOnline("Missing match action sequence"); return; }
                int actor = Array.IndexOf(onlineSeats, intent.Seat);
                if (actor < 0 || liveState == null || actor != liveState.acting_player) { FailOnline("Unexpected acting player"); return; }
                onlineAppliedSequence = intent.Seq; onlineBridgeBusy = true; onlineSubmitting = false;
                waiting = true; liveActions = new BridgeAction[0]; ClearSelection();
                bridge.Act(intent.ActionId, actor); onlineActions++;onlineNextApply=Time.realtimeSinceStartup+onlineTestApplyDelay; return;
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
            if (liveState.GameOver && Flag("-onlineAutoSmoke")) onlineHashes.Enqueue(new RelayHashCheckpoint(-1,onlineAppliedSequence));
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
            result.cosmeticsReady=onlineCosmeticsReady;result.tile0=BoardSkin.Selected(0).name;result.tile1=BoardSkin.Selected(1).name;result.localCoin=onlineLocalCoin;
            result.checkpointWaits=onlineCheckpointWaits;
            var output = Arg("-playtestResult"); if (output != null) File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log("ONLINE_MATCH_SMOKE " + (result.passed ? "PASS" : "FAIL"));
            Application.Quit(result.passed ? 0 : 1);
        }
        void CloseOnline() {
            var relay=onlineRelay;onlineRelay=null;MatchCosmetics.Clear();onlineCosmeticsReady=false;
            onlineIntents.Clear();onlineHashes.Clear();while(onlineCallbacks.TryDequeue(out _)){}
            onlineBridgeBusy=onlineFailed=onlineHashBusy=onlineSubmitting=onlineHashBarrier=false;
            onlineHashTurn=onlineAppliedSequence=0;onlineAnnouncedTurn=onlineActions=onlineHpChecks=onlineCapitalChanges=0;onlineLastHpSum=-1;
            onlineNextApply=onlineTestApplyDelay=0;onlineCheckpointWaits=0;
            if(relay!=null)_=relay.DisposeAsync().AsTask();
        }
    }
}
