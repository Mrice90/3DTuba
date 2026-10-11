using System;
using System.Collections.Generic;
using UnityEngine;
using EngineBoard = InfiniteConquest.RulesCore.Core.BoardPosition;

namespace InfiniteConquest.Proof {
    public enum BoardShape { Rectangle, Hexagon }

    // AI-110: the one place the client's board size and shape live.
    //
    // Rules* is the grid the rules engine plays on. It comes straight from the native RulesCore
    // (BoardPosition.Width / Height / PlotHeight), so when the engine changes size the client follows on
    // the next build with no other edits. Planned* is the 6 x 8 rectangle Matt picked on 2026-10-11 (four
    // home rows a side); Hex* is the hexagon-shaped alternative he asked to see (radius 3 = 37 hexes, a
    // neutral middle row, 15 home hexes a side). ENGINE-HANDOFF-6x8.md lists the engine side of both.
    //
    // The drawn board defaults to the rules grid. "-board 6x8" (or "planned") and "-board hex" draw the
    // previews: the engine's grid sits inside the drawn shape and stays the only playable area; the other
    // cells are drawn but inert until the engine plays that board.
    public static class BoardConfig {
        public const int RulesWidth = EngineBoard.Width, RulesHeight = EngineBoard.Height, RulesPlotHeight = EngineBoard.PlotHeight;
        public const int PlannedWidth = 6, PlannedHeight = 8, PlannedPlotHeight = 4;
        // Hexagon preview: every cell within HexRadius of HexCenter (rules coordinates). Radius 3 is the
        // hexagon closest to 48 cells (37; radius 4 is 61) and, centred on (1,3), it contains the whole 4 x 6
        // rules grid, so the engine can still play inside it.
        public const int HexRadius = 3;
        public static readonly Vector2Int HexCenter = new Vector2Int(1, 3);

        public static BoardShape Shape { get; private set; } = BoardShape.Rectangle;
        // Bounding size of the drawn board, in cells.
        public static int Width { get; private set; } = RulesWidth;
        public static int Height { get; private set; } = RulesHeight;
        public static int PlotHeight { get; private set; } = RulesPlotHeight;
        // Rectangle: drawn cell of rules cell (0,0); the rules grid sits centred in a larger drawn board.
        public static Vector2Int Offset { get; private set; }
        // Every drawn cell, in rules coordinates (cells outside the rules grid are preview-only).
        public static IReadOnlyList<Vector2Int> DrawnCells => drawn;
        static readonly List<Vector2Int> drawn = new List<Vector2Int>();
        static Vector2 centroid;   // raw layout units, see BoardLayout.CellCenter
        public static Vector2 RawCentroid => centroid;

        public static bool IsPreview => Shape != BoardShape.Rectangle || Width != RulesWidth || Height != RulesHeight;
        public static int CellCount => drawn.Count;
        public static string Label => Shape == BoardShape.Hexagon ? "hex" + drawn.Count : Width + "x" + Height;

        static BoardConfig() { UseRules(); }

        public static void Use(int width, int height, int plotHeight) {
            if (width < RulesWidth || height < RulesHeight)
                throw new ArgumentException($"The drawn board {width}x{height} must contain the rules grid {RulesWidth}x{RulesHeight}");
            Shape = BoardShape.Rectangle;
            Width = width; Height = height; PlotHeight = Mathf.Clamp(plotHeight, 1, height - 1);
            Offset = new Vector2Int((width - RulesWidth) / 2, (height - RulesHeight) / 2);
            drawn.Clear();
            for (int dx = 0; dx < width; dx++) for (int dy = 0; dy < height; dy++) drawn.Add(new Vector2Int(dx - Offset.x, dy - Offset.y));
            Recentre();
        }
        public static void UseRules() => Use(RulesWidth, RulesHeight, RulesPlotHeight);
        public static void UsePlanned() => Use(PlannedWidth, PlannedHeight, PlannedPlotHeight);
        public static void UseHexagon() {
            Shape = BoardShape.Hexagon; drawn.Clear();
            int minX = int.MaxValue, maxX = int.MinValue;
            for (int y = HexCenter.y - HexRadius; y <= HexCenter.y + HexRadius; y++)
                for (int x = HexCenter.x - HexRadius - 1; x <= HexCenter.x + HexRadius + 1; x++)
                    if (MovementState.HexDistance(HexCenter.x, HexCenter.y, x, y) <= HexRadius) {
                        drawn.Add(new Vector2Int(x, y)); minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    }
            Width = maxX - minX + 1; Height = 2 * HexRadius + 1; PlotHeight = HexRadius;
            Offset = new Vector2Int(-minX, HexRadius - HexCenter.y);
            Recentre();
        }
        static void Recentre() {
            var sum = Vector2.zero;
            foreach (var c in drawn) sum += new Vector2(c.x + .5f * (c.y & 1), c.y);
            centroid = sum / drawn.Count;
        }

        // "-board 6x8", "-board planned", "-board hex" or "-board rules". Anything else keeps the rules grid.
        public static void ApplyCommandLine(string[] args) {
            int i = Array.IndexOf(args, "-board");
            string v = i >= 0 && i + 1 < args.Length ? args[i + 1].Trim().ToLowerInvariant() : null;
            if (v == null || v == "rules") { UseRules(); return; }
            if (v == "planned") { UsePlanned(); return; }
            if (v == "hex" || v == "hexagon") { UseHexagon(); return; }
            var p = v.Split('x');
            if (p.Length == 2 && int.TryParse(p[0], out var w) && int.TryParse(p[1], out var h) && w >= RulesWidth && h >= RulesHeight) {
                Use(w, h, w == PlannedWidth && h == PlannedHeight ? PlannedPlotHeight : h / 2);
                return;
            }
            Debug.LogWarning("BOARD ignoring -board " + v + "; using the rules grid " + RulesWidth + "x" + RulesHeight);
            UseRules();
        }

        public static bool InRules(int x, int y) => x >= 0 && y >= 0 && x < RulesWidth && y < RulesHeight;
        // Home side of a drawn cell (rules coordinates): 0 = Zeus, 1 = Poseidon, -1 = neutral (hexagon middle row).
        public static int HomeSide(int x, int y) {
            if (Shape == BoardShape.Hexagon) return y < HexCenter.y ? 0 : y > HexCenter.y ? 1 : -1;
            return y + Offset.y < PlotHeight ? 0 : 1;
        }
    }
}
