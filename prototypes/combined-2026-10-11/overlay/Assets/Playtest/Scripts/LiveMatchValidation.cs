using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        [Serializable] sealed class LiveValidationResult {
            public bool passed; public int actions, capacityChecks, spellTargets, structureAbilities, winner, turn;
            // AI-080-RESTORATION-R3
            public int abilityListChecks, commandCenterActivations,
                capitalDamageChecks, stormRelayChecks, healChecks, losBlockedSeen, buriedSourcesSeen, hpSnapshotChecks;
            public bool rejectionShown;
            // AI-080-REACTION-VISUAL-CUE: decisions taken inside a reaction window, each with the cue up.
            public int reactionWindows, reactionCueChecks;
            public string error; public string[] checks;
        }
        static int ValidationPriority(BridgeAction action) {
            switch (action.type) {
                case "attack": return 0; case "cast": return 1; case "activate": return 2;
                case "play": return 3; case "burrow": return 4; case "move": return 5;
                case "blink": return 6; default: return 7;
            }
        }
        IEnumerator ValidateLiveMatch(int seed) {
            var result = new LiveValidationResult(); var checks = new List<string>();
            var random = new System.Random(seed); speed = 200; sfx.Muted = true;
            float deadline = Time.realtimeSinceStartup + 180;
            var shots = Arg("-gameplayLiveShots");
            if (shots != null) Directory.CreateDirectory(shots);
            bool openingShot = false, abilityShot = false, spellShot = false, rejectionTried = false, reactionShot = false;
            int pendingHpSeat = -1, pendingHpBefore = 0, pendingHpDirection = 0; string pendingHpWhat = null, pendingHpCard = null;
            while (Time.realtimeSinceStartup < deadline && result.actions < 1500) {
                if (mode != Mode.Live || bridge == null || !bridge.Alive) { result.error = "Live bridge unavailable: " + liveStatus; break; }
                if (waiting || liveState == null) { yield return null; continue; }
                if (pendingHpSeat >= 0) {
                    int now = model.CapitalHp[pendingHpSeat];
                    bool over = liveState.GameOver;
                    bool moved = pendingHpDirection < 0 ? now < pendingHpBefore || over : now > pendingHpBefore;
                    if (!moved) { result.error = $"Displayed capital HP did not change after {pendingHpWhat} ({pendingHpBefore} -> {now})."; break; }
                    if (!over && liveState.HasCapitals && liveState.capitals[pendingHpSeat].hp != now) { result.error = "Displayed capital HP differs from the bridge snapshot."; break; }
                    if (pendingHpDirection < 0) { result.capitalDamageChecks++; if (pendingHpCard == "zeus_storm_relay_pylon") result.stormRelayChecks++; } else result.healChecks++;
                    pendingHpSeat = -1;
                }
                if (liveState.GameOver) { result.passed = true; break; }
                if (liveState.reaction_pending) {
                    TrackReactionWindow();
                    if (!ReactionCueActive || !liveActions.Any(a => a.type == "pass_reaction")) { result.error = "Reaction window open without the React-now cue or a Pass choice."; break; }
                    result.reactionCueChecks++;
                    if (shots != null && !reactionShot) { var savedSpeed = speed; speed = 1; yield return Shot(shots, "05-reaction-window.png"); speed = savedSpeed; reactionShot = true; }
                }
                if (!liveState.HasCapitals) { result.error = "Bridge client_metadata (capitals) missing."; break; }
                if (model.CapitalHp[0] != liveState.capitals[0].hp || model.CapitalHp[1] != liveState.capitals[1].hp) { result.error = "HUD capital HP differs from snapshot."; break; }
                result.hpSnapshotChecks++;
                // Command-center data must agree with the bridge's legal activations.
                var legalActs = liveActions.Where(a => a.type == "activate").ToArray();
                if (Abilities.Count(a => a.Ready) != legalActs.Length || legalActs.Any(a => !Abilities.Any(e => e.Ready && e.action_id == a.id && e.instance_id == a.instance_id))) { result.error = "Ability list disagrees with legal activations."; break; }
                result.abilityListChecks++;
                result.losBlockedSeen += Abilities.Count(a => a.reason != null && a.reason.StartsWith("No line of sight"));
                result.buriedSourcesSeen += Abilities.Count(a => a.buried);
                if (!rejectionTried && liveActions.Length > 0 && !liveState.reaction_pending) {
                    rejectionTried = true;
                    bridge.Act("r-stale-validation"); waiting = true;
                    while (waiting && Time.realtimeSinceStartup < deadline) yield return null;
                    result.rejectionShown = LastRejection != null && liveStatus.StartsWith("Not allowed");
                    if (!result.rejectionShown) { result.error = "Engine rejection was not shown."; break; }
                    continue;
                }
                if (!liveState.slots_enabled) { result.error = "Packaged slot policy is not enabled."; break; }
                var player = liveState.players[humanSeat];
                if (player.slots_available != Mathf.Max(0, player.slots_total - player.slots_used)) { result.error = "Capacity metadata mismatch."; break; }
                var hand = player.hand.ToDictionary(c => c.instance_id);
                foreach (var legal in liveActions) {
                    if ((legal.type == "play" || legal.type == "burrow") && hand.TryGetValue(legal.instance_id ?? "", out var card) && card.slot_cost > 0) {
                        result.capacityChecks++;
                        if (card.slot_cost > player.slots_available) { result.error = "Capacity-invalid summon offered."; break; }
                    }
                }
                if (result.error != null) break;
                if (liveActions.Length == 0) { result.error = "No legal actions or end turn."; break; }
                if (shots != null && !openingShot) {
                    var savedSpeed = speed; speed = 1; yield return Shot(shots, "01-live-summon-slots.png"); speed = savedSpeed; openingShot = true;
                }
                int priority = liveActions.Min(ValidationPriority);
                var options = liveActions.Where(a => ValidationPriority(a) == priority).ToArray();
                var action = options[random.Next(options.Length)];
                // R3: prefer capital heals and capital-damage activations so both HP paths are exercised.
                var ownCap = liveState.capitals[humanSeat];
                var heal = liveActions.FirstOrDefault(a => a.type == "cast" && a.card_id == "poseidon_restorative_tide" && a.target_instance_id == ownCap.instance_id && ownCap.damage > 0);
                var capitalShot = liveActions.FirstOrDefault(a => a.type == "activate" && Abilities.Any(e => e.action_id == a.id && (e.effect_type == "DAMAGE_ENEMY_CAPITAL" || e.effect_type == "HEAL_CAPITAL" && ownCap.damage > 0)));
                if (heal != null) action = heal; else if (capitalShot != null) action = capitalShot;
                if (heal != null && action == heal) { pendingHpSeat = humanSeat; pendingHpBefore = model.CapitalHp[humanSeat]; pendingHpDirection = 1; pendingHpWhat = "Restorative Tide"; pendingHpCard = heal.card_id; }
                if (action.type == "activate") {
                    var entry = Abilities.First(e => e.action_id == action.id);
                    if (entry.effect_type == "DAMAGE_ENEMY_CAPITAL") { pendingHpSeat = 1 - humanSeat; pendingHpBefore = model.CapitalHp[1 - humanSeat]; pendingHpDirection = -1; pendingHpWhat = entry.name; pendingHpCard = entry.card_id; }
                    if (entry.effect_type == "HEAL_CAPITAL" && ownCap.damage > 0) { pendingHpSeat = humanSeat; pendingHpBefore = model.CapitalHp[humanSeat]; pendingHpDirection = 1; pendingHpWhat = entry.name; pendingHpCard = entry.card_id; }
                    // Through the command center: select that exact instance, then confirm.
                    CommandCenterPick(entry);
                    if (liveSelected != entry.instance_id || commandSelected != entry.instance_id) { result.error = "Command center did not select " + entry.name; break; }
                    if (!board.IsLegal(new Vector2Int(entry.x, entry.y))) { result.error = "Command center did not highlight the source hex."; break; }
                    var piece0 = PieceOf(action.instance_id);
                    if (piece0 != null && piece0.Card.type == "STRUCTURE") result.structureAbilities++;
                    CommandCenterConfirm();
                    if (!waiting) { result.error = "Command center confirm did not submit " + action.id; break; }
                    result.commandCenterActivations++;
                    result.actions++; yield return null; continue;
                }
                if (action.type == "end_turn") ActLive(action);
                else {
                    var piece = PieceOf(action.instance_id);
                    if (piece != null) SelectPiece(piece);
                    SelectLive(action.instance_id);
                    if (shots != null && action.type == "activate" && piece != null && piece.Card.type == "STRUCTURE" && !abilityShot) {
                        var savedSpeed = speed; speed = 1; yield return Shot(shots, "02-structure-ability.png"); speed = savedSpeed; abilityShot = true;
                    }
                    if (shots != null && action.type == "cast" && !spellShot) {
                        var savedSpeed = speed; speed = 1; yield return Shot(shots, "03-spell-legal-targets.png"); speed = savedSpeed; spellShot = true;
                    }
                    if (action.type == "activate" || TargetOf(action) == null) {
                        if (piece != null && piece.Card.type == "STRUCTURE") result.structureAbilities++;
                        ActLive(action);
                    } else {
                        var at = TargetOf(action);
                        if (!board.IsLegal(new Vector2Int(at.x, at.y))) { result.error = "Emitted target not marked legal."; break; }
                        Click(new Vector2Int(at.x, at.y), null);
                        if (action.destination != null && !waiting) Click(new Vector2Int(action.destination.x, action.destination.y), null);
                        if (!waiting && actionChoices.Any(a => a.id == action.id)) ActLive(action);
                        if (!waiting) { result.error = "Mouse flow did not submit selected action " + action.id; break; }
                        if (action.type == "cast") result.spellTargets++;
                    }
                }
                result.actions++; yield return null;
            }
            result.winner = model.Winner; result.turn = model.Turn; result.reactionWindows = ReactionWindowsOpened;
            if (!result.passed && result.error == null) result.error = "Live-match deadline/action limit exceeded.";
            if (result.passed) {
                checks.Add("Packaged rules bridge started with slot policy enabled");
                checks.Add("Capacity-valid summon offers and HUD metadata");
                checks.Add("Mouse targeting and explicit ability handlers submitted legal actions");
                checks.Add("Live Unity match reached GAME_OVER");
                if (shots != null) { speed = 1; yield return Shot(shots, "04-live-game-over.png"); }
            }
            result.checks = checks.ToArray();
            var path = Arg("-playtestResult"); if (path != null) File.WriteAllText(path, JsonUtility.ToJson(result, true));
            Debug.Log("GAMEPLAY_LIVE_SMOKE " + (result.passed ? "PASS" : "FAIL " + result.error));
            Application.Quit(result.passed ? 0 : 1);
        }
    }
}
