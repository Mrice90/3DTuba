using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    [Serializable] public sealed class CardFace {
        public string id, name, type, faction, rulesText, description, art;
        public int cost, developmentGoldCost, attack, defense, range, movement, hitPoints, rarity, gpGeneration, purchasePrice, slotSupply, slotCost;
        public string[] keywords;
    }
    [Serializable] sealed class CardFaceFile { public string source; public CardFace[] cards; }

    // AI-105: card stats, rules text and art for the hand, the full card view and the play pop-up.
    // Written by UnityProof/Tools/stage_card_faces.py from the pinned alpha jar. Cards without an
    // entry (the engine's generated tutor cards) get a name/type-only face from the catalog.
    public static class CardFaces {
        public static bool PurchaseMode;
        public static int PurchasePrice(CardFace f) => f.purchasePrice > 0 ? f.purchasePrice : f.type == "LAND" ? 1 : f.type == "STRUCTURE" ? Mathf.Max(2, f.developmentGoldCost) : Mathf.Max(1,f.cost);
        static readonly Dictionary<string,int> purchasePrices = new Dictionary<string,int>();
        public static void SetPurchasePrice(string id,int value) { if(id!=null) purchasePrices[id]=value; }
        static Dictionary<string, CardFace> byId;
        static readonly Dictionary<string, Texture2D> art = new Dictionary<string, Texture2D>();
        public static int Count { get { Load(); return byId.Count; } }
        public static IEnumerable<CardFace> All { get { Load(); return byId.Values; } }

        static void Load() {
            if (byId != null) return;
            byId = new Dictionary<string, CardFace>();
            var text = Resources.Load<TextAsset>("Playtest/card-faces");
            var file = text != null ? JsonUtility.FromJson<CardFaceFile>(text.text) : null;
            if (file?.cards != null) foreach (var c in file.cards) { c.rulesText = WithLineOfSight(CleanText(c.rulesText)); c.name=CleanText(c.name); c.description=CleanText(c.description); byId[c.id] = c; }
        }
        public static string CleanText(string text)=>(text??"").Replace("\u00e2\u20ac\u201d","-");

        // GameEngine/ActionHints require a clear sight line for ACTIVATED DAMAGE_ENEMY_CAPITAL abilities (Storm Relay
        // Pylon, Eagle's Perch Array). The staged faces omit it, so add it to the display text. Death triggers
        // (Keraunos Charging Spire) have no sight check in the engine and are left alone. Display only; no rule change.
        public const string LineOfSightNote = "Needs line of sight to the enemy Capital.";
        public static string WithLineOfSight(string rules) {
            if (string.IsNullOrEmpty(rules) || rules.Contains("line of sight")) return rules;
            int i = rules.IndexOf("damage to the enemy Capital", StringComparison.Ordinal);
            if (i < 0 || rules.IndexOf("Activate", StringComparison.Ordinal) < 0 || rules.IndexOf("Activate", StringComparison.Ordinal) > i) return rules;
            return rules.TrimEnd() + " " + LineOfSightNote;
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
                case "CHARACTER": return $"ATK {f.attack}   DEF {f.defense}\nRNG {f.range}   MOV {f.movement}";
                case "CAPITAL": return f.hitPoints > 0 ? $"HP {f.hitPoints}" : "";
                case "LAND": return PurchaseMode ? $"HP {f.hitPoints}  |  +{f.gpGeneration} gold/turn" : $"TURN {f.cost}+  |  {f.developmentGoldCost} GP  |  1/type/turn";
                case "STRUCTURE": return PurchaseMode ? $"HP {f.hitPoints} | +{f.gpGeneration} gold/turn\nSummon slots +{f.slotSupply}" : $"TURN {f.cost}+  |  {f.developmentGoldCost} GP  |  1/type/turn";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ IMGUI drawing
        static GUIStyle name, small, body, cost, footer, pendingArt;
        static void Styles() {
            if (name != null) return;
            name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
            name.normal.textColor = new Color(.98f,.94f,.83f);
            name.richText = false;
            small = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true }; small.normal.textColor = new Color(.85f, .9f, .97f);
            body = new GUIStyle(small) { fontStyle = FontStyle.Normal }; body.normal.textColor = new Color(.92f, .94f, .98f);
            cost = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; cost.normal.textColor = Color.black;
            pendingArt = new GUIStyle(small) {alignment=TextAnchor.MiddleCenter};
            footer = new GUIStyle(small) {fontSize=10,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};footer.normal.textColor=new Color(.27f,.22f,.13f);
        }

        // Draws a card face in r. compact = hand-sized (name, art, cost, stats); otherwise the full card.
        public static void Draw(Rect r, string id, bool compact, bool highlight = false, bool dim = false) {
            Styles();
            var f = Get(id);
            var fc = PlaytestCatalog.FactionColor(f.faction);
            var dark = PlaytestCatalog.FactionDark(f.faction);
            var prev = GUI.color;
            if (dim) GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * .7f);
            CardFinish.Frame(r, fc, highlight);
            float pad = compact ? 7 : 13;
            int ns = compact ? Mathf.Clamp((int)(r.width / 11f), 9, 12) : 20;
            name.fontSize = ns; small.fontSize = compact ? 10 : 13; body.fontSize = compact ? 10 : 14; cost.fontSize = compact ? 13 : 20;
            // cost gem
            float gem = compact ? 22 : 36;
            CardFinish.Coin(new Rect(r.x + pad, r.y + pad, gem, gem));
            int price=purchasePrices.TryGetValue(id,out var actualPrice)?actualPrice:PurchasePrice(f);
            GUI.Label(new Rect(r.x + pad, r.y + pad, gem, gem), f.type=="CAPITAL" ? "C" : PurchaseMode ? price.ToString() : f.type == "LAND" || f.type == "STRUCTURE" ? "T" + f.cost : f.cost.ToString(), cost);
            float nameWidth=r.width-gem-pad*2-4;
            float nameH = Mathf.Max(gem,name.CalcHeight(new GUIContent(f.name),nameWidth));
            nameH=Mathf.Min(nameH,compact?r.height*.28f:r.height*.19f);
            GUI.Label(new Rect(r.x + pad + gem + 4, r.y + pad - 2, nameWidth, nameH), f.name, name);
            // art
            float artTop = r.y + pad + Mathf.Max(gem, nameH) + 2;
            float artH = compact ? r.height * .43f : r.height * .30f;
            var artRect = new Rect(r.x + pad, artTop, r.width - pad * 2, artH);
            var tex = Art(id);
            Fill(new Rect(artRect.x-2,artRect.y-2,artRect.width+4,artRect.height+4),new Color(.05f,.04f,.03f));
            Fill(new Rect(artRect.x-1,artRect.y-1,artRect.width+2,artRect.height+2),new Color(.7f,.59f,.36f));
            if (tex != null) GUI.DrawTexture(artRect, tex, ScaleMode.ScaleAndCrop);
            else { Fill(artRect, Color.Lerp(dark, fc, .25f)); GUI.Label(artRect, "<i>art pending</i>", pendingArt); }
            float y = artRect.yMax + 5;
            CardFinish.Round(new Rect(r.x+pad-2,y-1,r.width-pad*2+4,r.yMax-y-pad+1),new Color(.91f,.88f,.79f),true);
            small.normal.textColor = new Color(.16f,.18f,.2f); body.normal.textColor = new Color(.12f,.13f,.14f);
            var typeLine = Cap(f.type) + (!compact && f.keywords != null && f.keywords.Length > 0 ? "  |  " + string.Join(", ", Array.ConvertAll(f.keywords, Cap)) : "");
            float typeHeight=compact?16:Mathf.Max(20,small.CalcHeight(new GUIContent("<b>"+typeLine+"</b>"),r.width-pad*2));
            GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2, typeHeight), $"<b>{typeLine}</b>", small); y += compact ? 15 : typeHeight+2;
            var stats = compact && f.type == "CHARACTER" ? $"A{f.attack}  D{f.defense}  R{f.range}  M{f.movement}" : compact && PurchaseMode ? (f.type=="LAND"?"+"+f.gpGeneration+" gold/turn":f.type=="STRUCTURE"?"+"+f.gpGeneration+" gold | +"+f.slotSupply+" slots":"FREE TO PLAY") : StatLine(f);
            if (stats.Length > 0) {float statHeight=compact?16:Mathf.Max(20,small.CalcHeight(new GUIContent(stats),r.width-pad*2)); GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2, statHeight), stats, small); y += statHeight+2; }
            if (!compact) {
                var text = DisplayRules(f,PurchaseMode);
                if (PurchaseMode && f.type == "CHARACTER") text = "Cannot attack on its summon turn.\n" + text;
                string mechanics=text;
                if (f.description != null && f.description.Length > 0 && f.description != f.rulesText) text += (text.Length > 0 ? "\n\n" : "") + "<i>" + f.description + "</i>";
                float footerHeight=PurchaseMode && f.type!="CAPITAL"?16:0;
                float space=Mathf.Max(0,r.yMax-y-pad-footerHeight);
                while(body.fontSize>9 && body.CalcHeight(new GUIContent(text),r.width-pad*2)>space)body.fontSize--;
                if(mechanics.Length>0 && body.CalcHeight(new GUIContent(text),r.width-pad*2)>space) {
                    text=mechanics;body.fontSize=14;
                    while(body.fontSize>9 && body.CalcHeight(new GUIContent(text),r.width-pad*2)>space)body.fontSize--;
                }
                GUI.Label(new Rect(r.x + pad, y + 2, r.width - pad * 2, space), text, body);
                if(footerHeight>0) GUI.Label(new Rect(r.x+pad,r.yMax-pad-footerHeight,r.width-pad*2,footerHeight),"BUY ONCE  |  FREE PLAY",footer);
            }
            GUI.color = prev;
        }
        public static string DisplayRules(CardFace f,bool purchase) {
            string text=f.rulesText??"";
            foreach(var keyword in f.keywords??new string[0]) {
                string printed=Cap(keyword);
                if(text.Trim().Equals(printed,StringComparison.OrdinalIgnoreCase))text="";
                else if(text.StartsWith(printed+".",StringComparison.OrdinalIgnoreCase))text=text.Substring(printed.Length+1).TrimStart();
            }
            if(purchase && (f.type=="LAND" || f.type=="STRUCTURE")) {
                text=Regex.Replace(text,@"\bTurn \d+\.\s*","");
                text=Regex.Replace(text,@"Generates \d+ GP(?: each turn)?\.\s*","");
            }
            return text.Trim();
        }
        static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : s.Substring(0, 1).ToUpperInvariant() + s.Substring(1).ToLowerInvariant().Replace('_', ' ');
        static void Fill(Rect r, Color c) => GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, c, 0, 0);
    }
}

