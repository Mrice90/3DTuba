using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-080-RESTORATION-R3 (solo live play, bridge client_metadata):
    //  - capital HP follows the bridge snapshot (Storm Relay / Restorative Tide), with visible -N / +N numbers;
    //  - engine rejections are shown as a banner and in the status line;
    //  - ability command center: every activatable board source (buried lands/structures included) with
    //    hex, effect, GP cost and ready / used / blocked reason; click selects that exact instance.
    // The bridge's legal list stays the authority: only READY entries carry an action id, and that id is sent.
    public sealed partial class PlaytestGame {
        int liveRevision;
        bool capitalsSynced;
        // command center
        bool commandOpen = true;
        Rect commandRect;
        Vector2 commandScroll;
        string commandSelected;
        BridgeAbility[] Abilities => liveState?.abilities ?? new BridgeAbility[0];

        void ResetR3() {
            LastRejection = null;
            capitalsSynced = false; commandSelected = null; commandScroll = Vector2.zero;
            liveRevision = 0;
        }

        // ------------------------------------------------------------------ capital HP + rejections
        void SyncCapitals(BridgeState s) {
            if (!s.HasCapitals) return;
            foreach (var c in s.capitals) {
                if (c.seat < 0 || c.seat > 1) continue;
                model.CapitalMax[c.seat] = c.max_hp > 0 ? c.max_hp : model.CapitalMax[c.seat];
                int hp = c.destroyed ? 0 : c.hp;
                int delta = hp - model.CapitalHp[c.seat];
                // Events normally move the number first; this catches anything they did not report.
                if (capitalsSynced && delta != 0) {
                    var cap = PieceOf(c.instance_id) ?? CapitalOf(c.seat);
                    if (cap != null) vfx.FloatText(Chest(cap) + Vector3.up * .5f, (delta > 0 ? "+" : "") + delta,
                        delta > 0 ? new Color(.35f, 1f, .55f) : new Color(1f, .5f, .2f), speed);
                    model.AddLog($"{Faction(c.seat)} Capital HP {model.CapitalHp[c.seat]} -> {hp}");
                }
                model.CapitalHp[c.seat] = hp;
            }
            capitalsSynced = true;
        }
        public string LastRejection { get; private set; }
        void ShowRejection(string error) {
            var msg = string.IsNullOrEmpty(error) ? "The rules engine rejected that action." : error;
            const string prefix = "engine rejected action: ";
            if (msg.StartsWith(prefix)) msg = msg.Substring(prefix.Length);
            LastRejection = msg;
            liveStatus = "Not allowed: " + msg;
            Banner("Not allowed - " + msg, 4f);
            model.AddLog("Rejected: " + msg);
            sfx.PlayUi("error");
        }
        // Clicking one of your pieces that has nothing legal: say why (used, GP, line of sight, ...).
        bool ExplainBlockedSource(Piece hp) {
            var entries = Abilities.Where(a => a.x == hp.X && a.y == hp.Y && !a.Ready).ToArray();
            if (entries.Length == 0) return false;
            var a = entries.FirstOrDefault(e => e.instance_id == hp.InstanceId) ?? entries[0];
            SelectPiece(hp); liveSelected = null; ResetActionChoices();
            var target = AbilityTarget(a);
            board.SetLegal(null, target.HasValue ? new[] { target.Value } : null);
            liveStatus = a.name + ": " + a.reason + ".";
            sfx.PlayUi("error");
            return true;
        }
        Vector2Int? AbilityTarget(BridgeAbility a) {
            if (liveState == null || !liveState.HasCapitals || a == null) return null;
            int seat = a.effect_type == "DAMAGE_ENEMY_CAPITAL" ? 1 - humanSeat : a.effect_type == "HEAL_CAPITAL" ? humanSeat : -1;
            if (seat < 0) return null;
            var c = liveState.capitals.FirstOrDefault(k => k.seat == seat);
            return c == null || c.destroyed ? (Vector2Int?)null : new Vector2Int(c.x, c.y);
        }

        // ------------------------------------------------------------------ ability command center
        float CommandPanelBottom(float H) {
            float cw = Mathf.Clamp(H * .15f, 96, 132), ch = cw * 1.39f, lift = cw * .2f;
            bool handShown = liveState?.players != null && liveState.players.Length > humanSeat && (liveState.players[humanSeat].hand?.Length ?? 0) > 0;
            return H - 52 - 12 - (handShown ? ch + lift + 10 : 0);
        }
        void DrawCommandCenter(float W, float H) {
            commandRect = Rect.zero;
            if (mode != Mode.Live || liveState == null || !liveState.HasCapitals) return;
            var list = Abilities;
            int ready = list.Count(a => a.Ready);
            float top = 222, width = CommandPanelWidth(W);
            if (!commandOpen) {
                commandRect = new Rect(10, top, Mathf.Min(230,width), 30);
                if (GUI.Button(commandRect, $"Abilities Open  ({ready} ready / {list.Length})", buttonStyle)) { commandOpen = true; sfx.PlayUi("click"); }
                return;
            }
            float bottom = Mathf.Max(top + 120, CommandPanelBottom(H));
            commandRect = new Rect(10, top, width, bottom - top);
            GUI.Box(commandRect, "", panelStyle);
            GUI.Label(new Rect(commandRect.x + 10, top + 4, width - 70, 24), width<280?$"<b>Abilities</b> · {ready} ready":$"<b>Abilities</b>  <size=12>{ready} ready  |  {list.Length} sources</size>", labelStyle);
            if (GUI.Button(new Rect(commandRect.xMax - 56, top + 4, 48, 22), "Hide", buttonStyle)) { commandOpen = false; sfx.PlayUi("click"); return; }
            var area = new Rect(commandRect.x + 4, top + 32, width - 8, commandRect.height - 36);
            if (list.Length == 0) {
                GUI.Label(new Rect(area.x + 6, area.y, area.width - 12, 60), "None of your lands or structures on the board has an activated ability yet.", smallStyle);
                return;
            }
            // Rows grow to fit their wrapped effect and reason text, so long effects stay readable on small screens.
            float rw = area.width - 20, tw = rw - 12;
            string EffectLine(BridgeAbility a) => $"{a.effect}  |  <b>{a.gp_cost} GP</b>";
            string StatusLine(BridgeAbility a) => a.Ready ? "<color=#7dff9a>Ready</color>" : a.status == "USED" ? "<color=#a0a8b8>Used this turn</color>" : "<color=#ffb070>" + a.reason + "</color>";
            float EffectH(BridgeAbility a) => Mathf.Max(16, smallStyle.CalcHeight(new GUIContent(EffectLine(a)), tw));
            float StatusH(BridgeAbility a) => Mathf.Max(16, smallStyle.CalcHeight(new GUIContent(StatusLine(a)), tw));
            float RowH(BridgeAbility a) => 24 + EffectH(a) + 2 + StatusH(a) + 6;
            float contentH = list.Sum(a => RowH(a) + 4 + (a.instance_id == commandSelected && a.Ready ? 36 : 0));
            commandScroll = GUI.BeginScrollView(area, commandScroll, new Rect(0, 0, area.width - 18, Mathf.Max(area.height, contentH)));
            float y = 0;
            foreach (var a in list) {
                bool isSel = a.instance_id == commandSelected;
                float rowH = RowH(a), effectH = EffectH(a), statusH = StatusH(a);
                float h = rowH + (isSel && a.Ready ? 36 : 0);
                var row = new Rect(0, y, rw, h);
                var tint = isSel ? new Color(.35f, .5f, .8f, .45f) : a.Ready ? new Color(.15f, .4f, .2f, .35f) : new Color(.2f, .2f, .25f, .35f);
                GUI.DrawTexture(row, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, tint, 0, 0);
                string where = $"({a.x},{a.y}){(a.buried ? "  |  buried" : "")}";
                GUI.Label(new Rect(6, y + 2, tw, 22), $"<b>{a.name}</b>  <size=11>{where}</size>", labelStyle);
                GUI.Label(new Rect(6, y + 24, tw, effectH), EffectLine(a), smallStyle);
                GUI.Label(new Rect(6, y + 26 + effectH, tw, statusH), StatusLine(a), smallStyle);
                if (GUI.Button(new Rect(0, y, rw, rowH), "", GUIStyle.none)) CommandCenterPick(a);
                if (isSel && a.Ready) {
                    GUI.enabled = !waiting && !paused;
                    if (GUI.Button(new Rect(6, y + rowH + 2, rw * .62f, 30), $"Activate ({a.gp_cost} GP)", buttonStyle)) CommandCenterConfirm();
                    if (GUI.Button(new Rect(rw * .62f + 12, y + rowH + 2, rw * .38f - 18, 30), "Cancel", buttonStyle)) { commandSelected = null; ClearSelection(); }
                    GUI.enabled = true;
                }
                y += h + 4;
            }
            GUI.EndScrollView();
        }
        // Selects that exact card instance (buried sources included) and highlights it and its target.
        void CommandCenterPick(BridgeAbility a) {
            if (a == null || waiting) return;
            commandSelected = a.instance_id;
            var piece = PieceOf(a.instance_id);
            var target = AbilityTarget(a);
            var source = new Vector2Int(a.x, a.y);
            if (a.Ready) {
                var action = liveActions.FirstOrDefault(x => x.id == a.action_id && x.instance_id == a.instance_id);
                if (action == null) { liveStatus = a.name + " is no longer available."; sfx.PlayUi("error"); return; }
                if (piece != null) SelectPiece(piece); else board.Selected = source;
                SelectLive(a.instance_id);
                actionChoices = new[] { action };
                choicePrompt = $"Activate {a.name} at ({a.x},{a.y})?";
                board.SetLegal(new[] { source }, target.HasValue ? new[] { target.Value } : null);
                liveStatus = $"{a.name}: {a.effect}. Confirm with Activate ({a.gp_cost} GP).";
            } else {
                ClearSelection(); commandSelected = a.instance_id;
                if (piece != null) SelectPiece(piece); else { board.Selected = source; board.Refresh(); }
                board.SetLegal(null, target.HasValue ? new[] { target.Value } : null);
                liveStatus = a.name + ": " + (a.status == "USED" ? "already used this turn" : a.reason) + ".";
                sfx.PlayUi("error");
            }
        }
        void CommandCenterConfirm() {
            var a = Abilities.FirstOrDefault(x => x.instance_id == commandSelected && x.Ready);
            var action = a == null ? null : liveActions.FirstOrDefault(x => x.id == a.action_id && x.instance_id == a.instance_id);
            if (action == null) { liveStatus = "That ability is no longer available."; sfx.PlayUi("error"); return; }
            commandSelected = null;
            ActLive(action);
        }
    }
}
