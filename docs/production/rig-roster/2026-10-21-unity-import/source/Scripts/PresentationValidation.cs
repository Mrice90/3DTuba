using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        IEnumerator ValidatePresentation(Action<bool, string> check) {
            foreach (var type in new[] { "CHARACTER", "STRUCTURE", "CAPITAL", "LAND" }) {
                var card = PlaytestCatalog.All.First(c => c.type == type);
                var go = factory.Create(card, 0, out _); var piece = go.GetComponent<Piece>();
                piece.InstanceId = "motion-check-" + type;
                go.transform.position = new Vector3(60, 0, 0); go.transform.localScale = Vector3.one * .78f;
                go.transform.rotation = Quaternion.Euler(0, 15, 0);
                var start = go.transform.position; var facing = go.transform.rotation;
                try {
                    for (int repeat = 0; repeat < 2; repeat++) {
                        yield return PresentationMotion.Summon(piece, 8);
                        yield return PresentationMotion.Attack(piece, start + Vector3.forward, 8);
                        yield return PresentationMotion.Ability(piece, 8);
                        yield return PresentationMotion.Hit(piece, 8);
                    }
                    var pivot = PresentationMotion.Pivot(piece);
                    check(Vector3.Distance(go.transform.position, start) < .001f && Quaternion.Angle(go.transform.rotation, facing) < .01f
                        && Vector3.Distance(pivot.localScale, Vector3.one) < .001f && pivot.localPosition.sqrMagnitude < .000001f
                        && Quaternion.Angle(pivot.localRotation, Quaternion.identity) < .01f, type + " repeated animation restores root and visual baseline");
                    var destination = start + Vector3.right;
                    yield return PresentationMotion.Move(piece, destination, 8, .64f);
                    check(Vector3.Distance(go.transform.position, destination) < .001f && Vector3.Distance(go.transform.localScale, Vector3.one * .64f) < .001f,
                        type + " movement ends at authoritative position and stack scale");
                    yield return PresentationMotion.Destroy(piece, 8);
                    check(piece.OwnerRing == null || !piece.OwnerRing.activeSelf, type + " destruction hides ownership ring");
                    PresentationMotion.Settle(piece);
                    check(Vector3.Distance(pivot.localScale, Vector3.one) < .001f && pivot.localPosition.sqrMagnitude < .000001f
                        && Quaternion.Angle(pivot.localRotation, Quaternion.identity) < .01f, type + " settle restores an interrupted or collapsed pose");
                    var dying = PresentationMotion.Destroy(piece, 1);
                    yield return null;
                    var lateHit = PresentationMotion.Hit(piece, 1);
                    check(dying.Done && lateHit.Done && pivot.localScale.y < .1f, type + " late hit cannot revive an interrupted destruction");
                    PresentationMotion.Settle(piece);
                    var disabledMove = PresentationMotion.Move(piece, start, 1, .78f);
                    yield return null; go.SetActive(false);
                    check(disabledMove.Done && !PresentationMotion.IsBusy(piece) && Vector3.Distance(go.transform.position, start) < .001f,
                        type + " deactivation completes pending movement and handle");
                    go.SetActive(true);
                } finally { Destroy(go); }
            }
            yield return ValidateRigClips(check);
        }

        // AI-060-RIG-COMBAT: every rigged character plays its own clip for each board event and fires the
        // land/impact/down animation events, at the speed-8 pace the checks above use.
        IEnumerator ValidateRigClips(Action<bool, string> check) {
            int rigged = 0, clipsOk = 0, eventsOk = 0; var failed = new System.Collections.Generic.List<string>();
            foreach (var card in PlaytestCatalog.All.Where(c => c.type == "CHARACTER" && c.HasModel)) {
                var go = factory.Create(card, 0, out bool real); var piece = go.GetComponent<Piece>();
                var animator = go.GetComponentInChildren<Animator>();
                if (!real || animator == null || animator.runtimeAnimatorController == null) { Destroy(go); continue; }
                rigged++;
                piece.InstanceId = "rig-check-" + card.id;
                go.transform.position = new Vector3(70, 0, 0); go.transform.rotation = Quaternion.Euler(0, 15, 0);
                var events = animator.GetComponent<RigEvents>();
                var fired = new System.Collections.Generic.HashSet<string>();
                if (events != null) events.Fired += n => fired.Add(n);
                bool ok = true; string why = null;
                IEnumerator Step(MotionHandle h, string state, string ev) {
                    if (!h.UsedRigClip) { ok = false; why = why ?? h.Kind + " used no clip"; }
                    bool seen = false; float until = Time.realtimeSinceStartup + 2f;
                    while (Time.realtimeSinceStartup < until) {
                        var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                        seen |= info.IsName(state);
                        if (seen && (ev == null || fired.Contains(ev)) && h.Done) break;
                        yield return null;
                    }
                    if (!seen) { ok = false; why = why ?? h.Kind + " never entered " + state; }
                    if (ev != null && !fired.Contains(ev)) { ok = false; why = why ?? state + " never fired " + ev; }
                }
                try {
                    yield return Step(PresentationMotion.Summon(piece, 8), "Summon", "land");
                    yield return Step(PresentationMotion.Move(piece, go.transform.position + Vector3.right, 8), "Walk", null);
                    yield return Step(PresentationMotion.Attack(piece, go.transform.position + Vector3.forward, 8), "Attack", "impact");
                    yield return Step(PresentationMotion.Hit(piece, 8), "Hit", null);
                    yield return Step(PresentationMotion.Ability(piece, 8), "Attack", null);
                    yield return Step(PresentationMotion.Destroy(piece, 8), "Death", "down");
                    if (ok) clipsOk++; else failed.Add(card.id + ": " + why);
                    if (fired.Contains("land") && fired.Contains("impact") && fired.Contains("down")) eventsOk++;
                } finally { Destroy(go); }
            }
            // 47: the Thunder Ram rig came from a different source mesh than its token texture, so it keeps the static token.
            check(rigged == 48, "AI-060: 48 rigged characters carry a clip controller (" + rigged + ")");
            check(clipsOk == rigged, $"AI-060: summon/move/attack/hit/ability/destroy play the rig clips ({clipsOk}/{rigged}{(failed.Count > 0 ? "; " + string.Join("; ", failed.Take(4)) : "")})");
            check(eventsOk == rigged, $"AI-060: land/impact/down animation events fire ({eventsOk}/{rigged})");
        }
    }
}
