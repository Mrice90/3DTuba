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
        }
    }
}
