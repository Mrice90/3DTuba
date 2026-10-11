using System;
using UnityEngine;
using EngineBoard = InfiniteConquest.RulesCore.Core.BoardPosition;

namespace InfiniteConquest.Proof {
    // AI-110: the one place the client's board size lives.
    //
    // Rules* is the grid the rules engine plays on. It comes straight from the native RulesCore
    // (BoardPosition.Width / Height / PlotHeight), so when the engine changes size the client follows on
    // the next build with no other edits. Planned* is the next board Matt picked on 2026-10-11:
    // 6 x 8 with four home rows per side (ENGINE-HANDOFF-6x8.md lists the engine side of that change).
    //
    // The drawn board (Width / Height / PlotHeight) defaults to the rules grid. "-board 6x8" (or
    // "-board planned") draws the planned board as a preview: the engine's grid sits centred inside it and
    // stays the only playable area; the outer ring is drawn but inert until the engine plays 6 x 8.
    public static class BoardConfig {
        public const int RulesWidth = EngineBoard.Width, RulesHeight = EngineBoard.Height, RulesPlotHeight = EngineBoard.PlotHeight;
        public const int PlannedWidth = 6, PlannedHeight = 8, PlannedPlotHeight = 4;

        public static int Width { get; private set; } = RulesWidth;
        public static int Height { get; private set; } = RulesHeight;
        public static int PlotHeight { get; private set; } = RulesPlotHeight;
        // Drawn cell of rules cell (0,0).
        public static Vector2Int Offset { get; private set; }
        public static bool IsPreview => Width != RulesWidth || Height != RulesHeight;
        public static int CellCount => Width * Height;
        public static string Label => Width + "x" + Height;

        public static void Use(int width, int height, int plotHeight) {
            if (width < RulesWidth || height < RulesHeight)
                throw new ArgumentException($"The drawn board {width}x{height} must contain the rules grid {RulesWidth}x{RulesHeight}");
            Width = width; Height = height; PlotHeight = Mathf.Clamp(plotHeight, 1, height - 1);
            Offset = new Vector2Int((width - RulesWidth) / 2, (height - RulesHeight) / 2);
        }
        public static void UseRules() => Use(RulesWidth, RulesHeight, RulesPlotHeight);
        public static void UsePlanned() => Use(PlannedWidth, PlannedHeight, PlannedPlotHeight);

        // "-board 6x8", "-board planned" or "-board rules". Anything else keeps the rules grid.
        public static void ApplyCommandLine(string[] args) {
            int i = Array.IndexOf(args, "-board");
            string v = i >= 0 && i + 1 < args.Length ? args[i + 1].Trim().ToLowerInvariant() : null;
            if (v == null || v == "rules") { UseRules(); return; }
            if (v == "planned") { UsePlanned(); return; }
            var p = v.Split('x');
            if (p.Length == 2 && int.TryParse(p[0], out var w) && int.TryParse(p[1], out var h) && w >= RulesWidth && h >= RulesHeight) {
                Use(w, h, w == PlannedWidth && h == PlannedHeight ? PlannedPlotHeight : h / 2);
                return;
            }
            Debug.LogWarning("BOARD ignoring -board " + v + "; using the rules grid " + RulesWidth + "x" + RulesHeight);
            UseRules();
        }

        public static bool InRules(int x, int y) => x >= 0 && y >= 0 && x < RulesWidth && y < RulesHeight;
        // Home half of a drawn row: seat 0 (Zeus) owns the near PlotHeight rows.
        public static bool HomeOfSeat0(int drawnRow) => drawnRow < PlotHeight;
    }
}
