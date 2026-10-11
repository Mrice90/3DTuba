using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using InfiniteConquest.Playtest;
using InfiniteConquest.Proof;

// AI-110-BOARD-6x8-CLIENT: editor checks for the one board-size setting (BoardConfig).
// The rules grid must follow the engine and keep its exact old layout; the 6 x 8 preview must be a clean hex
// board with the rules grid centred inside it, engine adjacency intact, and the camera framing it at 1280x720.
// Unity -batchmode -quit -projectPath <board>/UnityProof -executeMethod BoardSizeChecks.Run
public static class BoardSizeChecks {
    static readonly List<string> checks = new List<string>();
    static readonly List<string> info = new List<string>();
    static void Check(bool ok, string name) { if (!ok) throw new Exception("BOARD_SIZE FAIL " + name); checks.Add(name); Debug.Log("BOARD_SIZE PASS " + name); }
    static void Info(string s) { info.Add(s); Debug.Log("BOARD_SIZE INFO " + s); }

    public static void Run() {
        checks.Clear(); info.Clear();
        try {
            Check(BoardConfig.RulesWidth == InfiniteConquest.RulesCore.Core.BoardPosition.Width
                  && BoardConfig.RulesHeight == InfiniteConquest.RulesCore.Core.BoardPosition.Height
                  && BoardConfig.RulesPlotHeight == InfiniteConquest.RulesCore.Core.BoardPosition.PlotHeight, "rules grid is read from the native engine");
            Check(BoardConfig.RulesWidth == 4 && BoardConfig.RulesHeight == 6 && BoardConfig.RulesPlotHeight == 3, "engine still plays 4x6 with 3 home rows");
            Check(MovementState.Width == BoardConfig.RulesWidth && MovementState.Height == BoardConfig.RulesHeight
                  && BoardLayout.Width == BoardConfig.RulesWidth && BoardLayout.Height == BoardConfig.RulesHeight
                  && BoardView.W == BoardConfig.RulesWidth && BoardView.H == BoardConfig.RulesHeight, "MovementState, BoardLayout and BoardView read the one setting");

            BoardConfig.ApplyCommandLine(new string[0]);
            Check(!BoardConfig.IsPreview && BoardConfig.Label == "4x6", "default (no -board) draws the rules grid");
            // The default layout is the exact pre-AI-110 formula, so the shipped board does not move.
            for (int x = 0; x < 4; x++) for (int y = 0; y < 6; y++) {
                float column = x + .5f * (y & 1) - 1.5f - .25f;
                var old = new Vector3(column * BoardLayout.Spacing, 0, (y - 2.5f) * BoardLayout.RowSpacing);
                if (Vector3.Distance(old, BoardLayout.CellCenter(x, y)) > 1e-5f) Check(false, $"default cell {x},{y} unchanged");
            }
            Check(true, "default 4x6 cell centres identical to the combined build (24 cells)");
            Check(Mathf.Abs(BoardLayout.ViewScale - 1) < 1e-5f && Mathf.Abs(BoardLayout.CameraScale - 1) < 1e-5f, "default camera/plinth scale is 1");
            Geometry("4x6");
            CapitalCells();

            foreach (var arg in new[] { "6x8", "planned", "PLANNED" }) {
                BoardConfig.ApplyCommandLine(new[] { "game.exe", "-board", arg });
                Check(BoardConfig.Width == 6 && BoardConfig.Height == 8 && BoardConfig.PlotHeight == 4, "-board " + arg + " draws 6x8 with 4 home rows");
            }
            BoardConfig.ApplyCommandLine(new[] { "game.exe", "-board", "3x3" });
            Check(!BoardConfig.IsPreview, "-board smaller than the rules grid falls back to the rules grid");
            BoardConfig.UsePlanned();
            Check(BoardConfig.CellCount == 48 && BoardConfig.Offset == new Vector2Int(1, 1), "6x8 preview: 48 cells, rules grid at drawn (1,1)");
            Info("ViewScale 6x8 = " + BoardLayout.ViewScale.ToString("0.000") + ", CameraScale " + BoardLayout.CameraScale.ToString("0.000"));
            Check(BoardLayout.ViewScale > 1.2f && BoardLayout.ViewScale < 1.6f, "6x8 camera/plinth scale is between 1.2 and 1.6");
            for (int y = 0; y < BoardConfig.RulesHeight; y++) {
                bool zeus = y < BoardConfig.RulesPlotHeight;
                if (BoardConfig.HomeOfSeat0(y + BoardConfig.Offset.y) != zeus) Check(false, "rules row " + y + " keeps its home side in the preview");
            }
            Check(true, "every rules row keeps its faction home side inside the 6x8 home halves");
            Geometry("6x8");
            CapitalCells();
            BoardViewTiles();
            CameraFraming();
            CameraSnapChecks.Run(); Check(true, "camera snap suite passes on the 6x8 preview");
        } finally { BoardConfig.UseRules(); }
        CameraSnapChecks.Run(); Check(true, "camera snap suite passes on the rules grid");
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/board-size-validation.json", "{\"passed\":true,\"checks\":" + checks.Count + ",\"info\":[" + string.Join(",", info.Select(s => "\"" + s.Replace("\"", "'") + "\"")) + "]}");
    }

    // Drawn cells form a clean hex board and rules adjacency equals world adjacency.
    static void Geometry(string label) {
        var drawn = new List<Vector3>();
        for (int dx = 0; dx < BoardConfig.Width; dx++) for (int dy = 0; dy < BoardConfig.Height; dy++) {
            var r = BoardLayout.FromDrawn(dx, dy); drawn.Add(BoardLayout.CellCenter(r.x, r.y));
        }
        float min = float.MaxValue;
        for (int i = 0; i < drawn.Count; i++) for (int j = i + 1; j < drawn.Count; j++) min = Mathf.Min(min, Vector3.Distance(drawn[i], drawn[j]));
        Check(Mathf.Abs(min - BoardLayout.Spacing) < .01f, label + " no overlapping tiles (closest centres one hex apart)");
        var c = drawn.Aggregate(Vector3.zero, (a, b) => a + b) / drawn.Count;
        Check(c.magnitude < .01f, label + " drawn board centred on the origin");
        int pairs = 0;
        for (int ax = 0; ax < BoardConfig.RulesWidth; ax++) for (int ay = 0; ay < BoardConfig.RulesHeight; ay++)
            for (int bx = 0; bx < BoardConfig.RulesWidth; bx++) for (int by = 0; by < BoardConfig.RulesHeight; by++) {
                bool engine = MovementState.HexDistance(ax, ay, bx, by) == 1;
                bool world = Mathf.Abs(Vector3.Distance(BoardLayout.CellCenter(ax, ay), BoardLayout.CellCenter(bx, by)) - BoardLayout.Spacing) < .01f;
                if (engine != world) Check(false, $"{label} adjacency {ax},{ay}-{bx},{by} engine={engine} world={world}");
                if (engine) pairs++;
            }
        Check(pairs > 0, label + " engine adjacency equals on-screen adjacency for all " + pairs / 2 + " neighbour pairs");
        var rc = Enumerable.Range(0, BoardConfig.RulesWidth * BoardConfig.RulesHeight)
            .Select(i => BoardLayout.CellCenter(i % BoardConfig.RulesWidth, i / BoardConfig.RulesWidth)).Aggregate(Vector3.zero, (a, b) => a + b) / (BoardConfig.RulesWidth * BoardConfig.RulesHeight);
        Check(Mathf.Abs(rc.z) < .01f && Mathf.Abs(rc.x) < BoardLayout.Spacing * .5f, label + " rules grid centred in the drawn board");
    }

    static void CapitalCells() {
        int z = 0, p = 0;
        for (int x = -1; x <= BoardConfig.Width; x++) for (int y = -1; y <= BoardConfig.Height; y++) {
            if (PlaytestGame.ValidCapitalCell(0, new Vector2Int(x, y))) z++;
            if (PlaytestGame.ValidCapitalCell(1, new Vector2Int(x, y))) p++;
        }
        Check(z == 12 && p == 12, BoardConfig.Label + " capital placement stays on the engine's 12 + 12 home cells");
    }

    static void BoardViewTiles() {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), font);
        var go = new GameObject("Board size check"); var board = go.AddComponent<BoardView>();
        try {
            board.Build(factory);
            var cells = go.GetComponentsInChildren<ProofCell>(true);
            Check(cells.Length == 24 && cells.All(c => BoardConfig.InRules(c.X, c.Y)), "6x8 preview: 24 input cells, all on the rules grid");
            Check(board.PreviewTileCount == 24, "6x8 preview: 24 inert preview tiles");
            Check(go.GetComponentsInChildren<MeshCollider>(true).Length == 48, "6x8 preview: 48 tiles drawn");
            var plinth = go.transform.Find("Board plinth").GetComponent<MeshFilter>().sharedMesh.bounds;
            Info("6x8 plinth extents " + plinth.extents.ToString("0.00"));
            Check(plinth.extents.x > 5f, "plinth grew with the board");
        } finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // At 1280x720 the live viewport is the top 76% of the screen (the hand and End turn sit below it).
    // Overview and top-down must show all 48 drawn cells; both seats' home views must show every rules cell.
    static void CameraFraming() {
        var go = new GameObject("Board size camera"); var cam = go.AddComponent<Camera>(); var rig = go.AddComponent<CameraRig>(); rig.InputEnabled = false;
        try {
            cam.fieldOfView = 38; cam.aspect = 1280f / (720f * .76f);
            var zeusCap = BoardLayout.CellCenter(1, 0); var poseidonCap = BoardLayout.CellCenter(2, 5);
            foreach (int seat in new[] { 0, 1 }) {
                string s = seat == 0 ? "Zeus" : "Poseidon";
                rig.SetHome(new Vector3(0, 0, .2f), -24, 50, 11.5f * BoardLayout.CameraScale, true);
                rig.FaceFrom(seat == 0 ? zeusCap : poseidonCap, Vector3.zero, true); rig.FaceOpponentFrom(seat == 0 ? poseidonCap : zeusCap, Vector3.zero);
                foreach (CameraRig.View v in Enum.GetValues(typeof(CameraRig.View))) {
                    rig.SnapTo(v); rig.Snap();
                    int drawnIn = 0, rulesIn = 0;
                    for (int dx = 0; dx < BoardConfig.Width; dx++) for (int dy = 0; dy < BoardConfig.Height; dy++) {
                        var r = BoardLayout.FromDrawn(dx, dy);
                        var vp = cam.WorldToViewportPoint(BoardLayout.CellCenter(r.x, r.y) + Vector3.up * BoardLayout.TileTop);
                        bool inside = vp.z > 0 && vp.x > .02f && vp.x < .98f && vp.y > .02f && vp.y < .98f;
                        if (inside) { drawnIn++; if (BoardConfig.InRules(r.x, r.y)) rulesIn++; }
                    }
                    Info($"{s} {v}: {drawnIn}/48 drawn, {rulesIn}/24 rules cells in view, distance {rig.Distance:0.0}");
                    Check(rulesIn == 24, s + " " + v + " shows every rules cell at 1280x720");
                    if (v == CameraRig.View.TopDown || v == CameraRig.View.Overview) Check(drawnIn == 48, s + " " + v + " shows the whole 6x8 board at 1280x720");
                    else Check(drawnIn >= 40, s + " " + v + " shows at least 40 of 48 cells at 1280x720");
                }
            }
        } finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}

public static class BoardBuild {
    // Board-size checks, then the combined chain (cue, native, R3, camera, coin, restoration, gameplay, audio, player build).
    // Unity -batchmode -quit -projectPath <board>/UnityProof -executeMethod BoardBuild.Build -buildPath <exe>
    public static void Build() { BoardSizeChecks.Run(); CombinedBuild.Build(); }
}
