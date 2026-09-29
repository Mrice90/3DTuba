using System;
using System.Collections.Generic;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    [Serializable] public sealed class CardEntry {
        public string id, name, faction, type, model, modelSource;
        public string[] sfx;
        public bool HasModel => !string.IsNullOrEmpty(model);
        public bool HasCue(string cue) => sfx != null && Array.IndexOf(sfx, cue) >= 0;
    }
    [Serializable] sealed class CatalogFile { public string source; public CardEntry[] cards; }

    // The 139-card catalog (presentation-manifest.json, AI-064) plus which cards have staged media.
    // Written by UnityProof/Tools/stage_playtest_assets.py into Resources/Playtest/cards.json.
    public static class PlaytestCatalog {
        static Dictionary<string, CardEntry> byId;
        static CardEntry[] all;
        public static CardEntry[] All { get { Load(); return all; } }
        public static void Load() {
            if (byId != null) return;
            var text = Resources.Load<TextAsset>("Playtest/cards");
            var file = text != null ? JsonUtility.FromJson<CatalogFile>(text.text) : null;
            all = file?.cards ?? new CardEntry[0];
            byId = new Dictionary<string, CardEntry>();
            foreach (var c in all) byId[c.id] = c;
        }
        // Unknown ids (e.g. the engine's demo capitals) still get a typed stand-in.
        public static CardEntry Get(string id) {
            Load();
            if (id != null && byId.TryGetValue(id, out var c)) return c;
            string faction = id != null && id.StartsWith("poseidon") ? "POSEIDON" : "ZEUS";
            string type = id != null && id.Contains("capital") ? "CAPITAL" : "CHARACTER";
            return new CardEntry { id = id ?? "unknown", name = Pretty(id), faction = faction, type = type, model = "", sfx = new string[0] };
        }
        public static string Pretty(string id) {
            if (string.IsNullOrEmpty(id)) return "?";
            var parts = id.Split('_');
            var words = new List<string>();
            for (int i = 1; i < parts.Length; i++) {
                if (parts[i] == "ability" || parts[i] == "keyword" || parts[i] == "apex" || parts[i] == "sharp" || parts[i] == "siege" || parts[i] == "structure") continue;
                words.Add(char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1));
            }
            return string.Join(" ", words);
        }

        // --- look ---
        public static Color FactionColor(string faction) =>
            faction == "POSEIDON" ? new Color(.10f, .72f, .82f) : new Color(.98f, .74f, .22f);
        public static Color FactionAccent(string faction) =>
            faction == "POSEIDON" ? new Color(.35f, .95f, 1f) : new Color(.72f, .58f, 1f);
        public static Color FactionDark(string faction) =>
            faction == "POSEIDON" ? new Color(.05f, .20f, .30f) : new Color(.30f, .22f, .10f);

        // --- AI-063 board-scale budgets ---
        // The contract measures heights in the same units as check_glb.py's 1.8-unit character; a hex
        // across the flats is taken as 2.0 of those units, so a character stands 0.9 of a hex tall.
        public static float ContractUnit => BoardLayout.TileSize / 2f;
        public static void Budget(string type, out float height, out float footprint) {
            float cu = ContractUnit, hex = BoardLayout.TileSize;
            switch (type) {
                case "LAND": height = .25f * cu; footprint = 1f * hex; break;
                case "STRUCTURE": height = 1.6f * cu; footprint = .9f * hex; break;
                case "CAPITAL": height = 2.2f * cu; footprint = 1f * hex; break;
                default: height = 1.8f * cu; footprint = .8f * hex; break;
            }
        }
    }
}
