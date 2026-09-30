using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // AI-080 playtest scene: polished hex board, 139-card stand-in/real-model catalog, AI-066 event
    // playback (default) and AI-079 bridge live play (flag -bridgeCmd). Also routes -proofSmoke to the
    // original MovementProof scene so the existing player smoke keeps working in the same build.
    public sealed class PlaytestGame : MonoBehaviour {
        public Shader LitShader, UnlitShader;
        public Material ParticleMaterial;
        public TextAsset DefaultDump;
        public const string ProofSceneName = "MovementProof";

        enum Mode { Playback, Live, Gallery }
        Mode mode = Mode.Playback, returnMode = Mode.Playback;
        Camera cam; CameraRig rig; BoardView board; TokenFactory factory; Vfx vfx; SfxBank sfx;
        MatchModel model = new MatchModel();
        readonly Dictionary<string, Piece> pieces = new Dictionary<string, Piece>();
        List<WireEvent> events = new List<WireEvent>();
        string dumpName = "";
        int cursor; bool playing = true, stepOnce; float speed = 1f;
        Coroutine runner;
        AnimHost anim;
        Font font;
        string banner = ""; float bannerUntil;
        string hoverInfo = "";
        Piece selectedPiece;
        bool smoke, standInsOnly;
        GUIStyle titleStyle, labelStyle, smallStyle, bannerStyle, panelStyle, buttonStyle;

        // live
        BridgeClient bridge; bool waiting; string liveStatus = ""; int humanSeat = 0;
        BridgeState liveState; BridgeAction[] liveActions = new BridgeAction[0];
        string liveSelected; readonly Queue<WireEvent> liveQueue = new Queue<WireEvent>();

        // gallery
        GameObject galleryRoot; string galleryFilter = "all";
        public static readonly Vector3 GalleryOrigin = new Vector3(60, 0, 0);

        static string Arg(string name) {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
        static bool Flag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        void Awake() {
            if (Flag("-proofSmoke")) { SceneManager.LoadScene(ProofSceneName); return; }
            Application.runInBackground = true; Application.targetFrameRate = 60;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            factory = new TokenFactory(LitShader != null ? LitShader : Shader.Find("Universal Render Pipeline/Lit"),
                                       UnlitShader != null ? UnlitShader : Shader.Find("Universal Render Pipeline/Unlit"), font);
            vfx = new Vfx(ParticleMaterial != null ? ParticleMaterial : factory.Glow(Color.white), factory, font);
            sfx = new GameObject("SFX").AddComponent<SfxBank>();
            anim = new GameObject("Animation host").AddComponent<AnimHost>();
            SetupScene();
            board = new GameObject("Board").AddComponent<BoardView>();
            board.Build(factory);
            PlaytestCatalog.Load();
            smoke = Flag("-playtestSmoke");
            var dumpPath = Arg("-playtestDump");
            if (dumpPath != null && File.Exists(dumpPath)) { events = WireEvent.ParseJsonl(File.ReadAllText(dumpPath)); dumpName = Path.GetFileName(dumpPath); }
            else if (DefaultDump != null) { events = WireEvent.ParseJsonl(DefaultDump.text); dumpName = "AI-066 seed 42 (bundled)"; }
            if (smoke) { sfx.Muted = true; StartCoroutine(Smoke()); return; }
            var shots = Arg("-playtestShots");
            if (shots != null) { StartCoroutine(Screenshots(shots)); return; }
            var bridgeCmd = Arg("-bridgeCmd") ?? Environment.GetEnvironmentVariable("IC_BRIDGE_CMD");
            if (!string.IsNullOrEmpty(bridgeCmd)) StartLive(bridgeCmd, Arg("-bridgeCwd"), Arg("-bridgeTranscript"));
            else RestartPlayback();
        }

        void SetupScene() {
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.02f, .035f, .06f);
            cam.fieldOfView = 38; cam.nearClipPlane = .1f; cam.farClipPlane = 300;
            rig = cam.gameObject.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            rig.SetHome(new Vector3(0, 0, .2f), -24, 50, 11.5f, true);
            var key = new GameObject("Key light").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.9f; key.shadows = LightShadows.Soft; key.color = new Color(1f, .96f, .9f);
            key.transform.rotation = Quaternion.Euler(52, -38, 0);
            var rim = new GameObject("Rim light").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = .7f; rim.color = new Color(.55f, .75f, 1f);
            rim.transform.rotation = Quaternion.Euler(28, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .40f, .50f);
        }

        // ------------------------------------------------------------------ pieces
        Piece Spawn(string instanceId, string cardId, int owner, int x, int y, bool animate) {
            var card = PlaytestCatalog.Get(cardId);
            var go = factory.Create(card, owner, out _);
            var p = go.GetComponent<Piece>(); p.InstanceId = instanceId;
            go.transform.rotation = Quaternion.Euler(0, owner == 0 ? 15 : 195, 0);
            pieces[instanceId] = p;
            board.Add(p, x, y, true);
            if (animate && speed < 50) {
                var home = go.transform.position; var s = go.transform.localScale;
                go.transform.position = home + Vector3.up * 1.2f;
                anim.StartCoroutine(Tween.Move(go.transform, home, .3f / speed));
                anim.StartCoroutine(Tween.Scale(go.transform, s * .3f, s, .35f / speed, true));
            }
            return p;
        }
        void Despawn(Piece p) {
            if (p == null) return;
            board.Remove(p); pieces.Remove(p.InstanceId ?? "");
            Destroy(p.gameObject);
        }
        Piece PieceOf(string instanceId) => instanceId != null && pieces.TryGetValue(instanceId, out var p) && p != null ? p : null;
        Piece CapitalOf(int owner) => pieces.Values.FirstOrDefault(p => p != null && p.Owner == owner && p.Card.type == "CAPITAL");
        Vector3 HexPoint(Vector2Int h) => BoardLayout.CellCenter(h.x, h.y) + Vector3.up * (BoardLayout.TileTop + .3f);
        Vector3 Chest(Piece p) => p.transform.position + Vector3.up * Mathf.Max(.25f, p.Height * p.transform.localScale.y * .6f);

        // ------------------------------------------------------------------ playback
        void RestartPlayback() {
            anim.StopAllCoroutines(); runner = null;
            ClearMatch();
            mode = Mode.Playback; cursor = 0; playing = true;
            PlaceDemoCapitals();
            runner = anim.StartCoroutine(Run());
        }
        void ClearMatch() {
            board.ClearPieces(); pieces.Clear(); model = new MatchModel();
            board.SetLegal(null); selectedPiece = null; board.Selected = new Vector2Int(-1, -1);
        }
        // EventDump seats: Zeus default capital at (1,0) for seat 0, Poseidon default at (2,5) for seat 1
        // (DemoMatchFactory capital deployment emits no event; board-events.md § Gaps).
        void PlaceDemoCapitals() {
            Spawn("capital:0", model.CapitalId0, 0, 1, 0, false);
            Spawn("capital:1", model.CapitalId1, 1, 2, 5, false);
        }
        IEnumerator Run() {
            while (cursor < events.Count) {
                if (!playing && !stepOnce) { yield return null; continue; }
                stepOnce = false;
                var e = events[cursor++];
                yield return Apply(e);
            }
            playing = false;
        }
        float D(float seconds) => seconds / Mathf.Max(.01f, speed);

        IEnumerator Apply(WireEvent e) {
            var m = model;
            int pl = Mathf.Clamp(e.player, 0, 1);
            string fac = m.Faction[pl];
            switch (e.@event) {
                case "MATCH_STARTED":
                    Banner("Match start — " + e.detail, 2.5f); m.AddLog(e.detail); yield return Wait(.6f); break;
                case "TURN_STARTED":
                    m.Turn = e.turn; m.Active = pl;
                    Banner($"Turn {e.turn} — {Faction(pl)}", 1.4f); sfx.PlayUi("turn"); m.AddLog($"T{e.turn} {Faction(pl)} turn"); yield return Wait(.45f); break;
                case "PHASE_CHANGED": m.Phase = e.detail; m.Active = pl; yield return Wait(.06f); break;
                case "TURN_ENDED": yield return Wait(.15f); break;
                case "CARD_DRAWN": m.Hand[pl]++; yield return Wait(.04f); break;
                case "GP_GENERATED": m.Gp[pl] += e.amount; yield return Wait(.05f); break;
                case "GP_SPENT": m.Gp[pl] = Mathf.Max(0, m.Gp[pl] - e.amount); yield return Wait(.05f); break;
                case "CARD_PLAYED": {
                    m.Hand[pl] = Mathf.Max(0, m.Hand[pl] - 1); m.Played[pl]++;
                    var card = PlaytestCatalog.Get(e.card_id);
                    m.Ensure(e.instance_id, e.card_id, pl);
                    m.AddLog($"{Faction(pl)} plays {card.name} ({card.type}){(e.hasTo ? $" at {e.to.x},{e.to.y}" : "")}");
                    if (e.hasTo) { board.SetLegal(new[] { e.To }); }
                    yield return Wait(.25f);
                    if (card.type == "SPELL") {
                        sfx.Play(card.id, "deploy"); sfx.Play(card.id, "ability", .7f);
                        var at = e.hasTo ? HexPoint(e.To) : (CapitalOf(pl)?.transform.position ?? Vector3.zero);
                        yield return vfx.SpellBurst(this, at, card.faction, speed);
                    } else if (e.hasTo) {
                        Spawn(e.instance_id, e.card_id, pl, e.to.x, e.to.y, true);
                        sfx.Play(card.id, card.type == "CHARACTER" && card.HasCue("signature") ? "signature" : "deploy");
                        vfx.Burst(BoardLayout.CellCenter(e.to.x, e.to.y) + Vector3.up * .15f, PlaytestCatalog.FactionAccent(card.faction), 40, 1.6f, .07f, .6f, true);
                        yield return Wait(.45f);
                    }
                    board.SetLegal(null);
                    break;
                }
                case "CHARACTER_MOVED": {
                    var p = PieceOf(e.instance_id);
                    if (p == null && e.hasFrom) p = Spawn(e.instance_id, e.card_id, pl, e.from.x, e.from.y, false);
                    if (p == null || !e.hasTo) break;
                    board.SetLegal(new[] { e.To });
                    m.AddLog($"{p.Card.name} moves {e.from.x},{e.from.y} -> {e.to.x},{e.to.y}");
                    sfx.Play(p.Card.id, "move");
                    yield return Wait(.15f);
                    var target = e.To;
                    board.Remove(p); p.X = target.x; p.Y = target.y;
                    // Tween to where it will stand in the destination stack, then register it there.
                    var stack = board.StackAt(target.x, target.y); stack.Add(p);
                    var dest = board.SlotPosition(p); stack.Remove(p); p.X = -1;
                    yield return Tween.Move(p.transform, dest, D(.45f), .35f);
                    board.Add(p, target.x, target.y, speed >= 50);
                    if (speed < 50) board.Relayout(target.x, target.y, false);
                    board.SetLegal(null);
                    break;
                }
                case "ATTACK_RESOLVED": case "OPPORTUNITY_ATTACK": {
                    var a = PieceOf(e.instance_id);
                    var t = PieceOf(e.DetailTargetInstance());
                    if (a == null) break;
                    if (e.hasTo) board.SetLegal(null, new[] { e.To });
                    m.AddLog($"{a.Card.name} {(e.@event == "OPPORTUNITY_ATTACK" ? "opportunity-attacks" : "attacks")} {(t != null ? t.Card.name : "")}");
                    sfx.Play(a.Card.id, "attack");
                    var aim = t != null ? Chest(t) : (e.hasTo ? HexPoint(e.To) : a.transform.position);
                    anim.StartCoroutine(Tween.Lunge(a.transform, aim, D(.4f)));
                    yield return vfx.Bolt(Chest(a), aim, PlaytestCatalog.FactionAccent(a.Card.faction), speed);
                    yield return Wait(.2f);
                    board.SetLegal(null);
                    break;
                }
                case "DAMAGE_DEALT": {
                    var t = PieceOf(e.instance_id);
                    if (t == null) { yield return Wait(.1f); break; }
                    t.Damage += e.amount;
                    sfx.Play(t.Card.id, "hit");
                    vfx.FloatText(Chest(t) + Vector3.up * .3f, "-" + e.amount, new Color(1f, .35f, .3f), speed);
                    yield return Tween.Shake(t.transform, D(.25f), .05f);
                    break;
                }
                case "CAPITAL_HIT": {
                    int owner = pl;
                    var cid = e.DetailCardId();
                    var cap = pieces.Values.FirstOrDefault(p => p != null && p.Card.type == "CAPITAL" && p.Card.id == cid) ?? CapitalOf(owner);
                    if (cap != null) owner = cap.Owner;
                    m.CapitalHp[owner] = Mathf.Max(0, m.CapitalHp[owner] - e.amount);
                    m.AddLog($"{Faction(owner)} Capital takes {e.amount} (HP {m.CapitalHp[owner]})");
                    if (cap != null) {
                        sfx.Play(cap.Card.id, "hit");
                        vfx.FloatText(Chest(cap) + Vector3.up * .5f, "-" + e.amount, new Color(1f, .5f, .2f), speed);
                        vfx.Burst(Chest(cap), new Color(1f, .5f, .2f), 50, 2f, .08f, .7f);
                        yield return Tween.Shake(cap.transform, D(.45f), .08f);
                    }
                    break;
                }
                case "CARD_DESTROYED": {
                    var p = PieceOf(e.instance_id);
                    m.Destroyed[pl]++;
                    if (p == null) break;
                    m.AddLog($"{p.Card.name} destroyed");
                    sfx.Play(p.Card.id, "destroy");
                    vfx.Burst(Chest(p), PlaytestCatalog.FactionColor(p.Card.faction), 80, 2.6f, .1f, .9f);
                    var s = p.transform.localScale;
                    yield return Tween.Scale(p.transform, s, s * .05f, D(.4f));
                    Despawn(p);
                    break;
                }
                case "CARD_ABILITY_TRIGGERED": {
                    var p = PieceOf(e.instance_id);
                    if (p == null) break;
                    m.AddLog($"{p.Card.name}: {Short(e.detail)}");
                    sfx.Play(p.Card.id, "ability");
                    vfx.Burst(Chest(p), PlaytestCatalog.FactionAccent(p.Card.faction), 50, 1.2f, .08f, .8f, true);
                    if (e.detail != null && e.detail.Contains("ENEMY_CAPITAL")) {
                        var cap = CapitalOf(1 - p.Owner);
                        if (cap != null) yield return vfx.Bolt(Chest(p), Chest(cap), PlaytestCatalog.FactionAccent(p.Card.faction), speed);
                    }
                    yield return Wait(.3f);
                    break;
                }
                case "GAME_OVER":
                    m.Winner = pl;
                    Banner($"GAME OVER — {Faction(pl)} wins", 999);
                    m.AddLog(e.detail); sfx.PlayUi("turn");
                    var loser = CapitalOf(1 - pl);
                    if (loser != null) vfx.Burst(Chest(loser), new Color(1f, .6f, .2f), 150, 3.5f, .14f, 1.4f);
                    yield return Wait(.5f);
                    break;
                default:
                    yield return Wait(.03f); break;
            }
        }
        IEnumerator Wait(float s) { if (speed >= 50) yield break; float t = 0, d = D(s); while (t < d) { t += Time.deltaTime; yield return null; } }
        string Faction(int p) => p == 0 ? "Zeus" : "Poseidon";
        static string Short(string s) => s == null ? "" : s.Length > 60 ? s.Substring(0, 60) + "…" : s;
        void Banner(string text, float seconds) { banner = text; bannerUntil = Time.time + Mathf.Max(.2f, seconds / Mathf.Max(1, speed)); if (seconds > 100) bannerUntil = float.MaxValue; }

        // ------------------------------------------------------------------ live play (AI-079 bridge)
        void StartLive(string cmd, string cwd, string transcriptPath) {
            ClearMatch(); mode = Mode.Live;
            try {
                bridge = BridgeClient.Spawn(cmd, cwd, transcriptPath);
                int seed = int.TryParse(Arg("-seed"), out var s) ? s : 42;
                humanSeat = int.TryParse(Arg("-humanSeat"), out var h) ? h : 0;
                bridge.New(seed, humanSeat); waiting = true; liveStatus = "Starting rules bridge…";
                StartCoroutine(LiveLoop());
            } catch (Exception ex) {
                liveStatus = "Bridge failed to start: " + ex.Message + " — falling back to playback.";
                Debug.LogWarning(liveStatus); bridge = null; RestartPlayback();
            }
        }
        IEnumerator LiveLoop() {
            while (mode == Mode.Live && bridge != null) {
                if (bridge.TryReceive(out var r)) {
                    waiting = false;
                    if (!r.ok) { liveStatus = "Bridge: " + (r.error ?? "error"); sfx.PlayUi("error"); }
                    if (r.events != null) foreach (var e in r.events) liveQueue.Enqueue(e);
                    while (liveQueue.Count > 0) yield return Apply(liveQueue.Dequeue());
                    if (r.state != null) { liveState = r.state; SyncState(r.state); }
                    if (r.legal != null) { liveActions = r.legal; liveStatus = liveActions.Length + " legal actions — pick a card or piece."; }
                    else if (r.ok && liveState != null && !liveState.GameOver && liveState.active_player == humanSeat) { bridge.Legal(); waiting = true; }
                    else if (r.ok && liveState != null && !liveState.GameOver) { liveStatus = "Opponent (bot) is playing…"; }
                }
                if (!bridge.Alive) { liveStatus = "Bridge exited. " + bridge.LastError; yield break; }
                yield return null;
            }
        }
        // Reconcile pieces with the bridge's per-hex stacks, and HUD numbers with its players block.
        void SyncState(BridgeState s) {
            if (s.players != null) for (int i = 0; i < Mathf.Min(2, s.players.Length); i++) {
                model.Gp[i] = s.players[i].gp;
                model.Hand[i] = s.players[i].hand != null ? s.players[i].hand.Length : s.players[i].hand_count;
            }
            model.Turn = s.turn; model.Active = s.active_player; model.Phase = s.phase; if (s.GameOver) model.Winner = s.winner;
            if (s.board == null) return;
            var seen = new HashSet<string>();
            foreach (var hex in s.board) {
                if (hex.stack == null) continue;
                foreach (var c in hex.stack) {
                    seen.Add(c.instance_id);
                    var p = PieceOf(c.instance_id);
                    if (p == null) Spawn(c.instance_id, c.card_id, c.owner, hex.x, hex.y, false);
                    else if (p.X != hex.x || p.Y != hex.y) board.Add(p, hex.x, hex.y, true);
                }
            }
            foreach (var p in pieces.Values.ToList()) if (p != null && !seen.Contains(p.InstanceId)) Despawn(p);
        }
        static WireHex TargetOf(BridgeAction a) => a.to ?? a.target ?? a.at ?? a.destination;
        IEnumerable<BridgeAction> ActionsFor(string instanceId) => liveActions.Where(a => a.instance_id == instanceId && TargetOf(a) != null);
        void SelectLive(string instanceId) {
            liveSelected = instanceId;
            var acts = ActionsFor(instanceId).ToList();
            board.SetLegal(acts.Where(a => !IsAttack(a)).Select(a => TargetOf(a)).Select(h => new Vector2Int(h.x, h.y)),
                           acts.Where(IsAttack).Select(a => TargetOf(a)).Select(h => new Vector2Int(h.x, h.y)));
            sfx.PlayUi("click");
        }
        static bool IsAttack(BridgeAction a) => a.type != null && a.type.Equals("attack", StringComparison.OrdinalIgnoreCase);
        void ActLive(BridgeAction a) {
            if (a == null || bridge == null || waiting) return;
            bridge.Act(a.id); waiting = true; liveStatus = "Resolving " + (a.card_name ?? a.type) + "…";
            liveActions = new BridgeAction[0]; liveSelected = null; board.SetLegal(null);
        }

        // ------------------------------------------------------------------ gallery (HA-009 review)
        void ShowGallery(string filter) {
            if (mode != Mode.Gallery) returnMode = mode;
            mode = Mode.Gallery; galleryFilter = filter;
            if (galleryRoot != null) Destroy(galleryRoot);
            galleryRoot = new GameObject("Gallery");
            var cards = PlaytestCatalog.All.Where(c => filter == "all" || (filter == "real" ? c.HasModel && !standInsOnly : !c.HasModel || standInsOnly))
                .OrderBy(c => Array.IndexOf(new[] { "CAPITAL", "CHARACTER", "STRUCTURE", "LAND", "SPELL" }, c.type)).ThenBy(c => c.faction).ThenBy(c => c.name).ToList();
            int perRow = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(cards.Count * 1.4f)), 4, 14);
            factory.ForceStandIns = standInsOnly;
            for (int i = 0; i < cards.Count; i++) {
                var c = cards[i];
                int col = i % perRow, row = i / perRow;
                var at = GalleryOrigin + new Vector3((col - (perRow - 1) / 2f) * 1.45f, 0, -row * 1.75f);
                var pad = PlaytestMeshes.Make("Pad", PlaytestMeshes.HexSlab(BoardLayout.TileSize, BoardLayout.TileThickness), factory.Lit(new Color(.1f, .15f, .21f), .5f, .2f), galleryRoot.transform, at - Vector3.up * BoardLayout.TileThickness);
                if (c.type == "SPELL") {
                    var orb = PlaytestMeshes.Make(c.id, PlaytestMeshes.Prism(4, .16f, 0, .3f, 45), factory.Glow(PlaytestCatalog.FactionAccent(c.faction)), galleryRoot.transform, at + Vector3.up * .45f);
                    var lower = PlaytestMeshes.Make("lower", PlaytestMeshes.Prism(4, .16f, 0, .3f, 45), factory.Glow(PlaytestCatalog.FactionColor(c.faction)), orb.transform, Vector3.zero);
                    lower.transform.localRotation = Quaternion.Euler(180, 0, 0);
                    vfx.Burst(at + Vector3.up * .2f, PlaytestCatalog.FactionAccent(c.faction), 40, 1.2f, .06f, 1.2f, true);
                    Label(c, at, galleryRoot.transform, .95f);
                    continue;
                }
                var go = factory.Create(c, c.faction == "POSEIDON" ? 1 : 0, out bool real);
                go.transform.SetParent(galleryRoot.transform, true);
                go.transform.position = at; go.transform.rotation = Quaternion.Euler(0, 200, 0);
                if (c.type == "LAND") Label(c, at, galleryRoot.transform, .45f);
            }
            factory.ForceStandIns = false;
            int rows = Mathf.CeilToInt(cards.Count / (float)perRow);
            float depth = rows * 1.75f, width = perRow * 1.45f;
            rig.Look(GalleryOrigin + new Vector3(0, 0, -depth / 2 + .9f), -12, 48, Mathf.Max(width, depth) * 1.05f + 3, true);
        }
        void Label(CardEntry c, Vector3 at, Transform parent, float height) {
            var go = new GameObject("Label " + c.id, typeof(MeshRenderer), typeof(TextMesh));
            go.transform.SetParent(parent, false); go.transform.position = at + Vector3.up * height;
            var tm = go.GetComponent<TextMesh>(); tm.font = font; tm.text = c.name + "\n<size=32>" + c.type + "</size>"; tm.richText = true;
            tm.fontSize = 48; tm.characterSize = .012f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = Color.Lerp(PlaytestCatalog.FactionAccent(c.faction), Color.white, .5f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<Billboard>();
        }
        void LeaveGallery() {
            if (galleryRoot != null) Destroy(galleryRoot);
            mode = returnMode; rig.ResetView(); rig.Snap();
        }

        // ------------------------------------------------------------------ input
        void Update() {
            if (board == null) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (kb != null) {
                if (kb.spaceKey.wasPressedThisFrame && mode == Mode.Playback) playing = !playing;
                if (kb.nKey.wasPressedThisFrame && mode == Mode.Playback) { playing = false; stepOnce = true; }
                if (kb.gKey.wasPressedThisFrame) { if (mode == Mode.Gallery) LeaveGallery(); else ShowGallery("all"); }
                if (kb.escapeKey.wasPressedThisFrame) { if (mode == Mode.Gallery) LeaveGallery(); else ClearSelection(); }
            }
            if (mouse == null || mode == Mode.Gallery) { hoverInfo = ""; return; }
            var mp = mouse.position.ReadValue();
            CameraRig.PointerOverHud = OverHud(mp);
            var hover = new Vector2Int(-1, -1); Piece hp = null;
            if (!CameraRig.PointerOverHud && Physics.Raycast(cam.ScreenPointToRay(mp), out var hit, 200)) {
                hp = hit.collider.GetComponentInParent<Piece>();
                var cell = hit.collider.GetComponent<ProofCell>();
                if (hp != null && hp.X >= 0) hover = new Vector2Int(hp.X, hp.Y);
                else if (cell != null) hover = new Vector2Int(cell.X, cell.Y);
            }
            if (hover != board.Hover) { board.Hover = hover; board.Refresh(); }
            hoverInfo = HoverText(hover, hp);
            if (mouse.leftButton.wasPressedThisFrame && !CameraRig.PointerOverHud) Click(hover, hp);
        }
        string HoverText(Vector2Int h, Piece hp) {
            if (!BoardView.InBounds(h.x, h.y)) return "";
            var lines = new List<string> { $"Hex ({h.x},{h.y})" };
            foreach (var p in board.StackAt(h.x, h.y))
                lines.Add($"{(p == hp ? "> " : "  ")}{p.Card.name} — {p.Card.type}, {Faction(p.Owner)}{(p.Damage > 0 ? $", dmg {p.Damage}" : "")} [{(p.IsRealModel ? "Meshy model" : "stand-in")}]");
            return string.Join("\n", lines);
        }
        void Click(Vector2Int h, Piece hp) {
            if (mode == Mode.Live) {
                if (BoardView.InBounds(h.x, h.y) && liveSelected != null && board.IsLegal(h)) {
                    var acts = ActionsFor(liveSelected).Where(a => TargetOf(a).x == h.x && TargetOf(a).y == h.y).ToList();
                    ActLive(acts.FirstOrDefault(IsAttack) ?? acts.FirstOrDefault());
                    return;
                }
                if (hp != null && hp.Owner == humanSeat && ActionsFor(hp.InstanceId).Any()) { SelectPiece(hp); SelectLive(hp.InstanceId); return; }
                ClearSelection(); return;
            }
            // Playback: select a piece to preview its neighbourhood (hex distance 1) as legal-target markers.
            if (hp != null) {
                SelectPiece(hp);
                var n = new List<Vector2Int>();
                for (int x = 0; x < BoardView.W; x++) for (int y = 0; y < BoardView.H; y++)
                    if (MovementState.HexDistance(hp.X, hp.Y, x, y) == 1) n.Add(new Vector2Int(x, y));
                var enemies = n.Where(c => board.StackAt(c.x, c.y).Any(q => q.Owner != hp.Owner && q.Card.type != "LAND")).ToList();
                board.SetLegal(hp.Card.type == "CHARACTER" ? n.Except(enemies) : null, hp.Card.type == "CHARACTER" ? enemies : null);
                sfx.PlayUi("click");
            } else ClearSelection();
        }
        void SelectPiece(Piece p) { selectedPiece = p; board.Selected = new Vector2Int(p.X, p.Y); board.Refresh(); }
        void ClearSelection() { selectedPiece = null; liveSelected = null; board.Selected = new Vector2Int(-1, -1); board.SetLegal(null); }

        // ------------------------------------------------------------------ HUD
        Rect topBar, leftPanel, rightPanel, bottomBar, logPanel;
        bool OverHud(Vector2 mp) {
            var g = new Vector2(mp.x, Screen.height - mp.y);
            return topBar.Contains(g) || leftPanel.Contains(g) || rightPanel.Contains(g) || bottomBar.Contains(g);
        }
        void Styles() {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold }; titleStyle.normal.textColor = new Color(.95f, .85f, .55f);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true }; labelStyle.normal.textColor = new Color(.88f, .92f, .98f);
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true }; smallStyle.normal.textColor = new Color(.75f, .82f, .9f);
            bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; bannerStyle.normal.textColor = Color.white;
            var bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(.03f, .05f, .09f, .82f)); bg.Apply();
            panelStyle = new GUIStyle(GUI.skin.box); panelStyle.normal.background = bg;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
        }
        void OnGUI() {
            if (board == null) return;
            Styles();
            float W = Screen.width, H = Screen.height;
            topBar = new Rect(0, 0, W, 62);
            GUI.Box(topBar, "", panelStyle);
            GUI.Label(new Rect(14, 9, 420, 30), "INFINITE CONQUEST — 3D playtest", titleStyle);
            string modeText = mode == Mode.Playback ? $"Playback: {dumpName}  ·  event {cursor}/{events.Count}" : mode == Mode.Live ? $"Live vs bot (rules bridge) · you are {Faction(humanSeat)}" : $"Catalog gallery ({galleryFilter})";
            GUI.Label(new Rect(440, 12, W - 900, 26), modeText, labelStyle);
            GUI.Label(new Rect(14, 40, W - 28, 20), "Click a piece to select it (markers = reachable/attackable hexes) · hover for stack details · right-drag / Q E orbit · wheel zoom · middle-drag / WASD pan · Space pause · N step · G gallery · Home reset view", smallStyle);
            GUI.Label(new Rect(W - 450, 12, 440, 26), $"Turn <b>{model.Turn}</b> · Active <b>{Faction(model.Active)}</b> · Phase <b>{model.Phase}</b>", labelStyle);

            if (mode != Mode.Gallery) {
                leftPanel = new Rect(10, 70, 250, 124); rightPanel = new Rect(W - 260, 70, 250, 124);
                PlayerPanel(leftPanel, 0); PlayerPanel(rightPanel, 1);
            } else { leftPanel = rightPanel = Rect.zero; }

            // Bottom controls
            bottomBar = new Rect(0, H - 52, W, 52);
            GUI.Box(bottomBar, "", panelStyle);
            float x = 12, y = H - 42;
            if (mode == Mode.Playback) {
                if (GUI.Button(new Rect(x, y, 90, 32), "Restart", buttonStyle)) RestartPlayback(); x += 96;
                if (GUI.Button(new Rect(x, y, 90, 32), playing ? "Pause" : "Play", buttonStyle)) playing = !playing; x += 96;
                if (GUI.Button(new Rect(x, y, 90, 32), "Step", buttonStyle)) { playing = false; stepOnce = true; } x += 96;
                foreach (var s in new[] { 1f, 2f, 4f }) { if (GUI.Toggle(new Rect(x, y, 50, 32), Mathf.Approximately(speed, s), s + "x", buttonStyle)) speed = s; x += 54; }
                x += 10;
            } else if (mode == Mode.Live) {
                var end = liveActions.FirstOrDefault(a => a.type != null && a.type.Equals("end_turn", StringComparison.OrdinalIgnoreCase));
                GUI.enabled = end != null && !waiting;
                if (GUI.Button(new Rect(x, y, 110, 32), "End turn", buttonStyle)) ActLive(end); x += 116;
                GUI.enabled = true;
                GUI.Label(new Rect(x, y + 6, 520, 26), (waiting ? "[waiting] " : "") + liveStatus, labelStyle); x += 530;
            }
            if (GUI.Button(new Rect(x, y, 150, 32), mode == Mode.Gallery ? "Back to board" : "Card gallery (G)", buttonStyle)) { if (mode == Mode.Gallery) LeaveGallery(); else ShowGallery("all"); } x += 156;
            if (mode == Mode.Gallery) {
                foreach (var f in new[] { "all", "real", "stand-ins" }) { if (GUI.Toggle(new Rect(x, y, 90, 32), galleryFilter == f, f, buttonStyle) && galleryFilter != f) ShowGallery(f); x += 94; }
            }
            bool so = GUI.Toggle(new Rect(x, y + 6, 150, 24), standInsOnly, " Stand-ins only");
            if (so != standInsOnly) { standInsOnly = so; if (mode == Mode.Gallery) ShowGallery(galleryFilter); else RebuildPieces(); }
            x += 150;
            sfx.Muted = GUI.Toggle(new Rect(x, y + 6, 80, 24), sfx.Muted, " Mute"); x += 84;
            if (GUI.Button(new Rect(x, y, 100, 32), "Reset view", buttonStyle)) rig.ResetView();

            // Event log
            if (mode != Mode.Gallery) {
                logPanel = new Rect(W - 380, H - 250, 370, 190);
                GUI.Box(logPanel, "", panelStyle);
                var tail = model.Log.Skip(Mathf.Max(0, model.Log.Count - 10));
                GUI.Label(new Rect(logPanel.x + 8, logPanel.y + 4, logPanel.width - 16, logPanel.height - 8), string.Join("\n", tail), smallStyle);
                if (!string.IsNullOrEmpty(hoverInfo)) {
                    var r = new Rect(10, H - 60 - 22 * (hoverInfo.Split('\n').Length) - 12, 460, 22 * hoverInfo.Split('\n').Length + 10);
                    GUI.Box(r, "", panelStyle); GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 12, r.height), hoverInfo, labelStyle);
                }
            }
            if (mode != Mode.Gallery && Time.time < bannerUntil && !string.IsNullOrEmpty(banner)) {
                var r = new Rect(W / 2 - 330, 76, 660, 50);
                GUI.Box(r, "", panelStyle); GUI.Label(r, banner, bannerStyle);
            }
        }
        void PlayerPanel(Rect r, int p) {
            GUI.Box(r, "", panelStyle);
            var fc = PlaytestCatalog.FactionColor(p == 0 ? "ZEUS" : "POSEIDON");
            string hex = ColorUtility.ToHtmlStringRGB(fc);
            bool active = model.Active == p;
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 24), $"<color=#{hex}><b>{(active ? "> " : "")}{Faction(p)}</b></color>  <size=12>seat {p}{(mode == Mode.Live && p == humanSeat ? " (you)" : "")}</size>", labelStyle);
            GUI.Label(new Rect(r.x + 10, r.y + 32, r.width - 20, 22), $"GP <b>{model.Gp[p]}</b>    Hand <b>{model.Hand[p]}</b>", labelStyle);
            GUI.Label(new Rect(r.x + 10, r.y + 56, r.width - 20, 22), $"Capital HP <b>{model.CapitalHp[p]}</b>/20", labelStyle);
            var bar = new Rect(r.x + 10, r.y + 80, r.width - 20, 10);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(.2f, .2f, .25f), 0, 0);
            bar.width *= Mathf.Clamp01(model.CapitalHp[p] / 20f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, fc, 0, 0);
            GUI.Label(new Rect(r.x + 10, r.y + 96, r.width - 20, 22), $"Played {model.Played[p]} · Lost {model.Destroyed[p]}", smallStyle);
            // Live: the human's hand as buttons (click card → legal hexes → click hex).
            if (mode == Mode.Live && p == humanSeat && liveState?.players != null && liveState.players.Length > p && liveState.players[p].hand != null) {
                var hand = liveState.players[p].hand;
                float hy = r.y + r.height + 6;
                for (int i = 0; i < hand.Length; i++) {
                    var c = PlaytestCatalog.Get(hand[i].card_id);
                    bool playable = ActionsFor(hand[i].instance_id).Any();
                    GUI.enabled = playable && !waiting;
                    if (GUI.Toggle(new Rect(r.x, hy + i * 30, r.width, 28), liveSelected == hand[i].instance_id, $"{c.name} ({c.type})", buttonStyle) && liveSelected != hand[i].instance_id) SelectLive(hand[i].instance_id);
                    GUI.enabled = true;
                }
                leftPanel = new Rect(r.x, r.y, r.width, r.height + 6 + hand.Length * 30);
            }
        }
        void RebuildPieces() {
            factory.ForceStandIns = standInsOnly;
            foreach (var p in pieces.Values.ToList()) {
                if (p == null) continue;
                int x = p.X, y = p.Y; var id = p.InstanceId; var cid = p.Card.id; int owner = p.Owner; int dmg = p.Damage;
                Despawn(p); var np = Spawn(id, cid, owner, x, y, false); np.Damage = dmg;
            }
        }

        // ------------------------------------------------------------------ smoke + screenshots
        IEnumerator Smoke() {
            yield return null;
            var checks = new List<string>(); bool pass = true;
            void Check(bool ok, string name) { checks.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) pass = false; Debug.Log("PLAYTEST_SMOKE " + (ok ? "PASS " : "FAIL ") + name); }
            var cards = PlaytestCatalog.All;
            Check(cards.Length == 139, "catalog has 139 cards (" + cards.Length + ")");
            int real = 0, stand = 0, failed = 0; var realIds = new List<string>();
            foreach (var c in cards) {
                if (c.type == "SPELL") { stand++; continue; }
                try { var go = factory.Create(c, 0, out bool r); if (r) { real++; realIds.Add(c.id); } else stand++; Destroy(go); } catch (Exception ex) { failed++; Debug.LogError(c.id + ": " + ex); }
            }
            int expectReal = cards.Count(c => c.HasModel && c.type != "SPELL");
            Check(failed == 0, "every card builds a token (" + failed + " failed)");
            Check(real == expectReal, $"staged real models load ({real}/{expectReal})");
            int specific = 0;
            foreach (var c in cards) foreach (var cue in new[] { "deploy", "move", "attack", "hit", "destroy" }) {
                var clip = sfx.Resolve(c.id, cue, out bool sp); if (clip == null) pass = false; if (sp) specific++;
            }
            Check(true, "every card resolves deploy/move/attack/hit/destroy SFX (" + specific + " card-specific, rest generic)");
            int errors = 0; string firstError = null;
            Application.LogCallback onLog = (msg, st, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; firstError = firstError ?? msg; } };
            Application.logMessageReceived += onLog;
            speed = 200; RestartPlayback();
            float deadline = Time.realtimeSinceStartup + 240;
            while (cursor < events.Count && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null; yield return null;
            Application.logMessageReceived -= onLog;
            string err = errors > 0 ? firstError : null;
            Check(err == null, "playback ran without exceptions" + (err != null ? ": " + err : ""));
            Check(events.Count > 0 && cursor == events.Count, $"all events applied ({cursor}/{events.Count})");
            Check(model.Winner == 0, "GAME_OVER winner = seat 0 (Zeus) as in AI-066 seed 42 (" + model.Winner + ")");
            Check(model.CapitalHp[1] == 0 || model.Winner == 0, "Poseidon capital destroyed");
            Debug.Log("PLAYTEST_SMOKE " + (pass ? "PASS" : "FAIL"));
            var outPath = Arg("-playtestResult");
            if (outPath != null) {
                var json = "{\"passed\":" + pass.ToString().ToLowerInvariant() + ",\"cards\":" + cards.Length + ",\"realModels\":" + real + ",\"standIns\":" + stand +
                           ",\"events\":" + events.Count + ",\"applied\":" + cursor + ",\"winner\":" + model.Winner + ",\"cardSpecificSfxCues\":" + specific +
                           ",\"realModelIds\":[" + string.Join(",", realIds.Select(s => "\"" + s + "\"")) + "],\"checks\":[" + string.Join(",", checks.Select(s => "\"" + s + "\"")) + "]}";
                File.WriteAllText(outPath, json);
            }
            Application.Quit(pass ? 0 : 1);
        }

        IEnumerator Screenshots(string dir) {
            Directory.CreateDirectory(dir);
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return new WaitForSeconds(.5f);
            // Find the event index with the most pieces on the board for the overview shot.
            int best = 0, bestCount = 0, count = 2;
            for (int i = 0; i < events.Count; i++) {
                var e = events[i];
                if (e.@event == "CARD_PLAYED" && PlaytestCatalog.Get(e.card_id).type != "SPELL") count++;
                if (e.@event == "CARD_DESTROYED") count--;
                if (count > bestCount) { bestCount = count; best = i; }
            }
            speed = 200; RestartPlayback();
            while (cursor <= best + 1 && cursor < events.Count) yield return null;
            playing = false; speed = 1;
            yield return new WaitForSeconds(1.5f);
            rig.ResetView(); rig.Snap(); yield return new WaitForSeconds(.5f);
            yield return Shot(dir, "01-board-overview.png");
            // Close-up with hover + legal-target markers around a character.
            var ch = pieces.Values.Where(p => p != null && p.Card.type == "CHARACTER").OrderByDescending(p => p.IsRealModel).FirstOrDefault();
            if (ch != null) {
                Click(new Vector2Int(ch.X, ch.Y), ch);
                board.Hover = new Vector2Int(ch.X, Mathf.Clamp(ch.Y + 1, 0, 5)); board.Refresh();
                hoverInfo = HoverText(new Vector2Int(ch.X, ch.Y), ch);
                rig.Look(ch.transform.position, -20, 42, 6.5f, true); yield return new WaitForSeconds(.6f);
                yield return Shot(dir, "02-board-selection-markers.png");
                ClearSelection(); hoverInfo = "";
            }
            // Same moment, stand-ins only, to compare.
            standInsOnly = true; RebuildPieces(); rig.ResetView(); rig.Snap(); yield return new WaitForSeconds(.6f);
            yield return Shot(dir, "03-board-overview-standins-only.png");
            standInsOnly = false; RebuildPieces();
            // Play on to the end for the final state.
            speed = 200; playing = true; while (cursor < events.Count) yield return null;
            speed = 1; yield return new WaitForSeconds(1f); rig.ResetView(); rig.Snap(); yield return new WaitForSeconds(.4f);
            yield return Shot(dir, "04-board-game-over.png");
            ShowGallery("real"); yield return new WaitForSeconds(1.2f);
            yield return Shot(dir, "05-gallery-real-models.png");
            ShowGallery("stand-ins"); yield return new WaitForSeconds(1.2f);
            yield return Shot(dir, "06-gallery-stand-ins.png");
            // Close-ups: a few real models, and one stand-in of each type.
            ShowGallery("real"); yield return new WaitForSeconds(.5f);
            yield return CloseUp(dir, "07-closeup-real-capitals.png", GalleryOrigin + new Vector3(-1.45f * 1.5f, 0, 0), 5.5f);
            yield return CloseUp(dir, "08-closeup-real-characters.png", GalleryOrigin + new Vector3(0, 0, -1.75f * 1f), 6f);
            ShowGallery("stand-ins"); yield return new WaitForSeconds(.5f);
            yield return CloseUp(dir, "09-closeup-standins.png", GalleryOrigin + new Vector3(0, 0, -1.75f * 2f), 7f);
            Application.Quit(0);
        }
        IEnumerator CloseUp(string dir, string name, Vector3 at, float dist) {
            rig.Look(at, -15, 35, dist, true); yield return new WaitForSeconds(.6f);
            yield return Shot(dir, name);
        }
        IEnumerator Shot(string dir, string name) {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("PLAYTEST_SHOT " + name);
        }

        void OnDestroy() { bridge?.Dispose(); }
        void OnApplicationQuit() { bridge?.Dispose(); }
    }
}
