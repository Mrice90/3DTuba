using System;
using System.Collections.Generic;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    [Serializable] public sealed class CardFace {
        public string id, name, type, faction, rulesText, description, art;
        public int cost, attack, defense, range, movement, hitPoints, rarity;
        public string[] keywords;
    }
    [Serializable] sealed class CardFaceFile { public string source; public CardFace[] cards; }

    // AI-105: card stats, rules text and art for the hand, the full card view and the play pop-up.
    // Written by UnityProof/Tools/stage_card_faces.py from the pinned alpha jar. Cards without an
    // entry (the engine's generated tutor cards) get a name/type-only face from the catalog.
    public static class CardFaces {
        static Dictionary<string, CardFace> byId;
        static readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        public static int Count { get { Load(); return byId.Count; } }

        static void Load() {
            if (byId != null) return;
            byId = new Dictionary<string, CardFace>();
            var text = Resources.Load<TextAsset>("Playtest/card-faces");
            var file = text != null ? JsonUtility.FromJson<CardFaceFile>(text.text) : null;
            if (file?.cards != null) foreach (var c in file.cards) byId[c.id] = c;
        }

        public static CardFace Get(string id) {
            Load();
            if (id != null && byId.TryGetValue(id, out var f)) return f;
            var c = PlaytestCatalog.Get(id);
            f = new CardFace { id = c.id, name = c.name, type = c.type, faction = c.faction, rulesText = "", description = "", art = "", keywords = new string[0] };
            if (id != null) byId[id] = f;
            return f;
        }

        public static Texture2D Art(string id) {
            var f = Get(id);
            if (string.IsNullOrEmpty(f.art)) return null;
            if (!art.TryGetValue(f.art, out var t)) { t = Resources.Load<Texture2D>(f.art); art[f.art] = t; }
            return t;
        }

        public static string StatLine(CardFace f) {
            switch (f.type) {
                case "CHARACTER": return $"ATK {f.attack}   DEF {f.defense}   RNG {f.range}   MOV {f.movement}";
                case "CAPITAL": return f.hitPoints > 0 ? $"HP {f.hitPoints}" : "";
                case "STRUCTURE": return f.defense > 0 || f.attack > 0 ? $"ATK {f.attack}   DEF {f.defense}" : "";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ IMGUI drawing
        static GUIStyle name, small, body, cost;
        static void Styles() {
            if (name != null) return;
            name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
            name.normal.textColor = Color.white;
            small = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true }; small.normal.textColor = new Color(.85f, .9f, .97f);
            body = new GUIStyle(small) { fontStyle = FontStyle.Normal }; body.normal.textColor = new Color(.92f, .94f, .98f);
            cost = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; cost.normal.textColor = Color.black;
        }

        // Draws a card face in r. compact = hand-sized (name, art, cost, stats); otherwise the full card.
        public static void Draw(Rect r, string id, bool compact, bool highlight = false, bool dim = false) {
            Styles();
            var f = Get(id);
            var fc = PlaytestCatalog.FactionColor(f.faction);
            var dark = PlaytestCatalog.FactionDark(f.faction);
            var prev = GUI.color;
            if (dim) GUI.color = new Color(1, 1, 1, .55f);
            Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), highlight ? Color.Lerp(fc, Color.white, .45f) : fc);
            Fill(r, Color.Lerp(dark, Color.black, .35f));
            float pad = compact ? 5 : 10;
            int ns = compact ? Mathf.Clamp((int)(r.width / 11f), 9, 12) : 20;
            name.fontSize = ns; small.fontSize = compact ? 10 : 13; body.fontSize = compact ? 10 : 14; cost.fontSize = compact ? 13 : 20;
            // cost gem
            float gem = compact ? 22 : 36;
            Fill(new Rect(r.x + pad - 1, r.y + pad - 1, gem + 2, gem + 2), Color.black);
            Fill(new Rect(r.x + pad, r.y + pad, gem, gem), Color.Lerp(fc, Color.white, .25f));
            GUI.Label(new Rect(r.x + pad, r.y + pad, gem, gem), f.cost.ToString(), cost);
            float nameH = compact ? r.width * .27f : 46;
            GUI.Label(new Rect(r.x + pad + gem + 4, r.y + pad - 2, r.width - gem - pad * 2 - 4, nameH), f.name, name);
            // art
            float artTop = r.y + pad + Mathf.Max(gem, nameH) + 2;
            float artH = compact ? r.height * .48f : r.height * .45f;
            var artRect = new Rect(r.x + pad, artTop, r.width - pad * 2, artH);
            var tex = Art(id);
            if (tex != null) GUI.DrawTexture(artRect, tex, ScaleMode.ScaleAndCrop);
            else { Fill(artRect, Color.Lerp(dark, fc, .25f)); GUI.Label(artRect, "<i>art pending</i>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }); }
            float y = artRect.yMax + 3;
            var typeLine = Cap(f.type) + (f.keywords != null && f.keywords.Length > 0 ? " · " + string.Join(", ", Array.ConvertAll(f.keywords, Cap)) : "");
            GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2, compact ? 16 : 22), $"<b>{typeLine}</b>", small); y += compact ? 15 : 22;
            var stats = compact && f.type == "CHARACTER" ? $"A{f.attack}  D{f.defense}  R{f.range}  M{f.movement}" : StatLine(f);
            if (stats.Length > 0) { GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2, compact ? 16 : 22), stats, small); y += compact ? 16 : 24; }
            if (!compact) {
                var text = f.rulesText ?? "";
                if (f.description != null && f.description.Length > 0 && f.description != text) text += (text.Length > 0 ? "\n\n" : "") + "<i>" + f.description + "</i>";
                GUI.Label(new Rect(r.x + pad, y + 2, r.width - pad * 2, r.yMax - y - pad), text, body);
            }
            GUI.color = prev;
        }
        static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant().Replace('_', ' ');
        static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, c, 0, 0);
    }
}
