using System.Collections;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public enum MotionKind { Move, Attack, Summon, Destroy, Ability, Hit }

    // SP2-PRESENTATION: reusable event motion for board pieces (characters, structures, capitals, lands).
    // Poses are written to a "Motion pivot" child that wraps the piece's visuals, always as an absolute offset
    // from an identity baseline. BoardView keeps owning the root (slot position, stack scale, owner facing), so
    // repeated, overlapping or interrupted motions cannot accumulate scale, rotation or position drift.
    // This is procedural transform motion, not skeletal animation. A model that ships an Animator controller with
    // a trigger named after the MotionKind (or a legacy Animation clip of that name) also gets it played; none
    // of the 90 imported token FBXs has one today (Generic rig, importAnimation 0, no skin deformers).
    public static class PresentationMotion {
        public const string PivotName = "Motion pivot";
        // PlaytestGame treats speed >= 50 as "apply instantly" (smoke and screenshot fast-forward); so does this.
        public const float InstantSpeed = 50f;

        public static float BaseSeconds(MotionKind kind) {
            switch (kind) {
                case MotionKind.Move: return .45f;
                case MotionKind.Attack: return .4f;
                case MotionKind.Summon: return .5f;
                case MotionKind.Destroy: return .6f;
                case MotionKind.Ability: return .55f;
                default: return .25f;
            }
        }
        public static float Duration(MotionKind kind, float speed) => speed >= InstantSpeed ? 0 : BaseSeconds(kind) / Mathf.Max(.01f, speed);

        // Slides the root to destination (and to destinationScale when >= 0, so a following BoardView.Add snap
        // does not pop). If interrupted, the root still lands exactly on destination.
        public static MotionHandle Move(Piece p, Vector3 destination, float speed, float destinationScale = -1) =>
            Run(p, MotionKind.Move, speed, destination, destinationScale);
        // Characters wind up and lunge toward aim; structures and capitals recoil in place.
        public static MotionHandle Attack(Piece p, Vector3 aim, float speed) => Run(p, MotionKind.Attack, speed, aim);
        // Characters drop in and land; structures and capitals rise out of the hex; lands settle.
        public static MotionHandle Summon(Piece p, float speed) => Run(p, MotionKind.Summon, speed, null);
        // Terminal: leaves the visuals collapsed (name plate and owner ring hidden). The caller despawns afterwards.
        // from (attacker position) picks the fall direction; null uses a stable per-instance direction.
        public static MotionHandle Destroy(Piece p, float speed, Vector3? from = null) => Run(p, MotionKind.Destroy, speed, from);
        // Characters rise and spin once; structures and capitals charge-pulse and twist.
        public static MotionHandle Ability(Piece p, float speed) => Run(p, MotionKind.Ability, speed, null);
        // Damage reaction. Replaces Tween.Shake, which writes the root's world position and can fight BoardView.
        public static MotionHandle Hit(Piece p, float speed, Vector3? from = null) => Run(p, MotionKind.Hit, speed, from);

        // Stops any motion on the piece and restores its baseline pose (also un-collapses a destroyed piece).
        public static void Settle(Piece p) {
            if (p == null) return;
            var host = p.GetComponent<PieceMotion>();
            if (host != null) host.Settle();
        }
        public static bool IsBusy(Piece p) {
            var host = p != null ? p.GetComponent<PieceMotion>() : null;
            return host != null && host.Busy;
        }

        // The child that carries every visual except the billboard name plate. Created on first use.
        public static Transform Pivot(Piece p) {
            var root = p.transform;
            var existing = root.Find(PivotName);
            if (existing != null) return existing;
            var pivot = new GameObject(PivotName).transform;
            pivot.SetParent(root, false);
            for (int i = root.childCount - 1; i >= 0; i--) {
                var c = root.GetChild(i);
                if (c == pivot || (p.Plate != null && c == p.Plate.transform)) continue;
                // pivot is identity in root space, so keeping local values keeps every world pose unchanged
                c.SetParent(pivot, false);
                c.SetSiblingIndex(0);
            }
            return pivot;
        }

        static MotionHandle Run(Piece p, MotionKind kind, float speed, Vector3? point, float scale = -1) {
            if (p == null) return MotionHandle.Completed(kind);
            // explicit == null: Unity's fake-null from GetComponent defeats ??
            var host = p.GetComponent<PieceMotion>();
            if (host == null) host = p.gameObject.AddComponent<PieceMotion>();
            return host.Begin(kind, speed, point, scale);
        }
    }

    // Yield it from a coroutine to wait, or ignore it to fire and forget.
    public sealed class MotionHandle : CustomYieldInstruction {
        internal PieceMotion Owner;
        internal bool Finished;
        public MotionKind Kind { get; }
        // True when an Animator trigger or legacy clip named after Kind was played alongside the procedural motion.
        public bool UsedRigClip { get; internal set; }
        public bool Done => Finished || Owner == null;
        public override bool keepWaiting => !Done;
        internal MotionHandle(MotionKind kind) { Kind = kind; }
        internal static MotionHandle Completed(MotionKind kind) => new MotionHandle(kind) { Finished = true };
    }

    public sealed class PieceMotion : MonoBehaviour {
        enum Body { Character, Structure, Land }
        Piece piece; Transform pivot; Body body; float h;
        Coroutine current; MotionHandle handle; MotionKind kind;
        bool moving; Vector3 moveEnd; float moveEndScale = -1;
        Animator rigAnimator; float rigSpeed;
        bool collapsed, plateHidden, ringHidden; Vector3? destroyFrom;
        public bool Busy => current != null;

        void Init() {
            if (pivot != null) return;
            piece = GetComponent<Piece>();
            pivot = PresentationMotion.Pivot(piece);
            string type = piece.Card != null ? piece.Card.type : "CHARACTER";
            body = type == "STRUCTURE" || type == "CAPITAL" ? Body.Structure : type == "LAND" ? Body.Land : Body.Character;
            // Piece.Height is measured at creation with the root at scale 1, so it is in pivot-local units.
            h = Mathf.Max(.2f, piece.Height);
        }

        internal MotionHandle Begin(MotionKind k, float speed, Vector3? point, float scale) {
            Init();
            // A collapsed piece is waiting to be despawned; late events must not stand it back up.
            if (collapsed) return MotionHandle.Completed(k);
            Interrupt();
            // Interrupt finishes a terminal destruction; late reactions must not revive its visuals.
            if (collapsed) return MotionHandle.Completed(k);
            kind = k; handle = new MotionHandle(k) { Owner = this };
            if (k == MotionKind.Move) { moving = true; moveEnd = point ?? transform.position; moveEndScale = scale; }
            if (k == MotionKind.Destroy) destroyFrom = point;
            float dur = PresentationMotion.Duration(k, speed);
            if (dur <= 0 || !isActiveAndEnabled) { Complete(); return handle; }
            handle.UsedRigClip = PlayRig(k, speed);
            float amp = handle.UsedRigClip ? .4f : 1f;
            if (k == MotionKind.Summon || k == MotionKind.Destroy) SetDecor(false, k == MotionKind.Destroy);
            current = StartCoroutine(Drive(dur, Pose(k, point, amp)));
            return handle;
        }

        public void Settle() { Init(); Interrupt(); collapsed = false; Baseline(); SetDecor(true, true); }

        void OnDisable() { if (pivot != null) Interrupt(); }

        IEnumerator Drive(float dur, System.Action<float> pose) {
            float t = 0;
            pose(0);
            while (t < dur) {
                yield return null;
                t += Time.deltaTime;
                pose(Mathf.Clamp01(t / dur));
            }
            current = null;
            Complete();
        }

        // Ends the running motion at its authoritative end state.
        void Interrupt() {
            if (current != null) { StopCoroutine(current); current = null; }
            if (handle != null && !handle.Finished) Complete();
        }
        void Complete() {
            if (moving) {
                transform.position = moveEnd;
                if (moveEndScale >= 0) transform.localScale = Vector3.one * moveEndScale;
                moving = false;
            }
            if (rigAnimator != null) { rigAnimator.speed = rigSpeed; rigAnimator = null; }
            if (kind == MotionKind.Destroy) { DestroyPose(destroyFrom)(1); SetDecor(false, true); collapsed = true; }
            else { Baseline(); SetDecor(true, false); }
            if (handle != null) handle.Finished = true;
        }

        void Baseline() { pivot.localPosition = Vector3.zero; pivot.localRotation = Quaternion.identity; pivot.localScale = Vector3.one; }
        void Set(Vector3 pos, Quaternion rot, Vector3 scale) { pivot.localPosition = pos; pivot.localRotation = rot; pivot.localScale = scale; }

        // Only restores what a motion hid, so plate/ring visibility set elsewhere is left alone.
        void SetDecor(bool on, bool ring) {
            if (!on) {
                if (piece.Plate != null && piece.Plate.activeSelf) { piece.Plate.SetActive(false); plateHidden = true; }
                if (ring && piece.OwnerRing != null && piece.OwnerRing.activeSelf) { piece.OwnerRing.SetActive(false); ringHidden = true; }
                return;
            }
            if (plateHidden && piece.Plate != null) piece.Plate.SetActive(true);
            plateHidden = false;
            if (ring) {
                if (ringHidden && piece.OwnerRing != null) piece.OwnerRing.SetActive(true);
                ringHidden = false;
            }
        }

        bool PlayRig(MotionKind k, float speed) {
            string name = k.ToString();
            var animator = pivot.GetComponentInChildren<Animator>();
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null) {
                foreach (var p in animator.parameters) {
                    if (p.type != AnimatorControllerParameterType.Trigger || p.name != name) continue;
                    rigAnimator = animator; rigSpeed = animator.speed;
                    animator.speed = rigSpeed * Mathf.Clamp(speed, .1f, 8f);
                    animator.SetTrigger(name);
                    return true;
                }
            }
            var legacy = pivot.GetComponentInChildren<Animation>();
            if (legacy != null && legacy.isActiveAndEnabled && legacy[name] != null) {
                legacy[name].speed = Mathf.Clamp(speed, .1f, 8f);
                legacy.CrossFade(name, .1f);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ poses (k runs 0 -> 1)
        static float Smooth(float k) => Mathf.SmoothStep(0, 1, k);
        static float Bell(float k) => Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
        static float OutBack(float k) { const float c = 1.7f; float u = k - 1; return 1 + (c + 1) * u * u * u + c * u * u; }
        // Ramps in over the first 20% and out over the last 25%, so facing turns never snap.
        static float Envelope(float k) => Mathf.Clamp01(Mathf.Min(k / .2f, (1 - k) / .25f));

        // World direction to a flat, normalised root-local direction (forward when degenerate).
        Vector3 LocalDir(Vector3 world) {
            world.y = 0;
            var d = transform.InverseTransformDirection(world); d.y = 0;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }
        // Model forward is assumed to be +Z, as the owner-facing root rotation implies; turns are capped so a
        // model authored with another forward still reads as leaning toward the action.
        static Quaternion Face(Vector3 localDir, float maxYaw, float weight) =>
            Quaternion.Euler(0, Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -maxYaw, maxYaw) * weight, 0);
        static Quaternion Lean(Vector3 localDir, float degrees) => Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, localDir));
        Vector3 FallbackDir() {
            int seed = Mathf.Abs((piece.InstanceId ?? piece.name).GetHashCode());
            float a = (seed % 360) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        }

        System.Action<float> Pose(MotionKind k, Vector3? point, float amp) {
            switch (k) {
                case MotionKind.Move: return MovePose(amp);
                case MotionKind.Attack: return AttackPose(point ?? transform.position + transform.forward, amp);
                case MotionKind.Summon: return SummonPose();
                case MotionKind.Destroy: return DestroyPose(point);
                case MotionKind.Ability: return AbilityPose(amp);
                default: return HitPose(point, amp);
            }
        }

        System.Action<float> MovePose(float amp) {
            Vector3 from = transform.position, to = moveEnd;
            float fromScale = transform.localScale.x, toScale = moveEndScale >= 0 ? moveEndScale : fromScale;
            var dir = LocalDir(to - from);
            int steps = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(from, to) / .4f), 1, 4);
            return k => {
                float e = Smooth(k);
                transform.position = Vector3.Lerp(from, to, e);
                transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, e);
                if (body == Body.Character) {
                    float hop = h * amp * (.2f * Bell(k) + .05f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * steps)));
                    float land = k > .85f ? Bell((k - .85f) / .15f) * .08f * amp : 0;
                    float env = Envelope(k);
                    Set(new Vector3(0, hop, 0), Lean(dir, 10 * amp * env) * Face(dir, 40, env), new Vector3(1 + land * .5f, 1 - land, 1 + land * .5f));
                } else {
                    Set(new Vector3(0, h * (body == Body.Land ? .03f : .05f) * Bell(k), 0), Quaternion.identity, Vector3.one);
                }
            };
        }

        System.Action<float> AttackPose(Vector3 aim, float amp) {
            var world = aim - transform.position; world.y = 0;
            var dir = LocalDir(world);
            // Lunge a world distance, converted so the root's stack scale does not shorten it.
            var lunge = transform.InverseTransformVector(world.normalized * Mathf.Min(.35f, world.magnitude * .4f));
            lunge.y = 0;
            return k => {
                if (body == Body.Character) {
                    float reach, lean;
                    if (k < .3f) { float u = Smooth(k / .3f); reach = -.15f * u; lean = -6 * u; }
                    else if (k < .5f) { float u = (k - .3f) / .2f; u = 1 - (1 - u) * (1 - u); reach = Mathf.Lerp(-.15f, 1, u); lean = Mathf.Lerp(-6, 14, u); }
                    else { float u = Smooth((k - .5f) / .5f); reach = 1 - u; lean = 14 * (1 - u); }
                    float env = Envelope(k);
                    Set(lunge * reach * amp, Lean(dir, lean * amp) * Face(dir, 45, env), Vector3.one);
                } else if (body == Body.Structure) {
                    // charge (compress), fire (kick back), then a damped settle
                    float s = k < .2f ? -Smooth(k / .2f) : -Mathf.Cos((k - .2f) / .8f * Mathf.PI * 3) * Mathf.Pow(1 - (k - .2f) / .8f, 2);
                    float kick = k < .2f ? 0 : Bell(Mathf.Clamp01((k - .2f) / .3f));
                    Set(-dir * .04f * kick * amp, Lean(dir, -5 * kick * amp), new Vector3(1 - .035f * s, 1 + .07f * s, 1 - .035f * s));
                } else {
                    float s = .04f * Bell(k);
                    Set(Vector3.zero, Quaternion.identity, new Vector3(1, 1 + s, 1));
                }
            };
        }

        System.Action<float> SummonPose() {
            return k => {
                if (body == Body.Character) {
                    float u = Mathf.Clamp01(k / .7f), v = Mathf.Clamp01((k - .7f) / .3f);
                    float y = .9f * h * (1 - u * u);                 // falls under gravity
                    float grow = Mathf.Lerp(.35f, 1, Smooth(u));
                    float squash = k > .7f ? .14f * Mathf.Sin(v * Mathf.PI) * (1 - v * .5f) : 0;
                    Set(new Vector3(0, y, 0), Quaternion.Euler(0, -120 * (1 - Smooth(u)), 0), new Vector3(grow * (1 + squash * .5f), grow * (1 - squash), grow * (1 + squash * .5f)));
                } else if (body == Body.Structure) {
                    // rises out of the hex from its base, overshoots, then settles
                    float sy = Mathf.Max(.02f, OutBack(k)), sxz = Mathf.Lerp(.55f, 1, Smooth(k));
                    Set(Vector3.zero, Quaternion.Euler(0, -30 * (1 - Smooth(k)), 0), new Vector3(sxz, sy, sxz));
                } else {
                    float u = Mathf.Clamp01(k / .75f), v = Mathf.Clamp01((k - .75f) / .25f);
                    Set(new Vector3(0, .35f * (1 - u * u), 0), Quaternion.identity, new Vector3(1, 1 - .25f * Bell(v), 1));
                }
            };
        }

        System.Action<float> DestroyPose(Vector3? from) {
            var away = from.HasValue ? LocalDir(transform.position - from.Value) : FallbackDir();
            return k => {
                if (body == Body.Character) {
                    float u = Mathf.Clamp01(k / .55f), v = Mathf.Clamp01((k - .55f) / .45f);
                    float s = Mathf.Lerp(1, .05f, Smooth(v));
                    // topples about its feet away from the attacker, then sinks and fades out by scale
                    Set(new Vector3(0, -.25f * h * v, 0), Lean(away, 88 * u * u), Vector3.one * s);
                } else if (body == Body.Structure) {
                    float u = Mathf.Clamp01(k / .45f), v = Mathf.Clamp01((k - .35f) / .65f), f = Mathf.Clamp01((k - .8f) / .2f);
                    float a = .03f * h * u * (1 - v);
                    var jitter = new Vector3(Mathf.Sin(k * 61) * a, 0, Mathf.Cos(k * 47) * a);
                    float shrink = Mathf.Lerp(1, .05f, f);
                    // shudders, then slumps into rubble with a lean
                    Set(jitter + new Vector3(0, -.15f * h * v, 0), Lean(away, 10 * v), new Vector3(1 + .08f * v, Mathf.Lerp(1, .12f, v * v), 1 + .08f * v) * shrink);
                } else {
                    Set(new Vector3(0, -.05f * k, 0), Quaternion.identity, new Vector3(1, Mathf.Lerp(1, .05f, k), 1) * Mathf.Lerp(1, .6f, k));
                }
            };
        }

        System.Action<float> AbilityPose(float amp) {
            return k => {
                float b = Bell(k);
                if (body == Body.Character) {
                    // a rigged clip carries the gesture, so the procedural spin is dropped and only the lift remains
                    float spin = amp < 1 ? 0 : 360 * Smooth(k);
                    Set(new Vector3(0, .12f * h * b * amp, 0), Quaternion.Euler(0, spin, 0), Vector3.one * (1 + .08f * b));
                } else if (body == Body.Structure) {
                    float pulse = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2)) * b;
                    Set(new Vector3(0, .04f * h * b, 0), Quaternion.Euler(0, 20 * Mathf.Sin(k * Mathf.PI * 2) * amp, 0), new Vector3(1 - .03f * pulse, 1 + .1f * pulse * amp, 1 - .03f * pulse));
                } else {
                    Set(Vector3.zero, Quaternion.identity, new Vector3(1, 1 + .15f * b, 1));
                }
            };
        }

        System.Action<float> HitPose(Vector3? from, float amp) {
            var away = from.HasValue ? LocalDir(transform.position - from.Value) : Vector3.zero;
            return k => {
                float decay = 1 - k, b = Bell(Mathf.Clamp01(k * 2));
                float a = (body == Body.Character ? .05f : .02f) * decay;
                var shake = new Vector3(Mathf.Sin(k * 70) * a, 0, Mathf.Cos(k * 53) * a);
                if (body == Body.Character)
                    Set(shake + away * .08f * b * amp, away == Vector3.zero ? Quaternion.identity : Lean(away, 8 * b * amp), Vector3.one);
                else
                    Set(shake, Quaternion.identity, new Vector3(1, 1 - .03f * b, 1));
            };
        }
    }
}
