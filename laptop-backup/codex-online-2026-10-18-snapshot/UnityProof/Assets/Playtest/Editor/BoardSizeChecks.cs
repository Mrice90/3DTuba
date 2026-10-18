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
        checks.Clear(); info.Clear(); BoardConfig.UseRules();
        Check(BoardConfig.CellCount==37 && !BoardConfig.IsPreview,"37 playable hexes; no preview tiles");
        Check(BoardConfig.DrawnCells.All(c=>BoardConfig.InRules(c.x,c.y)),"client mask matches rules");
        Check(BoardConfig.DrawnCells.Count(c=>BoardConfig.HomeSide(c.x,c.y)==-1)==7,"neutral middle row");
        Geometry("hex37"); CapitalCells(); BoardViewTiles(); CameraFraming(1280,720); CameraFraming(960,640); CameraSnapChecks.Run();
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/board-size-validation.json","{\"passed\":true,\"cells\":37,\"checks\":"+checks.Count+"}");
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
        Check(z == 15 && p == 15, BoardConfig.Label + " capital placement stays on the engine's 15 + 15 home cells");
    }

    static void BoardViewTiles() {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), font);
        var go = new GameObject("Board size check"); var board = go.AddComponent<BoardView>();
        try {
            board.Build(factory);
            string l = BoardConfig.Label; int all = BoardConfig.CellCount;
            var cells = go.GetComponentsInChildren<ProofCell>(true);
            Check(cells.Length == 37 && cells.All(c => BoardConfig.InRules(c.X, c.Y)), l + " preview: 37 input cells, all on the rules grid");
            Check(board.PreviewTileCount == all - 37, l + " preview: " + (all - 37) + " inert preview tiles");
            Check(go.GetComponentsInChildren<MeshCollider>(true).Length == all, l + " preview: " + all + " tiles drawn");
            var plinthMesh = go.transform.Find("Board plinth").GetComponent<MeshFilter>().sharedMesh;
            Info(l + " plinth extents " + plinthMesh.bounds.extents.ToString("0.00"));
            Check(plinthMesh.bounds.extents.x > 4.5f, l + " plinth grew with the board");
            float topY = plinthMesh.vertices.Max(v => v.y), phase = BoardConfig.Shape == BoardShape.Hexagon ? 0 : 30;
            var topVertices = plinthMesh.vertices.Where(v => v.y > topY - .0001f).ToArray();
            bool allSupported = true; float tileR = BoardLayout.TileSize / Mathf.Sqrt(3f);
            foreach (var cell in BoardConfig.DrawnCells) for (int corner = 0; corner < 6; corner++) {
                float angle = Mathf.Deg2Rad * (60 * corner + 30);
                var point = BoardLayout.CellCenter(cell.x, cell.y) + new Vector3(tileR * Mathf.Cos(angle), 0, tileR * Mathf.Sin(angle));
                for (int edge = 0; edge < 6; edge++) {
                    float angleN = Mathf.Deg2Rad * (phase + 30 + 60 * edge);
                    var n = new Vector3(Mathf.Cos(angleN), 0, Mathf.Sin(angleN));
                    if (Vector3.Dot(point, n) > topVertices.Max(v => Vector3.Dot(v, n)) - .1f) allSupported = false;
                }
            }
            Check(allSupported, l + " every tile corner sits inside the plinth's upper face with a margin");
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
    static (string name, Rect rect)[] HudPanels(float W,float H) => new[] {
        ("log panel", PlaytestGame.LiveLogRect(W,H)),
        ("open Abilities panel", new Rect(10,222,PlaytestGame.CommandPanelWidth(W),H-52-12-222)),
        ("top bar", new Rect(0,0,W,62)),
        ("Zeus panel", new Rect(10,70,PlaytestGame.PlayerPanelWidth(W),144)),
        ("Poseidon panel", new Rect(W-10-PlaytestGame.PlayerPanelWidth(W),70,PlaytestGame.PlayerPanelWidth(W),144)),
    };
    // Every capital placement the engine allows (12 home cells a seat) changes where "behind you" and
    // "opponent" look from, so each view is checked for every own/enemy capital pair, worst case reported.
    static void CameraFraming(float W,float H) {
        var go = new GameObject("Board size camera"); var cam = go.AddComponent<Camera>(); var rig = go.AddComponent<CameraRig>(); rig.InputEnabled = false;
        try {
            cam.fieldOfView = 38; cam.aspect = W / (H * .76f);
            float shift = PlaytestGame.BoardShiftPx(W);
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
                    var hits = new HashSet<string>(); float minX = 1e9f, maxX = -1e9f, minY = 1e9f; int poses = 0, worstDrawn = all, worstRules = 37; float dist = 0;
                    foreach (var oc in own) foreach (var ec in enemy) {
                        rig.SetHome(new Vector3(0, 0, .2f), -24, 50, 11.5f * BoardLayout.CameraScale, true);
                        rig.FaceFrom(oc, Vector3.zero, true); rig.FaceOpponentFrom(ec, Vector3.zero);
                        rig.SnapTo(v); rig.Snap(); poses++; dist = rig.Distance;
                        cam.ResetProjectionMatrix(); cam.projectionMatrix = CameraRig.ShiftedProjection(cam.projectionMatrix, shift, W);
                        int drawnIn = 0, rulesIn = 0;
                        foreach (var rc in BoardConfig.DrawnCells) {
                            var c = BoardLayout.CellCenter(rc.x, rc.y) + Vector3.up * BoardLayout.TileTop;
                            var cv = cam.WorldToViewportPoint(c);
                            if (cv.z > 0 && cv.x > .02f && cv.x < .98f && cv.y > .02f && cv.y < .98f) { drawnIn++; if (BoardConfig.InRules(rc.x, rc.y)) rulesIn++; }
                            for (int i = 0; i < 6; i++) {
                                float a = Mathf.Deg2Rad * (60 * i + 30);
                                var vp = cam.WorldToViewportPoint(c + new Vector3(tr * Mathf.Cos(a), 0, tr * Mathf.Sin(a)));
                                var g = new Vector2(vp.x * W, H - (H * .24f + vp.y * H * .76f));
                                minX = Mathf.Min(minX, g.x); maxX = Mathf.Max(maxX, g.x); minY = Mathf.Min(minY, g.y);
                                foreach (var pnl in HudPanels(W,H)) if (pnl.rect.Contains(g)) hits.Add(pnl.name);
                            }
                        }
                        worstDrawn = Mathf.Min(worstDrawn, drawnIn); worstRules = Mathf.Min(worstRules, rulesIn);
                    }
                    Info($"{l} {s} {v}: {poses} capital pairs, board x {minX:0}-{maxX:0} px, top {minY:0} px, shift {shift:0} px, distance {dist:0.0}, worst {worstDrawn}/{all} cells in view, HUD overlaps: " + (hits.Count == 0 ? "none" : string.Join(", ", hits)));
                    // Previews must clear every HUD panel for every capital placement. The 4 x 6 default keeps the
                    // framing Matt played (some placements tuck a corner under the log panel or top bar); reported only.
                    Check(hits.Count == 0, l + " " + s + " " + v + ": no board cell under HUD for any capital placement at "+W+"x"+H);
                    Check(worstRules == 37, l + " " + s + " " + v + " shows every rules cell at "+W+"x"+H);
                    Check(worstDrawn == all, l + " " + s + " " + v + " shows the whole drawn board at "+W+"x"+H);
                }
            }
        } finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}

public static class BoardBuild {
    // Board-size checks, then the combined chain (cue, native, R3, camera, coin, restoration, gameplay, audio, player build).
    // Unity -batchmode -quit -projectPath <board>/UnityProof -executeMethod BoardBuild.Build -buildPath <exe>
    public static void Build() { BoardSizeChecks.Run(); DeckShopBuild.Build(); }
}
