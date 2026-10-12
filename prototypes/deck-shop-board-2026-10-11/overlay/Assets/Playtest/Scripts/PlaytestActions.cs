using System;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        BridgeAction[] actionChoices = new BridgeAction[0], destinationChoices = new BridgeAction[0];
        Vector2 actionScroll;
        string choicePrompt = "";

        string UnplayableReason(BridgeHandCard handCard) {
            var face = CardFaces.Get(handCard.card_id);
            var player = liveState.players[humanSeat];
            if (waiting) return "Wait for this action to finish.";
            bool development = !liveState.purchase_economy && (face.type == "LAND" || face.type == "STRUCTURE");
            if (development && player.personal_turn < face.cost) return face.name + $" unlocks on your turn {face.cost}; this is your turn {player.personal_turn}.";
            int gold = liveState.purchase_economy ? 0 : development ? face.developmentGoldCost : face.cost;
            if (gold > player.gp) return face.name + $" needs {gold} GP; you have {player.gp}.";
            if (liveState.slots_enabled && handCard.slot_cost > player.slots_available)
                return face.name + $" needs {handCard.slot_cost} open summon slots; you have {player.slots_available}. Build a structure or free slots.";
            if (face.type == "SPELL") return face.name + " has no legal target right now. Check target type and ownership; covered cards cannot be targeted.";
            return face.name + " has no legal placement right now. Check the card's timing and placement rules.";
        }

        void ResetActionChoices() {
            actionChoices = new BridgeAction[0]; destinationChoices = new BridgeAction[0];
            choicePrompt = ""; actionScroll = Vector2.zero;
        }
        void RefreshActionMarkers() {
            var actions = destinationChoices.Length > 0 ? destinationChoices : ActionsFor(liveSelected).ToArray();
            Func<BridgeAction, WireHex> position = destinationChoices.Length > 0
                ? a => a.destination : a => TargetOf(a);
            board.SetLegal(actions.Where(a => position(a) != null && !IsAttack(a)).Select(a => position(a)).Select(h => new Vector2Int(h.x, h.y)),
                           actions.Where(a => position(a) != null && IsAttack(a)).Select(a => position(a)).Select(h => new Vector2Int(h.x, h.y)));
        }
        void ChooseLiveActions(BridgeAction[] actions) {
            if (actions.Length == 0) return;
            // A relocation spell's source and destination are two separate decisions.
            if (actions.All(a => a.destination != null)) {
                destinationChoices = actions; actionChoices = new BridgeAction[0];
                choicePrompt = "Choose a highlighted destination."; liveStatus = choicePrompt;
                RefreshActionMarkers(); return;
            }
            if (actions.Length == 1 && actions[0].destination == null) { ActLive(actions[0]); return; }
            actionChoices = actions; actionScroll = Vector2.zero;
            choicePrompt = "Choose an action for this hex."; liveStatus = choicePrompt;
        }
        void ClickLiveTarget(Vector2Int hex) {
            if (destinationChoices.Length > 0) {
                var choices = LiveActionSelection.AtDestination(destinationChoices, hex.x, hex.y);
                if (choices.Length == 1) ActLive(choices[0]);
                else if (choices.Length > 1) { actionChoices = choices; choicePrompt = "Choose an action."; }
                return;
            }
            ChooseLiveActions(LiveActionSelection.AtTarget(ActionsFor(liveSelected), hex.x, hex.y));
        }
        void OpenAbilities(Vector2Int hex) {
            if (mode != Mode.Live || waiting || !BoardView.InBounds(hex.x, hex.y)) return;
            var choices = liveActions.Where(a => LiveActionSelection.Is(a, "activate") && LiveActionSelection.At(a.at, hex.x, hex.y)).ToArray();
            if (choices.Length == 0) {
                var blocked = Abilities.Where(e => e.x == hex.x && e.y == hex.y).ToArray();
                liveStatus = blocked.Length > 0 ? string.Join("  |  ", blocked.Select(e => e.name + ": " + (e.status == "USED" ? "used this turn" : e.reason)))
                    : "No ability available here. Check ownership, GP, line of sight and whether it was used this turn.";
                return;
            }
            var source = PieceOf(choices[0].instance_id);
            if (source != null) SelectPiece(source);
            SelectLive(choices[0].instance_id);
            actionChoices = choices; choicePrompt = "Choose a land or structure ability.";
        }
        string ActionLabel(BridgeAction action) {
            if (LiveActionSelection.Is(action, "activate")) return "Activate " + PlaytestCatalog.Get(action.card_id).name;
            var target = PieceOf(action.target_instance_id);
            string name = target != null ? target.Card.name : action.target_card_id != null ? PlaytestCatalog.Get(action.target_card_id).name : "";
            string verb = LiveActionSelection.Is(action, "cast") ? "Cast" : LiveActionSelection.Is(action, "attack") ? "Attack"
                : LiveActionSelection.Is(action, "burrow") ? "Burrow" : LiveActionSelection.Is(action, "blink") ? "Blink" : "Move / play";
            var at = TargetOf(action);
            return verb + (name.Length > 0 ? " - " + name : at != null ? $" ({at.x},{at.y})" : "");
        }
        // Reuses the log panel while selecting, so its hit area is already excluded from board input.
        bool DrawLiveActionPanel() {
            if (mode != Mode.Live || liveSelected == null) return false;
            var source = PieceOf(liveSelected);
            var sourceActions = ActionsFor(liveSelected).ToArray();
            var activations = sourceActions.Where(a => LiveActionSelection.Is(a, "activate") || TargetOf(a) == null).ToArray();
            var stackSources = source != null ? board.StackAt(source.X, source.Y)
                .Where(p => p != null && p.Owner == humanSeat && ActionsFor(p.InstanceId).Any()).ToArray() : new Piece[0];
            var options = actionChoices.Length > 0 ? actionChoices : activations;
            var rows = options.Length + (stackSources.Length > 1 ? stackSources.Length : 0);
            GUI.Label(new Rect(logPanel.x + 8, logPanel.y + 5, logPanel.width - 16, 42),
                choicePrompt.Length > 0 ? choicePrompt : source != null ? source.Card.name + " - choose an action or highlighted hex." : "Choose a highlighted target or placement hex.", smallStyle);
            var area = new Rect(logPanel.x + 6, logPanel.y + 49, logPanel.width - 12, logPanel.height - 85);
            actionScroll = GUI.BeginScrollView(area, actionScroll, new Rect(0, 0, area.width - 20, Mathf.Max(area.height, rows * 34)));
            float y = 0;
            GUI.enabled = !waiting && !paused && !MatchOver;
            if (stackSources.Length > 1) foreach (var piece in stackSources) {
                if (GUI.Button(new Rect(0, y, area.width - 22, 30), "Select " + piece.Card.name, buttonStyle)) { SelectPiece(piece); SelectLive(piece.InstanceId); }
                y += 34;
            }
            foreach (var action in options) {
                if (GUI.Button(new Rect(0, y, area.width - 22, 30), ActionLabel(action), buttonStyle)) {
                    if (action.destination != null && destinationChoices.Length == 0) ChooseLiveActions(new[] { action });
                    else ActLive(action);
                }
                y += 34;
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(logPanel.x + 8, logPanel.yMax - 32, logPanel.width - 16, 26), "Cancel selection", buttonStyle)) ClearSelection();
            GUI.enabled = true;
            return true;
        }
    }
}
