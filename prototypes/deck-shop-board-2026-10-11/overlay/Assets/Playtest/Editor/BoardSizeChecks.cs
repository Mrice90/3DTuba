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
            CameraFraming();

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
                int side = y < BoardConfig.RulesPlotHeight ? 0 : 1;
                if (BoardConfig.HomeSide(0, y) != side) Check(false, "rules row " + y + " keeps its home side in the preview");
            }
            Check(true, "every rules row keeps its faction home side inside the 6x8 home halves");
            Geometry("6x8");
            CapitalCells();
            BoardViewTiles();
            CameraFraming();
            CameraSnapChecks.Run(); Check(true, "camera snap suite passes on the 6x8 preview");

            // Hexagon preview (-board hex): radius 3 around rules (1,3).
            foreach (var arg in new[] { "hex", "HEXAGON" }) {
                BoardConfig.ApplyCommandLine(new[] { "game.exe", "-board", arg });
                Check(BoardConfig.Shape == BoardShape.Hexagon && BoardConfig.CellCount == 37 && BoardConfig.Label == "hex37", "-board " + arg + " draws the 37-hex hexagon");
            }
            var cellsHex = BoardConfig.DrawnCells;
            Check(cellsHex.All(c => MovementState.HexDistance(1, 3, c.x, c.y) <= 3) && cellsHex.Distinct().Count() == 37, "hexagon = every cell within 3 of its centre, no duplicates");
            Check(Enumerable.Range(0, 24).All(i => cellsHex.Contains(new Vector2Int(i % 4, i / 4))), "hexagon contains the whole 4x6 rules grid");
            int zeusHome = cellsHex.Count(c => BoardConfig.HomeSide(c.x, c.y) == 0), poseidonHome = cellsHex.Count(c => BoardConfig.HomeSide(c.x, c.y) == 1);
            Check(zeusHome == 15 && poseidonHome == 15 && cellsHex.Count(c => BoardConfig.HomeSide(c.x, c.y) == -1) == 7, "hexagon home halves 15 + 15 with a 7-hex neutral middle row");
            var rows = cellsHex.GroupBy(c => c.y).OrderBy(g => g.Key).Select(g => g.Count()).ToArray();
            Check(rows.SequenceEqual(new[] { 4, 5, 6, 7, 6, 5, 4 }), "hexagon rows 4-5-6-7-6-5-4");
            Info("hexagon ViewScale " + BoardLayout.ViewScale.ToString("0.000") + ", CameraScale " + BoardLayout.CameraScale.ToString("0.000"));
            Geometry("hex37");
            CapitalCells();
            BoardViewTiles();
            CameraFraming();
            CameraSnapChecks.Run(); Check(true, "camera snap suite passes on the hexagon preview");
        } finally { BoardConfig.UseRules(); }
        CameraSnapChecks.Run(); Check(true, "camera snap suite passes on the rules grid");
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/board-size-validation.json", "{\"passed\":true,\"checks\":" + checks.Count + ",\"info\":[" + string.Join(",", info.Select(s => "\"" + s.Replace("\"", "'") + "\"")) + "]}");
    }

    // Drawn cells form a clean hex board and rules adjacency equals world adjacency.
    static void Geometry(string label) {
        var drawn = BoardConfig.DrawnCells.Select(r => BoardLayout.CellCenter(r.x, r.y)).ToList();
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
        if (BoardConfig.Shape == BoardShape.Rectangle)
            Check(Mathf.Abs(rc.z) < .01f && Mathf.Abs(rc.x) < BoardLayout.Spacing * .5f, label + " rules grid centred in the drawn board");
        else Check(rc.magnitude < BoardLayout.Spacing, label + " rules grid within one hex of the hexagon centre");
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
            string l = BoardConfig.Label; int all = BoardConfig.CellCount;
            var cells = go.GetComponentsInChildren<ProofCell>(true);
            Check(cells.Length == 24 && cells.All(c => BoardConfig.InRules(c.X, c.Y)), l + " preview: 24 input cells, all on the rules grid");
            Check(board.PreviewTileCount == all - 24, l + " preview: " + (all - 24) + " inert preview tiles");
            Check(go.GetComponentsInChildren<MeshCollider>(true).Length == all, l + " preview: " + all + " tiles drawn");
            var plinthMesh = go.transform.Find("Board plinth").GetComponent<MeshFilter>().sharedMesh;
            Info(l + " plinth extents " + plinthMesh.bounds.extents.ToString("0.00"));
            Check(plinthMesh.bounds.extents.x > 4.5f, l + " plinth grew with the board");
            if (BoardConfig.Shape == BoardShape.Hexagon) {
                // Flat-topped hexagonal plinth: every tile corner must sit inside it.
                float rin = plinthMesh.bounds.extents.z, rt = BoardLayout.TileSize / Mathf.Sqrt(3f); bool inside = true;
                foreach (var c in BoardConfig.DrawnCells) for (int i = 0; i < 6; i++) {
                    float a = Mathf.Deg2Rad * (60 * i + 30); var q = BoardLayout.CellCenter(c.x, c.y) + new Vector3(rt * Mathf.Cos(a), 0, rt * Mathf.Sin(a));
                    if (Mathf.Abs(q.z) > rin || .8660254f * Mathf.Abs(q.x) + .5f * Mathf.Abs(q.z) > rin) inside = false;
                }
                Check(inside, "hexagon plinth follows the board outline (every tile corner on it)");
            }
        } finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // At 1280x720 the live viewport is the top 76% of the screen (the hand and End turn sit below it).
    // Overview and top-down must show all 48 drawn cells; both seats' home views must show every rules cell.
    // HUD rects that sit over the live viewport at 1280x720 (GUI coordinates, y down).
    static readonly (string name, Rect rect)[] HudPanels = {
        ("log panel", PlaytestGame.LiveLogRect(1280, 720)),
        ("open Abilities panel", new Rect(10, 222, 312, 474 - 222)),
        ("top bar", new Rect(0, 0, 1280, 62)),
        ("Zeus panel", new Rect(10, 70, 250, 144)),
        ("Poseidon panel", new Rect(1020, 70, 250, 144)),
    };
    // Every capital placement the engine allows (12 home cells a seat) changes where "behind you" and
    // "opponent" look from, so each view is checked for every own/enemy capital pair, worst case reported.
    static void CameraFraming() {
        var go = new GameObject("Board size camera"); var cam = go.AddComponent<Camera>(); var rig = go.AddComponent<CameraRig>(); rig.InputEnabled = false;
        try {
            cam.fieldOfView = 38; cam.aspect = 1280f / (720f * .76f);
            float shift = PlaytestGame.BoardShiftPx(1280);
            float tr = BoardLayout.TileSize / Mathf.Sqrt(3f);
            int all = BoardConfig.CellCount; string l = BoardConfig.Label;
            List<Vector3> Home(int seat) {
                var list = new List<Vector3>();
                for (int x = 0; x < BoardConfig.RulesWidth; x++) for (int y = 0; y < BoardConfig.RulesHeight; y++)
                    if (PlaytestGame.ValidCapitalCell(seat, new Vector2Int(x, y))) list.Add(BoardLayout.CellCenter(x, y));
                return list;
            }
            foreach (int seat in new[] { 0, 1 }) {
                string s = seat == 0 ? "Zeus" : "Poseidon";
                var own = Home(seat); var enemy = Home(1 - seat);
                foreach (CameraRig.View v in Enum.GetValues(typeof(CameraRig.View))) {
                    var hits = new HashSet<string>(); float minX = 1e9f, maxX = -1e9f, minY = 1e9f; int poses = 0, worstDrawn = all, worstRules = 24; float dist = 0;
                    foreach (var oc in own) foreach (var ec in enemy) {
                        rig.SetHome(new Vector3(0, 0, .2f), -24, 50, 11.5f * BoardLayout.CameraScale, true);
                        rig.FaceFrom(oc, Vector3.zero, true); rig.FaceOpponentFrom(ec, Vector3.zero);
                        rig.SnapTo(v); rig.Snap(); poses++; dist = rig.Distance;
                        cam.ResetProjectionMatrix(); cam.projectionMatrix = CameraRig.ShiftedProjection(cam.projectionMatrix, shift, 1280);
                        int drawnIn = 0, rulesIn = 0;
                        foreach (var rc in BoardConfig.DrawnCells) {
                            var c = BoardLayout.CellCenter(rc.x, rc.y) + Vector3.up * BoardLayout.TileTop;
                            var cv = cam.WorldToViewportPoint(c);
                            if (cv.z > 0 && cv.x > .02f && cv.x < .98f && cv.y > .02f && cv.y < .98f) { drawnIn++; if (BoardConfig.InRules(rc.x, rc.y)) rulesIn++; }
                            for (int i = 0; i < 6; i++) {
                                float a = Mathf.Deg2Rad * (60 * i + 30);
                                var vp = cam.WorldToViewportPoint(c + new Vector3(tr * Mathf.Cos(a), 0, tr * Mathf.Sin(a)));
                                var g = new Vector2(vp.x * 1280, 720 - (720 * .24f + vp.y * 720 * .76f));
                                minX = Mathf.Min(minX, g.x); maxX = Mathf.Max(maxX, g.x); minY = Mathf.Min(minY, g.y);
                                foreach (var pnl in HudPanels) if (pnl.rect.Contains(g)) hits.Add(pnl.name);
                            }
                        }
                        worstDrawn = Mathf.Min(worstDrawn, drawnIn); worstRules = Mathf.Min(worstRules, rulesIn);
                    }
                    Info($"{l} {s} {v}: {poses} capital pairs, board x {minX:0}-{maxX:0} px, top {minY:0} px, shift {shift:0} px, distance {dist:0.0}, worst {worstDrawn}/{all} cells in view, HUD overlaps: " + (hits.Count == 0 ? "none" : string.Join(", ", hits)));
                    // Previews must clear every HUD panel for every capital placement. The 4 x 6 default keeps the
                    // framing Matt played (some placements tuck a corner under the log panel or top bar); reported only.
                    if (BoardConfig.IsPreview) Check(hits.Count == 0, l + " " + s + " " + v + ": no board cell under the log, Abilities, top or player panels, any capital placement, 1280x720");
                    Check(worstRules == 24, l + " " + s + " " + v + " shows every rules cell at 1280x720");
                    Check(worstDrawn == all, l + " " + s + " " + v + " shows the whole drawn board at 1280x720");
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
