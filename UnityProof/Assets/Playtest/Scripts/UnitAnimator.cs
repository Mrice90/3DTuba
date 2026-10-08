using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // AI-060b per-event animation controller. Every board piece gets one (TokenFactory.Create). It moves
    // the piece's visual parts onto a "Pivot" child so body motion (lean, squash, topple, hover) never
    // fights BoardView, which owns the root's position and slot scale.
    //
    // Clip sources, per cue (summon / idle / move / attack / hit / death):
    //   1. a skeletal clip baked into the staged model (Resources.LoadAll<AnimationClip>(card.model)), matched
    //      by name (Meshy auto-animate exports "Walking", "Attack", "Dead"...), played through a PlayableGraph;
    //   2. otherwise a procedural clip below, tuned per type: CHARACTER, STRUCTURE, CAPITAL, LAND.
    // Root translation (hops along the hex path, lunges) is always procedural so it matches the board.
    public sealed class UnitAnimator : MonoBehaviour {
        public static TokenFactory Factory;
        public static Vfx Fx;
        // Where the last attack came from, so the hit flinch and death topple lean away from it.
        public static Vector3 LastStrikeFrom;

        Piece piece;
        Transform pivot;
        Quaternion face;                     // world facing (yaw only)
        Vector3 lean;                        // local euler, applied after facing
        Vector3 offset;                      // world offset of the pivot from the root
        Vector3 squash = Vector3.one;
        public bool Busy { get; private set; }
        public bool Dead { get; private set; }
        float idlePhase;

        Animator rigAnimator;
        readonly Dictionary<string, AnimationClip> rigClips = new Dictionary<string, AnimationClip>();
        PlayableGraph graph;
        string rigPlaying;

        public string Type => piece != null && piece.Card != null ? piece.Card.type : "CHARACTER";
        public bool HasClip(string cue) => rigClips.ContainsKey(cue);
        public int ClipCount => rigClips.Count;

        void Awake() {
            piece = GetComponent<Piece>();
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            var move = new List<Transform>();
            foreach (Transform c in transform) if (c != pivot && c.name != "Name plate" && c.name != "Owner ring") move.Add(c);
            foreach (var c in move) c.SetParent(pivot, true);
            face = transform.rotation;
            idlePhase = (Mathf.Abs((piece != null && piece.Card != null ? piece.Card.id : name).GetHashCode()) % 628) / 100f;
            FindRig();
        }

        // Until the piece first turns, it faces the way its root does (Spawn sets the owner's facing).
        bool faced;

        void FindRig() {
            if (piece == null || piece.Card == null || !piece.IsRealModel || string.IsNullOrEmpty(piece.Card.model)) return;
            rigAnimator = pivot.GetComponentInChildren<Animator>();
            if (rigAnimator == null) return;
            foreach (var clip in Resources.LoadAll<AnimationClip>(piece.Card.model)) {
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                var cue = CueForClipName(clip.name);
                if (cue != null && !rigClips.ContainsKey(cue)) rigClips[cue] = clip;
            }
            if (rigClips.Count == 0) { rigAnimator = null; return; }
            PlayRig("idle", true);
        }

        // Meshy and Mixamo clip names → our cue vocabulary.
        public static string CueForClipName(string clipName) {
            string n = clipName.ToLowerInvariant();
            int bar = n.LastIndexOf('|'); if (bar >= 0) n = n.Substring(bar + 1);
            if (n.Contains("death") || n.Contains("dead") || n.Contains("die") || n.Contains("dying")) return "death";
            if (n.Contains("hit") || n.Contains("hurt") || n.Contains("damage") || n.Contains("react")) return "hit";
            if (n.Contains("attack") || n.Contains("shoot") || n.Contains("slash") || n.Contains("punch") || n.Contains("strike") || n.Contains("cast")) return "attack";
            if (n.Contains("walk") || n.Contains("run") || n.Contains("move")) return "move";
            if (n.Contains("summon") || n.Contains("spawn") || n.Contains("appear") || n.Contains("intro") || n.Contains("arise")) return "summon";
            if (n.Contains("idle") || n.Contains("stand") || n.Contains("breath")) return "idle";
            return null;
        }

        float PlayRig(string cue, bool loop = false) {
            if (rigAnimator == null || !rigClips.TryGetValue(cue, out var clip)) return 0;
            if (graph.IsValid()) graph.Destroy();
            var playable = AnimationPlayableUtilities.PlayClip(rigAnimator, clip, out graph);
            if (loop) playable.SetDuration(double.MaxValue);
            rigPlaying = cue;
            return clip.length;
        }
        void ReturnToIdle() { if (rigAnimator != null && rigPlaying != "idle" && !Dead) PlayRig("idle", true); }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        void LateUpdate() {
            if (pivot == null) return;
            Vector3 idle = Vector3.zero; Vector3 sway = Vector3.zero;
            if (!Busy && !Dead && Type == "CHARACTER" && rigAnimator == null) {
                // Hover servos: techno-myth units idle with a slow bob and a slight weight shift.
                float t = Time.time * 1.9f + idlePhase;
                idle = Vector3.up * (.025f + Mathf.Sin(t) * .02f);
                sway = new Vector3(Mathf.Sin(t * .5f) * 1.5f, 0, Mathf.Sin(t * .7f + 1f) * 2f);
            }
            pivot.position = transform.position + offset + idle;
            pivot.rotation = Facing * Quaternion.Euler(lean + sway);
            pivot.localScale = squash;
            if (flashSaved != null && Time.time >= flashUntil) Unflash();
        }

        Quaternion Facing => faced ? face : Quaternion.Euler(0, transform.eulerAngles.y, 0);
        void ResetPose() { lean = Vector3.zero; offset = Vector3.zero; squash = Vector3.one; }
        static float D(float seconds, float speed) => seconds / Mathf.Max(.01f, speed);
        static bool Instant(float speed) => speed >= 50;
        public void FaceToward(Vector3 worldPoint) {
            face = Facing; faced = true;
            var d = worldPoint - transform.position; d.y = 0;
            if (d.sqrMagnitude > 1e-4f) face = Quaternion.LookRotation(d.normalized, Vector3.up);
        }
        IEnumerator Turn(Vector3 worldPoint, float dur) {
            if (!faced) { faced = true; face = Quaternion.Euler(0, transform.eulerAngles.y, 0); }
            var d = worldPoint - transform.position; d.y = 0;
            if (d.sqrMagnitude < 1e-4f) yield break;
            Quaternion from = face, to = Quaternion.LookRotation(d.normalized, Vector3.up);
            for (float t = 0; t < dur; t += Time.deltaTime) {
                if (this == null) yield break;
                face = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t / dur));
                yield return null;
            }
            face = to;
        }

        Vector3 Chest => transform.position + Vector3.up * Mathf.Max(.25f, piece != null ? piece.Height * transform.localScale.y * .6f : .4f);
        Color Accent => PlaytestCatalog.FactionAccent(piece != null && piece.Card != null ? piece.Card.faction : "");
        Color Main => PlaytestCatalog.FactionColor(piece != null && piece.Card != null ? piece.Card.faction : "");
        Color Dark => PlaytestCatalog.FactionDark(piece != null && piece.Card != null ? piece.Card.faction : "");

        // ------------------------------------------------------------------ summon
        // CHARACTER: teleport beam + materialise. STRUCTURE/CAPITAL: rise out of the ground with dust and a
        // core flash. LAND: terraform, the slab grows up out of the tile with a ripple ring.
        public IEnumerator Summon(float speed) {
            if (Instant(speed)) yield break;
            Busy = true;
            float rig = PlayRig("summon");
            var at = transform.position;
            switch (Type) {
                case "CHARACTER": {
                    var beam = Factory != null ? PlaytestMeshes.Make("Summon beam", PlaytestMeshes.Prism(6, .16f, .16f, 3.2f), Factory.Glow(Color.Lerp(Accent, Color.white, .35f)), null, at) : null;
                    Fx?.Burst(at + Vector3.up * .05f, Accent, 50, 1.4f, .07f, .7f, true);
                    squash = new Vector3(.05f, 2.4f, .05f);
                    float dur = D(.42f, speed);
                    for (float t = 0; t < dur; t += Time.deltaTime) {
                        if (this == null) { if (beam) Destroy(beam); yield break; }
                        float k = t / dur, e = 1 + 2.4f * Mathf.Pow(k - 1, 3) + 1.4f * Mathf.Pow(k - 1, 2);
                        squash = new Vector3(Mathf.LerpUnclamped(.05f, 1f, e), Mathf.Lerp(2.4f, 1f, Mathf.SmoothStep(0, 1, k)), Mathf.LerpUnclamped(.05f, 1f, e));
                        if (beam) beam.transform.localScale = new Vector3(1 - k, 1, 1 - k);
                        yield return null;
                    }
                    if (beam) Destroy(beam);
                    yield return Squash(new Vector3(1.15f, .82f, 1.15f), D(.16f, speed));
                    Fx?.Burst(at + Vector3.up * .04f, Color.Lerp(Main, Color.white, .5f), 24, .9f, .05f, .4f);
                    break;
                }
                case "STRUCTURE": case "CAPITAL": {
                    bool cap = Type == "CAPITAL";
                    float h = Mathf.Max(.4f, piece != null ? piece.Height : .8f), dur = D(cap ? .9f : .7f, speed);
                    var dust = new Color(.55f, .52f, .48f);
                    Fx?.Burst(at + Vector3.up * .05f, dust, cap ? 70 : 45, 1.3f, .1f, .9f);
                    for (float t = 0; t < dur; t += Time.deltaTime) {
                        if (this == null) yield break;
                        float k = Mathf.SmoothStep(0, 1, t / dur);
                        offset = new Vector3(Mathf.Sin(t * 61) * .025f * (1 - k), -h * (1 - k), Mathf.Cos(t * 47) * .025f * (1 - k));
                        yield return null;
                    }
                    offset = Vector3.zero;
                    Fx?.Burst(Chest, Accent, cap ? 80 : 50, 1.8f, .08f, .7f, true);
                    CameraShake(cap ? .18f : .1f);
                    yield return Squash(new Vector3(1.06f, .9f, 1.06f), D(.18f, speed));
                    break;
                }
                case "LAND": {
                    float dur = D(.45f, speed);
                    var ring = Factory != null ? PlaytestMeshes.Make("Terraform ring", PlaytestMeshes.HexRing(.5f, .44f), Factory.Glow(Main), null, at + Vector3.up * .02f) : null;
                    for (float t = 0; t < dur; t += Time.deltaTime) {
                        if (this == null) { if (ring) Destroy(ring); yield break; }
                        float k = t / dur;
                        squash = new Vector3(1, Mathf.Max(.02f, 1 + 2.2f * Mathf.Pow(k - 1, 3) + 1.2f * Mathf.Pow(k - 1, 2)), 1);
                        lean = new Vector3(Mathf.Sin(k * 18) * 3 * (1 - k), 0, Mathf.Cos(k * 15) * 3 * (1 - k));
                        if (ring) ring.transform.localScale = Vector3.one * (1 + k * 1.6f);
                        yield return null;
                    }
                    if (ring) Destroy(ring);
                    break;
                }
            }
            if (this == null) yield break;
            if (rig > 0) yield return WaitFor(Mathf.Max(0, D(rig, speed) - D(.6f, speed)));
            ResetPose(); Busy = false; ReturnToIdle();
        }

        // ------------------------------------------------------------------ move
        // Hops hex by hex along the path (turn, anticipate, hop, land with a dust puff). Structures never
        // move; anything else that does slides with the same hops.
        public IEnumerator Move(IList<Vector3> waypoints, float speed) {
            if (waypoints == null || waypoints.Count == 0) yield break;
            if (Instant(speed)) { transform.position = waypoints[waypoints.Count - 1]; yield break; }
            Busy = true;
            bool rigged = PlayRig("move", true) > 0;
            float body = rigged ? .3f : 1f;
            yield return Turn(waypoints[0], D(.12f, speed));
            if (this == null) yield break;
            yield return Squash(new Vector3(1 + .1f * body, 1 - .14f * body, 1 + .1f * body), D(.08f, speed));
            for (int i = 0; i < waypoints.Count; i++) {
                if (this == null) yield break;
                Vector3 from = transform.position, to = waypoints[i];
                if (i > 0) yield return Turn(to, D(.08f, speed));
                float dur = D(.3f, speed) * Mathf.Clamp(Vector3.Distance(from, to) / 1.3f, .4f, 1.4f);
                for (float t = 0; t < dur; t += Time.deltaTime) {
                    if (this == null) yield break;
                    float k = Mathf.Clamp01(t / dur), e = Mathf.SmoothStep(0, 1, k);
                    transform.position = Vector3.Lerp(from, to, e) + Vector3.up * .22f * Mathf.Sin(k * Mathf.PI);
                    lean = new Vector3(12f * body * Mathf.Sin(k * Mathf.PI), 0, 0);
                    float s = Mathf.Sin(k * Mathf.PI) * .08f * body;
                    squash = new Vector3(1 - s * .5f, 1 + s, 1 - s * .5f);
                    yield return null;
                }
                transform.position = to;
                lean = Vector3.zero;
                Fx?.Burst(to + Vector3.up * .03f, new Color(.6f, .6f, .62f), 12, .7f, .05f, .35f);
                yield return Squash(new Vector3(1 + .12f * body, 1 - .16f * body, 1 + .12f * body), D(.1f, speed));
            }
            if (this == null) yield break;
            ResetPose(); Busy = false; ReturnToIdle();
        }

        // ------------------------------------------------------------------ attack
        // Wind-up (turn, lean back, charge glow), then Strike. Melee lunges and slashes; ranged and
        // structures recoil while the caller fires the bolt. Returns once the blow lands.
        public IEnumerator Windup(Vector3 aim, float speed) {
            if (Instant(speed)) yield break;
            Busy = true;
            LastStrikeFrom = transform.position;
            bool rigged = PlayRig("attack") > 0;
            yield return Turn(aim, D(.12f, speed));
            if (this == null) yield break;
            float body = rigged ? .3f : 1f, dur = D(.2f, speed);
            Fx?.Burst(Chest, Accent, 16, .5f, .05f, .35f, true);
            for (float t = 0; t < dur; t += Time.deltaTime) {
                if (this == null) yield break;
                float k = Mathf.SmoothStep(0, 1, t / dur);
                lean = new Vector3(-14f * k * body, 0, 0);
                squash = new Vector3(1 + .05f * k * body, 1 - .07f * k * body, 1 + .05f * k * body);
                yield return null;
            }
        }

        public IEnumerator Strike(Vector3 aim, bool melee, float speed) {
            if (Instant(speed)) yield break;
            LastStrikeFrom = transform.position;
            var d = aim - transform.position; d.y = 0;
            var dir = d.sqrMagnitude > 1e-4f ? d.normalized : Facing * Vector3.forward;
            bool turret = Type == "STRUCTURE" || Type == "CAPITAL";
            float body = rigAnimator != null ? .3f : 1f;
            if (melee && !turret) {
                float reach = Mathf.Min(.45f, d.magnitude * .45f), dur = D(.12f, speed);
                for (float t = 0; t < dur; t += Time.deltaTime) {
                    if (this == null) yield break;
                    float k = Mathf.SmoothStep(0, 1, t / dur);
                    offset = dir * reach * k; lean = new Vector3(Mathf.Lerp(-14f, 22f, k) * body, 0, 0); squash = Vector3.one;
                    yield return null;
                }
                Slash(aim, dir);
            } else {
                // Recoil: kick back and squash while the bolt leaves.
                offset = -dir * (turret ? .04f : .1f); lean = new Vector3(-8f * body, 0, 0);
                squash = turret ? new Vector3(1.04f, .92f, 1.04f) : Vector3.one;
                Fx?.Burst(Chest + dir * .2f, Color.Lerp(Accent, Color.white, .5f), 18, 1.6f, .05f, .25f);
            }
            if (this != null) StartCoroutine(Recover(D(.28f, speed)));
        }

        IEnumerator Recover(float dur) {
            Vector3 o0 = offset, l0 = lean, s0 = squash;
            for (float t = 0; t < dur; t += Time.deltaTime) {
                float k = Mathf.SmoothStep(0, 1, t / dur);
                offset = Vector3.Lerp(o0, Vector3.zero, k); lean = Vector3.Lerp(l0, Vector3.zero, k); squash = Vector3.Lerp(s0, Vector3.one, k);
                yield return null;
            }
            ResetPose(); Busy = false; ReturnToIdle();
        }

        void Slash(Vector3 aim, Vector3 dir) {
            if (Factory == null) return;
            // An energy arc across the target: a thin glowing blade swept sideways, gone in a blink.
            var arc = PlaytestMeshes.Make("Slash", PlaytestMeshes.Prism(4, .02f, .02f, .8f, 45), Factory.Glow(Color.Lerp(Accent, Color.white, .55f)), null, aim);
            arc.transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 0, 55) * Quaternion.Euler(90, 0, 0);
            arc.transform.position = aim - arc.transform.up * .4f;
            arc.AddComponent<Fade>().Life = .18f;
            Fx?.Burst(aim, Accent, 26, 1.8f, .06f, .35f);
        }

        // ------------------------------------------------------------------ hit
        // Flinch away from the attacker with a white flash; heavy hits (3+) shake the camera.
        public IEnumerator Hit(int amount, float speed) {
            if (Instant(speed) || Dead) yield break;
            bool wasBusy = Busy; Busy = true;
            bool rigged = PlayRig("hit") > 0;
            var away = transform.position - LastStrikeFrom; away.y = 0;
            if (away.sqrMagnitude < 1e-4f) away = -(Facing * Vector3.forward);
            away.Normalize();
            var localAway = Quaternion.Inverse(Facing) * away;
            float body = rigged ? .3f : 1f, mag = Mathf.Clamp(amount, 1, 5) / 5f;
            Flash(D(.07f, speed));
            if (amount >= 3) CameraShake(.06f + .02f * amount);
            bool turret = Type == "STRUCTURE" || Type == "CAPITAL" || Type == "LAND";
            float dur = D(.28f, speed);
            for (float t = 0; t < dur; t += Time.deltaTime) {
                if (this == null) yield break;
                float k = t / dur, env = Mathf.Sin(k * Mathf.PI) * (1 - k * .5f);
                if (turret) offset = new Vector3(Mathf.Sin(t * 80) * .04f, 0, Mathf.Cos(t * 67) * .04f) * (1 - k);
                else {
                    offset = away * (.08f + .1f * mag) * env;
                    lean = new Vector3(localAway.z, 0, -localAway.x) * (14f + 12f * mag) * env * body;
                }
                yield return null;
            }
            if (this == null) yield break;
            if (!wasBusy) { ResetPose(); Busy = false; ReturnToIdle(); } else { offset = Vector3.zero; lean = Vector3.zero; }
        }

        // White flash: swap every body material for a white glow, restored by LateUpdate. A flash during a
        // flash only extends it, so the saved materials are always the real ones.
        Renderer[] flashRs; Material[][] flashSaved; float flashUntil;
        void Flash(float dur) {
            if (Factory == null) return;
            flashUntil = Mathf.Max(flashUntil, Time.time + dur);
            if (flashSaved != null) return;
            var white = Factory.Glow(new Color(1f, .97f, .9f));
            flashRs = pivot.GetComponentsInChildren<Renderer>();
            flashSaved = new Material[flashRs.Length][];
            for (int i = 0; i < flashRs.Length; i++) {
                if (flashRs[i] is ParticleSystemRenderer) continue;
                flashSaved[i] = flashRs[i].sharedMaterials;
                var w = new Material[flashSaved[i].Length]; for (int j = 0; j < w.Length; j++) w[j] = white;
                flashRs[i].sharedMaterials = w;
            }
        }
        void Unflash() {
            for (int i = 0; i < flashRs.Length; i++) if (flashRs[i] != null && flashSaved[i] != null) flashRs[i].sharedMaterials = flashSaved[i];
            flashRs = null; flashSaved = null;
        }

        // ------------------------------------------------------------------ death / destruction
        // CHARACTER: stagger, topple away from the killer, dissolve into rising sparks and sink, leaving
        // a scorch mark. STRUCTURE/CAPITAL: shudder, blow apart into debris with fire and smoke, then
        // collapse into the ground. LAND: crack and sink. The caller despawns the piece afterwards.
        public IEnumerator Death(float speed) {
            if (Dead) yield break;
            Dead = true; Busy = true;
            if (Instant(speed)) yield break;
            float rig = PlayRig("death");
            var at = transform.position;
            var away = at - LastStrikeFrom; away.y = 0;
            if (away.sqrMagnitude < 1e-4f) away = -(Facing * Vector3.forward);
            away.Normalize();
            var localAway = Quaternion.Inverse(Facing) * away;
            switch (Type) {
                case "CHARACTER": {
                    Flash(D(.08f, speed));
                    if (rig > 0) {
                        yield return WaitFor(D(Mathf.Min(rig, 1.6f), speed));
                    } else {
                        // stagger back
                        float dur = D(.18f, speed);
                        for (float t = 0; t < dur; t += Time.deltaTime) {
                            if (this == null) yield break;
                            float k = Mathf.SmoothStep(0, 1, t / dur);
                            offset = away * .14f * k; lean = new Vector3(localAway.z, 0, -localAway.x) * 18f * k;
                            yield return null;
                        }
                        // topple
                        dur = D(.32f, speed);
                        Vector3 l0 = lean, l1 = new Vector3(localAway.z, 0, -localAway.x) * 82f;
                        for (float t = 0; t < dur; t += Time.deltaTime) {
                            if (this == null) yield break;
                            float k = t / dur; k *= k;
                            lean = Vector3.Lerp(l0, l1, k);
                            yield return null;
                        }
                        lean = l1;
                        Fx?.Burst(at + away * .3f + Vector3.up * .05f, new Color(.55f, .55f, .58f), 20, 1f, .06f, .5f);
                    }
                    Fx?.Burst(Chest, Main, 70, 1.2f, .08f, 1.1f, true);
                    Fx?.Burst(Chest, Accent, 30, 2.4f, .05f, .6f);
                    Scorch(at, .42f);
                    float sink = D(.45f, speed);
                    Vector3 o0 = offset;
                    for (float t = 0; t < sink; t += Time.deltaTime) {
                        if (this == null) yield break;
                        float k = t / sink;
                        offset = o0 + Vector3.down * .5f * k;
                        squash = Vector3.one * (1 - k * .9f);
                        yield return null;
                    }
                    break;
                }
                case "STRUCTURE": case "CAPITAL": {
                    bool cap = Type == "CAPITAL";
                    float h = Mathf.Max(.4f, piece != null ? piece.Height : .8f);
                    float dur = D(cap ? .6f : .4f, speed);
                    for (float t = 0; t < dur; t += Time.deltaTime) {
                        if (this == null) yield break;
                        float k = t / dur;
                        offset = new Vector3(Mathf.Sin(t * 90) * .05f * k, 0, Mathf.Cos(t * 73) * .05f * k);
                        yield return null;
                    }
                    Flash(D(.1f, speed));
                    Fx?.Burst(Chest, new Color(1f, .55f, .2f), cap ? 160 : 100, cap ? 3.6f : 2.8f, .12f, .9f);
                    Fx?.Burst(at + Vector3.up * h * .3f, new Color(.3f, .3f, .32f), cap ? 90 : 60, .8f, .2f, 1.8f, true);
                    SpawnDebris(at, h, cap ? 14 : 9);
                    CameraShake(cap ? .35f : .2f);
                    Scorch(at, .55f);
                    dur = D(cap ? .8f : .55f, speed);
                    for (float t = 0; t < dur; t += Time.deltaTime) {
                        if (this == null) yield break;
                        float k = Mathf.SmoothStep(0, 1, t / dur);
                        offset = Vector3.down * h * .85f * k;
                        lean = new Vector3(localAway.z, 0, -localAway.x) * 14f * k;
                        squash = new Vector3(1 + .1f * k, 1 - .55f * k, 1 + .1f * k);
                        yield return null;
                    }
                    break;
                }
                default: { // LAND
                    float dur = D(.4f, speed);
                    Fx?.Burst(at + Vector3.up * .05f, new Color(.5f, .45f, .4f), 40, 1.2f, .08f, .8f);
                    for (float t = 0; t < dur; t += Time.deltaTime) {
                        if (this == null) yield break;
                        float k = t / dur;
                        squash = new Vector3(1, 1 - k, 1);
                        lean = new Vector3(Mathf.Sin(t * 50) * 3 * (1 - k), 0, 0);
                        yield return null;
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ helpers
        IEnumerator Squash(Vector3 to, float dur) {
            Vector3 from = squash;
            for (float t = 0; t < dur; t += Time.deltaTime) {
                if (this == null) yield break;
                float k = t / dur;
                squash = k < .4f ? Vector3.Lerp(from, to, k / .4f) : Vector3.Lerp(to, Vector3.one, (k - .4f) / .6f);
                yield return null;
            }
            if (this != null) squash = Vector3.one;
        }
        static IEnumerator WaitFor(float s) { for (float t = 0; t < s; t += Time.deltaTime) yield return null; }

        void Scorch(Vector3 at, float size) {
            if (Factory == null) return;
            var mark = PlaytestMeshes.Make("Scorch", PlaytestMeshes.HexSlab(size, .006f), Factory.Lit(new Color(.06f, .06f, .07f), .1f, 0), null, at + Vector3.up * .004f);
            mark.AddComponent<Fade>().Life = 2.5f;
        }

        void SpawnDebris(Vector3 at, float h, int count) {
            if (Factory == null) return;
            var rng = new System.Random(GetInstanceID());
            var mats = new[] { Factory.Lit(Dark, .3f, .5f), Factory.Lit(Color.Lerp(Main, new Color(.6f, .62f, .68f), .55f), .5f, .6f), Factory.Glow(Accent) };
            for (int i = 0; i < count; i++) {
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = .05f + (float)rng.NextDouble() * .15f, sz = .04f + (float)rng.NextDouble() * .07f;
                var start = at + new Vector3(Mathf.Cos(a) * r, h * (.2f + (float)rng.NextDouble() * .6f), Mathf.Sin(a) * r);
                var chunk = PlaytestMeshes.Make("Debris", PlaytestMeshes.Prism(4 + rng.Next(3), sz, sz * .7f, sz * 1.4f), mats[i % 3 == 2 && i < 6 ? 2 : rng.Next(2)], null, start);
                var deb = chunk.AddComponent<Debris>();
                deb.Velocity = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (1f + (float)rng.NextDouble() * 1.8f) + Vector3.up * (1.6f + (float)rng.NextDouble() * 2f);
                deb.Spin = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f, (float)rng.NextDouble() - .5f) * 900f;
                deb.Floor = at.y + .02f;
            }
        }

        public static void CameraShake(float amount) { CameraRig.Trauma = Mathf.Min(1f, CameraRig.Trauma + amount); }

        // Hex path for a move: the alpha only reports from/to, so walk neighbour by neighbour, always to the
        // neighbour closest to the destination (ties broken by staying straight).
        public static List<Vector2Int> HexPath(Vector2Int from, Vector2Int to) {
            var path = new List<Vector2Int>();
            var cur = from; int guard = 0;
            while (cur != to && guard++ < 32) {
                Vector2Int best = cur; int bestD = int.MaxValue;
                for (int x = 0; x < BoardLayout.Width; x++) for (int y = 0; y < BoardLayout.Height; y++) {
                    if (MovementState.HexDistance(cur.x, cur.y, x, y) != 1) continue;
                    int d = MovementState.HexDistance(x, y, to.x, to.y);
                    if (d < bestD) { bestD = d; best = new Vector2Int(x, y); }
                }
                if (best == cur) break;
                path.Add(best); cur = best;
            }
            if (path.Count == 0 || path[path.Count - 1] != to) path.Add(to);
            return path;
        }
    }

    // Shrinks an effect object to nothing over its life, then removes it.
    sealed class Fade : MonoBehaviour {
        public float Life = 1;
        float t; Vector3 start;
        void Start() { start = transform.localScale; }
        void Update() {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Life);
            transform.localScale = start * (1 - k * k);
            if (t >= Life) Destroy(gameObject);
        }
    }

    // A chunk of a destroyed structure: ballistic arc, spin, a bounce or two, then it shrinks away.
    sealed class Debris : MonoBehaviour {
        public Vector3 Velocity, Spin;
        public float Floor;
        float t;
        void Update() {
            float dt = Time.deltaTime; t += dt;
            Velocity += Physics.gravity * dt;
            transform.position += Velocity * dt;
            transform.Rotate(Spin * dt, Space.Self);
            if (transform.position.y < Floor) {
                transform.position = new Vector3(transform.position.x, Floor, transform.position.z);
                Velocity = new Vector3(Velocity.x * .5f, -Velocity.y * .35f, Velocity.z * .5f);
                Spin *= .5f;
            }
            if (t > 1.6f) transform.localScale *= Mathf.Max(0, 1 - dt * 4f);
            if (t > 2.4f) Destroy(gameObject);
        }
    }
}
